using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Alpha6Ops.Core;
using Alpha6Ops.Desktop;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
    private static readonly List<object> Results = [];
    private static string root = "";
    private static int failed;
    private static string? only;
    private static readonly Dictionary<string, object> Metrics = [];
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    private static readonly ActiveFlightPlan Plan = JsonSerializer.Deserialize<ActiveFlightPlan>(File.ReadAllText(Fixture("active-flight.json")), Json)!;
    private static readonly FlightRecoveryState State = JsonSerializer.Deserialize<FlightRecoveryState>(File.ReadAllText(Fixture("active-flight-state.json")), Json)!;
    private static string PathName(string dir) => Path.Combine(dir, "active-flight-state.json");
    private static string Seed(string name)
    {
        var dir = Path.Combine(root, name); Directory.CreateDirectory(dir);
        File.Copy(Fixture("active-flight-state.json"), PathName(dir)); return dir;
    }
    private static void Require(bool valid, string reason) { if (!valid) throw new Exception(reason); }
    private static void Check(string name, Action test)
    {
        if (only is not null && name != only) return;
        var time = Stopwatch.StartNew(); string? error = null;
        try { test(); } catch (Exception e) { failed++; error = e.ToString(); }
        Results.Add(new { name, passed = error is null, milliseconds = time.Elapsed.TotalMilliseconds, error });
        Console.WriteLine($"{(error is null ? "PASS" : "FAIL")} {name}{(error is null ? "" : ": " + error.Split('\n')[0])}");
    }
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.FirstOrDefault() == "--storage") return ProductStorageChecks.Run(args);
        if (args.FirstOrDefault() == "--storage-instance-child") return ProductStorageChecks.HoldIdentity(args);
        if (args.FirstOrDefault() == "--retention") return ProductionRetentionChecks.Run(args);
        if (args.FirstOrDefault() == "--logbook") return LogbookChecks.Run(args);
        if (args.FirstOrDefault() == "--terminal") return TerminalShutdownChecks.Run(args);
        if (args.FirstOrDefault() is "--active-restart" or "--active-child") return ActiveFlightRecoveryChecks.Run(args);
        root = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine("work/checkpoint-7.23", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        if (args.FirstOrDefault() == "--probe") { Probe(); return 0; }
        if (args.FirstOrDefault() == "--child") return Child(args);
        only = args.Length > 2 ? args[2] : null;
        CrashReporter.Install(new System.Windows.Application(), Path.Combine(root, "intentional-failure"));
        Check("TRANSIENT_DESTINATION_LOCK_RECOVERS", () =>
        {
            var dir = Seed("transient");
            var old = File.ReadAllText(PathName(dir));
            var held = new FileStream(PathName(dir), FileMode.Open, FileAccess.Read, FileShare.Read);
            Exception? error = null;
            var task = Task.Run(() => { try { FlightRecoveryStore.Save(State with { Aircraft = "Airbus A220-300" }, dir); } catch (Exception e) { error = e; } });
            try
            {
                Require(SpinWait.SpinUntil(() => File.Exists(PathName(dir) + ".tmp") || task.IsCompleted, 3000), "Save did not begin");
                Thread.Sleep(30);
                Require(File.ReadAllText(PathName(dir)) == old, "Locked authoritative file changed");
            }
            finally { held.Dispose(); }
            Require(task.Wait(3000), "Save retry did not finish");
            Require(error is null, "Save failed instead of recovering after short lock: " + error);
            Require(FlightRecoveryStore.Load(Plan, dir)?.Aircraft == "Airbus A220-300", "New state not persisted");
        });
        Check("FAILED_SAVE_PRESERVES_OLD_AND_CLEANS_TEMP", () =>
        {
            var dir = Seed("permanent"); var old = File.ReadAllText(PathName(dir));
            using var held = new FileStream(PathName(dir), FileMode.Open, FileAccess.Read, FileShare.Read);
            var time = Stopwatch.StartNew(); Exception? error = null;
            try { FlightRecoveryStore.Save(State with { Aircraft = "Boeing 737-800" }, dir); } catch (Exception e) { error = e; }
            Require(error is IOException or UnauthorizedAccessException, "Permanent lock falsely succeeded");
            Require(time.Elapsed < TimeSpan.FromSeconds(2), "Failure exceeded bounded retry budget");
            Require((int?)error!.Data["RecoveryAttempts"] == 5 && (string?)error.Data["RecoveryStage"] == "replace", "Failure lacks retry/stage diagnostics");
            Require(File.ReadAllText(PathName(dir)) == old, "Previous valid file lost");
            Require(!File.Exists(PathName(dir) + ".tmp"), "Failed save left temporary file");
        });
        Check("INCOMPLETE_JSON_REJECTED", () =>
        {
            var dir = Seed("incomplete");
            File.WriteAllText(PathName(dir), "{\"FlightNumber\":\"BASE718\",\"PlannedDepartureUtc\":\"2026-09-17T02:05:00+00:00\"}");
            Require(FlightRecoveryStore.Load(Plan, dir) is null, "Incomplete JSON accepted as flight truth");
        });
        AdditionalChecks();
        File.WriteAllText(Path.Combine(root, "summary.json"), JsonSerializer.Serialize(new { executed = Results.Count, passed = Results.Count - failed, failed, checks = Results, metrics = Metrics }, Json));
        return failed == 0 ? 0 : 1;
    }
    private static Process ChildProcess(string dir, string mode)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in new[] { "--child", dir, mode }) info.ArgumentList.Add(arg);
        return Process.Start(info)!;
    }
    private static int Child(string[] args)
    {
        if (args[2] == "load")
        {
            var restored = FlightRecoveryStore.Load(Plan, root);
            File.WriteAllText(Path.Combine(root, "child-loaded.json"), JsonSerializer.Serialize(restored, Json)); return restored is null ? 2 : 0;
        }
        File.WriteAllText(Path.Combine(root, "child-ready"), "ready");
        while (!File.Exists(Path.Combine(root, "child-go"))) Thread.Sleep(1);
        FlightRecoveryStore.Save(State with { Aircraft = "Boeing 777-300ER" }, root);
        File.WriteAllText(Path.Combine(root, "child-saved"), "saved");
        Thread.Sleep(30000); return 0;
    }
    private static void ColdLoad(string dir, string expectedAircraft)
    {
        using var child = ChildProcess(dir, "load");
        Require(child.WaitForExit(10000) && child.ExitCode == 0, "Cold process load failed");
        var restored = JsonSerializer.Deserialize<FlightRecoveryState>(File.ReadAllText(Path.Combine(dir, "child-loaded.json")), Json)!;
        Require(restored.Aircraft == expectedAircraft && restored.PhaseEvents.Count == 2 && restored.TrackingEvents.Count == 1, "Cold process recovery content changed");
    }
    private static void AdditionalChecks()
    {
        Check("MALFORMED_MONITOR_REJECTED_WITHOUT_STARTUP_CRASH", () =>
        {
            var dir = Seed("invalid-monitor"); File.Copy(Fixture("active-flight.json"), Path.Combine(dir, "active-flight.json"));
            foreach (var monitor in new[] { "{}", "{\"Fired\":null}" })
            {
                File.WriteAllText(PathName(dir), File.ReadAllText(Fixture("active-flight-state.json")).Replace("\"MonitorState\":null", "\"MonitorState\":" + monitor));
                using var owner = new FlightSessionCoordinator(dir, true, new InlineDispatcher());
                Require(owner.Snapshot.Assignment?.FlightNumber == "BASE718" && owner.ReadTimeline() is null, "Malformed monitor recovered as partial flight truth");
            }
            var valid = State with { MonitorState = new FlightTrackingEventMonitor().CaptureState() };
            FlightRecoveryStore.Save(valid, dir);
            using var restored = new FlightSessionCoordinator(dir, true, new InlineDispatcher());
            Require(restored.ReadTimeline()?.Events.Count == 2, "Valid monitor lost compatibility");
        });
        Check("HISTORICAL_SCHEMA_NAN_AND_NULL_OPTIONALS", () =>
        {
            var dir = Seed("legacy"); var original = FlightRecoveryStore.Load(Plan, dir)!;
            Require(original.Phase == FlightPhase.Airborne && original.ActualOut == DateTimeOffset.Parse("2026-09-17T02:06:00Z") && original.MonitorState is null && double.IsNaN(original.LastTelemetry!.AltitudeFeet), "Historical fixture not restored faithfully");
            FlightRecoveryStore.Save(original, dir);
            Require(JsonSerializer.Serialize(FlightRecoveryStore.Load(Plan, dir), Json) == JsonSerializer.Serialize(State, Json), "Round trip changed historical state");
            ColdLoad(dir, "Airbus A320");
        });
        Check("MISMATCHED_FLIGHT_AND_DATE_NOT_RESTORED", () =>
        {
            var dir = Seed("mismatch");
            Require(FlightRecoveryStore.Load(Plan with { FlightNumber = "OTHER" }, dir) is null && FlightRecoveryStore.Load(Plan with { PlannedDepartureUtc = Plan.PlannedDepartureUtc.AddDays(1) }, dir) is null, "Assignment identity ignored");
        });
        Check("CORRUPT_NULL_AND_MISSING_MEMBERS_REJECTED", () =>
        {
            var dir = Seed("corrupt");
            foreach (var json in new[] { "{\"FlightNumber\":", "null", "[]", File.ReadAllText(Fixture("active-flight-state.json")).Replace("\"BASE718\"", "null"), File.ReadAllText(Fixture("active-flight-state.json")).Replace("\"Phase\":2,", ""), File.ReadAllText(Fixture("active-flight-state.json")).Replace("\"Phase\":2,", "\"Phase\":999,") })
            { File.WriteAllText(PathName(dir), json); Require(FlightRecoveryStore.Load(Plan, dir) is null, "Corrupt state restored"); }
            Require(Directory.GetFiles(dir, "Alpha6OPS-crash-*.json").Length == 6, "Corrupt load failures not observable");
        });
        Check("INVALID_SAVE_CANNOT_REPLACE_VALID_STATE", () =>
        {
            var dir = Seed("invalid-save"); var before = File.ReadAllText(PathName(dir));
            try { FlightRecoveryStore.Save(State with { TrackingEvents = null! }, dir); throw new Exception("Invalid save succeeded"); } catch (JsonException) { }
            Require(File.ReadAllText(PathName(dir)) == before && !File.Exists(PathName(dir) + ".tmp"), "Invalid save changed files");
        });
        Check("READONLY_TARGET_FAILS_WITHOUT_RETRY_OR_DATA_LOSS", () =>
        {
            var dir = Seed("readonly"); var before = File.ReadAllText(PathName(dir)); File.SetAttributes(PathName(dir), FileAttributes.ReadOnly);
            try
            {
                Exception? error = null; try { FlightRecoveryStore.Save(State, dir); } catch (Exception e) { error = e; }
                Require(error is UnauthorizedAccessException && (int?)error.Data["RecoveryAttempts"] == 1, "Known permanent target retried or succeeded");
                Require(File.ReadAllText(PathName(dir)) == before, "Read-only target lost");
            }
            finally { File.SetAttributes(PathName(dir), FileAttributes.Normal); }
        });
        Check("INVALID_DIRECTORY_FAILS_AND_REMAINS_A_FILE", () =>
        {
            var path = Path.Combine(root, "not-a-directory"); File.WriteAllText(path, "preserve");
            try { FlightRecoveryStore.Save(State, path); throw new Exception("Invalid path succeeded"); } catch (IOException) { }
            Require(File.ReadAllText(path) == "preserve", "Invalid path was altered");
        });
        Check("LOCKED_STAGE_FAILURE_IS_BOUNDED_AND_LEFTOVER_IS_MANAGED", () =>
        {
            var dir = Seed("stage-lock"); var temp = PathName(dir) + ".tmp"; File.WriteAllText(temp, "{partial");
            var before = File.ReadAllText(PathName(dir));
            using (var held = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var watch = Stopwatch.StartNew(); Exception? error = null;
                try { FlightRecoveryStore.Save(State, dir); } catch (Exception e) { error = e; }
                Require(error is IOException && (string?)error.Data["RecoveryStage"] == "stage/flush" && watch.ElapsedMilliseconds < 2000, "Stage lock not reported/bounded");
                Require(File.ReadAllText(PathName(dir)) == before && Directory.GetFiles(dir, "*.tmp").Length == 1, "Old file lost or orphan count grew");
            }
            Require(Directory.GetFiles(dir, "Alpha6OPS-crash-*.json").Length == 1, "Failed cleanup not reported");
            FlightRecoveryStore.Save(State, dir); Require(!File.Exists(temp), "Next save failed to reclaim orphan");
        });
        Check("SEQUENTIAL_5000_COMPLETE_REPLACEMENTS", () =>
        {
            var dir = Seed("sequential"); var latencies = new List<double>(); var total = Stopwatch.StartNew();
            for (var i = 0; i < 5000; i++)
            {
                var state = State with { SavedUtc = State.SavedUtc.AddSeconds(i), RouteProgress = i / 5000d };
                var watch = Stopwatch.StartNew(); FlightRecoveryStore.Save(state, dir); latencies.Add(watch.Elapsed.TotalMilliseconds);
                var loaded = FlightRecoveryStore.Load(Plan, dir);
                Require(JsonSerializer.Serialize(loaded, Json) == JsonSerializer.Serialize(state, Json), "Recovery differs at sample " + i);
            }
            latencies.Sort(); Metrics["sequential"] = new { replacements = 5000, roundTrips = 5000, milliseconds = total.Elapsed.TotalMilliseconds, saveP50Ms = latencies[2500], saveP95Ms = latencies[4750], saveP99Ms = latencies[4950], saveMaxMs = latencies[^1] };
            Require(Directory.GetFiles(dir).Length == 1, "Unexpected report or temp during sequential saves");
        });
        Check("CONCURRENT_WRITERS_AND_READERS_RETURN_WHOLE_STATES", () =>
        {
            var dir = Seed("concurrent"); var errors = new System.Collections.Concurrent.ConcurrentQueue<Exception>(); int reads = 0;
            var writers = Enumerable.Range(0, 4).Select(writer => Task.Run(() =>
            {
                try { for (var i = 0; i < 250; i++) FlightRecoveryStore.Save(State with { Aircraft = "Writer " + writer, RouteProgress = i / 250d }, dir); } catch (Exception e) { errors.Enqueue(e); }
            })).ToArray();
            var reader = Task.Run(() =>
            {
                try
                {
                    do { var state = FlightRecoveryStore.Load(Plan, Path.Combine(dir, ".")); Require(state is not null && state.PhaseEvents.Count == 2 && state.TrackingEvents.Count == 1, "Read partial/null state"); Interlocked.Increment(ref reads); }
                    while (!writers.All(t => t.IsCompleted));
                }
                catch (Exception e) { errors.Enqueue(e); }
            });
            Require(Task.WaitAll([.. writers, reader], 30000), "Concurrent work timed out");
            Require(errors.IsEmpty, "Concurrent errors: " + string.Join("; ", errors));
            FlightRecoveryStore.Save(State with { Aircraft = "FINAL" }, dir);
            Require(FlightRecoveryStore.Load(Plan, dir)?.Aircraft == "FINAL", "Final checkpoint lost");
            Require(Directory.GetFiles(dir).Length == 1, "Concurrent operation leaked temp/report");
            Metrics["concurrent"] = new { replacements = 1001, overlappingReads = reads };
        });
        Check("SAVE_DELETE_LOAD_OVERLAP_AND_FINAL_DELETE", () =>
        {
            var dir = Seed("delete-race");
            var writer = Task.Run(() => { for (var i = 0; i < 100; i++) FlightRecoveryStore.Save(State, dir); });
            var deleter = Task.Run(() => { for (var i = 0; i < 100; i++) FlightRecoveryStore.Delete(dir); });
            var reader = Task.Run(() => { for (var i = 0; i < 100; i++) { var state = FlightRecoveryStore.Load(Plan, dir); Require(state is null || state.PhaseEvents.Count == 2, "Delete race exposed incomplete state"); } });
            Require(Task.WaitAll([writer, deleter, reader], 15000), "Delete race timed out");
            FlightRecoveryStore.Delete(dir); Require(Directory.GetFiles(dir).Length == 0, "Final delete failed to clear state/staging");
            FlightRecoveryStore.Save(State, dir); Require(FlightRecoveryStore.Load(Plan, dir) is not null, "Recreation after delete failed");
        });
        Check("EXTERNAL_READ_HANDLE_CANNOT_CAUSE_PARTIAL_PUBLICATION", () =>
        {
            var dir = Seed("external-read"); var old = File.ReadAllText(PathName(dir));
            Exception? error = null;
            using (var stream = new FileStream(PathName(dir), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                // FileShare.Delete is not sufficient for MoveFileEx overwrite on every
                // Windows configuration. Either result must leave a complete checkpoint.
                try { FlightRecoveryStore.Save(State with { Aircraft = "Airbus A380" }, dir); } catch (Exception e) { error = e; }
                using var reader = new StreamReader(stream); Require(reader.ReadToEnd() == old, "Existing handle observed overwritten bytes");
                Require(error is null || error is IOException or UnauthorizedAccessException, "Unexpected failure type");
                Require(FlightRecoveryStore.Load(Plan, dir)?.Aircraft == (error is null ? "Airbus A380" : "Airbus A320"), "Publication was neither intact old nor complete new state");
            }
            Metrics["externalDeleteSharedHandle"] = new { replacementSucceededWhileHeld = error is null, hresult = error?.HResult };
            FlightRecoveryStore.Save(State with { Aircraft = "Airbus A380" }, dir);
            Require(FlightRecoveryStore.Load(Plan, dir)?.Aircraft == "Airbus A380", "New reader did not see published file");
        });
        Check("PARTIAL_OR_VALID_ORPHAN_NEVER_OVERRIDES_AUTHORITATIVE_FILE", () =>
        {
            var dir = Seed("orphans");
            foreach (var staging in new[] { "{partial", JsonSerializer.Serialize(State with { Aircraft = "STALE TEMP" }, Json) })
            { File.WriteAllText(PathName(dir) + ".tmp", staging); ColdLoad(dir, "Airbus A320"); }
            File.Delete(PathName(dir)); Require(FlightRecoveryStore.Load(Plan, dir) is null, "Orphan auto-promoted without authority");
            FlightRecoveryStore.Delete(dir); Require(!File.Exists(PathName(dir) + ".tmp"), "Delete missed orphan");
        });
        Check("PROCESS_EXIT_BEFORE_REPLACE_PRESERVES_OLD_CHECKPOINT", () =>
        {
            var dir = Seed("crash-before-replace"); var old = File.ReadAllText(PathName(dir));
            using var held = new FileStream(PathName(dir), FileMode.Open, FileAccess.Read, FileShare.Read);
            using var child = ChildProcess(dir, "save");
            try
            {
                Require(SpinWait.SpinUntil(() => File.Exists(Path.Combine(dir, "child-ready")), 10000), "Child not ready");
                File.WriteAllText(Path.Combine(dir, "child-go"), "go");
                Require(SpinWait.SpinUntil(() => File.Exists(PathName(dir) + ".tmp"), 5000), "Child did not stage");
                child.Kill(); Require(child.WaitForExit(3000), "Child failed to stop");
                Require(File.ReadAllText(PathName(dir)) == old, "Interrupted replace lost old state");
            }
            finally { if (!child.HasExited) { child.Kill(); child.WaitForExit(3000); } }
            held.Dispose(); ColdLoad(dir, "Airbus A320");
            FlightRecoveryStore.Save(State with { Aircraft = "RESTARTED" }, dir);
            Require(!File.Exists(PathName(dir) + ".tmp") && FlightRecoveryStore.Load(Plan, dir)?.Aircraft == "RESTARTED", "Restart did not reclaim staging");
        });
        Check("PROCESS_EXIT_AFTER_REPLACE_REOPENS_NEW_CHECKPOINT", () =>
        {
            var dir = Seed("crash-after-replace"); using var child = ChildProcess(dir, "save");
            try
            {
                Require(SpinWait.SpinUntil(() => File.Exists(Path.Combine(dir, "child-ready")), 10000), "Child not ready");
                File.WriteAllText(Path.Combine(dir, "child-go"), "go");
                Require(SpinWait.SpinUntil(() => File.Exists(Path.Combine(dir, "child-saved")), 5000), "Child save failed");
                child.Kill(); Require(child.WaitForExit(3000), "Child failed to stop");
            }
            finally { if (!child.HasExited) { child.Kill(); child.WaitForExit(3000); } }
            ColdLoad(dir, "Boeing 777-300ER"); Require(!File.Exists(PathName(dir) + ".tmp"), "Successful save left stage");
        });
        Check("INDEPENDENT_PROFILE_SAVES_DO_NOT_SHARE_TRANSACTION_LOCK", () =>
        {
            var blocked = Seed("independent-blocked"); var free = Seed("independent-free");
            using var held = new FileStream(PathName(blocked), FileMode.Open, FileAccess.Read, FileShare.Read);
            var task = Task.Run(() => { try { FlightRecoveryStore.Save(State, blocked); } catch (IOException) { } catch (UnauthorizedAccessException) { } });
            Require(SpinWait.SpinUntil(() => File.Exists(PathName(blocked) + ".tmp"), 3000), "Blocked save not started");
            FlightRecoveryStore.Save(State with { Aircraft = "FREE PROFILE" }, free);
            Require(!task.IsCompleted && FlightRecoveryStore.Load(Plan, free)?.Aircraft == "FREE PROFILE", "Unrelated directory serialized behind blocked save");
            Require(task.Wait(3000), "Blocked save failed to terminate");
        });
        Check("COORDINATOR_PERMANENT_FAILURE_REMAINS_DIAGNOSTIC_AND_RECOVERS", () =>
        {
            var dir = Seed("coordinator-failure"); File.Copy(Fixture("active-flight.json"), Path.Combine(dir, "active-flight.json"));
            using var owner = new FlightSessionCoordinator(dir, true, new InlineDispatcher());
            var before = File.ReadAllText(PathName(dir));
            using (var held = new FileStream(PathName(dir), FileMode.Open, FileAccess.Read, FileShare.Read)) owner.ApplyGates("4", "5");
            var reports = Directory.GetFiles(Path.Combine(root, "intentional-failure", "CrashReports"), "*.json");
            Require(reports.Length == 2 && File.ReadAllText(PathName(dir)) == before, "Expected two visible failed coordinator saves preserving old state");
            foreach (var report in reports)
            {
                using var parsed = JsonDocument.Parse(File.ReadAllText(report)); var data = parsed.RootElement;
                Require(data.GetProperty("source").GetString() == "flight_recovery_save" && data.GetProperty("extra").GetProperty("stage").GetString() == "replace" && data.GetProperty("extra").GetProperty("attempts").GetInt32() == 5, "Failure lost stage/attempt diagnostics");
            }
            owner.ApplyGates("6", "7");
            Require(FlightRecoveryStore.Load(Plan, dir)!.TrackingEvents.Count > State.TrackingEvents.Count, "Recovery did not catch up after lock released");
            owner.Dispose(); ColdLoadCoordinator(dir);
        });
        Check("FLUSH_COST_IS_MEASURED_WITH_REAL_WRITES", () =>
        {
            var dir = Path.Combine(root, "flush-cost"); Directory.CreateDirectory(dir); var data = new byte[16000];
            var timings = new List<object>();
            foreach (var durable in new[] { false, true })
            {
                var times = new List<double>();
                for (var i = 0; i < 500; i++)
                {
                    var watch = Stopwatch.StartNew();
                    using (var stream = new FileStream(Path.Combine(dir, "stage.tmp"), FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(data); stream.Flush(durable); }
                    times.Add(watch.Elapsed.TotalMilliseconds);
                }
                times.Sort(); timings.Add(new { durable, writes = 500, p50Ms = times[250], p95Ms = times[475], maxMs = times[^1] });
            }
            Metrics["flushCost"] = timings;
        });
    }
    private sealed class InlineDispatcher : IFlightRuntimeDispatcher
    {
        private readonly int thread = Environment.CurrentManagedThreadId;
        public void VerifyAccess() => Require(thread == Environment.CurrentManagedThreadId, "Wrong dispatcher thread");
        public void Post(Action action) { VerifyAccess(); action(); }
    }
    private static void ColdLoadCoordinator(string dir)
    {
        using var reopened = new FlightSessionCoordinator(dir, true, new InlineDispatcher());
        Require(reopened.Snapshot.Assignment?.DepartureGate == "6", "Latest assignment not readable after reopening");
        Require(FlightRecoveryStore.Load(Plan, dir) is not null, "Last recovery missing after owner disposal");
    }
    private static void Probe()
    {
        var observations = new List<object>();
        foreach (var mode in new[] { "Move", "Replace" })
        {
            var dir = Seed(mode); var path = PathName(dir); var temp = path + ".tmp";
            var errors = new List<object>(); var clock = Stopwatch.StartNew();
            for (var i = 0; i < 5000; i++)
            {
                File.WriteAllText(temp, "{\"sample\":" + i + ",\"data\":\"" + new string('x', 16000) + "\"}");
                try { if (mode == "Move") File.Move(temp, path, true); else File.Replace(temp, path, null); }
                catch (Exception e)
                {
                    var before = new { targetExists = File.Exists(path), tempExists = File.Exists(temp), attributes = File.Exists(path) ? File.GetAttributes(path).ToString() : "missing" };
                    Thread.Sleep(10); string? retryError = null;
                    try { if (mode == "Move") File.Move(temp, path, true); else File.Replace(temp, path, null); } catch (Exception retry) { retryError = retry.ToString(); }
                    errors.Add(new { iteration = i, type = e.GetType().Name, e.HResult, before, retryError });
                }
            }
            observations.Add(new { mode, attempted = 5000, milliseconds = clock.Elapsed.TotalMilliseconds, errors });
        }
        File.WriteAllText(Path.Combine(root, "probe.json"), JsonSerializer.Serialize(new { runtime = Environment.Version.ToString(), os = Environment.OSVersion.ToString(), observations }, Json));
        Console.WriteLine(JsonSerializer.Serialize(observations, Json));
    }
}
