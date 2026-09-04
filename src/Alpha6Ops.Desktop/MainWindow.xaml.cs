using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Alpha6Ops.Core;
using Forms = System.Windows.Forms;

namespace Alpha6Ops.Desktop;

public partial class MainWindow : Window
{
    private readonly Forms.NotifyIcon tray;
    private readonly ObservableCollection<string> milestones = new();
    private readonly CancellationTokenSource lifetime = new();
    private FlightSession session = new(Demo.Rotation());
    private bool exiting;
    private bool running;
    private bool trayHintShown;
    private CancellationTokenSource? liveCancellation;
    private TimelineRecorder? liveRecorder;
    private DateTimeOffset? liveLast;
    private string? liveAircraft;
    private bool liveInvalid;
    private TestFlightLog? flightLog;
    private string? lastJournal;
    private readonly ProgramMonitor? programMonitor;
    private ActiveFlightPlan? activePlan;
    private DateTimeOffset? actualDeparture;
    private DateTimeOffset? actualArrival;
    private static string LogDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Alpha6Designs", "Alpha6OPS", "TestLogs");
    internal bool IsRunning => running;
    internal bool TrayVisible => tray.Visible;
    internal IReadOnlyList<LegProjection> Projection => RotationPlanner.Project(session.Rotation);
    private string SelectedFixture => (string)(FixtureCombo.SelectedItem ?? EmbeddedReplay.Fixtures[0]);

