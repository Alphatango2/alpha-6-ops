using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Alpha6Ops.Desktop;

internal static class ProgramMonitorSmokeTest
{
    internal static async Task RunAsync(MainWindow owner, string outputDirectory)
    {
        var checks = new List<string>();
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks.Add(label); }
        var directory = Path.Combine(outputDirectory, "monitor-lifecycle");
        var logs = Path.Combine(directory, "TestLogs"); Directory.CreateDirectory(logs);
        File.WriteAllText(Path.Combine(logs, "Alpha6OPS-one.jsonl"), "{}");
        var statuses = new List<string>(); var dispatcher = Dispatcher.CurrentDispatcher;
        using var monitor = new ProgramMonitor(logs, status =>
        {
            Check(dispatcher.CheckAccess(), "status callback owns dispatcher"); statuses.Add(status);
        }, directory);
        await monitor.WriteHeartbeatAsync();
        Check(statuses.Count == 1 && statuses[0].StartsWith("PROGRAM HEALTHY", StringComparison.Ordinal), "startup publishes healthy after indexing");
        var gate = typeof(LogFileDatabase).GetField("gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(monitor.Database)!;
        var tick = typeof(ProgramMonitor).GetMethod("OnTimerTick", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Task heartbeat;
        // Hold only this test database's lock to make an in-flight refresh deterministic.
        Monitor.Enter(gate);
        try
        {
            monitor.Update("Connected", "Test aircraft", DateTimeOffset.Parse("2026-09-02T10:00:00Z"), true);
            heartbeat = monitor.WriteHeartbeatAsync();
            Check(ReferenceEquals(heartbeat, monitor.WriteHeartbeatAsync()), "overlapping heartbeat requests share one task");
            for (var i = 0; i < 50; i++) tick.Invoke(monitor, [null, EventArgs.Empty]);
            var sentinel = await dispatcher.InvokeAsync(() => true, DispatcherPriority.Send);
            Check(sentinel && !heartbeat.IsCompleted, "dispatcher remains available while indexing waits");
            monitor.Update("Connected", sampleReceived: true);
        }
        finally { Monitor.Exit(gate); }
        await heartbeat;
        using (var snapshot = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "program-status.json"))))
            Check(snapshot.RootElement.GetProperty("samples").GetInt64() == 1, "heartbeat uses immutable captured telemetry fields");
        Check(statuses.Count == 2, "skipped timer ticks do not queue extra work");
        var fresh = monitor.RefreshNowAsync();
        var next = monitor.RefreshNowAsync();
        var rowsTask = monitor.ReadRecentAsync();
        await Task.WhenAll(fresh, next, rowsTask);
        Check(rowsTask.Result.Count == 1, "explicit refresh and recent reads serialize");
        using (var snapshot = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "program-status.json"))))
            Check(snapshot.RootElement.GetProperty("samples").GetInt64() == 2, "explicit refresh captures newer telemetry");
        var view = new LogDatabaseWindow(monitor, await monitor.ReadRecentAsync()) { Owner = owner };
        view.Show(); await view.RefreshRowsAsync();
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Check(view.FilesGrid.Items.Count == 1, "log viewer asynchronously reads indexed rows");
        DashboardSmokeTest.Capture(view, Path.Combine(outputDirectory, "diagnostic-files-preview.png"));
        Monitor.Enter(gate);
        Task reading;
        try
        {
            reading = view.RefreshRowsAsync(); view.Close();
            Check(!reading.IsCompleted, "closing log viewer does not synchronously wait for its read");
        }
        finally { Monitor.Exit(gate); }
        await reading;
        Check(!view.IsVisible, "read completion does not reopen a closed view");

        // Force a status-write error without touching any real profile or weakening I/O rules.
        var temporary = Path.Combine(directory, "program-status.json.tmp"); Directory.CreateDirectory(temporary);
        await monitor.RefreshNowAsync();
        Check(statuses[^1].StartsWith("PROGRAM MONITOR ERROR", StringComparison.Ordinal), "I/O failure publishes monitor error on dispatcher");
        Directory.Delete(temporary);
        await monitor.RefreshNowAsync();
        Check(statuses[^1].StartsWith("PROGRAM HEALTHY", StringComparison.Ordinal), "heartbeat recovers after I/O failure");

        // A second thread holds the SQLite gate until Stop has begun, so Stop must join
        // an active worker without depending on a dispatcher continuation.
        using var held = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var blocker = Task.Run(() => { lock (gate) { held.Set(); release.Wait(); } });
        await Task.Run(() => held.Wait());
        var pending = monitor.WriteHeartbeatAsync();
        var publications = statuses.Count;
        var releasing = Task.Run(() => { Thread.Sleep(50); release.Set(); });
        monitor.Stop("smoke_stop_in_flight");
        await Task.WhenAll(pending, blocker, releasing);
        await monitor.WriteHeartbeatAsync(); await monitor.RefreshNowAsync();
        Check(statuses.Count == publications, "stop suppresses pending and future publications");
        Check((await monitor.ReadRecentAsync()).Count == 0, "stopped monitor never accesses disposed database");
        using (var stopped = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "program-status.json"))))
            Check(!stopped.RootElement.GetProperty("running").GetBoolean() && stopped.RootElement.GetProperty("reason").GetString() == "smoke_stop_in_flight", "stopped status remains final after active worker finishes");
        using (var reopened = new LogFileDatabase(directory)) Check(reopened.ReadRecent().Count == 1, "shutdown releases database after durable refresh");
        var replacement=await ReplacementBehavior(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory,"program-status-replacement.json"),JsonSerializer.Serialize(replacement,new JsonSerializerOptions{WriteIndented=true}));
        Check(replacement.TransientStopped,"released transient status lock publishes the final stopped marker");
        Check(replacement.HeldOldFilePreserved&&replacement.HeldBounded&&replacement.HeldReportObserved,
            "bounded held-lock failure preserves the old complete file, returns within two seconds and emits one five-attempt diagnostic");
        Check(replacement.ReadOnlyOneAttempt,"a read-only status target fails permanently after one replacement attempt");
        Check(replacement.StressComplete,"500 atomic replacements remain complete JSON under concurrent read/delete sharing");
        File.WriteAllText(Path.Combine(outputDirectory, "monitor-lifecycle-smoke.json"), JsonSerializer.Serialize(new { passed = true, count = checks.Count, checks }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task<ReplacementResult> ReplacementBehavior(string outputDirectory)
    {
        var transientRoot=Path.Combine(outputDirectory,"monitor-status-transient");var transientLogs=Path.Combine(transientRoot,"TestLogs");Directory.CreateDirectory(transientLogs);
        bool transientStopped;long transientElapsedMs;
        using(var transient=new ProgramMonitor(transientLogs,_=>{},transientRoot))
        {
            await transient.RefreshNowAsync();var target=Path.Combine(transientRoot,"program-status.json");
            using var held=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.Read);
            var release=Task.Run(()=>{Thread.Sleep(30);held.Dispose();});var transientWatch=Stopwatch.StartNew();transient.Stop("transient_lock");transientWatch.Stop();transientElapsedMs=transientWatch.ElapsedMilliseconds;await release;
            using var document=JsonDocument.Parse(File.ReadAllText(target));transientStopped=!document.RootElement.GetProperty("running").GetBoolean();
        }
        var heldRoot=Path.Combine(outputDirectory,"monitor-status-held");var heldLogs=Path.Combine(heldRoot,"TestLogs");Directory.CreateDirectory(heldLogs);
        bool oldPreserved,bounded,reportObserved;var heldAttempts=0;var heldHResult=0;long heldElapsedMs;
        using(var permanent=new ProgramMonitor(heldLogs,_=>{},heldRoot))
        {
            await permanent.RefreshNowAsync();var target=Path.Combine(heldRoot,"program-status.json");var before=File.ReadAllBytes(target);
            using var held=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.Read);var watch=Stopwatch.StartNew();
            permanent.Stop("permanent_held_lock");watch.Stop();heldElapsedMs=watch.ElapsedMilliseconds;
            oldPreserved=before.SequenceEqual(File.ReadAllBytes(target));bounded=watch.Elapsed<TimeSpan.FromSeconds(2);
            var reports=Directory.Exists(Path.Combine(heldRoot,"CrashReports"))?Directory.GetFiles(Path.Combine(heldRoot,"CrashReports"),"Alpha6OPS-crash-*.json"):[];
            if(reports.Length==1)
            {
                using var report=JsonDocument.Parse(File.ReadAllText(reports[0]));var root=report.RootElement;
                heldAttempts=root.GetProperty("extra").GetProperty("attempts").GetInt32();heldHResult=root.GetProperty("extra").GetProperty("hresult").GetInt32();
                reportObserved=root.GetProperty("source").GetString()=="program_status_stop"&&root.GetProperty("extra").GetProperty("stage").GetString()=="replace"&&heldAttempts==5;
            }
            else reportObserved=false;
        }
        var readOnlyRoot=Path.Combine(outputDirectory,"monitor-status-read-only");Directory.CreateDirectory(readOnlyRoot);var readOnlyPath=Path.Combine(readOnlyRoot,"program-status.json");
        var readOnlyStore=new ProgramStatusStore(readOnlyPath);readOnlyStore.Write(new{sequence=0});File.SetAttributes(readOnlyPath,FileAttributes.ReadOnly);bool readOnlyOneAttempt=false;var readOnlyAttempts=0;var readOnlyHResult=0;
        try{readOnlyStore.Write(new{sequence=1});}
        catch(Exception error){readOnlyAttempts=error.Data["StatusAttempts"] is int attempts?attempts:0;readOnlyHResult=error.HResult;readOnlyOneAttempt=error.Data["StatusStage"] as string=="replace"&&readOnlyAttempts==1;}
        finally{File.SetAttributes(readOnlyPath,FileAttributes.Normal);}
        var stressRoot=Path.Combine(outputDirectory,"monitor-status-stress");Directory.CreateDirectory(stressRoot);var stressStore=new ProgramStatusStore(Path.Combine(stressRoot,"program-status.json"));stressStore.Write(new{sequence=0,payload=new string('X',1024)});
        var done=0;var parsed=0;var parseFailures=0;var reader=Task.Run(()=>
        {
            while(Volatile.Read(ref done)==0)
            {
                try{using var document=stressStore.Read();if(document is null||!document.RootElement.TryGetProperty("sequence",out _))Interlocked.Increment(ref parseFailures);else Interlocked.Increment(ref parsed);}
                catch{Interlocked.Increment(ref parseFailures);}
            }
        });
        for(var sequence=1;sequence<=500;sequence++)stressStore.Write(new{sequence,payload=new string('X',1024)});
        Volatile.Write(ref done,1);await reader;using var final=stressStore.Read();
        var stressComplete=parsed>0&&parseFailures==0&&final?.RootElement.GetProperty("sequence").GetInt32()==500;
        return new(transientStopped,transientElapsedMs,oldPreserved,bounded,reportObserved,heldAttempts,heldElapsedMs,heldHResult,
            readOnlyOneAttempt,readOnlyAttempts,readOnlyHResult,stressComplete,parsed,parseFailures);
    }
    private sealed record ReplacementResult(bool TransientStopped,long TransientElapsedMs,bool HeldOldFilePreserved,bool HeldBounded,bool HeldReportObserved,
        int HeldAttempts,long HeldElapsedMs,int HeldHResult,bool ReadOnlyOneAttempt,int ReadOnlyAttempts,int ReadOnlyHResult,
        bool StressComplete,int StressReads,int StressFailures);
}
