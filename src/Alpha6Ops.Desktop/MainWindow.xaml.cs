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
using System.Windows.Media.Animation;
using Alpha6Ops.Core;
using Forms = System.Windows.Forms;

namespace Alpha6Ops.Desktop;

public partial class MainWindow : Window
{
    private readonly Forms.NotifyIcon tray;
    private readonly System.Drawing.Icon trayIcon;
    private readonly ObservableCollection<string> milestones = new();
    private readonly ObservableCollection<TrackingEventEntry> trackingEvents = new();
    private readonly CancellationTokenSource lifetime = new();
    private FlightSession session = new(Demo.Rotation());
    private bool exiting;
    private bool running;
    private bool trayHintShown;
    private CancellationTokenSource? liveCancellation;
    private TimelineRecorder? liveRecorder;
    private DateTimeOffset? liveLast;
    private string? liveAircraft;
    private string liveSource = "MSFS 2024";
    private string? lastScenarioEvent;
    private Telemetry? liveTelemetry;
    private double? liveRouteProgress;
    private FlightTrackingEventMonitor? trackingEventMonitor;
    private bool liveInvalid;
    private bool liveNeedsBaseline;
    private TestFlightLog? flightLog;
    private string? lastJournal;
    private readonly ProgramMonitor? programMonitor;
    private ActiveFlightPlan? activePlan;
    private readonly UserPreferences? preferences;
    private GeneralSettings generalSettings = GeneralSettings.Defaults;
    private readonly FlightHistoryDatabase? flightHistory;
    private readonly bool diagnosticMode;
    private readonly string stateDirectory;
    // Single-leg rotation for the live-tracked assignment. Actuals are applied through the same
    // RotationPlanner.ApplyMilestone/Project path the fixture-replay session uses, so a live flight
    // and a replayed one compute delay/ETA identically instead of the tracker hand-rolling its own math.
    private AircraftRotation? liveRotation;
    private static string LogDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Alpha6Designs", "Alpha6OPS", "TestLogs");
    internal bool IsRunning => running;
    internal bool TrayVisible => tray.Visible;
    internal IReadOnlyList<LegProjection> Projection => RotationPlanner.Project(session.Rotation);
    internal FlightHistoryDatabase? FlightHistory => flightHistory;
    internal FlightPhase? RecoveredFlightPhase=>liveRecorder?.Phase;
    internal int RecoveredTrackingEventCount=>trackingEvents.Count;
    internal double? RecoveredRouteProgress=>liveRouteProgress;
    internal ActiveFlightPlan? ActivePlanForTest=>activePlan;
    private string SelectedFixture => (string)(FixtureCombo.SelectedItem ?? EmbeddedReplay.Fixtures[0]);
    private string PilotName => string.IsNullOrWhiteSpace(PilotNameBox.Text) ? "Unspecified" : PilotNameBox.Text.Trim();

    public MainWindow(string? diagnosticDirectory = null)
    {
        diagnosticMode = diagnosticDirectory is not null;
        stateDirectory=diagnosticDirectory??CrashReporter.RootDirectory;
        InitializeComponent();
        try { flightHistory = new FlightHistoryDatabase(diagnosticDirectory ?? CrashReporter.RootDirectory); }
        catch (Exception error) { CrashReporter.Write("flight_history_startup", error); }
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";
        VersionText.Text = $"ALPHA 6 OPS  •  v{version}";
        Title = $"Alpha 6 OPS v{version}";
        SourceInitialized += (_, _) => { InitializeMonitorTracking(); ApplyInitialWindowSize(); };
        trayIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "") ?? (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
        tray = new Forms.NotifyIcon
        {
            Icon = trayIcon,
            Text = "Alpha 6 OPS — Flight monitoring",
            Visible = true
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Alpha 6 OPS", null, (_, _) => Dispatcher.Invoke(RestoreWindow));
        menu.Items.Add("Exit Alpha 6 OPS", null, (_, _) => Dispatcher.Invoke(ExitApplication));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreWindow);
        EventsList.ItemsSource = milestones;
        Closing += OnClosing;
        StateChanged += (_, _) => OnWindowStateChanged();
        activePlan = ActiveFlightPlanStore.Load(stateDirectory);
        if(activePlan is not null)RestoreFlightRecovery(FlightRecoveryStore.Load(activePlan,stateDirectory));
        preferences = diagnosticDirectory is null ? UserPreferencesStore.Load() : null;
        generalSettings = GeneralSettingsStore.Load(diagnosticDirectory);
        FixtureCombo.ItemsSource = EmbeddedReplay.Fixtures;
        RestoreDashboardPreferences(preferences);
        ResetPreview();
        InitializeDashboard(diagnosticDirectory);
        ApplyGeneralSettings(generalSettings);
        try { programMonitor = new ProgramMonitor(diagnosticDirectory is null ? LogDirectory : Path.Combine(diagnosticDirectory, "TestLogs"), status => ProgramHealthText.Text = status, diagnosticDirectory); }
        catch (Exception error)
        {
            CrashReporter.Write("program_monitor_startup", error);
            ProgramHealthText.Text = "PROGRAM MONITOR UNAVAILABLE • " + error.GetBaseException().Message;
        }
    }

