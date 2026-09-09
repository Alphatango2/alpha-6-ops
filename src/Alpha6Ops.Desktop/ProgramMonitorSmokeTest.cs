using System;
using System.Collections.Generic;
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
        File.WriteAllText(Path.Combine(outputDirectory, "monitor-lifecycle-smoke.json"), JsonSerializer.Serialize(new { passed = true, count = checks.Count, checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
