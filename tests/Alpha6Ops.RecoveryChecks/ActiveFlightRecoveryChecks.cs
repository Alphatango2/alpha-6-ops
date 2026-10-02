using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Threading;
using Alpha6Ops.Core;
using Alpha6Ops.Desktop;

// Separate OS processes exercise the real coordinator, recovery store, SQLite and
// connection callbacks. Only the external simulator is replaced with fixed input.
internal static class ActiveFlightRecoveryChecks
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-17T02:05:00Z");
    private static readonly List<object> Results = [];
    private static int failures;
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    private static ActiveFlightPlan Plan => Read<ActiveFlightPlan>(Fixture("active-flight.json"));
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json)!;
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Save(string path, object value)
    {
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(value, Json));
        File.Move(path + ".tmp", path, true);
    }
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    private static void Check(string name, bool valid, string detail)
    {
        if (!valid) failures++;
        Results.Add(new { name, passed = valid, detail });
        Console.WriteLine($"{(valid ? "PASS" : "FAIL")} {name}: {detail}");
    }

    internal static int Run(string[] args)
    {
        var root = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(root);
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        CrashReporter.Install(new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }, root);
        try
        {
            if (args[0] == "--active-child") return Child(root, args[2]);
            var sources = new[] { Fixture("active-flight.json"), Fixture("active-flight-state.json") }.ToDictionary(p => p, Hash);
            Save(Path.Combine(root, "source-hashes.json"), sources);
            foreach (var shutdown in new[] { "kill", "clean" }) Scenario(Path.Combine(root, shutdown), shutdown);
            EtaContinuity(Path.Combine(root, "eta-continuity"));
            foreach(var interruption in new[]{"kill","locked-replace"})CompletionBoundary(Path.Combine(root,"completion-boundary-"+interruption),interruption);
            ReconciliationRetry(Path.Combine(root,"reconciliation-retry"));
            JoinedRecovery(Path.Combine(root, "joined"));
            SequenceReopen(Path.Combine(root, "sequence"));
            LegacyPirep(Path.Combine(root, "legacy"));
            Check("SOURCE_FIXTURES_UNCHANGED", sources.All(pair => Hash(pair.Key) == pair.Value), "Frozen assignment/recovery fixture SHA-256 hashes unchanged.");
            Save(Path.Combine(root, "summary.json"), new { executed = Results.Count, passed = Results.Count - failures, failed = failures, evidencePool = "synthetic active-flight recovery; no native MSFS", checks = Results });
            return failures == 0 ? 0 : 1;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(root, "harness-error.txt"), error.ToString());
            Console.Error.WriteLine(error);
            return 2;
        }
    }

    private static Process Launch(string root, string stage)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var value in new[] { "--active-child", root, stage }) info.ArgumentList.Add(value);
        return Process.Start(info)!;
    }
    private static void RunChild(string root, string stage)
    {
        using var child = Launch(root, stage);
        if(!child.WaitForExit(30000)){child.Kill();child.WaitForExit(5000);throw new TimeoutException("Child timed out: "+stage);}
        Require(child.ExitCode == 0, "Child failed: " + stage + "; inspect harness-error.txt");
    }
    private static void Scenario(string root, string shutdown)
    {
        Directory.CreateDirectory(root);
        Save(Path.Combine(root, "telemetry-source.json"), Samples());
        var sourceHash = Hash(Path.Combine(root, "telemetry-source.json"));
        using (var child = Launch(root, "prepare-" + shutdown))
        {
            try
            {
            Require(SpinWait.SpinUntil(() => File.Exists(Path.Combine(root, "prepared.json")) || child.HasExited, 30000), "Airborne child did not become ready");
            Require(File.Exists(Path.Combine(root, "prepared.json")), "Airborne child failed before persisting state");
            if (shutdown == "kill") child.Kill();
            Require(child.WaitForExit(10000), "Airborne child did not exit");
            if (shutdown == "clean") Require(child.ExitCode == 0, "Clean shutdown failed");
            }
            finally{if(!child.HasExited){child.Kill();child.WaitForExit(5000);}}
        }
        var prepared = Read<Evidence>(Path.Combine(root, "prepared.json"));
        var assignmentHash = Hash(Path.Combine(root, "active-flight.json"));
        var priorJournal=prepared.Snapshot.LastJournal!;var priorJournalHash=Hash(priorJournal);
        // The mismatched-route branch runs against its own copy of the checkpoint.
        var mismatch = Path.Combine(root, "mismatch"); Directory.CreateDirectory(mismatch);
        foreach (var file in Directory.GetFiles(root)) File.Copy(file, Path.Combine(mismatch, Path.GetFileName(file)));
        RunChild(mismatch, "mismatch");
        Check(shutdown + "_MISMATCH_REJECTED", !Read<Evidence>(Path.Combine(mismatch, "mismatched.json")).Snapshot.Association.Accepted, "A conflicting simulator route cannot inherit recovered assignment acceptance.");
        var rejectedRoots=new List<string>{mismatch};
        foreach(var changed in new[]{"backward","aircraft","position"})
        {
            var rejected=Path.Combine(root,changed);Directory.CreateDirectory(rejected);rejectedRoots.Add(rejected);
            foreach(var file in Directory.GetFiles(root))File.Copy(file,Path.Combine(rejected,Path.GetFileName(file)));
            RunChild(rejected,changed);
            var evidence=Read<Evidence>(Path.Combine(rejected,"mismatched.json"));
            Check(shutdown+"_"+changed.ToUpperInvariant()+"_INVALIDATES", !evidence.Snapshot.Association.Accepted&&evidence.Snapshot.PhaseEvents.Length==0&&evidence.Rows.Single(row=>row.Id==prepared.Rows.Single().Id).FinalPhase=="Invalid", "An incompatible recovered aircraft, clock or position cannot inherit the old operational session.");
        }

        RunChild(root, "resume");
        var restored = Read<Evidence>(Path.Combine(root, "restored.json"));
        var resumed = Read<Evidence>(Path.Combine(root, "resumed.json"));
        var reconnected = Read<Evidence>(Path.Combine(root, "reconnected.json"));
        var completed = Read<Evidence>(Path.Combine(root, "completed.json"));
        var initialId = prepared.Rows.Single().Id;
        Check(shutdown + "_AIRBORNE_CHECKPOINT", prepared.Snapshot.Phase == FlightPhase.Airborne && prepared.Snapshot.PhaseEvents.Length == 2, "Interruption follows observed OUT and OFF in a separate process.");
        Check(shutdown + "_ASSIGNMENT_IMMUTABLE", assignmentHash == Hash(Path.Combine(root, "active-flight.json")), "Restart/reconnect/landing preserve accepted assignment bytes.");
        Check(shutdown + "_COLD_RESTORE", restored.Snapshot.Phase == FlightPhase.Airborne && Same(prepared.Snapshot.PhaseEvents, restored.Snapshot.PhaseEvents) && Same(prepared.Snapshot.Events, restored.Snapshot.Events), "Cold coordinator restores phase, exact milestone times and existing event history.");
        Check(shutdown + "_SESSION_CONTINUITY", resumed.Snapshot.Association.Accepted && resumed.Snapshot.ObservedSessionStart == Start && !resumed.Snapshot.JoinedInProgress, "Departure-location verification and session start survive airborne recovery without a simulator route.");
        Check(shutdown + "_STEADY_ENGINES_NO_DUPLICATE", resumed.Snapshot.Events.Count(e => e.Title == "Engine 1 running") == 1 && resumed.Snapshot.Events.Count(e => e.Title == "Engine 2 running") == 1, "An unchanged per-engine Running state does not create another running event after restart.");
        Check(shutdown + "_RECONNECT_SAME_SESSION", reconnected.Snapshot.Association.Accepted && reconnected.Snapshot.ObservedSessionStart == Start && reconnected.Rows.Length == 1 && reconnected.Rows[0].Id == initialId, "Explicit disconnect/reconnect continues the existing operational session/history row.");
        var phases = completed.Snapshot.PhaseEvents;
        Check(shutdown + "_ON_IN_ONCE", phases.Length == 4 && phases.Select(e => e.Phase).SequenceEqual(new[] { FlightPhase.TaxiOut, FlightPhase.Airborne, FlightPhase.TaxiIn, FlightPhase.Complete }) && phases.Select(e => e.At).SequenceEqual(new[] { Start.AddSeconds(1), Start.AddSeconds(6), Start.AddSeconds(7203), Start.AddSeconds(7211) }), "Each OUT/OFF/ON/IN occurs once, at the independently specified simulator times.");
        Check(shutdown + "_ONE_COMPLETED_HISTORY", completed.Rows.Length == 1 && completed.Rows[0].Id == initialId && completed.Rows[0].FinalPhase == "Complete", "The original durable flight row reaches Complete.");
        Check(shutdown + "_OLD_EVENTS_IMMUTABLE", completed.Events.Take(prepared.Events.Length).SequenceEqual(prepared.Events), "Pre-interruption database event sequence, payloads and timestamps remain byte-equivalent.");
        Check(shutdown + "_ALL_EVENTS_DURABLE", completed.Events.Length == completed.Snapshot.Events.Length && completed.Events.Select(e => e.Sequence).SequenceEqual(Enumerable.Range(1, completed.Events.Length)), "Every tracking event appends exactly once with contiguous durable sequences.");
        Check(shutdown + "_ELIGIBLE_AFTER_LANDING", completed.Snapshot.PirepEligible && completed.Snapshot.ActualOut == Start.AddSeconds(1) && completed.Snapshot.ActualIn == Start.AddSeconds(7211), "Recovery continues through normal PIREP eligibility with preserved actuals.");
        RunChild(root, "submit");
        var submitted = Read<Submission>(Path.Combine(root, "submitted.json"));
        RunChild(root, "audit");
        var audit = Read<Evidence>(Path.Combine(root, "audit.json"));
        Check(shutdown + "_COLD_COMPLETE_SUBMISSION", submitted.EligibleBefore && submitted.First && !submitted.Second, "Another cold process submits once and the cleared assignment prevents repeat coordinator submission.");
        var pireps = audit.Events.Where(e => e.Kind == "pirep_submitted").ToArray();
        Check(shutdown + "_ONE_DURABLE_PIREP", audit.Rows.Length == 1 && audit.Rows[0].Id == initialId && pireps.Length == 1, "A final cold process finds one original flight row and one PIREP.");
        var actuals = pireps.Length == 1 ? JsonDocument.Parse(pireps[0].DetailJson) : null;
        Check(shutdown + "_PIREP_ACTUALS", actuals is not null && actuals.RootElement.GetProperty("actualOut").GetDateTimeOffset() == Start.AddSeconds(1) && actuals.RootElement.GetProperty("actualOff").GetDateTimeOffset() == Start.AddSeconds(6) && actuals.RootElement.GetProperty("actualOn").GetDateTimeOffset() == Start.AddSeconds(7203) && actuals.RootElement.GetProperty("actualIn").GetDateTimeOffset() == Start.AddSeconds(7211), "PIREP retains the four dated observed simulator actuals.");
        actuals?.Dispose();
        Check(shutdown + "_CLOSEOUT_DURABLE", audit.Snapshot.Assignment is null && !File.Exists(Path.Combine(root, "active-flight-state.json")) && !File.Exists(Path.Combine(root, "active-flight.json")), "Assignment and recovery remain cleared after final process restart.");
        Check(shutdown + "_SYNTHETIC_SOURCE_IMMUTABLE", sourceHash == Hash(Path.Combine(root, "telemetry-source.json")), "The synthetic telemetry input is unchanged by all processes.");
        Check(shutdown + "_PRIOR_JOURNAL_IMMUTABLE", priorJournalHash==Hash(priorJournal), "The previous connection journal remains unchanged while new transport connections use their own journals.");
        var reports = Directory.GetFiles(root, "Alpha6OPS-crash-*.json", SearchOption.AllDirectories).Where(p => !rejectedRoots.Any(branch=>p.StartsWith(branch + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))).ToArray();
        Check(shutdown + "_REPORT_GUARD", reports.All(p => Read<JsonElement>(p).GetProperty("source").GetString() == "previous_unclean_exit") && reports.Length == (shutdown == "kill" ? 1 : 0), "Only the deliberately killed process may produce one previous_unclean_exit report; no persistence errors allowed.");
    }

    private static void EtaContinuity(string root)
    {
        Directory.CreateDirectory(root);
        var plannedOut=DateTimeOffset.Parse("2026-09-28T21:45:00Z");
        var plan=new ActiveFlightPlan("JST771","VH-ETA","YPAD","YMML",plannedOut,DateTimeOffset.Parse("2026-09-28T23:12:59Z"),
            "SimBrief",Route:"YPAD DCT YMML",RoutePoints:[new("YPAD",-34.945,138.530556,"Airport"),new("YMML",-37.673333,144.843333,"Airport")],AircraftType:"Airbus A320",CruiseAltitudeFeet:35000);
        var input=EtaSamples();Save(Path.Combine(root,"eta-source.json"),input);
        FlightSessionSnapshot before;
        using(var source=new SyntheticSource())
        using(var owner=new FlightSessionCoordinator(root,true,new InlineDispatcher(),source))
        {
            owner.AcceptAssignment(plan);var connection=owner.ConnectSimulatorAsync();
            foreach(var reading in input.Take(input.Length-1))source.Emit(reading);
            before=owner.Snapshot;Save(Path.Combine(root,"eta-before.json"),Capture(owner));
            owner.Dispose();Pump(connection);
        }
        FlightSessionSnapshot after;
        using(var source=new SyntheticSource())
        using(var owner=new FlightSessionCoordinator(root,true,new InlineDispatcher(),source))
        {
            var connection=owner.ConnectSimulatorAsync();source.Emit(input[^1]);after=owner.Snapshot;
            Save(Path.Combine(root,"eta-after.json"),Capture(owner));owner.Dispose();Pump(connection);
        }
        var expected=DateTimeOffset.Parse("2026-09-28T22:50:00Z");
        var beforeEvidence=Read<Evidence>(Path.Combine(root,"eta-before.json"));
        var afterEvidence=Read<Evidence>(Path.Combine(root,"eta-after.json"));
        Check("ETA_RESTART_PRECONDITION",before.EstimatedArrival==expected&&before.Phase==FlightPhase.Airborne,
            $"The production coordinator publishes the JST771 pre-restart ETA 22:50Z; actual {before.EstimatedArrival:O}.");
        Check("ETA_RESTART_CONTINUITY",after.EstimatedArrival==expected,
            $"Nine simulator seconds later, recovery preserves ETA 22:50Z under normal two-minute anti-churn; actual {after.EstimatedArrival:O}.");
        Check("ETA_RESTART_NO_DUPLICATE_AUTHORITY",Same(beforeEvidence.Snapshot.PhaseEvents,afterEvidence.Snapshot.PhaseEvents)
            &&Same(beforeEvidence.Snapshot.Events,afterEvidence.Snapshot.Events)&&beforeEvidence.Rows.Length==1&&afterEvidence.Rows.Length==1
            &&beforeEvidence.Rows[0].Id==afterEvidence.Rows[0].Id,
            "ETA recovery does not duplicate lifecycle, tracking events or the durable flight row.");
        var saved=Read<FlightRecoveryState>(Path.Combine(root,"active-flight-state.json"));
        Check("ETA_RECOVERY_STATE_SERIALIZED",saved.ArrivalEstimatorState is{LastPhase:FlightPhase.Airborne,Published:var publication}
            &&publication==expected,"The optional predictive checkpoint is serialized separately from lifecycle authority.");
        var invalidRoot=Path.Combine(root,"invalid-state");Directory.CreateDirectory(invalidRoot);
        foreach(var file in Directory.GetFiles(root))File.Copy(file,Path.Combine(invalidRoot,Path.GetFileName(file)),true);
        var invalid=saved with{ArrivalEstimatorState=saved.ArrivalEstimatorState! with{Published=saved.LastSimulatorUtc!.Value.AddHours(37)}};
        FlightRecoveryStore.Save(invalid,invalidRoot);
        FlightSessionSnapshot fallback;
        using(var source=new SyntheticSource())
        using(var owner=new FlightSessionCoordinator(invalidRoot,true,new InlineDispatcher(),source))
        {
            var connection=owner.ConnectSimulatorAsync();var last=input[^1];
            source.Emit(last with{Telemetry=last.Telemetry with{At=last.Telemetry.At.AddSeconds(9)}});fallback=owner.Snapshot;
            Save(Path.Combine(root,"eta-invalid-fallback.json"),Capture(owner));owner.Dispose();Pump(connection);
        }
        Check("ETA_INVALID_STATE_DISCARDED",fallback.EstimatedArrival==DateTimeOffset.Parse("2026-09-28T23:10:00Z")
            &&fallback.PhaseEvents.SequenceEqual(after.PhaseEvents),
            $"An invalid future predictive anchor is discarded and reconstructed without lifecycle mutation; actual {fallback.EstimatedArrival:O}.");
    }

    private static int Child(string root, string stage)
    {
        var source = new SyntheticSource();
        using var owner = new FlightSessionCoordinator(root, true, new InlineDispatcher(), source);
        var input = Read<LiveReading[]>(Path.Combine(root, "telemetry-source.json"));
        if(stage.StartsWith("completion-boundary-",StringComparison.Ordinal))
        {
            owner.AcceptAssignment(Plan);
            var connection=owner.ConnectSimulatorAsync();
            foreach(var reading in input.Where(s=>s.Telemetry.At<=Start.AddSeconds(20)))source.Emit(reading);
            Pump(owner.Monitor!.WriteHeartbeatAsync());
            // Advance the flight to the destination through a normal reconnect,
            // then stop at the actual AddTrackingEvent -> durable Save boundary.
            owner.DisconnectSimulator();Pump(connection);connection=owner.ConnectSimulatorAsync();
            FileStream? held=null;var interrupted=false;
            using var listener=new ActivityListener
            {
                ShouldListenTo=observedSource=>observedSource.Name=="Alpha6Ops.Recovery",
                Sample=(ref ActivityCreationOptions<ActivityContext> options)=>ActivitySamplingResult.AllData,
                ActivityStopped=activity=>
                {
                    if(activity.OperationName!="Save"||interrupted)return;
                    var path=Path.Combine(root,"active-flight-state.json");
                    var state=Read<FlightRecoveryState>(path);
                    if(state.Phase!=FlightPhase.Complete||state.FlightHistoryId is null||state.CompletedFlightHistoryId is not null)return;
                    interrupted=true;
                    Save(Path.Combine(root,"completion-boundary.json"),state);
                    if(stage=="completion-boundary-kill")Thread.Sleep(Timeout.Infinite);
                    held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
                }
            };
            ActivitySource.AddActivityListener(listener);
            try{foreach(var reading in input.Where(s=>s.Telemetry.At>=Start.AddSeconds(7200)&&s.Telemetry.At<=Start.AddSeconds(7214)))source.Emit(reading);}
            finally{held?.Dispose();}
            Require(interrupted,"The production completion save boundary was not reached.");
            owner.Dispose();Pump(connection);return 0;
        }
        if (stage.StartsWith("prepare-", StringComparison.Ordinal))
        {
            owner.AcceptAssignment(Plan);
            var connection = owner.ConnectSimulatorAsync();
            if(stage=="prepare-joined")
            {
                var reading=input.Single(s=>s.Telemetry.At==Start.AddSeconds(10));
                source.Emit(reading with{Evidence=reading.Evidence! with{Route=new("YSSY","YMHB")}});
                Save(Path.Combine(root,"prepared.json"),Capture(owner));
                owner.Dispose();Pump(connection);return 0;
            }
            foreach (var sample in input.Where(s => s.Telemetry.At <= Start.AddSeconds(20))) source.Emit(sample);
            Pump(owner.Monitor!.WriteHeartbeatAsync());
            Save(Path.Combine(root, "prepared.json"), Capture(owner));
            if (stage == "prepare-kill") Thread.Sleep(Timeout.Infinite);
            owner.Dispose(); Pump(connection); return 0;
        }
        if (stage == "audit") { Save(Path.Combine(root, "audit.json"), Capture(owner)); return 0; }
        if(stage=="reconcile-save")
        {
            Save(Path.Combine(root,"reconciliation-failed.json"),Capture(owner));
            owner.ApplyGates("25","A13");
            return 0;
        }
        if (stage == "submit")
        {
            var eligible = owner.Snapshot.PirepEligible;
            var first = owner.SubmitPirep(); var second = owner.SubmitPirep();
            Save(Path.Combine(root, "submitted.json"), new Submission(eligible, first, second)); return 0;
        }
        Save(Path.Combine(root, "restored.json"), Capture(owner));
        if(stage=="restore-joined")return 0;
        var resumedConnection = owner.ConnectSimulatorAsync();
        if (stage is "mismatch" or "backward" or "aircraft" or "position")
        {
            var reading = input.Single(s => s.Telemetry.At == Start.AddSeconds(7200));
            reading=stage switch
            {
                "mismatch"=>reading with{Evidence=reading.Evidence! with{Route=new("YMML","YPAD")}},
                "backward"=>reading with{Telemetry=reading.Telemetry with{At=Start.AddSeconds(19)}},
                "aircraft"=>reading with{Aircraft="Boeing 737"},
                "position"=>reading with{Telemetry=reading.Telemetry with{At=Start.AddSeconds(21),LatitudeDegrees=50,LongitudeDegrees=0},Evidence=reading.Evidence! with{Position=new(50,0)}},
                _=>reading
            };
            source.Emit(reading);
            Save(Path.Combine(root, "mismatched.json"), Capture(owner));
        }
        else
        {
            foreach (var reading in input.Where(s => s.Telemetry.At >= Start.AddSeconds(7200) && s.Telemetry.At <= Start.AddSeconds(7201))) source.Emit(reading);
            Save(Path.Combine(root, "resumed.json"), Capture(owner));
            owner.DisconnectSimulator(); Pump(resumedConnection);
            resumedConnection = owner.ConnectSimulatorAsync();
            source.Emit(input.Single(s => s.Telemetry.At == Start.AddSeconds(7202)));
            Save(Path.Combine(root, "reconnected.json"), Capture(owner));
            foreach (var reading in input.Where(s => s.Telemetry.At > Start.AddSeconds(7202))) source.Emit(reading);
            Save(Path.Combine(root, "completed.json"), Capture(owner));
        }
        owner.Dispose(); Pump(resumedConnection); return 0;
    }

    private static void SequenceReopen(string root)
    {
        string id;
        using (var database = new FlightHistoryDatabase(root))
        {
            id = database.BeginFlight("Synthetic pilot", "live", null, "A320", "YSSY", "YMHB", "BASE718", "test");
            database.RecordEvent(id, "tracking_event", Start, new { value = "immutable-before" });
        }
        using var reopened = new FlightHistoryDatabase(root);
        Exception? failure = null;
        try { reopened.RecordEvent(id, "tracking_event", Start.AddSeconds(1), new { value = "after" }); }
        catch (IOException error) { failure = error; }
        var rows = reopened.ReadFlightEvents(id);
        Check("REOPEN_APPENDS_SEQUENCE", failure is null && rows.Count == 2 && rows[0].Sequence == 1 && rows[1].Sequence == 2, "Reopened SQLite appends to MAX(sequence), preserving the previous event: " + failure?.Message);
    }

    private static void JoinedRecovery(string root)
    {
        Directory.CreateDirectory(root);Save(Path.Combine(root,"telemetry-source.json"),Samples());
        RunChild(root,"prepare-joined");RunChild(root,"restore-joined");
        var restored=Read<Evidence>(Path.Combine(root,"restored.json"));
        Check("JOINED_IN_PROGRESS_RESTORED",restored.Snapshot.JoinedInProgress&&restored.Snapshot.ActualOut is null&&restored.Snapshot.PhaseEvents.Length==0&&restored.Rows.Length==1,"An airborne join stays explicitly joined in progress after a cold restart and cannot manufacture OUT/OFF.");
    }

    private static void LegacyPirep(string root)
    {
        Directory.CreateDirectory(root); ActiveFlightPlanStore.Save(Plan, root);
        var legacy = Read<FlightRecoveryState>(Fixture("active-flight-state.json"));
        FlightRecoveryStore.Save(legacy with { Phase = FlightPhase.Complete, PhaseEvents = [new(FlightPhase.TaxiOut, Start.AddSeconds(1)), new(FlightPhase.Airborne, Start.AddSeconds(6)), new(FlightPhase.TaxiIn, Start.AddSeconds(7203)), new(FlightPhase.Complete, Start.AddSeconds(7211))] }, root);
        using (var database = new FlightHistoryDatabase(root))
        {
            var id = database.BeginFlight("Prior pilot", "live", null, "A320", "YSSY", "YMHB", Plan.FlightNumber, "test");
            database.EndFlight(id, "Complete");
        }
        using var owner = new FlightSessionCoordinator(root, true, new InlineDispatcher());
        Check("LEGACY_MISSING_ID_NOT_ELIGIBLE", !owner.Snapshot.PirepEligible&&owner.Snapshot.Complete&&owner.Snapshot.PhaseEvents.Length==4, "A recovered completed flight without durable identity retains its actuals but does not advertise PIREP eligibility.");
        Check("LEGACY_MISSING_ID_REVIEW_STATUS",owner.Snapshot.TrackingStatus.Contains("review",StringComparison.OrdinalIgnoreCase)&&owner.Snapshot.TrackingStatus.Contains("history",StringComparison.OrdinalIgnoreCase),"The restored status explicitly explains that the history identity needs review.");
        var accepted = owner.SubmitPirep();
        var row = owner.History!.ReadRecentFlights().Single();
        Check("LEGACY_MISSING_ID_FAILS_SAFE", !accepted && owner.History.ReadFlightEvents(row.Id).Count == 0 && owner.Snapshot.Assignment is not null && File.Exists(Path.Combine(root, "active-flight-state.json")), "Legacy recovery with no saved history identity cannot submit against an unrelated same-number completed flight.");
    }

    private static void CompletionBoundary(string root,string interruption)
    {
        Directory.CreateDirectory(root);Save(Path.Combine(root,"telemetry-source.json"),Samples());
        using(var child=Launch(root,"completion-boundary-"+interruption))
        {
            try
            {
                Require(SpinWait.SpinUntil(()=>File.Exists(Path.Combine(root,"completion-boundary.json"))||child.HasExited,30000),"Completion child did not reach its durable boundary.");
                Require(File.Exists(Path.Combine(root,"completion-boundary.json")),"Completion child exited before the boundary.");
                if(interruption=="kill")child.Kill();
                Require(child.WaitForExit(10000),"Completion child did not exit.");
                if(interruption!="kill")Require(child.ExitCode==0,"Locked replacement scenario failed.");
            }
            finally{if(!child.HasExited){child.Kill();child.WaitForExit(5000);}}
        }
        var state=Read<FlightRecoveryState>(Path.Combine(root,"active-flight-state.json"));
        var id=state.FlightHistoryId!;
        FlightHistoryEntry original;FlightHistoryEventEntry[] prefix;
        using(var database=new FlightHistoryDatabase(root)){original=database.ReadRecentFlights().Single();prefix=database.ReadFlightEvents(id).ToArray();}
        Check(interruption+"_COMPLETE_BOUNDARY_REPRODUCED",state.Phase==FlightPhase.Complete&&state.CompletedFlightHistoryId is null&&id==original.Id&&(interruption=="kill"?original.FinalPhase!="Complete":original.FinalPhase=="Complete"),"The real AddTrackingEvent checkpoint has Complete phase and only its active history ID; database completion differs at the two interruption boundaries.");
        RunChild(root,"audit");var restored=Read<Evidence>(Path.Combine(root,"audit.json"));
        Check(interruption+"_COMPLETE_BOUNDARY_RECONCILED",restored.Snapshot.PirepEligible&&restored.Rows.Length==1&&restored.Rows[0].Id==id&&restored.Rows[0].FinalPhase=="Complete", "A cold coordinator reconciles the saved Complete phase with its exact existing history row.");
        Check(interruption+"_COMPLETE_BOUNDARY_EVENTS_PRESERVED",restored.Events.SequenceEqual(prefix)&&restored.Snapshot.PhaseEvents.Length==4,"Completion reconciliation preserves old event payloads/sequences and all four milestones.");
        RunChild(root,"audit");var repeated=Read<Evidence>(Path.Combine(root,"audit.json"));
        Check(interruption+"_COMPLETE_BOUNDARY_IDEMPOTENT",repeated.Rows.SequenceEqual(restored.Rows)&&repeated.Events.SequenceEqual(restored.Events),"A second cold reconciliation does not rewrite ended time, append events or create another flight.");
        RunChild(root,"submit");var submitted=Read<Submission>(Path.Combine(root,"submitted.json"));
        Check(interruption+"_COMPLETE_BOUNDARY_SUBMITS",submitted.EligibleBefore&&submitted.First&&!submitted.Second,"The formerly stranded flight submits exactly once after cold restart.");
        RunChild(root,"audit");var final=Read<Evidence>(Path.Combine(root,"audit.json"));
        Check(interruption+"_COMPLETE_BOUNDARY_ONE_PIREP",final.Rows.Length==1&&final.Rows[0].Id==id&&final.Events.Count(e=>e.Kind=="pirep_submitted")==1&&final.Snapshot.Assignment is null&&!File.Exists(Path.Combine(root,"active-flight-state.json")),"The original row contains one PIREP and closeout survives the final process restart.");
        var reports=Directory.GetFiles(root,"Alpha6OPS-crash-*.json",SearchOption.AllDirectories).Select(Read<JsonElement>).ToArray();
        var expected=interruption=="kill"?"previous_unclean_exit":"flight_recovery_save";
        Check(interruption+"_COMPLETE_BOUNDARY_REPORT_GUARD",reports.Length==1&&reports[0].GetProperty("source").GetString()==expected&&(interruption=="kill"||reports[0].GetProperty("extra").GetProperty("stage").GetString()=="replace"&&reports[0].GetProperty("extra").GetProperty("attempts").GetInt32()==5),"Exactly the injected termination/replacement failure is reported; recovery persistence errors remain visible.");
    }

    private static void ReconciliationRetry(string root)
    {
        Directory.CreateDirectory(root);Save(Path.Combine(root,"telemetry-source.json"),Samples());
        using(var child=Launch(root,"completion-boundary-kill"))
        {
            try
            {
                Require(SpinWait.SpinUntil(()=>File.Exists(Path.Combine(root,"completion-boundary.json"))||child.HasExited,30000),"Retry fixture did not reach completion boundary.");
                Require(File.Exists(Path.Combine(root,"completion-boundary.json")),"Retry fixture exited before completion boundary.");
                child.Kill();Require(child.WaitForExit(10000),"Retry fixture did not exit.");
            }
            finally{if(!child.HasExited){child.Kill();child.WaitForExit(5000);}}
        }
        var before=Read<FlightRecoveryState>(Path.Combine(root,"active-flight-state.json"));
        var id=before.FlightHistoryId!;
        using(var writeLock=new SqliteWriteLock(Path.Combine(root,"Alpha6OPS-flights.sqlite")))RunChild(root,"reconcile-save");
        var blocked=Read<Evidence>(Path.Combine(root,"reconciliation-failed.json"));
        var saved=Read<FlightRecoveryState>(Path.Combine(root,"active-flight-state.json"));
        Check("RECONCILE_WRITE_FAILURE_INELIGIBLE",blocked.Snapshot.Complete&&!blocked.Snapshot.PirepEligible&&blocked.Snapshot.TrackingStatus.Contains("review",StringComparison.OrdinalIgnoreCase),"A failed completion UPDATE keeps actuals visible and withholds PIREP eligibility.");
        Check("RECONCILE_WRITE_FAILURE_RETAINS_ID",saved.FlightHistoryId==id&&saved.CompletedFlightHistoryId is null&&saved.Phase==FlightPhase.Complete&&Read<ActiveFlightPlan>(Path.Combine(root,"active-flight.json")).DepartureGate=="25","Saving a gate change after failed reconciliation retains the same exact pending history ID.");
        RunChild(root,"audit");var retried=Read<Evidence>(Path.Combine(root,"audit.json"));
        Check("RECONCILE_WRITE_FAILURE_RETRIES",retried.Snapshot.PirepEligible&&retried.Rows.Length==1&&retried.Rows[0].Id==id&&retried.Rows[0].FinalPhase=="Complete","Once the writer lock is released a cold restart completes the original exact history row.");
        RunChild(root,"submit");var submitted=Read<Submission>(Path.Combine(root,"submitted.json"));
        RunChild(root,"audit");var final=Read<Evidence>(Path.Combine(root,"audit.json"));
        Check("RECONCILE_WRITE_FAILURE_ONE_PIREP",submitted.First&&!submitted.Second&&final.Rows.Length==1&&final.Rows[0].Id==id&&final.Events.Count(e=>e.Kind=="pirep_submitted")==1&&final.Snapshot.Assignment is null,"The retained identity supports exactly one final PIREP and durable closeout after retry.");
        var reports=Directory.GetFiles(root,"Alpha6OPS-crash-*.json",SearchOption.AllDirectories).Select(Read<JsonElement>).ToArray();
        Check("RECONCILE_WRITE_FAILURE_REPORT_GUARD",reports.Length==2&&reports.Count(r=>r.GetProperty("source").GetString()=="previous_unclean_exit")==1&&reports.Count(r=>r.GetProperty("source").GetString()=="flight_history_recovery")==1,"Exactly one intentional unclean exit and one SQLite completion failure are reported, with no startup/storage failures hidden.");
    }

    private sealed class SqliteWriteLock:IDisposable
    {
        private IntPtr database;
        internal SqliteWriteLock(string path)
        {
            Require(SqliteOpen(path,out database)==0,"Could not open SQLite lock connection.");
            var result=SqliteExec(database,"BEGIN IMMEDIATE;",IntPtr.Zero,IntPtr.Zero,out var error);
            var message=Marshal.PtrToStringUTF8(error);if(error!=IntPtr.Zero)SqliteFree(error);
            if(result!=0){SqliteClose(database);database=IntPtr.Zero;throw new IOException("Could not hold SQLite writer lock: "+message);}
        }
        public void Dispose(){if(database!=IntPtr.Zero){SqliteClose(database);database=IntPtr.Zero;}}
    }
    [DllImport("winsqlite3.dll",EntryPoint="sqlite3_open",CallingConvention=CallingConvention.Cdecl)]private static extern int SqliteOpen([MarshalAs(UnmanagedType.LPUTF8Str)]string path,out IntPtr db);
    [DllImport("winsqlite3.dll",EntryPoint="sqlite3_exec",CallingConvention=CallingConvention.Cdecl)]private static extern int SqliteExec(IntPtr db,[MarshalAs(UnmanagedType.LPUTF8Str)]string sql,IntPtr callback,IntPtr argument,out IntPtr error);
    [DllImport("winsqlite3.dll",EntryPoint="sqlite3_free",CallingConvention=CallingConvention.Cdecl)]private static extern void SqliteFree(IntPtr value);
    [DllImport("winsqlite3.dll",EntryPoint="sqlite3_close",CallingConvention=CallingConvention.Cdecl)]private static extern int SqliteClose(IntPtr db);

    private static Evidence Capture(FlightSessionCoordinator owner)
    {
        var rows = owner.History!.ReadRecentFlights().ToArray();
        return new(owner.Snapshot, rows, rows.SelectMany(row => owner.History.ReadFlightEvents(row.Id)).ToArray());
    }
    private static bool Same<T>(T left, T right) => JsonSerializer.Serialize(left, Json) == JsonSerializer.Serialize(right, Json);
    private sealed record Evidence(FlightSessionSnapshot Snapshot, FlightHistoryEntry[] Rows, FlightHistoryEventEntry[] Events);
    private sealed record Submission(bool EligibleBefore, bool First, bool Second);
    private static void Pump(Task task)
    {
        if (!task.IsCompleted)
        {
            var dispatcher = Dispatcher.CurrentDispatcher; var frame = new DispatcherFrame();
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
    private sealed class InlineDispatcher : IFlightRuntimeDispatcher
    {
        private readonly int thread = Environment.CurrentManagedThreadId;
        public void VerifyAccess() => Require(thread == Environment.CurrentManagedThreadId, "Wrong dispatcher thread");
        public void Post(Action action) { VerifyAccess(); action(); }
    }
    private sealed class SyntheticSource : ISimulatorConnectionSource, IDisposable
    {
        private Action<LiveReading>? received;
        public bool IsRunning() => true;
        public void Launch() => throw new InvalidOperationException("Synthetic recovery must never launch MSFS");
        public Task RunAsync(Action<string> opened, Action<LiveReading> received, CancellationToken cancellation)
        {
            this.received = received; opened("Synthetic process recovery input");
            return Task.Delay(Timeout.Infinite, cancellation);
        }
        internal void Emit(LiveReading reading) => received!(FlightSessionDiagnosticAccess.WithTerminalFixture(reading));
        public void Dispose() { }
    }
    private static LiveReading[] EtaSamples()
    {
        var origin=new GeoPosition(-34.945,138.530556);var destination=new GeoPosition(-37.673333,144.843333);
        var start=DateTimeOffset.Parse("2026-09-28T21:41:30Z");var values=new List<LiveReading>();
        LiveReading Reading(DateTimeOffset at,bool ground,double speed,double fraction)
        {
            var position=new GeoPosition(origin.Latitude+(destination.Latitude-origin.Latitude)*fraction,origin.Longitude+(destination.Longitude-origin.Longitude)*fraction);
            var engines=new[]{new EngineAssessment(1,EngineCondition.Running,"Synthetic fixed evidence"),new EngineAssessment(2,EngineCondition.Running,"Synthetic fixed evidence")};
            var sample=new Telemetry(at,ground,speed,ground&&speed<1,true,LatitudeDegrees:position.Latitude,LongitudeDegrees:position.Longitude,
                AltitudeFeet:ground?20:33000,PressureAltitudeFeet:ground?20:33000,AltitudeAboveGroundFeet:ground?0:32500,
                VerticalSpeedFeetPerMinute:ground?0:500,IndicatedAirspeedKnots:speed,GearExtendedRatio:ground?1:0,EngineStates:engines,RunningEngineCount:2,RunningEngineMask:3);
            return new("Fenix A320",sample,new(position,"VH-ETA",null,true,PlanStatus:"No simulator flight plan"));
        }
        values.Add(Reading(start,true,0,0));values.Add(Reading(start.AddSeconds(1),true,3,0));values.Add(Reading(start.AddSeconds(5),true,3,0));
        values.Add(Reading(start.AddSeconds(6),false,180,.001));values.Add(Reading(start.AddSeconds(10),false,200,.0015));
        for(var minute=1;minute<=30;minute++)
        {
            var fraction=minute<=20?.001+.139*minute/20:.14+.14*(minute-20)/10;
            values.Add(Reading(start.AddSeconds(6).AddMinutes(minute),false,450,fraction));
        }
        values.Add(Reading(DateTimeOffset.Parse("2026-09-28T22:12:34Z"),false,450,.298));
        values.Add(Reading(DateTimeOffset.Parse("2026-09-28T22:12:43Z"),false,450,.2984));
        return values.ToArray();
    }
    private static LiveReading[] Samples()
    {
        return new[] { 0, 1, 5, 6, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 7200, 7201, 7202, 7203, 7206, 7210, 7211, 7214, 7215 }.Select(seconds =>
        {
            var ground = seconds < 6 || seconds >= 7203;
            var stopped = seconds == 0 || seconds >= 7211;
            var engine = seconds < 7211;
            var fraction = seconds < 7200 ? seconds * .0001 : .9997;
            var position = new GeoPosition(-33.946 + (-42.836111 + 33.946) * fraction, 151.177111 + (147.510278 - 151.177111) * fraction);
            var speed = stopped ? 0 : ground ? (seconds >= 7203 ? 15 : 3) : 180;
            var states = new[] { new EngineAssessment(1, engine ? EngineCondition.Running : EngineCondition.Off, "Synthetic fixed evidence"), new EngineAssessment(2, engine ? EngineCondition.Running : EngineCondition.Off, "Synthetic fixed evidence") };
            var sample = new Telemetry(Start.AddSeconds(seconds), ground, speed, stopped, engine, LatitudeDegrees: position.Latitude, LongitudeDegrees: position.Longitude, AltitudeFeet: ground ? 20 : 32000, PressureAltitudeFeet: ground ? 20 : 32000, AltitudeAboveGroundFeet: ground ? 0 : 30000, VerticalSpeedFeetPerMinute: ground ? 0 : 500, IndicatedAirspeedKnots: speed, GearExtendedRatio: ground ? 1 : 0, EngineStates: states, RunningEngineCount: engine ? 2 : 0, RunningEngineMask: engine ? 3 : 0);
            return new LiveReading("Fenix A320", sample, new FlightEvidence(position, "VH-TEST", null, true, PlanStatus: "No simulator flight plan"));
        }).ToArray();
    }
}