    internal void RestoreDashboardPreferences(UserPreferences? saved)
    {
        FixtureCombo.SelectedIndex = Math.Max(0, EmbeddedReplay.Fixtures.ToList().IndexOf(saved?.Fixture ?? EmbeddedReplay.Fixtures[0]));
        PilotNameBox.Text = saved?.PilotName ?? "";
        // Legacy saved Advanced=true must not reopen the bottom panel on launch.
        // Rotation details remain available explicitly from Flight tools.
        SetAdvanced(false);
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
        var work = Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).WorkingArea;
        var available = new Size(work.Width / dpi.DpiScaleX, work.Height / dpi.DpiScaleY);
        MinWidth = Math.Min(640, available.Width); MinHeight = Math.Min(480, available.Height);
        var optimize = !diagnosticMode && ShouldOptimizeToMonitor(preferences, available);
        var requested = optimize ? available : new Size(preferences?.Width ?? available.Width, preferences?.Height ?? available.Height);
        var fitted = FitWindowSize(requested, available);
        Width = fitted.Width; Height = fitted.Height;
        if (optimize) WindowState = WindowState.Maximized;
        else
        {
            // WPF computes CenterScreen from the XAML dimensions before this saved size is
            // applied. Center the final physical window instead so every launch is balanced.
            var physicalWidth = (int)Math.Round(fitted.Width * dpi.DpiScaleX);
            var physicalHeight = (int)Math.Round(fitted.Height * dpi.DpiScaleY);
            var position = CenteredWorkPosition(work, physicalWidth, physicalHeight);
            WindowStartupLocation = WindowStartupLocation.Manual;
            SetWindowPos(new System.Windows.Interop.WindowInteropHelper(this).Handle, IntPtr.Zero,
                position.X, position.Y, 0, 0, 0x0015); // NOSIZE | NOZORDER | NOACTIVATE
        }
    }

    private void ApplyGeneralSettings(GeneralSettings settings)
    {
        generalSettings = settings;
        AdvancedButton.Visibility = settings.ShowAdvancedControls ? Visibility.Visible : Visibility.Collapsed;
        if (!settings.ShowAdvancedControls) SetAdvanced(false);
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
        RefreshDashboardFlight(false);
    }

    internal void ResetPreview()
    {
        if (running) return;
        session = new FlightSession(Demo.Rotation());
        milestones.Clear();
        UpdateHeroProgress(0,false);
        StatusText.Text = "Assignment ready. Connect at the gate to begin live flight tracking.";
        tray.Text = "Alpha 6 OPS — Ready for flight";
        RefreshRotation();
    }

    internal async Task RunReplayAsync(int sampleDelayMilliseconds = 650)
    {
        if (running) return;
        ResetPreview();
        running = true;
        ConnectButton.IsEnabled = ConnectFlightLabButton.IsEnabled = false;
        ReplayButton.IsEnabled = ResetButton.IsEnabled = false;
        StatusText.Text = "Replaying recorded simulator data. You can minimize to the tray; the replay will continue.";
        var firstLeg = session.Rotation.Legs[0];
        var flightId = BeginFlightHistory("replay", SelectedFixture, session.Rotation.AircraftId, firstLeg.Origin, firstLeg.Destination, firstLeg.Id);
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
                    RecordFlightEvent(flightId, "phase", milestone.At, new { phase = milestone.Phase.ToString(), label = PhaseLabel(milestone.Phase) });
                }
                UpdateHeroProgress(ReplayProgress.Value+1);
                RefreshRotation();
            }
            var summary = string.Join(" ", Projection.Skip(1).Select(l => $"{l.Id} {(l.DepartureDelayMinutes == 0 ? "on time" : $"{l.DepartureDelayMinutes:+0;-0} min")}."));
            StatusText.Text = $"Replay complete. {summary} Nothing is sent to a server.";
            EndFlightHistory(flightId, session.Phase.ToString());
            if (!IsVisible) tray.ShowBalloonTip(4000, "Alpha 6 OPS — Replay complete", summary, Forms.ToolTipIcon.Info);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            EndFlightHistory(flightId, "Cancelled");
        }
        catch (Exception error) when (error is IOException or JsonException or ArgumentException)
        {
            StatusText.Text = $"Replay could not finish: {error.Message} Use Reset preview to try again.";
            EndFlightHistory(flightId, "Error");
        }
        finally
        {
            running = false;
            ConnectButton.IsEnabled = ConnectFlightLabButton.IsEnabled = true;
            ReplayButton.IsEnabled = ResetButton.IsEnabled = true;
        }
    }

    private string? BeginFlightHistory(string source, string? sourceDetail, string aircraft, string? origin, string? destination, string? flightNumber)
    {
        if (flightHistory is null) return null;
        try { return flightHistory.BeginFlight(PilotName, source, sourceDetail, aircraft, origin, destination, flightNumber, Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { CrashReporter.Write("flight_history_begin", error); return null; }
    }
    private void RecordFlightEvent(string? flightId, string kind, DateTimeOffset? at, object detail)
    {
        if (flightId is null || flightHistory is null) return;
        try { flightHistory.RecordEvent(flightId, kind, at, detail); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { CrashReporter.Write("flight_history_record", error); }
    }
    private void EndFlightHistory(string? flightId, string finalPhase)
    {
        if (flightId is null || flightHistory is null) return;
        try { flightHistory.EndFlight(flightId, finalPhase); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { CrashReporter.Write("flight_history_end", error); }
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
    private void OnWindowStateChanged()
    {
        MaximizeIcon.Visibility = WindowState == WindowState.Maximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreIcon.Visibility = WindowState == WindowState.Maximized ? Visibility.Visible : Visibility.Collapsed;
        if (WindowState == WindowState.Minimized && generalSettings.MinimizeToTray) MinimizeToTray();
    }
    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    private void ToggleMaximizeWindow_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }
    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        if (generalSettings.MinimizeToTray) Close();
        else ExitApplication();
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (exiting) return;
        e.Cancel = true;
        if (generalSettings.MinimizeToTray) MinimizeToTray();
        else Dispatcher.BeginInvoke(new Action(() => ExitApplication()));
    }
    internal void ExitApplication() => ExitApplication(null);
    internal void ExitApplication(string? preferencesDirectory)
    {
        if (exiting) return;
        exiting = true;
        dashboardClock.Stop();
        var savedBounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        UserPreferencesStore.Save(new UserPreferences(savedBounds.Width, savedBounds.Height, WindowState == WindowState.Maximized, AdvancedPanel.Visibility == Visibility.Visible, SelectedFixture, PilotNameBox.Text), preferencesDirectory);
        FinishLog("application_exit");
        programMonitor?.Stop("application_exit");
        flightHistory?.Dispose();
        lifetime.Cancel();
        liveCancellation?.Cancel();
        tray.Visible = false;
        tray.ContextMenuStrip?.Dispose();
        tray.Dispose();
        trayIcon.Dispose();
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
        if (running || liveCancellation is not null)
        {
            OpsNoticeWindow.Show(this, "Active flight", "Finish the replay or disconnect the simulator before changing the active flight.");
            return;
        }
        var dialog = new ActiveFlightWindow(activePlan) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Plan is null) return;
        activePlan = dialog.Plan;
        ActiveFlightPlanStore.Save(activePlan,stateDirectory);
        FlightRecoveryStore.Delete(stateDirectory);ResetLiveTrackingState();
        RefreshLiveTracker(liveAircraft, liveLast, liveRecorder?.Phase, "Assignment ready. Connect to MSFS 2024 to begin tracking.");
    }
    private void ClearFlight_Click(object sender,RoutedEventArgs e)
    {
        if(running||liveCancellation is not null){OpsNoticeWindow.Show(this,"Active flight","Finish the replay or disconnect the simulator before clearing the active flight.");return;}
        ActiveFlightPlanStore.Delete(stateDirectory);FlightRecoveryStore.Delete(stateDirectory);activePlan=null;ResetLiveTrackingState();
        RefreshLiveTracker(null,null,null,"No active flight");ShowDashboard();
    }
    private void ResetLiveTrackingState()
    {
        liveRecorder=null;liveLast=null;liveAircraft=null;liveTelemetry=null;liveRouteProgress=null;liveRotation=null;liveInvalid=false;liveNeedsBaseline=false;
        trackingEventMonitor=null;trackingEvents.Clear();milestones.Clear();LiveTimelineButton.IsEnabled=LiveDebriefButton.IsEnabled=false;
    }
    private void RestoreFlightRecovery(FlightRecoveryState? state)
    {
        if(state is null||activePlan is null)return;
        liveLast=state.LastSimulatorUtc;liveAircraft=state.Aircraft;liveTelemetry=state.LastTelemetry;liveRouteProgress=state.RouteProgress;liveSource=state.Source;
        var profile=AircraftGroundProfiles.ForFamily(state.Aircraft);liveRecorder=new TimelineRecorder(new PhaseDetector(profile,state.Phase,state.LastSimulatorUtc),state.PhaseEvents);
        var baseRotation=BuildLiveRotation(activePlan,state.Aircraft,profile);
        if(baseRotation is not null)liveRotation=baseRotation with{Legs=[baseRotation.Legs[0] with{ActualOut=state.ActualOut,ActualIn=state.ActualIn}]};
        trackingEventMonitor=new FlightTrackingEventMonitor(state.MonitorState);foreach(var entry in state.TrackingEvents)trackingEvents.Add(entry);
        foreach(var flightEvent in state.PhaseEvents)milestones.Add($"RECOVERED {flightEvent.At.UtcDateTime:HH:mm:ss}Z   {PhaseLabel(flightEvent.Phase)}");
        liveNeedsBaseline=true;
    }
    private void SaveFlightRecovery()
    {
        if(activePlan is null||liveRecorder is null||liveAircraft is null)return;
        var leg=liveRotation?.Legs[0];
        try{FlightRecoveryStore.Save(new(activePlan.FlightNumber,activePlan.PlannedDepartureUtc,liveSource,liveAircraft,liveRecorder.Phase,liveLast,liveTelemetry,liveRouteProgress,leg?.ActualOut,leg?.ActualIn,liveRecorder.Events.ToArray(),trackingEvents.ToArray(),trackingEventMonitor?.CaptureState(),DateTimeOffset.UtcNow),stateDirectory);}
        catch(Exception error) when(error is IOException or UnauthorizedAccessException){CrashReporter.Write("flight_recovery_save",error);}
    }
    private static AircraftRotation? BuildLiveRotation(ActiveFlightPlan? plan, string? simulatorAircraft, AircraftGroundProfile groundProfile)
    {
        if (plan is null) return null;
        var aircraft = string.IsNullOrWhiteSpace(plan.Registration) ? simulatorAircraft ?? "UNKNOWN" : plan.Registration;
        return new AircraftRotation("alpha6", aircraft, groundProfile.MinimumTurnMinutes, [new(plan.FlightNumber, plan.Origin, plan.Destination, plan.PlannedDepartureUtc, plan.PlannedArrivalUtc)]);
    }
    private void LiveTimeline_Click(object sender, RoutedEventArgs e)
    {
        if (liveRecorder is null) return;
        new TimelineWindow(liveRecorder.ToTimeline()) { Owner = this }.ShowDialog();
    }
    private void LiveDebrief_Click(object sender, RoutedEventArgs e)
    {
        if (liveRecorder is null) return;
        var legs = liveRotation is not null ? RotationPlanner.Project(liveRotation) : [];
        new DebriefWindow(liveRecorder.Phase, liveRecorder.Events, DebriefSummary.Segments(liveRecorder.Events), legs) { Owner = this }.ShowDialog();
    }
    private void Logs_Click(object sender, RoutedEventArgs e)
    {
        if (programMonitor is null) { OpsNoticeWindow.Show(this, "Alpha 6 OPS", "The diagnostic database is unavailable. A crash report was saved with the startup error.", true); return; }
        programMonitor.WriteHeartbeat();
        new LogDatabaseWindow(programMonitor.Database) { Owner = this }.ShowDialog();
    }
    private void FlightHistory_Click(object sender, RoutedEventArgs e)
    {
        if (flightHistory is null) { OpsNoticeWindow.Show(this, "Alpha 6 OPS", "The flight history database is unavailable. A crash report was saved with the startup error.", true); return; }
        new FlightHistoryWindow(flightHistory) { Owner = this }.ShowDialog();
    }

    // Capped so a flaky connection settles into a slow, unobtrusive retry rather than a tight loop
    // or an ever-growing wait; retried indefinitely until Disconnect is clicked, since MSFS being
    // closed for a while (or not open yet when Connect was clicked) is not a reason to give up.
    private static readonly TimeSpan[] ReconnectBackoff = [TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30)];

    internal bool CanQuickConnect => ConnectButton.IsEnabled && liveCancellation is null && !running;

    private async void QuickConnect_Click(object sender, RoutedEventArgs e)
    {
        SetAdvanced(false);
        if (CanQuickConnect) await ConnectSimulatorAsync(showRotationDetails: false);
        else OpenTools(); // Inspect/cancel an existing session; never disconnect it by accident.
    }

    private async void Connect_Click(object sender, RoutedEventArgs e) =>
        await ConnectSimulatorAsync(showRotationDetails: true);

    private async void ConnectFlightLab_Click(object sender, RoutedEventArgs e) =>
        await ConnectSimulatorAsync(showRotationDetails: true, useFlightLab: true);

    private async Task ConnectSimulatorAsync(bool showRotationDetails, bool useFlightLab = false)
    {
        if (liveCancellation is not null || running) return;
        liveCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        liveInvalid=false;liveNeedsBaseline=liveRecorder is not null;liveSource = useFlightLab ? "FLIGHT LAB" : "MSFS 2024"; lastScenarioEvent = null;
        if(liveRecorder is null){liveLast=null;liveAircraft=null;liveTelemetry=null;liveRouteProgress=null;trackingEventMonitor=new();trackingEvents.Clear();liveRotation=null;LiveTimelineButton.IsEnabled=LiveDebriefButton.IsEnabled=false;milestones.Clear();}
        ConnectButton.IsEnabled = ConnectFlightLabButton.IsEnabled = ReplayButton.IsEnabled = ResetButton.IsEnabled = false;
        DisconnectButton.IsEnabled = true;
        SetAdvanced(showRotationDetails);
        StartLog(useFlightLab ? "flight_lab" : "live_simconnect");
        var attempt = 0;
        try
        {
            while (true)
            {
                attempt++;
                var sourceName=useFlightLab?"Alpha 6 Flight Lab":"the local simulator";
                ConnectionText.Text = attempt == 1 ? $"Connecting to {sourceName}…" : $"Reconnecting to {sourceName} (attempt {attempt})…";
                programMonitor?.Update("Connecting");
                SetConnectionBadge(useFlightLab?"CONNECTING TO FLIGHT LAB":"CONNECTING TO SIMULATOR", "#FFCA45", "#433817");
                try
                {
                    var currentAttempt=attempt;
                    Action<string> opened=message => Dispatcher.BeginInvoke(new Action(() => { if (exiting) return; RecordLog("connection_opened", null, new { message,source=liveSource });AddTrackingEvent(trackingEventMonitor?.SystemEvent(DateTimeOffset.UtcNow,useFlightLab?(currentAttempt>1?"Flight Lab reconnected":"Flight Lab connected"):(currentAttempt>1?"SimConnect reconnected":"SimConnect connected"),message)); ConnectionText.Text = message; SetConnectionBadge(useFlightLab?"FLIGHT LAB CONNECTED":"SIMULATOR CONNECTED", "#65E697", "#173D27"); programMonitor?.Update("Connected"); }));
                    Action<LiveReading> received=reading => Dispatcher.BeginInvoke(new Action(() => ObserveLive(reading)));
                    if(useFlightLab)await FlightLabSource.RunAsync(opened,received,liveCancellation.Token);
                    else await SimConnectSource.RunAsync(opened,received,liveCancellation.Token);
                    break; // RunAsync only returns normally once its cooperative cancellation check trips
                }
                catch (IOException error)
                {
                    RecordLog("connection_error", liveLast, new { message = error.GetBaseException().Message, type = error.GetType().Name, attempt });
                    AddTrackingEvent(trackingEventMonitor?.SystemEvent(liveLast??DateTimeOffset.UtcNow,"Simulator connection lost",error.GetBaseException().Message,"alert"));
                    var delay = ReconnectBackoff[Math.Min(attempt - 1, ReconnectBackoff.Length - 1)];
                    ConnectionText.Text = $"{error.GetBaseException().Message} Retrying in {delay.TotalSeconds:0}s… Click Disconnect to stop.";
                    SetConnectionBadge("SIMULATOR CONNECTION LOST — RETRYING", "#FFCA45", "#433817");
                    programMonitor?.Update("Reconnecting");
                    await Task.Delay(delay, liveCancellation.Token);
                }
            }
            ConnectionText.Text = "Disconnected. Live milestones remain visible until the next connection or replay.";
            SetConnectionBadge(useFlightLab?"FLIGHT LAB DISCONNECTED":"SIMULATOR DISCONNECTED", "#A9AD9F", "#30342C");
            programMonitor?.Update("Disconnected");
        }
        catch (Exception error) when (error is IOException or DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or TypeInitializationException or OperationCanceledException)
        {
            RecordLog("connection_error", liveLast, new { message = error.GetBaseException().Message, type = error.GetType().Name });
            ConnectionText.Text = error is OperationCanceledException ? "Disconnected." : error.GetBaseException().Message;
            SetConnectionBadge(error is OperationCanceledException ? (useFlightLab?"FLIGHT LAB DISCONNECTED":"SIMULATOR DISCONNECTED") : (useFlightLab?"FLIGHT LAB CONNECTION FAILED":"SIMULATOR CONNECTION FAILED"), error is OperationCanceledException ? "#A9AD9F" : "#FF9690", error is OperationCanceledException ? "#30342C" : "#512621");
            programMonitor?.Update(error is OperationCanceledException ? "Disconnected" : "Connection failed");
        }
        finally
        {
            FinishLog("connection_closed");
            liveCancellation.Dispose(); liveCancellation = null;
            ConnectButton.IsEnabled = ConnectFlightLabButton.IsEnabled = ReplayButton.IsEnabled = ResetButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
        }
    }
    private void Disconnect_Click(object sender, RoutedEventArgs e) => liveCancellation?.Cancel();

    private void ObserveLive(LiveReading reading)
    {
        if (liveCancellation is null || liveCancellation.IsCancellationRequested || exiting) return;
        var s = reading.Telemetry;
        liveTelemetry=s;liveRouteProgress=reading.RouteProgress;
        liveSource=reading.Source;
        programMonitor?.Update("Connected", reading.Aircraft, s.At, sampleReceived: true);
        RecordLog("telemetry", s.At, new { aircraft = reading.Aircraft, onGround = s.OnGround, groundSpeedKnots = s.GroundSpeedKnots, indicatedAirspeedKnots=double.IsFinite(s.IndicatedAirspeedKnots)?(double?)s.IndicatedAirspeedKnots:null, verticalSpeedFeetPerMinute=double.IsFinite(s.VerticalSpeedFeetPerMinute)?(double?)s.VerticalSpeedFeetPerMinute:null, gearExtendedRatio=double.IsFinite(s.GearExtendedRatio)?(double?)s.GearExtendedRatio:null, flapsExtendedRatio=double.IsFinite(s.FlapsExtendedRatio)?(double?)s.FlapsExtendedRatio:null, pitchDegrees=double.IsFinite(s.PitchDegrees)?(double?)s.PitchDegrees:null, bankDegrees=double.IsFinite(s.BankDegrees)?(double?)s.BankDegrees:null, fuelTotalWeightPounds=double.IsFinite(s.FuelTotalWeightPounds)?(double?)s.FuelTotalWeightPounds:null, runningEngineCount=s.RunningEngineCount>=0?(int?)s.RunningEngineCount:null, runningEngineMask=s.RunningEngineMask>=0?(int?)s.RunningEngineMask:null, parkingBrake = s.ParkingBrake, enginesRunning = s.EnginesRunning, paused = s.Paused, slewing = s.Slewing, latitude = s.HasPosition?(double?)s.LatitudeDegrees:null, longitude = s.HasPosition?(double?)s.LongitudeDegrees:null, altitudeFeet = double.IsFinite(s.AltitudeFeet)?(double?)s.AltitudeFeet:null, altitudeAboveGroundFeet=double.IsFinite(s.AltitudeAboveGroundFeet)?(double?)s.AltitudeAboveGroundFeet:null, headingDegrees = double.IsFinite(s.HeadingDegrees)?(double?)s.HeadingDegrees:null, routeProgress=reading.RouteProgress });
        if(reading.ScenarioEvent is {Length:>0} scenario&&scenario!=lastScenarioEvent){lastScenarioEvent=scenario;RecordLog("flight_lab_event",s.At,new{scenario});}
        LiveText.Text = $"{reading.Source} • {reading.Aircraft} • {s.At.UtcDateTime:HH:mm:ss}Z • {s.GroundSpeedKnots:0.0} kt • Brake {(s.ParkingBrake ? "set" : "released")} • {(s.Paused ? "Paused" : s.Slewing ? "Slew" : s.OnGround ? "On ground" : "Airborne")}";
        var continuous=liveNeedsBaseline?(liveAircraft is null||reading.Aircraft==liveAircraft):TelemetryContinuity.IsContinuous(liveLast,liveAircraft,s.At,reading.Aircraft);
        liveNeedsBaseline=false;
        if (!liveInvalid && !continuous)
        {
            liveInvalid = true;
            RecordLog("monitor_invalidated", s.At, new { reason = "discontinuous_telemetry", previousSimulatorUtc = liveLast, previousAircraft = liveAircraft });
            AddTrackingEvent(trackingEventMonitor?.SystemEvent(s.At,reading.Aircraft!=liveAircraft?"Aircraft changed":"Simulator clock discontinuity","Tracking stopped to protect the flight record.","alert"));
        }
        liveLast = s.At; liveAircraft = reading.Aircraft;
        if (liveInvalid) { ConnectionText.Text = "Aircraft changed or the simulator clock jumped. Click Disconnect, then Connect to begin a new monitor session."; RefreshLiveTracker(reading.Aircraft, s.At, liveRecorder?.Phase, "Tracking stopped because the aircraft or simulator clock changed."); return; }
        if (liveRecorder is null)
        {
            if (!s.OnGround || s.GroundSpeedKnots >= 0.5 || !s.ParkingBrake || s.Paused || s.Slewing)
            { ConnectionText.Text = "Connected. Waiting for a stationary aircraft with parking brake set and simulation unpaused."; var observed = !s.OnGround ? FlightPhase.Airborne : s.GroundSpeedKnots >= 1 ? FlightPhase.TaxiOut : FlightPhase.AtGate; RefreshLiveTracker(reading.Aircraft, s.At, observed, s.Paused ? "Simulator paused" : s.Slewing ? "Slew mode" : !s.OnGround ? "Airborne • departure time unavailable" : "Taxiing • monitor awaiting a gate state"); return; }
            var groundProfile = AircraftGroundProfiles.ForFamily(reading.Aircraft);
            liveRecorder = new TimelineRecorder(new PhaseDetector(groundProfile));
            liveRotation = BuildLiveRotation(activePlan, reading.Aircraft, groundProfile);
            RecordLog("monitor_armed", s.At, new { aircraft = reading.Aircraft });
            if(activePlan is not null)AddTrackingEvent(trackingEventMonitor?.SystemEvent(s.At,"Assignment loaded",$"{activePlan.FlightNumber} • {activePlan.Origin} to {activePlan.Destination}"));
            AddTrackingEvent(trackingEventMonitor?.SystemEvent(s.At,"Aircraft identified",$"{activePlan?.AircraftType??"TYPE UNKNOWN"} • {reading.Aircraft}{(string.IsNullOrWhiteSpace(activePlan?.Registration)?"":$" • {activePlan.Registration}")}"));
            var gate=activePlan?.DepartureGate is {Length:>0}?$"Gate {activePlan.DepartureGate}":"Departure gate unassigned";var fuel=double.IsFinite(s.FuelTotalWeightPounds)?$" • Fuel {s.FuelTotalWeightPounds:0} lb":"";
            AddTrackingEvent(trackingEventMonitor?.SystemEvent(s.At,"Preflight at gate",$"{gate} • Parking brake set • Engines off{fuel}"));
            AddTrackingEvent(trackingEventMonitor?.SystemEvent(s.At,"Flight monitoring armed",$"{reading.Aircraft} • stationary at gate"));
        }
        var milestone=liveRecorder.Observe(s);
        if (milestone is not null)
        {
            milestones.Add($"LIVE {milestone.At.UtcDateTime:HH:mm:ss}Z   {PhaseLabel(milestone.Phase)}");
            RecordLog("flight_milestone", milestone.At, new { phase = milestone.Phase.ToString(), label = PhaseLabel(milestone.Phase) });
            if (liveRotation is not null) liveRotation = RotationPlanner.ApplyMilestone(liveRotation, milestone);
            if (milestone.Phase == FlightPhase.Complete) SaveFlightLog();
        }
        var eventProgress=reading.RouteProgress??FlightTrackingView?.TrackingMap.CompletedFraction??PhaseProgress(liveRecorder.Phase,s.At,liveRotation?.Legs[0].ActualOut,liveRotation is null?null:RotationPlanner.Project(liveRotation)[0].EstimatedIn)/100;
        foreach(var entry in trackingEventMonitor?.Observe(s,liveRecorder.Phase,milestone,eventProgress,activePlan,reading.ScenarioEvent)??[])AddTrackingEvent(entry);
        LiveTimelineButton.IsEnabled = LiveDebriefButton.IsEnabled = true;
        ConnectionText.Text = $"Connected • {PhaseLabel(liveRecorder.Phase)}. Active flight tracking uses the assignment shown below.";
        tray.Text = "Alpha 6 OPS — Live " + liveRecorder.Phase;
        var altitude=double.IsFinite(s.AltitudeFeet)?$" • {s.AltitudeFeet:0} ft":"";var heading=double.IsFinite(s.HeadingDegrees)?$" • {((s.HeadingDegrees%360)+360)%360:000}°":"";
        RefreshLiveTracker(reading.Aircraft, s.At, liveRecorder.Phase, $"Live telemetry • {s.GroundSpeedKnots:0.0} kt{altitude}{heading} • Brake {(s.ParkingBrake ? "set" : "released")}");
        SaveFlightRecovery();
    }
    private void StartLog(string mode)
    {
        FinishLog("new_connection");
        try
        {
            flightLog = new TestFlightLog(LogDirectory, Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown", mode);
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
        ConnectionBadgeText.Text = label switch
        {
            "CONNECTING TO SIMULATOR" => "CONNECTING",
            "CONNECTING TO FLIGHT LAB" => "LAB CONNECTING",
            "FLIGHT LAB CONNECTED" => "LAB CONNECTED",
            "FLIGHT LAB DISCONNECTED" => "LAB DISCONNECTED",
            "FLIGHT LAB CONNECTION FAILED" => "LAB FAILED",
            "SIMULATOR CONNECTION LOST — RETRYING" => "RECONNECTING",
            "SIMULATOR CONNECTION FAILED" => "FAILED",
            _ => label.Replace("SIMULATOR ", "")
        };
        ConnectionBadgeText.ToolTip = label;
        ConnectionDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        var failed = label == "SIMULATOR CONNECTION FAILED";
        var cardColor = (Color)ColorConverter.ConvertFromString(failed ? "#091219" : background);
        var cardBrush = new SolidColorBrush(cardColor);
        ConnectionBadge.Background = cardBrush;
        if (failed)
        {
            // One brief flash, then the neutral card with a persistent red dot and retry action.
            // Animate this state's brush only: a new connection replaces it immediately, so an
            // old flash can never overwrite a newer connecting/connected state.
            cardBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(
                (Color)ColorConverter.ConvertFromString(background), cardColor, TimeSpan.FromMilliseconds(650))
            { FillBehavior = FillBehavior.Stop });
        }
    }

    private void RefreshLiveTracker(string? simulatorAircraft, DateTimeOffset? simulatorTime, FlightPhase? phase, string status)
    {
        ClearFlightButton.IsEnabled=activePlan is not null&&liveCancellation is null&&!running;
        RefreshDashboardFlight(true);
        TrackerModeText.Text = liveSource=="FLIGHT LAB"?"ACTIVE FLIGHT • FLIGHT LAB":"ACTIVE FLIGHT • LIVE SIMCONNECT";
        if (activePlan is null)
        {
            AircraftText.Text = simulatorAircraft ?? "AIRCRAFT WAITING";
            RouteText.Text = "SET AN ACTIVE FLIGHT";
            DepartureText.Text = "Add the flight number, route and planned UTC times to calculate progress and ETA.";
            DepartedValueText.Text = ArrivalValueText.Text = ElapsedValueText.Text = "—";
            PhaseText.Text = phase is null ? "MONITORING" : PhaseLabel(phase.Value).ToUpperInvariant();
            ReplayProgress.Maximum = 100; UpdateHeroProgress(PhaseProgress(phase, null, null, null));
            StatusText.Text = status;
            RefreshFlightTrackingWorkspace(simulatorTime,phase,status);
            return;
        }
        var plan = activePlan;
        var leg = liveRotation?.Legs[0];
        var projection = liveRotation is not null ? RotationPlanner.Project(liveRotation)[0] : null;
        AircraftText.Text = !string.IsNullOrWhiteSpace(plan.AircraftType) ? plan.AircraftType : simulatorAircraft ?? "AIRCRAFT WAITING";
        RouteText.Text = $"{plan.Origin} → {plan.Destination}";
        DepartureText.Text = $"{plan.FlightNumber}  •  PLANNED {plan.PlannedDepartureUtc.UtcDateTime:HH:mm}Z  •  {plan.PlannedDuration.TotalHours:0.#} HR BLOCK";
        DepartedValueText.Text = leg?.ActualOut is { } departed ? departed.UtcDateTime.ToString("HH:mm:ss'Z'") : "—";
        ArrivalValueText.Text = leg?.ActualIn is { } arrived ? arrived.UtcDateTime.ToString("HH:mm:ss'Z'") : (projection?.EstimatedIn ?? plan.PlannedArrivalUtc).UtcDateTime.ToString("HH:mm'Z'");
        ElapsedValueText.Text = leg?.ActualOut is { } start && simulatorTime is { } now && now >= start ? FormatDuration((leg.ActualIn ?? now) - start) : "—";
        PhaseText.Text = phase is null ? "MONITORING" : PhaseLabel(phase.Value).ToUpperInvariant();
        ReplayProgress.Maximum = 100;
        var phaseProgress=PhaseProgress(phase, simulatorTime, leg?.ActualOut, projection?.EstimatedIn);
        var continuousProgress=liveRouteProgress is { } routeFraction?routeFraction*100:liveTelemetry?.HasPosition==true&&FlightTrackingView is not null?FlightTrackingView.TrackingMap.CompletedFraction*100:phaseProgress;
        UpdateHeroProgress(continuousProgress);
        StatusText.Text = status;
        RefreshFlightTrackingWorkspace(simulatorTime,phase,status);
    }

    private void RefreshFlightTrackingWorkspace(DateTimeOffset? simulatorTime,FlightPhase? phase,string status)
    {
        if(FlightTrackingView is null)return;
        var leg=liveRotation?.Legs.FirstOrDefault();
        var projection=liveRotation is null?null:RotationPlanner.Project(liveRotation).FirstOrDefault();
        var phaseLabel=phase is null?(activePlan is null?"NO ACTIVE FLIGHT":"READY"):PhaseLabel(phase.Value).ToUpperInvariant();
        var progress=PhaseProgress(phase,simulatorTime,leg?.ActualOut,projection?.EstimatedIn);
        var connected=liveCancellation is not null&&!liveCancellation.IsCancellationRequested;
        FlightTrackingView.Render(activePlan,liveAircraft,phaseLabel,status,leg?.ActualOut,leg?.ActualIn,projection?.EstimatedIn,progress,trackingEvents,connected,liveTelemetry,liveRouteProgress);
    }

    private void AddTrackingEvent(TrackingEventEntry? entry)
    {
        if(entry is null)return;
        if(entry.Title=="Simulator connection lost"&&trackingEvents.LastOrDefault() is {} previous&&previous.Title==entry.Title&&previous.Summary==entry.Summary)return;
        trackingEvents.Add(entry);RecordLog("tracking_event",entry.At,new{entry.Title,entry.Summary,entry.Detail,entry.Kind});SaveFlightRecovery();
    }

    private double PhaseProgress(FlightPhase? phase, DateTimeOffset? now, DateTimeOffset? start, DateTimeOffset? eta) => phase switch
    {
        FlightPhase.AtGate => 0,
        FlightPhase.TaxiOut => 8,
        FlightPhase.Airborne when start is { } departed && now is { } current && eta > departed => Math.Clamp(10 + 82 * (current - departed).TotalSeconds / (eta.Value - departed).TotalSeconds, 10, 92),
        FlightPhase.Airborne => 45,
        FlightPhase.TaxiIn => 95,
        FlightPhase.Complete => 100,
        _ => 0
    };
    private static string FormatDuration(TimeSpan duration) => $"{Math.Max(0, (int)duration.TotalHours):00}:{Math.Max(0, duration.Minutes):00}";
}
