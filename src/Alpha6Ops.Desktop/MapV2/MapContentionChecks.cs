using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop.MapV2;

// Explicit diagnostics only: all flight, recovery, journal and SQLite writes use fresh isolated directories.
internal static class MapContentionChecks
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() }
    };

    internal static async Task RunAsync(MainWindow main, WebMapRenderer renderer, string directory, Action<bool, string> check)
    {
        main.Dispatcher.VerifyAccess();
        string root = Path.Combine(directory, "contention-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var departure = AirportCatalog.Find("YSSY") ?? throw new InvalidOperationException("YSSY offline reference missing.");
        var arrival = AirportCatalog.Find("YMHB") ?? throw new InvalidOperationException("YMHB offline reference missing.");
        var start = new DateTimeOffset(2026, 9, 24, 2, 0, 0, TimeSpan.Zero);
        var plan = new ActiveFlightPlan("MAP722", "VH-MAP", "YSSY", "YMHB", start.AddSeconds(30), start.AddSeconds(3940),
            RoutePoints: [new("YSSY", departure.Position.Latitude, departure.Position.Longitude),
                new("YMHB", arrival.Position.Latitude, arrival.Position.Longitude)], AircraftType: "A320");
        var samples = FlightSamples(start, departure.Position, arrival.Position);
        var baseline = await RunFlightAsync(main, null, Path.Combine(root, "baseline"), plan, samples);
        var stressed = await RunFlightAsync(main, renderer, Path.Combine(root, "stressed"), plan, samples);

        check(baseline.SampleCount == samples.Count && stressed.SampleCount == samples.Count,
            "Contention: all authoritative samples recorded in baseline and browser-stalled run.");
        check(baseline.TimelineHash == stressed.TimelineHash,
            "Contention: recorded telemetry, per-sample phases and event counts are byte-equivalent.");
        check(baseline.EventHash == stressed.EventHash && baseline.EventCount > 10,
            "Contention: nontrivial ordered operational events are identical.");
        check(baseline.OperationalHash==stressed.OperationalHash,
            "Contention: ETA, progress, phase, actual times and PIREP eligibility match at every input sample.");
        check(baseline.RecoveryIdentityValid&&stressed.RecoveryIdentityValid,
            "Contention: each complete recovery identifies its own sole completed live history row.");
        check(baseline.RecoveryHash==stressed.RecoveryHash,
            "Contention: final complete recovery matches excluding wall-clock save metadata and the verified per-run history ID.");
        check(baseline.Phase == FlightPhase.Complete && stressed.Phase == FlightPhase.Complete
            && baseline.PhaseEvents == 4 && stressed.PhaseEvents == 4,
            "Contention: real telemetry reaches OUT/OFF/ON/IN and Complete without injected final state.");
        check(baseline.PirepEligible && stressed.PirepEligible && baseline.Submitted && stressed.Submitted
            && baseline.Durable && stressed.Durable,
            "Contention: real PIREP command persists and survives independent SQLite reopening in both runs.");
        check(baseline.HistoryHash == stressed.HistoryHash && baseline.PirepHash == stressed.PirepHash,
            "Contention: durable history and PIREP contents match excluding wall-clock submission metadata.");
        check(baseline.GenerationUnchanged && stressed.GenerationUnchanged,
            "Contention: map stress never reconnects either simulator session.");
        check(stressed.SamplesDuringStall > 0 && stressed.BrowserStallMs >= 950,
            "Contention: authoritative samples processed while actual WebView JavaScript was blocked for one second.");
        check(stressed.ProjectionError is null && stressed.Projected > 0 && stressed.VisualCoalesced > 0,
            "Contention: real projection and renderer mailbox coalesce visual snapshots without losing flight samples.");
        // 7.24C approved role revision: retain the original predicate/results,
        // separately from direct product acceptance. Never conceal an excursion.
        var heartbeatPassed = stressed.HeartbeatCount > 0 && stressed.HeartbeatMaxMs < 500
            && stressed.HeartbeatMaxMs <= Math.Max(150, baseline.HeartbeatMaxMs + 100);
        var heartbeat = new { passed=heartbeatPassed, role="diagnostic", releaseBlocking=false,
            thresholdsChanged=false, baselineMaxMs=baseline.HeartbeatMaxMs, stressedMaxMs=stressed.HeartbeatMaxMs,
            absoluteLimitMs=500, relativeAllowanceMs=100, floorMs=150,
            relativeLimitMs=Math.Max(150,baseline.HeartbeatMaxMs+100),
            condition="maximum <500ms and <=max(150ms, baseline maximum+100ms)",
            disposition=heartbeatPassed?"Historical predicate passed":"EXCURSION: correlate with direct product metrics; historical predicate failed" };
        Write(Path.Combine(directory,"heartbeat-diagnostic.json"),heartbeat);
        Console.WriteLine("Background heartbeat diagnostic: "+heartbeat.disposition);
        check(baseline.HeartbeatCount>0&&stressed.HeartbeatCount>0,
            "Contention: Background heartbeat measured and separately reported under unchanged historical thresholds.");
        Write(Path.Combine(directory, "contention.json"), new
        {
            isolatedDirectory = root,
            sampleCount = samples.Count,
            baseline,
            stressed,
            heartbeat,
            limitations = "Diagnostic source feeds production connection callbacks at accelerated cadence. Actual native SimConnect is not exercised. " +
                "One complete flight per arm and a one-second browser stall are bounded evidence, not a long-run contention guarantee. " +
                "History equality excludes database IDs, recording wall-clock times, journal paths and PIREP submittedUtc; simulator times and event payloads are compared. " +
                "Recovery equality normalizes a completed history ID only after verifying that it identifies that run's sole Complete live flight; all other recovery fields remain compared."
        });
    }

    private static async Task<Result> RunFlightAsync(MainWindow main, WebMapRenderer? renderer,
        string directory, ActiveFlightPlan plan, IReadOnlyList<LiveReading> readings)
    {
        Directory.CreateDirectory(directory);
        var dispatcher=main.Dispatcher;
        using var trace=Environment.GetEnvironmentVariable("S4_CONTENTION_TRACE")=="1"
            ?new DispatcherSchedulingTrace(main,(DispatcherTimer)FlightSessionDiagnosticAccess.Get(main,"dashboardClock")!,[]):null;
        var native=trace is not null&&Environment.GetEnvironmentVariable("S4_HEARTBEAT_NATIVE_TRACE")=="1"
            ?new HeartbeatNativeObserver(trace,renderer):null;
        var source = new DiagnosticSource();
        using var owner = new FlightSessionCoordinator(directory, true, new WpfFlightRuntimeDispatcher(dispatcher), source);
        owner.AcceptAssignment(plan);
        owner.SetPilotName("Map contention diagnostic");
        Task connection = owner.ConnectSimulatorAsync();
        await source.Ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        long initialGeneration = owner.Snapshot.ConnectionGeneration;
        bool generationUnchanged = true;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<string>? stall = null;
        int samplesDuringStall = 0;
        var processingTimes = new List<long>();
        var operational=new List<object>();
        DateTimeOffset? previousAt = null;
        string? projectionError = null;
        long producedBefore = renderer is null ? 0 : Interlocked.Read(ref renderer.Metrics.Produced);
        long rendererCoalescedBefore = renderer is null ? 0 : Metric(renderer, "coalesced");
        MapProjectionPump? pump = renderer is null ? null : new MapProjectionPump(renderer, renderer.Epoch,
            renderer.Metrics, error => Interlocked.Exchange(ref projectionError, error.ToString()));
        var heartbeat = new List<double>();
        var tickWatch = Stopwatch.StartNew();
        var timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += Tick;
        void Tick(object? sender, EventArgs args)
        {
            heartbeat.Add(tickWatch.Elapsed.TotalMilliseconds);
            trace?.Mark("heartbeat",value:heartbeat[^1]);
            tickWatch.Restart();
        }
        void SnapshotChanged(object? sender, EventArgs args)
        {
            var snapshot = owner.Snapshot;
            generationUnchanged &= snapshot.ConnectionGeneration == initialGeneration;
            if (snapshot.Telemetry is not {} telemetry || telemetry.At == previousAt) return;
            previousAt = telemetry.At;
            trace?.Mark("sample-processed",value:operational.Count);
            operational.Add(new{telemetry.At,snapshot.Phase,snapshot.EstimatedArrival,snapshot.RouteProgress,snapshot.ActualOut,snapshot.ActualIn,snapshot.PirepEligible,snapshot.Events,snapshot.PhaseEvents});
            processingTimes.Add(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            pump?.Offer(snapshot, false);
            if (telemetry.At == readings[^1].Telemetry.At) completed.TrySetResult();
        }
        owner.SnapshotChanged += SnapshotChanged;
        var total = Stopwatch.StartNew();
        double stallMs = 0;
        try
        {
            timer.Start();
            trace?.Mark("feed-start");
            if (renderer is not null)
                stall = renderer.Core.ExecuteScriptAsync("(()=>{const epochStart=Date.now(),start=performance.now();while(performance.now()-start<1000){};return {elapsed:performance.now()-start,epochStart,epochEnd:Date.now()};})()");
            var producer = Task.Run(async () =>
            {
                foreach (var reading in readings)
                {
                    trace?.Mark("sample-emitted");
                    source.Emit(reading);
                    // Still 500x faster than simulator time, but avoid turning a map
                    // contention test into hundreds of recovery-file replacements/sec.
                    await Task.Delay(20).ConfigureAwait(false);
                }
            });
            await Task.WhenAll(producer, completed.Task).WaitAsync(TimeSpan.FromSeconds(60));
            double feedMs = total.Elapsed.TotalMilliseconds;
            if (stall is not null)
            {
                string result = await stall.WaitAsync(TimeSpan.FromSeconds(5));
                File.WriteAllText(Path.Combine(directory, "browser-stall-result.json"), result);
                using var document = JsonDocument.Parse(result);
                stallMs = document.RootElement.GetProperty("elapsed").GetDouble();
                double stallStart = document.RootElement.GetProperty("epochStart").GetDouble();
                double stallEnd = document.RootElement.GetProperty("epochEnd").GetDouble();
                samplesDuringStall = processingTimes.Count(at => at >= stallStart && at <= stallEnd);
            }
            await Task.Delay(100);
            if(Environment.GetEnvironmentVariable("S4_HEARTBEAT_CONTEXTS")=="1")
                Write(Path.Combine(directory,"pending-heartbeat.json"),new{ageMs=tickWatch.Elapsed.TotalMilliseconds});
            timer.Stop();
            trace?.Mark("feed-stop");
            if (pump is not null && renderer is not null)
            {
                // Observe the actual production queue counters until the final input is consumed.
                var deadline = Stopwatch.StartNew();
                while (!ProjectionDrained(renderer) && deadline.Elapsed < TimeSpan.FromSeconds(2)) await Task.Delay(10);
                if (!ProjectionDrained(renderer)) projectionError ??= "Production projection queue failed to drain within two seconds.";
                await pump.DisposeAsync();
                await Task.Delay(150);
            }
            var snapshot = owner.Snapshot;
            var recovery = FlightRecoveryStore.Load(plan, directory);
            if (recovery?.LastSimulatorUtc != readings[^1].Telemetry.At)
                throw new InvalidOperationException("Final recovery checkpoint does not contain the final authoritative sample.");
            var timeline = owner.ReadTimeline() ?? throw new InvalidOperationException("No authoritative recorder was created.");
            var history = owner.History ?? throw new InvalidOperationException("Isolated flight database unavailable.");
            var flight = history.ReadRecentFlights().Single();
            var recoveryIdentityValid=recovery!.FlightHistoryId is null&&recovery.CompletedFlightHistoryId==flight.Id&&flight.FinalPhase=="Complete"
                &&history.MatchesFlight(flight.Id,plan.FlightNumber,plan.Origin,plan.Destination);
            var beforeSubmission = history.ReadFlightEvents(flight.Id);
            string historyHash = Hash(beforeSubmission.Select(e => new { e.Sequence, e.Kind, e.SimulatorUtc, e.DetailJson }));
            bool submitted = owner.SubmitPirep();
            bool durable;
            string pirepHash;
            using (var reopened = new FlightHistoryDatabase(directory))
            {
                var persisted = reopened.ReadFlightEvents(flight.Id);
                var pireps = persisted.Where(e => e.Kind == "pirep_submitted").ToArray();
                durable = pireps.Length == 1 && reopened.ReadRecentFlights().Single().FinalPhase == "Complete"
                    && !File.Exists(Path.Combine(directory, "active-flight.json"))
                    && !File.Exists(Path.Combine(directory, "active-flight-state.json"));
                if (pireps.Length == 1)
                {
                    using var payload = JsonDocument.Parse(pireps[0].DetailJson);
                    pirepHash = Hash(payload.RootElement.EnumerateObject().Where(p => p.Name != "submittedUtc")
                        .ToDictionary(p => p.Name, p => p.Value.Clone()));
                }
                else pirepHash = "missing";
                Write(Path.Combine(directory, "durable-history.json"), new { flight, events = persisted });
            }
            Write(Path.Combine(directory, "authoritative-timeline.json"), timeline);
            Write(Path.Combine(directory, "operational-events.json"), snapshot.Events);
            var resultValue = new Result(timeline.Snapshots.Count, Hash(timeline with{Snapshots=timeline.Snapshots.Select(s=>s with{Sample=StableEvidenceTimes(s.Sample)}).ToArray()}), Hash(snapshot.Events), snapshot.Events.Length,
                snapshot.Phase, snapshot.PhaseEvents.Length, snapshot.PirepEligible, submitted, durable, historyHash, pirepHash,
                generationUnchanged && owner.Snapshot.ConnectionGeneration == initialGeneration,
                samplesDuringStall, stallMs, feedMs, heartbeat.Count, heartbeat.DefaultIfEmpty().Max(),
                renderer is null ? 0 : Interlocked.Read(ref renderer.Metrics.Produced) - producedBefore,
                (pump?.Coalesced ?? 0) + (renderer is null ? 0 : Metric(renderer, "coalesced") - rendererCoalescedBefore),
                projectionError, renderer?.Measurements,Hash(operational),Hash(recovery! with{SavedUtc=default,LastTelemetry=recovery.LastTelemetry is {} last?StableEvidenceTimes(last):null,CompletedFlightHistoryId=recoveryIdentityValid?"<verified-flight-id>":recovery.CompletedFlightHistoryId}),recoveryIdentityValid);
            Write(Path.Combine(directory, "timings.json"), new { heartbeatIntervalsMs = heartbeat, projectionQueueOffered = pump?.Offered ?? 0, projectionQueueCoalesced = pump?.Coalesced ?? 0, result = resultValue });
            return resultValue;
        }
        finally
        {
            timer.Stop();
            timer.Tick -= Tick;
            owner.SnapshotChanged -= SnapshotChanged;
            if (pump is not null) await pump.DisposeAsync();
            owner.DisconnectSimulator();
            await connection.WaitAsync(TimeSpan.FromSeconds(5));
            native?.Dispose();
            if(trace is not null)Write(Path.Combine(directory,"scheduling-trace.json"),await trace.FinishAsync());
        }
    }

    internal static IReadOnlyList<LiveReading> FlightSamples(DateTimeOffset start, GeoPosition origin, GeoPosition destination, int stepSeconds=10)
    {
        var result = new List<LiveReading>();
        var from = new GeoPoint(origin.Longitude, origin.Latitude);
        var to = new GeoPoint(destination.Longitude, destination.Latitude);
        // Approximately one hour airborne, sampled every ten simulator seconds. Positions follow one physical great circle.
        for (int second = 0; second <= 3940; second += stepSeconds)
        {
            bool airborne = second is >= 130 and < 3740;
            bool stopped = second < 30 || second >= 3900;
            double fraction = Math.Clamp((second - 130d) / 3600, 0, 1);
            var position = MapGeography.Interpolate(from, to, fraction);
            double altitude = airborne ? Math.Min(32000, Math.Min((second - 120) * 40, (3740 - second) * 40)) : 30;
            double speed = stopped ? 0 : airborne ? (altitude < 10000 ? 220 : 420) : 15;
            double vertical = !airborne ? 0 : second < 920 ? 2400 : second > 2940 ? -2400 : 0;
            bool engines = second >= 10 && second < 3900;
            var sample = new Telemetry(start.AddSeconds(second), !airborne, speed, stopped, engines,
                LatitudeDegrees: position.Latitude, LongitudeDegrees: position.Longitude, AltitudeFeet: altitude,
                HeadingDegrees: 195, IndicatedAirspeedKnots: speed, VerticalSpeedFeetPerMinute: vertical,
                GearExtendedRatio: altitude < 2000 ? 1 : 0, AltitudeAboveGroundFeet: airborne ? altitude : 0,
                FlapsExtendedRatio: altitude < 5000 ? .5 : 0, PitchDegrees: airborne ? Math.Sign(vertical) * 3 : 0,
                BankDegrees: 0, FuelTotalWeightPounds: 15000 - second, RunningEngineCount: engines ? 2 : 0,
                RunningEngineMask: engines ? 3 : 0, PressureAltitudeFeet: altitude);
            result.Add(new("Airbus A320 diagnostic", sample,
                new FlightEvidence(new(position.Latitude, position.Longitude), "VH-MAP", new("YSSY", "YMHB"), true),
                Source: "Map contention diagnostic"));
        }
        return result;
    }

    private static bool ProjectionDrained(WebMapRenderer renderer)
    {
        var queue = JsonSerializer.SerializeToElement(renderer.Metrics.ProjectionQueue?.Invoke());
        return queue.ValueKind == JsonValueKind.Object && !queue.GetProperty("pending").GetBoolean()
            && queue.GetProperty("consumed").GetInt64() >= queue.GetProperty("offered").GetInt64() - queue.GetProperty("coalesced").GetInt64();
    }
    private static long Metric(WebMapRenderer renderer, string name) => JsonSerializer.SerializeToElement(renderer.Measurements).GetProperty(name).GetInt64();
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Json)));
    // Arms run at different wall times. Compare every signal/policy value while
    // normalizing only newly recorded receipt clocks; raw evidence is saved above.
    private static Telemetry StableEvidenceTimes(Telemetry sample) => sample.TerminalShutdown is not {} evidence ? sample : sample with
    { TerminalShutdown=evidence with{ReceivedUtc=sample.At,Engines=evidence.Engines.Select(e=>e with{ReceivedUtc=sample.At}).ToImmutableArray()} };
    private static void Write<T>(string path, T value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Json));

    private sealed record Result(int SampleCount, string TimelineHash, string EventHash, int EventCount,
        FlightPhase? Phase, int PhaseEvents, bool PirepEligible, bool Submitted, bool Durable, string HistoryHash, string PirepHash,
        bool GenerationUnchanged, int SamplesDuringStall, double BrowserStallMs, double FeedMs,
        int HeartbeatCount, double HeartbeatMaxMs, long Projected, long VisualCoalesced, string? ProjectionError, object? MapMetrics,
        string OperationalHash,string RecoveryHash,bool RecoveryIdentityValid);

    private sealed class DiagnosticSource : ISimulatorConnectionSource
    {
        private Action<LiveReading>? receive;
        internal TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsRunning() => true;
        public void Launch() => throw new InvalidOperationException("Diagnostics must never launch a native simulator.");
        public async Task RunAsync(Action<string> opened, Action<LiveReading> received, CancellationToken cancellation)
        {
            receive = received;
            opened("Isolated diagnostic connection");
            Ready.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellation);
        }
        internal void Emit(LiveReading reading) => (receive ?? throw new InvalidOperationException("Diagnostic source not connected."))(FlightSessionDiagnosticAccess.WithTerminalFixture(reading));
    }
}
