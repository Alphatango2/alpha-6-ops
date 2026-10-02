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
    private readonly CancellationTokenSource lifetime = new();
    private FlightSession session = new(Demo.Rotation());
    private bool exiting;
    private bool running;
    private bool trayHintShown;
    private bool initializingPresentation=true;
    private bool openingLogs;
    private readonly UserPreferences? preferences;
    private GeneralSettings generalSettings = GeneralSettings.Defaults;
    private readonly bool diagnosticMode;
    private readonly string stateDirectory;
    // Single-leg rotation for the live-tracked assignment. Actuals are applied through the same
    // RotationPlanner.ApplyMilestone/Project path the fixture-replay session uses, so a live flight
    // and a replayed one compute delay/ETA identically instead of the tracker hand-rolling its own math.
    private static string LogDirectory => Path.Combine(CrashReporter.DiagnosticMode ? CrashReporter.RootDirectory : ProductStorage.Current.Diagnostics, "TestLogs");
    internal bool IsRunning => running;
    internal bool TrayVisible => tray.Visible;
    internal IReadOnlyList<LegProjection> Projection => RotationPlanner.Project(session.Rotation);
    internal FlightHistoryDatabase? FlightHistory => flightHistory;
    internal FlightPhase? RecoveredFlightPhase=>flightState.Phase;
    internal int RecoveredTrackingEventCount=>trackingEvents.Count;
    internal double? RecoveredRouteProgress=>liveRouteProgress;
    internal ActiveFlightPlan? ActivePlanForTest=>activePlan;
    private string SelectedFixture => (string)(FixtureCombo.SelectedItem ?? EmbeddedReplay.Fixtures[0]);
    private string PilotName => string.IsNullOrWhiteSpace(PilotNameBox.Text) ? "Unspecified" : PilotNameBox.Text.Trim();

    public MainWindow(string? diagnosticDirectory = null) : this(App.GetFlightSession(diagnosticDirectory), diagnosticDirectory) { }

    internal MainWindow(FlightSessionCoordinator coordinator, string? diagnosticDirectory = null)
    {
        this.coordinator=coordinator;
        diagnosticMode = diagnosticDirectory is not null;
        stateDirectory=diagnosticDirectory??CrashReporter.RootDirectory;
        InitializeComponent();
        InitializeShell();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";
        VersionText.Text = $"SIERRA 4  •  v{version}";
        Title = $"Sierra 4 v{version}";
        SourceInitialized += (_, _) => { InitializeMonitorTracking(); ApplyInitialWindowSize(); };
        trayIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "") ?? (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
        tray = new Forms.NotifyIcon
        {
            Icon = trayIcon,
            Text = "Sierra 4 — Flight monitoring",
            Visible = true
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Sierra 4", null, (_, _) => Dispatcher.Invoke(RestoreWindow));
        menu.Items.Add("Exit Sierra 4", null, (_, _) => Dispatcher.Invoke(ExitApplication));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreWindow);
        EventsList.ItemsSource = milestones;
        Closing += OnClosing;
        StateChanged += (_, _) => OnWindowStateChanged();
        preferences = diagnosticDirectory is null ? UserPreferencesStore.Load() : null;
        generalSettings = GeneralSettingsStore.Load(diagnosticDirectory);
        FixtureCombo.ItemsSource = EmbeddedReplay.Fixtures;
        RestoreDashboardPreferences(preferences);
        PilotNameBox.Text=coordinator.CurrentPilotName=="Unspecified"?"":coordinator.CurrentPilotName;
        ResetPreview();
        InitializeDashboard(diagnosticDirectory);
        ApplyGeneralSettings(generalSettings);
        initializingPresentation=false;
        coordinator.SnapshotChanged += RuntimeChanged;
        coordinator.DiagnosticsChanged += DiagnosticsChanged;
        DiagnosticsChanged(this,EventArgs.Empty);
        Closed += (_,_) => DisposePresentation();
        if(coordinator.Snapshot.Assignment is not null || coordinator.Snapshot.Telemetry is not null) RuntimeChanged(this,EventArgs.Empty);
        else RenderConnection(coordinator.Snapshot);
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
        ConnectButton.IsEnabled = false;
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
        lifetime.Cancel();
        DisposePresentation();
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
        if (running || flightState.ConnectionActive)
        {
            OpsNoticeWindow.Show(this, "Active flight", "Finish the replay or disconnect the simulator before changing the active flight.");
            return;
        }
        var dialog = new ActiveFlightWindow(activePlan) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Plan is null) return;
        coordinator.AcceptAssignment(dialog.Plan);
        RefreshLiveTracker(liveAircraft, liveLast, flightState.Phase, "Assignment ready. Connect to MSFS 2024 to begin tracking.");
    }
    private void ClearFlight_Click(object sender,RoutedEventArgs e)
    {
        if(running||flightState.ConnectionActive){OpsNoticeWindow.Show(this,"Active flight","Finish the replay or disconnect the simulator before clearing the active flight.");return;}
        coordinator.ClearAssignment();
        RefreshLiveTracker(null,null,null,"No active flight");ShowDashboard();
    }




    private void LiveTimeline_Click(object sender, RoutedEventArgs e)
    {
        if (flightState.Phase is null) return;
        new TimelineWindow(coordinator.ReadTimeline()!) { Owner = this }.ShowDialog();
    }
    private void LiveDebrief_Click(object sender, RoutedEventArgs e)
    {
        if (flightState.Phase is null) return;
        var legs = liveRotation is not null ? RotationPlanner.Project(liveRotation) : [];
        new DebriefWindow(flightState.Phase!.Value, flightState.PhaseEvents, DebriefSummary.Segments(flightState.PhaseEvents), legs) { Owner = this }.ShowDialog();
    }
    private async void Logs_Click(object sender, RoutedEventArgs e)
    {
        if (openingLogs) return;
        if (programMonitor is null) { OpsNoticeWindow.Show(this, "Alpha 6 OPS", "The diagnostic database is unavailable. A crash report was saved with the startup error.", true); return; }
        openingLogs = true;
        try
        {
            await programMonitor.RefreshNowAsync();
            var rows = await programMonitor.ReadRecentAsync();
            if (exiting) return;
            new LogDatabaseWindow(programMonitor, rows) { Owner = this }.ShowDialog();
        }
        finally { openingLogs = false; }
    }
    private void FlightHistory_Click(object sender, RoutedEventArgs e)
    {
        if (flightHistory is null) { OpsNoticeWindow.Show(this, "Alpha 6 OPS", "The flight history database is unavailable. A crash report was saved with the startup error.", true); return; }
        new FlightHistoryWindow(flightHistory) { Owner = this }.ShowDialog();
    }

    internal bool CanQuickConnect => ConnectButton.IsEnabled && !flightState.ConnectionActive && !running;

    private async void QuickConnect_Click(object sender, RoutedEventArgs e)
    {
        SetAdvanced(false);
        if (CanQuickConnect) await ConnectSimulatorAsync(showRotationDetails: false);
        else OpenTools(); // Inspect/cancel an existing session; never disconnect it by accident.
    }

    private async void Connect_Click(object sender, RoutedEventArgs e) =>
        await ConnectSimulatorAsync(showRotationDetails: true);

    private async Task ConnectSimulatorAsync(bool showRotationDetails)
    {
        if(running)return;
        SetAdvanced(showRotationDetails);
        await coordinator.ConnectSimulatorAsync();
    }
    private void Disconnect_Click(object sender, RoutedEventArgs e) => coordinator.DisconnectSimulator();






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
            coordinator.ExportFlightLog(journal, save.FileName);
            LogStatusText.Text = "Exported: " + save.FileName;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { LogStatusText.Text = "Could not export test log: " + error.Message; }
    }
    internal void SetConnectionBadge(string label, string color, string background)
    {
        ConnectionBadgeText.Text = label switch
        {
            "CONNECTING TO SIMULATOR" => "CONNECTING",
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
        ClearFlightButton.IsEnabled=activePlan is not null&&!flightState.ConnectionActive&&!running;
        RefreshDashboardFlight(true);
        if (currentLiveReading is not null && !CanPresentCandidate)
        {
            DashboardFlights = [];
            RefreshFlightsTable();
            RenderObservedFlight();
            RouteText.Text = currentLiveReading.Evidence?.Route is { } observedRoute
                ? $"{observedRoute.Origin} → {observedRoute.Destination}"
                : "FREE FLIGHT · DESTINATION UNKNOWN";
            DepartureText.Text = LiveContextSummary();
            DepartedValueText.Text = HeroDepartureText.Text;
            ArrivalValueText.Text = HeroArrivalText.Text;
            ElapsedValueText.Text = observedSessionStart is { } started && simulatorTime is { } observedNow && observedNow >= started ? FormatDuration(observedNow - started) : "—";
            PhaseText.Text = phase is null ? "OBSERVING" : DisplayPhase(phase.Value).ToUpperInvariant();
            ReplayProgress.Maximum = 100; ReplayProgress.Value = 0;
            StatusText.Text = status;
            RefreshFlightTrackingWorkspace(simulatorTime, phase, status);
            return;
        }
        TrackerModeText.Text = "ACTIVE FLIGHT • LIVE SIMCONNECT";
        if (activePlan is null)
        {
            AircraftText.Text = simulatorAircraft ?? "AIRCRAFT WAITING";
            RouteText.Text = "SET AN ACTIVE FLIGHT";
            DepartureText.Text = "Add the flight number, route and planned UTC times to calculate progress and ETA.";
            DepartedValueText.Text = ArrivalValueText.Text = ElapsedValueText.Text = "—";
            PhaseText.Text = phase is null ? "MONITORING" : DisplayPhase(phase.Value).ToUpperInvariant();
            ReplayProgress.Maximum = 100; UpdateHeroProgress(PhaseProgress(phase, null, null, null));
            StatusText.Text = status;
            RefreshFlightTrackingWorkspace(simulatorTime,phase,status);
            return;
        }
        var plan = activePlan;
        var leg = new FlightLeg(plan.FlightNumber,plan.Origin,plan.Destination,plan.PlannedDepartureUtc,plan.PlannedArrivalUtc,ActualFlightOut,ActualFlightIn);
        var projection = liveRotation is not null ? RotationPlanner.Project(liveRotation)[0] : null;
        var estimatedIn=flightState.EstimatedArrival;
        AircraftText.Text = AircraftIdentity.CanonicalType(plan.AircraftType,simulatorAircraft);
        RouteText.Text = $"{plan.Origin} → {plan.Destination}";
        DepartureText.Text = $"{plan.FlightNumber}  •  PLANNED {plan.PlannedDepartureUtc.UtcDateTime:HH:mm}Z  •  {plan.PlannedDuration.TotalHours:0.#} HR BLOCK";
        DepartedValueText.Text = leg?.ActualOut is { } departed ? (joinedInProgress?"≈":"")+departed.UtcDateTime.ToString("HH:mm'Z'") : "—";
        ArrivalValueText.Text = (ActualFlightIn??estimatedIn)?.UtcDateTime.ToString("HH:mm'Z'")??"—";
        HeroArrivalText.Text=(ActualFlightIn??estimatedIn)?.UtcDateTime.ToString("HH:mm")??"—";
        ElapsedValueText.Text = leg?.ActualOut is { } start && simulatorTime is { } now && now >= start ? FormatDuration((leg.ActualIn ?? now) - start) : "—";
        PhaseText.Text = phase is null ? "MONITORING" : DisplayPhase(phase.Value).ToUpperInvariant();
        ReplayProgress.Maximum = 100;
        var phaseProgress=PhaseProgress(phase, simulatorTime, leg?.ActualOut, estimatedIn);
        var continuousProgress=liveRouteProgress is { } routeFraction?routeFraction*100:0;
        UpdateHeroProgress(continuousProgress);
        StatusText.Text = status;
        UpdateHeroScheduleStatus(leg,estimatedIn);
        if (currentLiveReading is not null)
        {
            TrackerModeText.Text = liveAssociation.Accepted ? "LIVE FLIGHT • VERIFIED ASSIGNMENT" : "LIVE FLIGHT • ASSIGNMENT REVIEW";
            if(!liveAssociation.Accepted){HeroStatusText.Text="ASSIGNMENT REVIEW";HeroTimingText.Text=LiveContextSummary();HeroStatusText.Foreground=OpsUi.Brush("#FFDA00");HeroStatusBadge.Background=OpsUi.Brush("#433817");}
            if(!liveDataCurrent){TrackerModeText.Text="LAST OBSERVATION • TELEMETRY UNAVAILABLE";HeroStatusText.Text="LAST OBSERVED";}
            HeroTimingText.ToolTip = $"{LiveContextSummary()}. {liveAssociation.Explanation}";
            if (arrivalMismatch is not null)
            {
                HeroStatusText.Text = "AIRPORT REVIEW";
                HeroTimingText.Text = arrivalMismatch;
                HeroStatusText.Foreground = OpsUi.Brush("#FFDA00");
                HeroStatusBadge.Background = OpsUi.Brush("#433817");
            }
        }
        RefreshFlightTrackingWorkspace(simulatorTime,phase,status);
    }

    private void RefreshFlightTrackingWorkspace(DateTimeOffset? simulatorTime,FlightPhase? phase,string status)
    {
        if(FlightTrackingView is null)return;
        var leg=liveRotation?.Legs.FirstOrDefault();
        var projection=liveRotation is null?null:RotationPlanner.Project(liveRotation).FirstOrDefault();
        var phaseLabel=phase is null?(activePlan is null?"NO ACTIVE FLIGHT":"READY"):DisplayPhase(phase.Value).ToUpperInvariant();
        var estimatedIn=flightState.EstimatedArrival;
        var progress=(liveRouteProgress??0)*100;
        var connected=flightState.Connected;
        var routeProgress=liveRouteProgress;
        FlightTrackingView.Render(currentLiveReading is not null&&!CanPresentCandidate?null:activePlan,liveAircraft,phaseLabel,status,ActualFlightOut,ActualFlightIn,estimatedIn,progress,trackingEvents,connected,liveTelemetry,routeProgress,liveAssociation.Accepted,LiveRouteReliable,flightState.Complete,flightState.PirepEligible);
    }

    private string DisplayPhase(FlightPhase phase) => phase==FlightPhase.Airborne ? flightState.PhaseLabel : PhaseLabel(phase);
    private bool LiveRouteReliable => flightState.RouteReliable;
    private double? LiveRemainingDistance => flightState.RemainingDistanceNm;

    private void UpdateHeroScheduleStatus(FlightLeg? leg,DateTimeOffset? estimate)
    {
        if(activePlan is null||leg is null)return;
        if(!ScheduleTrusted)
        {
            HeroStatusText.Text="●  SCHEDULE REVIEW";HeroStatusText.Foreground=OpsUi.Brush("#FFDA00");HeroStatusBadge.Background=OpsUi.Brush("#433817");
            HeroTimingText.ToolTip=FlightClock.ReviewMessage(activePlan,ActualFlightOut??liveLast??activePlan.PlannedDepartureUtc);return;
        }
        var arrival=ActualFlightIn??estimate;
        if(arrival is null){HeroStatusText.Text="●  ESTIMATE PENDING";HeroStatusText.Foreground=OpsUi.Brush("#AFC0CC");HeroStatusBadge.Background=OpsUi.Brush("#1A2933");return;}
        var minutes=(int)Math.Round((arrival.Value-activePlan.PlannedArrivalUtc).TotalMinutes);var absolute=Math.Abs(minutes);
        HeroStatusText.Text=absolute<=5?"●  ON TIME":$"●  {absolute} MIN {(minutes<0?"EARLY":"LATE")}";
        var late=minutes>5;HeroStatusText.Foreground=OpsUi.Brush(late&&minutes>=30?"#FF9690":late?"#FFDA00":"#7DEA57");HeroStatusBadge.Background=OpsUi.Brush(late&&minutes>=30?"#512621":late?"#433817":"#163F24");
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