    public MainWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";
        VersionText.Text = $"ALPHA 6 OPS v{version} • DESKTOP PREVIEW";
        Title = $"Alpha 6 OPS v{version} — Desktop Preview";
        SourceInitialized += (_, _) => ApplyInitialWindowSize();
        tray = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Alpha 6 OPS — Replay preview",
            Visible = true
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Alpha 6 OPS", null, (_, _) => Dispatcher.Invoke(RestoreWindow));
        menu.Items.Add("Exit Alpha 6 OPS", null, (_, _) => Dispatcher.Invoke(ExitApplication));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreWindow);
        EventsList.ItemsSource = milestones;
        Closing += OnClosing;
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) MinimizeToTray(); };
        activePlan = ActiveFlightPlanStore.Load();
        FixtureCombo.ItemsSource = EmbeddedReplay.Fixtures;
        FixtureCombo.SelectedIndex = 0;
        SetAdvanced(false);
        ResetPreview();
        try { programMonitor = new ProgramMonitor(LogDirectory, status => ProgramHealthText.Text = status); }
        catch (Exception error)
        {
            CrashReporter.Write("program_monitor_startup", error);
            ProgramHealthText.Text = "PROGRAM MONITOR UNAVAILABLE • " + error.GetBaseException().Message;
        }
    }

    internal void SetAdvanced(bool value)
    {
        AdvancedPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        SimpleButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value ? "#282B23" : "#F9D928"));
        SimpleButton.Foreground = value ? Brushes.WhiteSmoke : Brushes.Black;
        AdvancedButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value ? "#F9D928" : "#282B23"));
        AdvancedButton.Foreground = value ? Brushes.Black : Brushes.WhiteSmoke;
    }

    private void ApplyInitialWindowSize()
    {
        // Set the initial physical size once; subsequent resizing belongs to the user.
        var dpi = VisualTreeHelper.GetDpi(this);
        Width = 1366 / dpi.DpiScaleX;
        Height = 768 / dpi.DpiScaleY;
    }

    private void RefreshRotation()
    {
        var legs = Projection;
        var next = legs.FirstOrDefault(leg => !leg.Completed);
        TrackerModeText.Text = "REPLAY FLIGHT • SAMPLE DATA";
        AircraftText.Text = "N600A6 • SAMPLE ASSIGNMENT";
        RouteText.Text = next is null ? "Rotation complete" : $"{next.Origin} → {next.Destination}";
        DepartureText.Text = next is null ? "All assigned legs complete." : $"{next.Id}  •  {next.EstimatedOut.UtcDateTime:HH:mm}Z  •  {(next.DepartureDelayMinutes == 0 ? "On time" : $"{next.DepartureDelayMinutes:+0;-0} min")}";
        PhaseText.Text = PhaseLabel(session.Phase).ToUpperInvariant();
        var first = legs[0];
        DepartedValueText.Text = session.Rotation.Legs[0].ActualOut is { } departed ? departed.UtcDateTime.ToString("HH:mm:ss'Z'") : "—";
        ArrivalValueText.Text = first.EstimatedIn.UtcDateTime.ToString("HH:mm'Z'");
        ElapsedValueText.Text = session.Rotation.Legs[0].ActualOut is { } start ? FormatDuration(((session.Rotation.Legs[0].ActualIn ?? start) - start)) : "—";
        RotationGrid.ItemsSource = legs.Select(leg => new
        {
            leg.Id, Route = $"{leg.Origin} → {leg.Destination}",
            Out = $"{leg.EstimatedOut.UtcDateTime:HH:mm}Z", In = $"{leg.EstimatedIn.UtcDateTime:HH:mm}Z",
            Delay = $"{leg.DepartureDelayMinutes:0} / {leg.ArrivalDelayMinutes:0} min",
            Status = leg.Completed ? "Actual" : "Projected"
        }).ToArray();
    }

    internal void ResetPreview()
    {
        if (running) return;
        session = new FlightSession(Demo.Rotation());
        milestones.Clear();
        ReplayProgress.Value = 0;
        StatusText.Text = "Ready. Run a short replay to see one arrival affect the rest of the aircraft's day.";
        tray.Text = "Alpha 6 OPS — Ready for replay";
        RefreshRotation();
    }

    internal async Task RunReplayAsync(int sampleDelayMilliseconds = 650)
    {
        if (running) return;
        ResetPreview();
        running = true;
        ConnectButton.IsEnabled = false;
        ReplayButton.IsEnabled = ResetButton.IsEnabled = false;
        StatusText.Text = "Replaying recorded simulator data. You can minimize to the tray; the replay will continue.";
        try
        {
            var samples = new List<Telemetry>();
            await foreach (var sample in new EmbeddedReplay(SelectedFixture).ReadAsync(lifetime.Token))
                samples.Add(sample);
            ReplayProgress.Maximum = samples.Count;
            foreach (var sample in samples)
            {
                if (sampleDelayMilliseconds > 0) await Task.Delay(sampleDelayMilliseconds, lifetime.Token);
                lifetime.Token.ThrowIfCancellationRequested();
                if (session.Observe(sample) is { } milestone)
                {
                    milestones.Add($"{milestone.At.UtcDateTime:HH:mm:ss}Z   {PhaseLabel(milestone.Phase)}");
                    tray.Text = $"Alpha 6 OPS — {PhaseLabel(milestone.Phase)}";
                }
                ReplayProgress.Value++;
                RefreshRotation();
            }
            var summary = string.Join(" ", Projection.Skip(1).Select(l => $"{l.Id} {(l.DepartureDelayMinutes == 0 ? "on time" : $"{l.DepartureDelayMinutes:+0;-0} min")}."));
            StatusText.Text = $"Replay complete. {summary} Nothing is sent to a server.";
            if (!IsVisible) tray.ShowBalloonTip(4000, "Alpha 6 OPS — Replay complete", summary, Forms.ToolTipIcon.Info);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or JsonException or ArgumentException)
        {
            StatusText.Text = $"Replay could not finish: {error.Message} Use Reset preview to try again.";
        }
        finally
        {
            running = false;
            ConnectButton.IsEnabled = true;
            ReplayButton.IsEnabled = ResetButton.IsEnabled = true;
        }
    }

    internal void MinimizeToTray()
    {
        Hide();
        if (!trayHintShown)
        {
            trayHintShown = true;
            tray.ShowBalloonTip(4000, "Alpha 6 OPS is still running", "Double-click the tray icon to reopen. Choose Exit Alpha 6 OPS to stop.", Forms.ToolTipIcon.Info);
        }
    }

    internal void RestoreWindow() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void OnClosing(object? sender, CancelEventArgs e) { if (!exiting) { e.Cancel = true; MinimizeToTray(); } }
    internal void ExitApplication()
    {
        if (exiting) return;
        exiting = true;
        FinishLog("application_exit");
        programMonitor?.Stop("application_exit");
        lifetime.Cancel();
        liveCancellation?.Cancel();
        tray.Visible = false;
        tray.ContextMenuStrip?.Dispose();
        tray.Dispose();
        Application.Current.Shutdown();
    }
    private static string PhaseLabel(FlightPhase phase) => phase switch
    {
        FlightPhase.AtGate => "At gate", FlightPhase.TaxiOut => "Block-out / taxi out",
        FlightPhase.Airborne => "Takeoff / airborne", FlightPhase.TaxiIn => "Landing / taxi in",
        FlightPhase.Complete => "Block-in / complete", _ => phase.ToString()
    };
    private async void Replay_Click(object sender, RoutedEventArgs e) => await RunReplayAsync();
    private void Reset_Click(object sender, RoutedEventArgs e) => ResetPreview();
    private void Simple_Click(object sender, RoutedEventArgs e) => SetAdvanced(false);
    private void Advanced_Click(object sender, RoutedEventArgs e) => SetAdvanced(true);
    private void Tray_Click(object sender, RoutedEventArgs e) => MinimizeToTray();
    private void Exit_Click(object sender, RoutedEventArgs e) => ExitApplication();
    private void Fleet_Click(object sender, RoutedEventArgs e) => new FleetWindow { Owner = this }.ShowDialog();
    private async void Timeline_Click(object sender, RoutedEventArgs e)
    {
        var timeline = await TimelineBuilder.BuildAsync(new EmbeddedReplay(SelectedFixture));
        new TimelineWindow(timeline) { Owner = this }.ShowDialog();
    }
    private async void Debrief_Click(object sender, RoutedEventArgs e)
    {
        var debriefSession = new FlightSession(Demo.Rotation());
        var events = new List<FlightEvent>();
        await foreach (var sample in new EmbeddedReplay(SelectedFixture).ReadAsync())
            if (debriefSession.Observe(sample) is { } ev) events.Add(ev);
        new DebriefWindow(debriefSession.Phase, events, DebriefSummary.Segments(events), RotationPlanner.Project(debriefSession.Rotation)) { Owner = this }.ShowDialog();
    }
    private void SetFlight_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ActiveFlightWindow(activePlan) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Plan is null) return;
        activePlan = dialog.Plan;
        ActiveFlightPlanStore.Save(activePlan);
        actualDeparture = actualArrival = null;
        RefreshLiveTracker(liveAircraft, liveLast, liveRecorder?.Phase, "Assignment ready. Connect to MSFS 2024 to begin tracking.");
    }
    private LegProjection? LiveLegProjection()
    {
        if (activePlan is null) return null;
        var plan = activePlan;
        var estimatedOut = actualDeparture ?? plan.PlannedDepartureUtc;
        var estimatedIn = actualArrival ?? (actualDeparture is { } departed ? departed + plan.PlannedDuration : plan.PlannedArrivalUtc);
        return new LegProjection(plan.FlightNumber, plan.Origin, plan.Destination, plan.PlannedDepartureUtc, plan.PlannedArrivalUtc,
            estimatedOut, estimatedIn, (estimatedOut - plan.PlannedDepartureUtc).TotalMinutes, (estimatedIn - plan.PlannedArrivalUtc).TotalMinutes, actualArrival is not null);
    }
    private void LiveTimeline_Click(object sender, RoutedEventArgs e)
    {
        if (liveRecorder is null) return;
        new TimelineWindow(liveRecorder.ToTimeline()) { Owner = this }.ShowDialog();
    }
    private void LiveDebrief_Click(object sender, RoutedEventArgs e)
    {
        if (liveRecorder is null) return;
        var legs = LiveLegProjection() is { } leg ? new[] { leg } : [];
        new DebriefWindow(liveRecorder.Phase, liveRecorder.Events, DebriefSummary.Segments(liveRecorder.Events), legs) { Owner = this }.ShowDialog();
    }
    private void Logs_Click(object sender, RoutedEventArgs e)
    {
        if (programMonitor is null) { MessageBox.Show(this, "The diagnostic database is unavailable. A crash report was saved with the startup error.", "Alpha 6 OPS", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        programMonitor.WriteHeartbeat();
        new LogDatabaseWindow(programMonitor.Database) { Owner = this }.ShowDialog();
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (liveCancellation is not null || running) return;
        liveCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        liveRecorder = null; liveLast = null; liveAircraft = null; liveInvalid = false;
        actualDeparture = actualArrival = null;
        LiveTimelineButton.IsEnabled = LiveDebriefButton.IsEnabled = false;
        ConnectButton.IsEnabled = ReplayButton.IsEnabled = ResetButton.IsEnabled = false;

        ConnectionText.Text = "Connecting to the local simulator…";
        programMonitor?.Update("Connecting");
        SetConnectionBadge("CONNECTING TO SIMULATOR", "#FFCA45", "#433817");
        milestones.Clear();
        SetAdvanced(true);
        try
        {
            StartLog();
            await SimConnectSource.RunAsync(message => Dispatcher.BeginInvoke(new Action(() => { if (exiting) return; RecordLog("connection_opened", null, new { message }); ConnectionText.Text = message; SetConnectionBadge("SIMULATOR CONNECTED", "#65E697", "#173D27"); programMonitor?.Update("Connected"); })),
                reading => Dispatcher.BeginInvoke(new Action(() => ObserveLive(reading))), liveCancellation.Token);
            ConnectionText.Text = "Disconnected. Live milestones remain visible until the next connection or replay.";
            SetConnectionBadge("SIMULATOR DISCONNECTED", "#A9AD9F", "#30342C");
            programMonitor?.Update("Disconnected");
        }
        catch (Exception error) when (error is IOException or DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or TypeInitializationException or OperationCanceledException)
        {
            RecordLog("connection_error", liveLast, new { message = error.GetBaseException().Message, type = error.GetType().Name });
            ConnectionText.Text = error is OperationCanceledException ? "Disconnected." : error.GetBaseException().Message;
            SetConnectionBadge(error is OperationCanceledException ? "SIMULATOR DISCONNECTED" : "SIMULATOR CONNECTION FAILED", error is OperationCanceledException ? "#A9AD9F" : "#FF9690", error is OperationCanceledException ? "#30342C" : "#512621");
            programMonitor?.Update(error is OperationCanceledException ? "Disconnected" : "Connection failed");
        }
        finally
        {
            FinishLog("connection_closed");
            liveCancellation.Dispose(); liveCancellation = null;
            ConnectButton.IsEnabled = ReplayButton.IsEnabled = ResetButton.IsEnabled = true;

        }
    }

    private void ObserveLive(LiveReading reading)
    {
        if (liveCancellation is null || liveCancellation.IsCancellationRequested || exiting) return;
        var s = reading.Telemetry;
        programMonitor?.Update("Connected", reading.Aircraft, s.At, sampleReceived: true);
        RecordLog("telemetry", s.At, new { aircraft = reading.Aircraft, onGround = s.OnGround, groundSpeedKnots = s.GroundSpeedKnots, parkingBrake = s.ParkingBrake, enginesRunning = s.EnginesRunning, paused = s.Paused, slewing = s.Slewing });
        LiveText.Text = $"{reading.Aircraft} • {s.At.UtcDateTime:HH:mm:ss}Z • {s.GroundSpeedKnots:0.0} kt • Brake {(s.ParkingBrake ? "set" : "released")} • {(s.Paused ? "Paused" : s.Slewing ? "Slew" : s.OnGround ? "On ground" : "Airborne")}";
        if (!liveInvalid && ((liveLast is not null && s.At < liveLast) || (liveAircraft is not null && reading.Aircraft != liveAircraft)))
        {
            liveInvalid = true;
            RecordLog("monitor_invalidated", s.At, new { reason = "aircraft_changed_or_clock_reversed", previousSimulatorUtc = liveLast, previousAircraft = liveAircraft });
        }
        liveLast = s.At; liveAircraft = reading.Aircraft;
        if (liveInvalid) { ConnectionText.Text = "Aircraft or simulator clock changed. Exit OPS and reopen it at the gate to begin a new monitor session."; RefreshLiveTracker(reading.Aircraft, s.At, liveRecorder?.Phase, "Tracking stopped because the aircraft or simulator clock changed."); return; }
        if (liveRecorder is null)
        {
            if (!s.OnGround || s.GroundSpeedKnots >= 0.5 || !s.ParkingBrake || s.Paused || s.Slewing)
            { ConnectionText.Text = "Connected. Waiting for a stationary aircraft with parking brake set and simulation unpaused."; var observed = !s.OnGround ? FlightPhase.Airborne : s.GroundSpeedKnots >= 1 ? FlightPhase.TaxiOut : FlightPhase.AtGate; RefreshLiveTracker(reading.Aircraft, s.At, observed, s.Paused ? "Simulator paused" : s.Slewing ? "Slew mode" : !s.OnGround ? "Airborne • departure time unavailable" : "Taxiing • monitor awaiting a gate state"); return; }
            liveRecorder = new TimelineRecorder(new PhaseDetector());
            RecordLog("monitor_armed", s.At, new { aircraft = reading.Aircraft });
        }
        if (liveRecorder.Observe(s) is { } milestone)
        {
            milestones.Add($"LIVE {milestone.At.UtcDateTime:HH:mm:ss}Z   {PhaseLabel(milestone.Phase)}");
            RecordLog("flight_milestone", milestone.At, new { phase = milestone.Phase.ToString(), label = PhaseLabel(milestone.Phase) });
            if (milestone.Phase == FlightPhase.TaxiOut) actualDeparture = milestone.At;
            if (milestone.Phase == FlightPhase.Complete) actualArrival = milestone.At;
            if (milestone.Phase == FlightPhase.Complete) SaveFlightLog();
        }
        LiveTimelineButton.IsEnabled = LiveDebriefButton.IsEnabled = true;
        ConnectionText.Text = $"Connected • {PhaseLabel(liveRecorder.Phase)}. Active flight tracking uses the assignment shown below.";
        tray.Text = "Alpha 6 OPS — Live " + liveRecorder.Phase;
        RefreshLiveTracker(reading.Aircraft, s.At, liveRecorder.Phase, $"Live telemetry • {s.GroundSpeedKnots:0.0} kt • Brake {(s.ParkingBrake ? "set" : "released")}");
    }
    private void StartLog()
    {
        FinishLog("new_connection");
        try
        {
            flightLog = new TestFlightLog(LogDirectory, Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown", "live_simconnect");
            lastJournal = flightLog.JournalPath;
            if (activePlan is not null) RecordLog("flight_assignment", null, activePlan);
            LogStatusText.Text = "Recording test log. Export it here after your flight—or any time something looks wrong.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { LogStatusText.Text = "Logging unavailable: " + e.Message; }
    }
    private void RecordLog(string kind, DateTimeOffset? at, object detail)
    {
        try { flightLog?.Record(kind, at, detail); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { flightLog?.Dispose(); flightLog = null; LogStatusText.Text = "Logging stopped: " + e.Message; }
    }
    private void SaveFlightLog()
    {
        try { flightLog?.SaveExport(); LogStatusText.Text = "Flight log saved. Click Export test log to save a JSON file to upload."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { LogStatusText.Text = "Log export failed: " + e.Message; }
    }
    private void FinishLog(string reason)
    {
        if (flightLog is null) return;
        try { flightLog.End(reason); LogStatusText.Text = "Test log saved. Use Export test log to choose where to save your upload."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { LogStatusText.Text = "Log finalization failed; the journal may still be available: " + e.Message; }
        finally { flightLog.Dispose(); flightLog = null; }
    }
    private void ExportLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var journal = lastJournal;
            if (journal is null)
            {
                var choose = new Microsoft.Win32.OpenFileDialog { Title = "Choose a saved test session", Filter = "OPS test journals (*.jsonl)|*.jsonl", InitialDirectory = Directory.Exists(LogDirectory) ? LogDirectory : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
                if (choose.ShowDialog(this) != true) return;
                journal = choose.FileName;
            }
            var save = new Microsoft.Win32.SaveFileDialog { Title = "Save test log to upload", Filter = "JSON test log (*.json)|*.json", DefaultExt = ".json", FileName = Path.GetFileNameWithoutExtension(journal) + ".json" };
            if (save.ShowDialog(this) != true) return;
            TestFlightLog.Export(journal, save.FileName);
            LogStatusText.Text = "Exported: " + save.FileName;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { LogStatusText.Text = "Could not export test log: " + error.Message; }
    }
    internal void SetConnectionBadge(string label, string color, string background)
    {
        ConnectionBadgeText.Text = label;
        ConnectionDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        ConnectionBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(background));
    }

    private void RefreshLiveTracker(string? simulatorAircraft, DateTimeOffset? simulatorTime, FlightPhase? phase, string status)
    {
        TrackerModeText.Text = "ACTIVE FLIGHT • LIVE SIMCONNECT";
        if (activePlan is null)
        {
            AircraftText.Text = simulatorAircraft ?? "AIRCRAFT WAITING";
            RouteText.Text = "SET AN ACTIVE FLIGHT";
            DepartureText.Text = "Add the flight number, route and planned UTC times to calculate progress and ETA.";
            DepartedValueText.Text = ArrivalValueText.Text = ElapsedValueText.Text = "—";
            PhaseText.Text = phase is null ? "MONITORING" : PhaseLabel(phase.Value).ToUpperInvariant();
            ReplayProgress.Maximum = 100; ReplayProgress.Value = PhaseProgress(phase, null, null);
            StatusText.Text = status;
            return;
        }
        var plan = activePlan;
        AircraftText.Text = string.Join(" • ", new[] { simulatorAircraft, string.IsNullOrWhiteSpace(plan.Registration) ? null : plan.Registration }.Where(x => !string.IsNullOrWhiteSpace(x)));
        RouteText.Text = $"{plan.Origin} → {plan.Destination}";
        DepartureText.Text = $"{plan.FlightNumber}  •  PLANNED {plan.PlannedDepartureUtc.UtcDateTime:HH:mm}Z  •  {plan.PlannedDuration.TotalHours:0.#} HR BLOCK";
        var eta = actualDeparture is { } departed ? departed + plan.PlannedDuration : plan.PlannedArrivalUtc;
        DepartedValueText.Text = actualDeparture?.UtcDateTime.ToString("HH:mm:ss'Z'") ?? "—";
        ArrivalValueText.Text = actualArrival?.UtcDateTime.ToString("HH:mm:ss'Z'") ?? eta.UtcDateTime.ToString("HH:mm'Z'");
        ElapsedValueText.Text = actualDeparture is { } start && simulatorTime is { } now && now >= start ? FormatDuration((actualArrival ?? now) - start) : "—";
        PhaseText.Text = phase is null ? "MONITORING" : PhaseLabel(phase.Value).ToUpperInvariant();
        ReplayProgress.Maximum = 100;
        ReplayProgress.Value = PhaseProgress(phase, simulatorTime, eta);
        StatusText.Text = status;
    }

    private double PhaseProgress(FlightPhase? phase, DateTimeOffset? now, DateTimeOffset? eta) => phase switch
    {
        FlightPhase.AtGate => 0,
        FlightPhase.TaxiOut => 8,
        FlightPhase.Airborne when actualDeparture is { } start && now is { } current && eta > start => Math.Clamp(10 + 82 * (current - start).TotalSeconds / (eta.Value - start).TotalSeconds, 10, 92),
        FlightPhase.Airborne => 45,
        FlightPhase.TaxiIn => 95,
        FlightPhase.Complete => 100,
        _ => 0
    };
    private static string FormatDuration(TimeSpan duration) => $"{Math.Max(0, (int)duration.TotalHours):00}:{Math.Max(0, duration.Minutes):00}";
}
