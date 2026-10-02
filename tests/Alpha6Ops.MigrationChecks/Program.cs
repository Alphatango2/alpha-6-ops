using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Alpha6Ops.Core;
using Alpha6Ops.Desktop;

// Test-only host. No production entry point, simulator connection, or user-profile
// write is needed. Reflection follows the existing Desktop diagnostic harnesses.
internal static class Program
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags Statics = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly Assembly Desktop = typeof(MainWindow).Assembly;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
    private static readonly List<CheckResult> Results = [];
    private static readonly List<MainWindow> Windows = [];
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-17T02:05:00Z");
    private static string output = "";
    private static readonly string[] Expected = [
        "SHELL_NATIVE_VIEWPORT", "SHELL_NAVIGATION_PURITY", "VIEW_RECREATE", "NAV_CONNECTION_OWNER", "NAV_OPERATIONAL_STATE", "REFRESH_PURITY", "FIRST_RENDER_PURITY", "HIDDEN_RUNTIME",
        "RECONNECT_REJECT_OLD_CALLBACK", "RECONNECT_ACCEPT_CURRENT_CALLBACK", "ASSIGNMENT_NO_ANIMATION_WAIT",
        "ASSIGNMENT_EVENTUAL_PERSISTENCE", "PIREP_SUBMIT_NO_ANIMATION_WAIT", "PIREP_CLOSEOUT_NO_ANIMATION_WAIT",
        "PIREP_EVENTUAL_DURABILITY", "TEXT_COMPLETION_INDEPENDENCE", "TEXT_HISTORY_INDEPENDENCE",
        "DATA_ASSIGNMENT", "DATA_RECOVERY", "DATA_GATES", "DATA_HISTORY", "PROFILE_HISTORY_READABLE",
        "PROFILE_GATES_READABLE", "PROFILE_SOURCE_UNCHANGED", "NATIVE_CONNECTION_LIFECYCLE", "NATIVE_RECONNECT", "REPLAY_COVERAGE",
        "SESSION_WITHOUT_VIEW", "SESSION_REATTACH", "SESSION_PENDING_REATTACH", "SNAPSHOT_DETACHED", "SNAPSHOT_INPUT_OWNERSHIP", "OBSERVER_FAILURE_ISOLATED"
    ];

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 4) { Console.Error.WriteLine("Usage: MigrationChecks OUTPUT JOURNAL_MANIFEST REPLAY_RESULTS PROFILE_DIRECTORY"); return 2; }
        output = Path.GetFullPath(args[0]);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
        { Console.Error.WriteLine("Use a fresh output directory; previous evidence is never overwritten."); return 2; }
        Directory.CreateDirectory(output);
        var app = new TestApplication();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var frame = new DispatcherFrame();
        var task = RunAsync(args);
        task.ContinueWith(_ => app.Dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        try { task.GetAwaiter().GetResult(); }
        catch (Exception error) { Check("HARNESS", false, error.ToString()); }
        finally
        {
            foreach (var window in Windows) Release(window);
            foreach (var id in Expected.Where(id => Results.All(result => result.Id != id)))
                Skip(id, "Not executed because an earlier harness operation failed.");
            var report = new { passed = Results.All(r => r.Status != "Failed"), checks = Results,
                counts = new { executed = Results.Count(r => r.Status != "Skipped"), passed = Results.Count(r => r.Status == "Passed"), failed = Results.Count(r => r.Status == "Failed"), skipped = Results.Count(r => r.Status == "Skipped") } };
            File.WriteAllText(Path.Combine(output, "migration-checks.json"), JsonSerializer.Serialize(report, Json));
            Console.WriteLine(JsonSerializer.Serialize(report.counts));
            app.Shutdown();
        }
        return Results.Any(r => r.Status == "Failed") || Results.Any(r => r.Status == "Skipped" && !r.Id.StartsWith("NATIVE_") && !r.Id.StartsWith("PROFILE_")) ? 1 : 0;
    }

    private static async Task RunAsync(string[] args)
    {
        await Section("views", ViewLifecycleAsync);
        await Section("application-owner", ApplicationOwnerAsync);
        await Section("sierra-shell", () =>
        {
            var window = Window("sierra-shell");
            var shell = window.FindName("Shell") as FrameworkElement;
            Check("SHELL_NATIVE_VIEWPORT", shell is not null && shell.ActualWidth > 0 &&
                !double.IsFinite(shell.Width) && shell is not System.Windows.Controls.Viewbox,
                "Sierra 4 shell occupies the native viewport without a fixed reference-surface Viewbox.");
            StartLive(window);
            var before=Snapshot(window);var token=Field(window,"liveCancellation");
            var navigation=Property(shell!,"Navigation")!;
            foreach(var destination in new[]{"Airports","Flights","Dispatch","Logbook","Operations"})
                Call(navigation,"Request",Enum.Parse(Type("ShellSection"),destination));
            Call(shell!,"OpenSearch");Call(shell!,"CloseOverlays");Call(shell!,"ShowUtilities");Call(shell!,"CloseOverlays");
            Check("SHELL_NAVIGATION_PURITY",before==Snapshot(window)&&ReferenceEquals(token,Field(window,"liveCancellation")),
                "All embedded Sierra destinations and search/status overlays preserve flight state, storage and simulator owner.");
            return Task.CompletedTask;
        });
        await Section("pending-connection-view", PendingConnectionViewAsync);
        await Section("first-render", () => { FirstRenderPurity(); return Task.CompletedTask; });
        await Section("reconnect", ReconnectAsync);
        await Section("dispatch", DispatchAsync);
        await Section("pirep", PirepAsync);
        await Section("text", () => { TextIndependence(); return Task.CompletedTask; });
        await Section("data", () => { FrozenData(); ProfileCopies(args[3]); return Task.CompletedTask; });
        await Section("coverage", () => { Coverage(args[1], args[2]); return Task.CompletedTask; });
        await Section("large-index", async () =>
        {
            var directory = Path.Combine(output, "large-index"); Directory.CreateDirectory(directory);
            await (Task)Static("DiagnosticIndexSmokeTest", "RunAsync", directory, true)!;
        });
        Skip("NATIVE_CONNECTION_LIFECYCLE", "No real SimConnect open/close was exercised. Synthetic tests do not prove native handles or worker disposal.");
        Skip("NATIVE_RECONNECT", "Rapid queued callbacks were tested below the native boundary; an actual simulator disconnect/reconnect remains a hardware validation.");
    }

    private static async Task Section(string name, Func<Task> body)
    {
        try { await body(); }
        catch (Exception error) { Check("HARNESS_" + name, false, (error is TargetInvocationException ? error.InnerException ?? error : error).ToString()); }
    }

    private static Type Type(string name) => Desktop.GetType("Alpha6Ops.Desktop." + name, true)!;
    private static object New(string name, params object?[] args) => Activator.CreateInstance(Type(name), Members, null, args, null)!;
    private static object? Call(object target, string method, params object?[] args) => target is MainWindow window ? FlightSessionDiagnosticAccess.Invoke(window,method,args) : target.GetType().GetMethods(Members | BindingFlags.DeclaredOnly).Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(target, args);
    private static object? Static(string type, string method, params object?[] args) => Type(type).GetMethods(Statics).Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(null, args);
    private static object? Field(object target, string name) => target is MainWindow window ? FlightSessionDiagnosticAccess.Get(window,name) : target.GetType().GetField(name, Members)!.GetValue(target);
    private static void Set(object target, string name, object? value) { if(target is MainWindow window)FlightSessionDiagnosticAccess.Set(window,name,value);else target.GetType().GetField(name, Members)!.SetValue(target,value); }
    private static object? Property(object target, string name) => target.GetType().GetProperty(name, Members)!.GetValue(target);
    private static object Plan() => JsonSerializer.Deserialize(File.ReadAllText(Fixture("active-flight.json")), Type("ActiveFlightPlan"), Json)!;
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    private static void Check(string id, bool passed, string detail) { Results.Add(new(id, passed ? "Passed" : "Failed", detail)); Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {id}: {detail}"); }
    private static void Skip(string id, string detail) { Results.Add(new(id, "Skipped", detail)); Console.WriteLine($"SKIP {id}: {detail}"); }
    private static MainWindow Window(string name, ISimulatorConnectionSource? source=null)
    {
        var directory = Path.Combine(output, name); Directory.CreateDirectory(directory);
        var owner=new FlightSessionCoordinator(directory,true,new WpfFlightRuntimeDispatcher(Dispatcher.CurrentDispatcher),source);
        var window = new MainWindow(owner,directory); Windows.Add(window); window.Show(); return window;
    }
    private static string Root(MainWindow window) => (string)Field(window, "stateDirectory")!;
    private static void Accept(MainWindow window) => Call(window, "AcceptDispatchFlight", Plan());
    private static void StartLive(MainWindow window)
    {
        Accept(window); Set(window, "liveCancellation", new CancellationTokenSource());
        Feed(window, 0, true, 0, true, 0);
        Feed(window, 1, true, 3, false, 0);
        Feed(window, 5, true, 3, false, 0);
        Feed(window, 6, false, 150, false, .01);
        Feed(window, 10, false, 180, false, .012);
        Feed(window, 20, false, 430, false, .02);
    }
    private static object Reading(int seconds, bool ground = false, double speed = 430, bool brake = false, double fraction = .02)
    {
        var position = new GeoPosition(-33.946 + (-42.836111 + 33.946) * fraction, 151.177111 + (147.510278 - 151.177111) * fraction);
        var sample = new Telemetry(Start.AddSeconds(seconds), ground, speed, brake, true,
            LatitudeDegrees: position.Latitude, LongitudeDegrees: position.Longitude, AltitudeFeet: ground ? 20 : 32000,
            PressureAltitudeFeet: ground ? 20 : 32000, AltitudeAboveGroundFeet: ground ? 0 : 30000,
            VerticalSpeedFeetPerMinute: ground ? 0 : 500, IndicatedAirspeedKnots: speed);
        return new LiveReading("Fenix A320", sample, new FlightEvidence(position, "VH-TEST", new("YSSY", "YMHB"), true));
    }
    private static void Feed(MainWindow window, int seconds, bool ground = false, double speed = 430, bool brake = false, double fraction = .02)
        => Call(window, "ObserveLive", Reading(seconds, ground, speed, brake, fraction));
    private static async Task Idle() => await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    private static async Task Until(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition()) { if (watch.Elapsed > TimeSpan.FromSeconds(12)) throw new TimeoutException("Async UI handler did not finish."); await Task.Delay(25); }
    }
    private static string Snapshot(MainWindow window)
    {
        var state = new Dictionary<string, object?>();
        foreach (var name in new[] { "activePlan", "liveTelemetry", "liveLast", "liveRouteProgress", "liveRotation", "liveAssociation", "trackingEvents", "joinedInProgress", "liveFlightHistoryId", "completedFlightHistoryId" }) state[name] = Field(window, name);
        var recorder = (TimelineRecorder?)Field(window, "liveRecorder");
        state["recorder"] = recorder is null ? null : new { recorder.Phase, recorder.Events, recorder.Snapshots.Count };
        foreach (var name in new[] { "arrivalEstimator", "routeTracker" })
        {
            var instance = Field(window, name)!;
            state[name] = instance.GetType().GetFields(Members).ToDictionary(f => f.Name, f => f.GetValue(instance));
        }
        state["monitor"] = Field(window, "trackingEventMonitor") is { } monitor ? Call(monitor, "CaptureState") : null;
        state["recovery"] = File.Exists(Path.Combine(Root(window), "active-flight-state.json")) ? File.ReadAllText(Path.Combine(Root(window), "active-flight-state.json")) : null;
        state["history"] = Call(Field(window, "flightHistory")!, "ReadRecentFlights", 1000);
        return JsonSerializer.Serialize(state, Json);
    }

    private static async Task ViewLifecycleAsync()
    {
        var window = Window("views"); StartLive(window); await Idle();
        var before = Snapshot(window); var token = Field(window, "liveCancellation"); var recorder = Field(window, "liveRecorder");
        var unloads = 0;
        foreach (var name in new[] { "FlightTrackingWorkspace", "DispatchWorkspace", "PilotLogbookWorkspace" })
        for (var cycle = 0; cycle < 3; cycle++)
        {
            var view = (UserControl)New(name); view.Unloaded += (_, _) => unloads++;
            if (name == "FlightTrackingWorkspace")
                Call(view, "Render", Field(window, "activePlan"), "Fenix A320", "CLIMB", "Live telemetry", Start.AddSeconds(1), null, null, 2d, Field(window, "trackingEvents"), true, Field(window, "liveTelemetry"), Field(window, "liveRouteProgress"), true, true, false, false);
            else if (name == "DispatchWorkspace") { Call(view, "ConfigureGates", Root(window)); Call(view, "Render", Field(window, "activePlan"), "VIEW RELEASE"); }
            else Call(view, "Render", Field(window, "flightHistory"));
            var host = new Window { Content = view, Width = 640, Height = 400, ShowInTaskbar = false };
            host.Show(); await Idle(); host.Content = null; host.Close(); await Idle();
        }
        Check("VIEW_RECREATE", unloads == 9 && before == Snapshot(window) && ReferenceEquals(recorder, Field(window, "liveRecorder")), $"Recreated/unloaded nine disposable view hosts; unload events={unloads}. UserControls have no IDisposable contract.");
        foreach (var method in new[] { "ShowDispatch", "ShowPilotLogbook", "ShowDashboard", "ShowFlightTracking" })
            for (var i = 0; i < 3; i++) { Call(window, method); await Idle(); }
        Check("NAV_CONNECTION_OWNER", ReferenceEquals(token, Field(window, "liveCancellation")) && !((CancellationTokenSource)token!).IsCancellationRequested,
            "Workspace switching preserved the connection owner/token; no native connection was opened by this harness.");
        Check("NAV_OPERATIONAL_STATE", before == Snapshot(window), "Twelve workspace switches must preserve assignment, actuals, events, estimator, tracker, history and recovery bytes.");
        before = Snapshot(window);
        for (var i = 0; i < 10; i++)
        {
            Call(window, "RefreshLiveTracker", "Fenix A320", Start.AddSeconds(20), FlightPhase.Airborne, "Presentation refresh");
            Call(window, "RefreshFlightTrackingWorkspace", Start.AddSeconds(20), FlightPhase.Airborne, "Presentation refresh");
        }
        Check("REFRESH_PURITY", before == Snapshot(window), "Twenty refresh calls against a settled sample must leave operational state and persisted recovery unchanged.");
        var liveRecorder = (TimelineRecorder)recorder!;
        var operationalAdvancement = true;
        var seconds = 24;
        foreach (var method in new[] { "ShowDispatch", "ShowPilotLogbook", "ShowDashboard", "ShowFlightTracking" })
        {
            Call(window, method); var priorCount = liveRecorder.Snapshots.Count; var priorProgress = (double?)Field(window, "liveRouteProgress");
            Feed(window, seconds, fraction: .02 + (seconds - 20) * .0002);
            using var saved = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(window), "active-flight-state.json")));
            operationalAdvancement &= liveRecorder.Snapshots.Count == priorCount + 1 && liveRecorder.Snapshots[^1].Sample.At == Start.AddSeconds(seconds)
                && (double?)Field(window, "liveRouteProgress") > priorProgress && saved.RootElement.GetProperty("LastSimulatorUtc").GetDateTimeOffset() == Start.AddSeconds(seconds);
            seconds += 4;
        }
        var history = Field(window, "flightHistory")!; var flightId = Field(window, "liveFlightHistoryId");
        var priorHistoryCount = ((IEnumerable)Call(history, "ReadFlightEvents", flightId)!).Cast<object>().Count();
        window.Hide(); Feed(window, seconds, true, 30, false, .024); Feed(window, seconds + 4, true, 20, false, .0241);
        using var finalRecovery = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(window), "active-flight-state.json")));
        Check("HIDDEN_RUNTIME", operationalAdvancement && ReferenceEquals(recorder, Field(window, "liveRecorder")) && liveRecorder.Phase == FlightPhase.TaxiIn
            && liveRecorder.Events.Any(e => e.Phase == FlightPhase.TaxiIn && e.At == Start.AddSeconds(seconds))
            && finalRecovery.RootElement.GetProperty("Phase").GetInt32() == (int)FlightPhase.TaxiIn
            && ((IEnumerable)Call(history, "ReadFlightEvents", flightId)!).Cast<object>().Count() > priorHistoryCount,
            "Each workspace advanced recorder snapshots, geographic progress and saved telemetry; hidden-window landing advanced phase, milestone history and persisted recovery.");
    }

    private static void FirstRenderPurity()
    {
        var window = Window("first-render"); StartLive(window);
        // Isolate the architectural contract: a valid current sample whose
        // estimator has not consumed it. This is not claimed as an observed
        // normal-flight defect; rendering must not be the owner of that work.
        ((ArrivalEstimator)Field(window, "arrivalEstimator")!).Reset();
        var before = Snapshot(window);
        Call(window, "RefreshFlightTrackingWorkspace", Start.AddSeconds(20), FlightPhase.Airborne, "First presentation");
        Check("FIRST_RENDER_PURITY", before == Snapshot(window), "A render with an unconsumed estimator must not advance its operational state; settled-sample idempotence is checked separately.");
    }

    private static async Task ApplicationOwnerAsync()
    {
        var window = Window("application-owner"); StartLive(window);
        var owner = typeof(MainWindow).GetField("coordinator", Members)?.GetValue(window);
        if (owner is null)
        {
            Check("SESSION_WITHOUT_VIEW", false, "The active session still belongs to MainWindow.");
            Check("SNAPSHOT_DETACHED", false, "There is no detached application snapshot.");
            Check("OBSERVER_FAILURE_ISOLATED", false, "There is no independent application publication boundary.");
            return;
        }
        var captured = Property(owner, "Snapshot")!;
        var serialized = JsonSerializer.Serialize(captured, captured.GetType(), Json);
        var recorder = Field(owner, "liveRecorder");
        Call(owner,"SetPilotName","Session pilot");
        Set(window, "exiting", true); window.Close(); await Idle();
        Call(owner, "ObserveLive", Reading(24, fraction: .021));
        using var saved = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(window), "active-flight-state.json")));
        Check("SESSION_WITHOUT_VIEW", ReferenceEquals(recorder, Field(owner, "liveRecorder")) &&
            ((TimelineRecorder)recorder!).Snapshots[^1].Sample.At == Start.AddSeconds(24) &&
            saved.RootElement.GetProperty("LastSimulatorUtc").GetDateTimeOffset() == Start.AddSeconds(24),
            "Closing the owning presentation leaves the application session processing and saving telemetry.");
        Check("SNAPSHOT_DETACHED", JsonSerializer.Serialize(captured, captured.GetType(), Json) == serialized &&
            !ReferenceEquals(captured, Property(owner, "Snapshot")), "An earlier immutable snapshot remains unchanged after the next sample.");
        var replacement=new MainWindow((FlightSessionCoordinator)owner,Root(window));Windows.Add(replacement);replacement.Show();await Idle();
        Check("SESSION_REATTACH", (string?)Field(owner,"PilotName")=="Session pilot" &&
            ReferenceEquals(recorder,Field(owner,"liveRecorder")) && replacement.RecoveredFlightPhase==FlightPhase.Airborne,
            "Reattaching a new presentation preserves the session's pilot identity, recorder and phase.");
        var rawEngines=new List<EngineReading>{new(1,1,1000,30,60,0)};
        var input=(LiveReading)Reading(26,fraction:.0215);
        input=input with{Telemetry=input.Telemetry with{EngineReadings=rawEngines}};
        Call(owner,"ObserveLive",input);
        var immutable=((FlightSessionCoordinator)owner).Snapshot;
        rawEngines.Clear();
        Check("SNAPSHOT_INPUT_OWNERSHIP",immutable.Telemetry?.EngineReadings?.Count==1&&
            immutable.Reading?.Telemetry.EngineReadings?.Count==1&&((Telemetry)Field(owner,"liveTelemetry")!).EngineReadings?.Count==1,
            "Mutating a simulator-owned input collection cannot alter runtime or published telemetry.");
        var changed = owner.GetType().GetEvent("SnapshotChanged", Members)!;
        EventHandler brokenView = (_, _) => throw new InvalidOperationException("Diagnostic subscriber failure");
        changed.AddEventHandler(owner, brokenView);
        try
        {
            Call(owner, "ObserveLive", Reading(28, fraction: .022));
            using var recovery = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(window), "active-flight-state.json")));
            Check("OBSERVER_FAILURE_ISOLATED", ((TimelineRecorder)recorder!).Snapshots[^1].Sample.At == Start.AddSeconds(28) &&
                recovery.RootElement.GetProperty("LastSimulatorUtc").GetDateTimeOffset() == Start.AddSeconds(28),
                "A broken presentation subscriber cannot abort operational processing or recovery persistence.");
        }
        finally { changed.RemoveEventHandler(owner, brokenView); }
    }

    private static async Task PendingConnectionViewAsync()
    {
        var source = new FakeSimulatorSource();
        var window = Window("pending-connection-view", source);
        var owner = FlightSessionDiagnosticAccess.Owner(window);
        var connecting = owner.ConnectSimulatorAsync();
        Set(window, "exiting", true); window.Close();
        var replacement = new MainWindow(owner, Root(window)); Windows.Add(replacement); replacement.Show();
        await Idle();
        Check("SESSION_PENDING_REATTACH", owner.Snapshot.Assignment is null && owner.Snapshot.Telemetry is null &&
            owner.Snapshot.Connection == SimulatorConnectionState.Connecting && source.Runs.Count == 1 &&
            !replacement.ConnectButton.IsEnabled && replacement.DisconnectButton.IsEnabled &&
            replacement.ConnectionText.Text == owner.Snapshot.ConnectionMessage,
            "A replacement view immediately reflects the existing pending connection and exposes Disconnect before any telemetry arrives.");
        Call(replacement, "Disconnect_Click", replacement.DisconnectButton, new RoutedEventArgs());
        await connecting;
    }

    private static async Task ReconnectAsync()
    {
        var source=new FakeSimulatorSource();
        var window=Window("reconnect",source);StartLive(window);
        Set(window,"liveCancellation",null);
        var owner=FlightSessionDiagnosticAccess.Owner(window);
        var first=owner.ConnectSimulatorAsync();
        var before=((Telemetry)Field(window,"liveTelemetry")!).At;
        source.Runs[0].Received((LiveReading)Reading(24)); // queued while old generation is current
        owner.DisconnectSimulator();await first;
        var second=owner.ConnectSimulatorAsync();
        // Also emulate an old worker reporting after the replacement has started.
        source.Runs[0].Received((LiveReading)Reading(25));
        source.Runs[0].Opened("obsolete connection");
        await Idle();
        var after=((Telemetry)Field(window,"liveTelemetry")!).At;
        Check("RECONNECT_REJECT_OLD_CALLBACK",after==before&&owner.Snapshot.ConnectionMessage!="obsolete connection",
            $"Old queued/delayed telemetry and opened callbacks rejected across replacement: before={before:O}, after={after:O}.");
        source.Runs[1].Received((LiveReading)Reading(28));await Idle();
        Check("RECONNECT_ACCEPT_CURRENT_CALLBACK",((Telemetry)Field(window,"liveTelemetry")!).At==Start.AddSeconds(28),
            "Current-generation sample processed through the production connection loop and injected source.");
        owner.DisconnectSimulator();await second;
    }

    private sealed class FakeSimulatorSource : ISimulatorConnectionSource
    {
        internal sealed record Run(Action<string> Opened,Action<LiveReading> Received);
        internal List<Run> Runs { get; }=[];
        public bool IsRunning()=>true;
        public void Launch()=>throw new InvalidOperationException("Diagnostics must not launch MSFS.");
        public async Task RunAsync(Action<string> opened,Action<LiveReading> received,CancellationToken cancellation)
        {
            Runs.Add(new(opened,received));
            await Task.Delay(Timeout.Infinite,cancellation);
        }
    }

    private static async Task DispatchAsync()
    {
        var window = Window("dispatch"); var view = Field(window, "DispatchView")!;
        var importJson = "{\"Plan\":" + File.ReadAllText(Fixture("active-flight.json")) + ",\"GeneratedUtc\":\"2026-09-17T01:55:00Z\",\"AircraftType\":\"A320\",\"Route\":\"YSSY DCT YMHB\"}";
        var draft = JsonSerializer.Deserialize(importJson, Type("SimBriefImport"), Json)!;
        Call(view, "PresentDraft", draft);
        var watch = Stopwatch.StartNew();
        Call(view, "Accept_Click", Field(view, "AcceptButton"), new RoutedEventArgs());
        var immediate = Field(window, "activePlan") is not null && File.Exists(Path.Combine(Root(window), "active-flight.json"));
        await Until(() => Field(window, "activePlan") is not null);
        Check("ASSIGNMENT_NO_ANIMATION_WAIT", immediate, $"Assignment persisted before first animation yield={immediate}; observed persistence after {watch.ElapsedMilliseconds} ms.");
        Check("ASSIGNMENT_EVENTUAL_PERSISTENCE", Static("ActiveFlightPlanStore", "Load", Root(window)) is not null, "Accepted assignment is eventually persisted by the real MainWindow handler.");
        await Until(()=>((Button)Field(view,"AcceptButton")!).Content?.ToString()=="VIEW FLIGHT TRACKING");
    }

    private static async Task PirepAsync()
    {
        var window = Window("pirep"); Accept(window);
        var milestones = new[] { new FlightEvent(FlightPhase.TaxiOut, Start.AddMinutes(1)), new FlightEvent(FlightPhase.Airborne, Start.AddMinutes(10)), new FlightEvent(FlightPhase.TaxiIn, Start.AddMinutes(120)), new FlightEvent(FlightPhase.Complete, Start.AddMinutes(128)) };
        Set(window, "liveRecorder", new TimelineRecorder(new PhaseDetector(initialPhase: FlightPhase.Complete, lastObserved: milestones[^1].At), milestones));
        var database = Field(window, "flightHistory")!;
        var id = (string)Call(database, "BeginFlight", "Test pilot", "live", null, "Airbus A320", "YSSY", "YMHB", "BASE718", "0.22.0")!;
        Call(database, "EndFlight", id, "Complete"); Set(window, "completedFlightHistoryId", id); Call(window, "SaveFlightRecovery");
        var view = Field(window, "FlightTrackingView")!; var watch = Stopwatch.StartNew();
        var animationFinished=false;
        ((FlightTrackingWorkspace)view).PirepCloseoutCompleted+=(_,_)=>animationFinished=true;
        bool Submitted() => ((IEnumerable)Call(database, "ReadFlightEvents", id)!).Cast<object>().Any(e => (string?)Property(e, "Kind") == "pirep_submitted");
        Call(view, "SubmitPirep_Click", Field(view, "SubmitPirepButton"), new RoutedEventArgs());
        var immediate = Submitted();
        await Until(Submitted);
        var submittedAt = watch.ElapsedMilliseconds;
        var closedAtPersistence = Field(window, "activePlan") is null && !File.Exists(Path.Combine(Root(window), "active-flight-state.json"));
        await Until(() => Field(window, "activePlan") is null);
        Check("PIREP_SUBMIT_NO_ANIMATION_WAIT", immediate, $"Durable submission before animation yield={immediate}; persisted after {submittedAt} ms.");
        Check("PIREP_CLOSEOUT_NO_ANIMATION_WAIT", closedAtPersistence, $"Closeout complete when persistence observed={closedAtPersistence}; closeout observed at {watch.ElapsedMilliseconds} ms, independently of the remaining animation.");
        Check("PIREP_EVENTUAL_DURABILITY", Submitted() && !File.Exists(Path.Combine(Root(window), "active-flight.json")) && !File.Exists(Path.Combine(Root(window), "active-flight-state.json")), "Real PIREP handler eventually persists submission and clears assignment/recovery.");
        await Until(()=>animationFinished);
    }

    private static void TextIndependence()
    {
        var window = Window("text"); Accept(window); var view = Field(window, "FlightTrackingView")!;
        var entries = Array.CreateInstance(Type("TrackingEventEntry"), 0);
        void Render(string label) => Call(view, "Render", Field(window, "activePlan"), "Airbus A320", label, "", Start, Start.AddMinutes(128), null, 100d, entries, false, null, 1d, true, true, true, true);
        Render("BLOCK-IN / COMPLETE"); var original = ((Button)Field(view, "SubmitPirepButton")!).Visibility;
        Render("Arrived at gate"); var renamed = ((Button)Field(view, "SubmitPirepButton")!).Visibility;
        Check("TEXT_COMPLETION_INDEPENDENCE", original == renamed && renamed == Visibility.Visible, $"Same actual IN and 100% progress: original PIREP visibility={original}; renamed phase={renamed}.");
        var english = Window("text-history-original"); Accept(english);
        var translated = Window("text-history-renamed"); Accept(translated);
        var originalEvent=new TrackingEventEntry(Start,"Block-out / taxi out","Ground movement","GS 3 KT"){Code=TrackingEventCode.BlockOut};
        Call(english, "AddTrackingEvent", originalEvent);
        Call(translated, "AddTrackingEvent", originalEvent with{Title="Gate departure"});
        var a = Field(english, "liveFlightHistoryId") is not null; var b = Field(translated, "liveFlightHistoryId") is not null;
        Check("TEXT_HISTORY_INDEPENDENCE", a && b, $"Semantically identical departure event: original creates history={a}; renamed creates history={b}.");
    }

    private static void FrozenData()
    {
        var directory = Path.Combine(output, "frozen-data"); Directory.CreateDirectory(directory);
        foreach (var name in new[] { "active-flight.json", "active-flight-state.json", "gate-history.json" }) File.Copy(Fixture(name), Path.Combine(directory, name));
        var plan = Static("ActiveFlightPlanStore", "Load", directory)!;
        Check("DATA_ASSIGNMENT", plan is not null && (string?)Property(plan, "FlightNumber") == "BASE718" && Property(plan, "DispatchEstimatedArrivalUtc") is null, "Frozen legacy assignment reads without a newer dispatch-estimate field.");
        var recovery = Static("FlightRecoveryStore", "Load", plan, directory)!;
        Check("DATA_RECOVERY", recovery is not null && (FlightPhase)Property(recovery, "Phase")! == FlightPhase.Airborne && double.IsNaN(((Telemetry)Property(recovery, "LastTelemetry")!).AltitudeFeet) && ((IEnumerable)Property(recovery, "PhaseEvents")!).Cast<object>().Count() == 2, "Frozen recovery preserves phase, UTC milestones and named NaN telemetry.");
        var gates = ((IEnumerable)Call(New("GateHistoryStore", directory), "Load")!).Cast<object>().ToArray();
        Check("DATA_GATES", gates.Length == 2 && (int)Property(gates[0], "UseCount")! == 1 && (int)Property(gates[1], "UseCount")! == 3, "Frozen gate records preserve legacy defaults and current usage counts.");
        CreateSqlite(Path.Combine(directory, "Alpha6OPS-flights.sqlite"), File.ReadAllText(Fixture("history.sql")));
        using var database = (IDisposable)New("FlightHistoryDatabase", directory);
        var rows = ((IEnumerable)Call(database, "ReadRecentFlights", 1000)!).Cast<object>().ToArray();
        var events = ((IEnumerable)Call(database, "ReadFlightEvents", "baseline-flight")!).Cast<object>().ToArray();
        Check("DATA_HISTORY", rows.Length == 1 && (string?)Property(rows[0], "FlightNumber") == "BASE718" && events.Length == 2 && (string?)Property(events[1], "Kind") == "pirep_submitted", "Frozen v0.22.0 SQL schema and historical event rows remain readable.");
    }

    private static void ProfileCopies(string profile)
    {
        var directory = Path.Combine(output, "profile-copy"); Directory.CreateDirectory(directory);
        var hashes = new Dictionary<string, string>();
        foreach (var name in new[] { "Alpha6OPS-flights.sqlite", "Alpha6OPS-flights.sqlite-wal", "Alpha6OPS-flights.sqlite-shm", "gate-history.json" })
        {
            var path = Path.Combine(profile, name); if (!File.Exists(path)) continue;
            hashes[path] = Hash(path); File.Copy(path, Path.Combine(directory, name));
        }
        if (File.Exists(Path.Combine(directory, "Alpha6OPS-flights.sqlite")))
        {
            using var database = (IDisposable)New("FlightHistoryDatabase", directory);
            var rows = ((IEnumerable)Call(database, "ReadRecentFlights", 1000)!).Cast<object>().ToArray();
            var count = rows.Sum(row => ((IEnumerable)Call(database, "ReadFlightEvents", Property(row, "Id"))!).Cast<object>().Count());
            Check("PROFILE_HISTORY_READABLE", rows.Length > 0, $"Copied profile: read {rows.Length} historical flights and {count} attached events (maximum 1,000 flights).");
        }
        else Skip("PROFILE_HISTORY_READABLE", "No existing profile history database supplied.");
        if (File.Exists(Path.Combine(directory, "gate-history.json")))
        {
            var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "gate-history.json"))).RootElement.GetArrayLength();
            var actual = ((IEnumerable)Call(New("GateHistoryStore", directory), "Load")!).Cast<object>().Count();
            Check("PROFILE_GATES_READABLE", actual == expected, $"Copied profile: {actual}/{expected} existing saved gates read.");
        }
        else Skip("PROFILE_GATES_READABLE", "No existing gate history supplied.");
        if (hashes.Count == 0) Skip("PROFILE_SOURCE_UNCHANGED", "No existing profile inputs supplied.");
        else Check("PROFILE_SOURCE_UNCHANGED", hashes.All(pair => Hash(pair.Key) == pair.Value), "Source profile SHA-256 hashes unchanged; all database opens/writes were against copies.");
    }

    private static void Coverage(string manifest, string reports)
    {
        var expected = new Dictionary<string, int> { ["JST221"] = 11965, ["QFA124"] = 11535, ["JST786"] = 6952, ["QLK1579"] = 9040, ["JST721"] = 7650, ["JST724"] = 7459, ["JST760"] = 10552, ["QLK1274"] = 10519, ["ANG2"] = 15694 };
        var records = new List<object>(); var flights = new HashSet<string>(); var okay = true; var total = 0; var rawTotal = 0;
        foreach (var path in JsonSerializer.Deserialize<string[]>(File.ReadAllText(manifest))!)
        {
            var before = Hash(path); var raw = 0; var eligible = 0; string? flight = null;
            foreach (var line in File.ReadLines(path))
            {
                using var document = JsonDocument.Parse(line); var root = document.RootElement;
                var kind = root.GetProperty("kind").GetString();
                if (kind is "flight_assignment" or "flight_assignment_changed") flight = root.GetProperty("detail").GetProperty("FlightNumber").GetString();
                if (kind == "telemetry") { raw++; if (flight is not null) eligible++; }
            }
            using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(reports, flight + ".json")));
            var processed = report.RootElement.GetProperty("samples").GetInt32(); var passed = report.RootElement.GetProperty("passed").GetBoolean();
            var unchanged = before == Hash(path);
            okay &= flight is not null && flights.Add(flight) && expected.TryGetValue(flight, out var wanted) && wanted == processed && processed == eligible && passed && unchanged;
            total += processed; rawTotal += raw;
            records.Add(new { flight, sha256 = before, rawTelemetry = raw, eligibleTelemetry = eligible, processed, beforeAssignment = raw - eligible, passed, unchanged });
        }
        var result = new { expectedSamples = 91366, processedSamples = total, rawSamples = rawTotal, flights = records };
        File.WriteAllText(Path.Combine(output, "replay-coverage.json"), JsonSerializer.Serialize(result, Json));
        Check("REPLAY_COVERAGE", okay && flights.Count == 9 && total == 91366, $"Independently counted {rawTotal} raw telemetry records; replay processed {total} eligible samples in {flights.Count} distinct flights; expected 91,366.");
    }

    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static void CreateSqlite(string path, string sql)
    {
        if (SqliteOpen(path, out var database) != 0) throw new IOException("Fixture database open failed.");
        try { if (SqliteExec(database, sql, IntPtr.Zero, IntPtr.Zero, out var error) != 0) { var message = Marshal.PtrToStringUTF8(error); SqliteFree(error); throw new IOException(message); } }
        finally { SqliteClose(database); }
    }
    private static void Release(MainWindow window)
    {
        // MainWindow.ExitApplication shuts down the entire app. Dispose this test
        // instance's owned resources instead, without changing production code.
        Call(window,"DisposePresentation");
        Set(window, "exiting", true); window.Close();
        FlightSessionDiagnosticAccess.Owner(window).Dispose();
    }
    private sealed record CheckResult(string Id, string Status, string Detail);
    // Load the actual production styles in a neutral WPF host. Constructing App
    // itself schedules its live startup; a derived App cannot load its BAML.
    private sealed class TestApplication : Application
    {
        internal TestApplication()
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Sierra4;component/DashboardStyles.xaml") });
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Sierra4;component/SierraDesignTokens.xaml") });
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Sierra4;component/SierraComponents.xaml") });
            Resources["Yellow"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 217, 40));
            var text = new Style(typeof(TextBlock)); text.Setters.Add(new Setter(Control.FontFamilyProperty, new System.Windows.Media.FontFamily("Bahnschrift SemiCondensed"))); Resources[typeof(TextBlock)] = text;
            foreach (var (type, key) in new[] { (typeof(Button), "OpsButton"), (typeof(TextBox), "OpsInput"), (typeof(ComboBox), "OpsCombo") })
                Resources[type] = new Style(type, (Style)Resources[key]);
        }
    }
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open", CallingConvention = CallingConvention.Cdecl)] private static extern int SqliteOpen([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr db);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_exec", CallingConvention = CallingConvention.Cdecl)] private static extern int SqliteExec(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, IntPtr callback, IntPtr argument, out IntPtr error);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_free", CallingConvention = CallingConvention.Cdecl)] private static extern void SqliteFree(IntPtr value);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)] private static extern int SqliteClose(IntPtr db);
}
