using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Alpha6Ops.Desktop;

internal static class DiagnosticIndexSmokeTest
{
    internal static async Task RunAsync(string outputDirectory, bool includeLargeWorkload = false)
    {
        outputDirectory = Path.GetFullPath(outputDirectory);
        var checks = new List<string>();
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks.Add(label); }
        var timings = new List<object>();
        foreach (var count in includeLargeWorkload ? new[] { 20, 1000 } : new[] { 20 })
        {
            var root = Path.Combine(outputDirectory, "index-" + count);
            var logs = Path.Combine(root, "TestLogs");
            var crashes = Path.Combine(root, "CrashReports");
            Directory.CreateDirectory(logs);
            for (var i = 0; i < count; i++)
            {
                var path = Path.Combine(logs, $"Alpha6OPS-{i:D4}-é'.jsonl");
                File.WriteAllText(path, "{}\n");
                File.SetLastWriteTimeUtc(path, new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc).AddSeconds(i));
            }
            using var database = new LogFileDatabase(root);
            var handle = (IntPtr)typeof(LogFileDatabase).GetField("database", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(database)!;
            var writes = 0;
            TraceCallback trace = (_, _, statement, _) =>
            {
                if ((Marshal.PtrToStringUTF8(Sql(statement)) ?? "").StartsWith("INSERT INTO diagnostic_file", StringComparison.Ordinal)) writes++;
                return 0;
            };
            Trace(handle, 1, trace, IntPtr.Zero);
            try
            {
                Check(database.Refresh(logs, crashes) == count, $"{count}: initial index and missing crash directory");
                Check(database.ReadRecent().Count == Math.Min(250, count), $"{count}: default recent limit");
                Check(database.ReadRecent(0).Count == 1 && database.ReadRecent(5000).Count == count, $"{count}: clamped limits");
                Check(database.ReadRecent()[0].Name.StartsWith($"Alpha6OPS-{count - 1:D4}", StringComparison.Ordinal), $"{count}: descending modification order");
                var indexed = Query(handle, "SELECT min(indexed_utc) FROM diagnostic_file;");
                for (var run = 0; run < 3; run++)
                {
                    writes = 0;
                    var before = GC.GetAllocatedBytesForCurrentThread();
                    var clock = Stopwatch.StartNew();
                    var sentinel = Dispatcher.CurrentDispatcher.InvokeAsync(() => clock.Elapsed.TotalMilliseconds, DispatcherPriority.Send);
                    database.Refresh(logs, crashes);
                    var elapsed = clock.Elapsed.TotalMilliseconds;
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                    var delay = await sentinel;
                    Check(writes == count, $"{count}: unchanged refresh {run} writes every timestamp");
                    timings.Add(new { files = count, workload = "unchanged", run, elapsedMs = elapsed, dispatcherDelayMs = delay, allocatedBytes = allocated, writes });
                }
                Check(string.CompareOrdinal(Query(handle, "SELECT min(indexed_utc) FROM diagnostic_file;"), indexed) > 0, $"{count}: indexed time refreshed");
                var changed = Directory.EnumerateFiles(logs).First();
                File.AppendAllText(changed, "appended");
                database.Refresh(logs, crashes);
                Check(database.ReadRecent(1000).Single(f => f.Path == changed).SizeBytes == new FileInfo(changed).Length, $"{count}: appended metadata");
                Directory.CreateDirectory(crashes);
                var added = Path.Combine(crashes, "Alpha6OPS-crash-new.json"); File.WriteAllText(added, "{}");
                Check(database.Refresh(logs, crashes) == count + 1 && database.ReadRecent()[0].Status == "Crash", $"{count}: new crash classification");
                File.Delete(added);
                Check(database.Refresh(logs, crashes) == count + 1 && database.ReadRecent()[0].Path == added, $"{count}: deleted rows retained");
                Check(database.Refresh(Path.Combine(root, "missing"), Path.Combine(root, "also-missing")) == count + 1, $"{count}: missing directories retain rows");
                File.WriteAllText(added, "{}");
                Query(handle, "CREATE TRIGGER fail_crash BEFORE INSERT ON diagnostic_file WHEN NEW.kind='crash_report' BEGIN SELECT RAISE(FAIL,'expected failure'); END;");
                indexed = Query(handle, "SELECT min(indexed_utc) FROM diagnostic_file WHERE kind='flight_log';");
                var failed = false;
                try { database.Refresh(logs, crashes); } catch (IOException) { failed = true; }
                Check(failed && string.CompareOrdinal(Query(handle, "SELECT min(indexed_utc) FROM diagnostic_file WHERE kind='flight_log';"), indexed) > 0, $"{count}: failure keeps earlier committed progress");
                Query(handle, "DROP TRIGGER fail_crash;");
                Check(database.Refresh(logs, crashes) == count + 1, $"{count}: refresh recovers after SQL failure");
                Check(Query(handle, "PRAGMA journal_mode;") == "wal" && Query(handle, "PRAGMA synchronous;") == "2", $"{count}: WAL and FULL retained");
            }
            finally { Trace(handle, 0, null, IntPtr.Zero); GC.KeepAlive(trace); }
            database.Dispose(); database.Dispose();
            using var reopened = new LogFileDatabase(root);
            Check(reopened.ReadRecent(1000).Count == Math.Min(1000, count + 1), $"{count}: disposal and reopen");
        }
        // Deterministic disappearance after the native upsert observes metadata for the first
        // file: the directory enumerator has already buffered the two names on Windows.
        var raceRoot = Path.Combine(outputDirectory, "index-disappearance");
        var raceLogs = Path.Combine(raceRoot, "logs"); Directory.CreateDirectory(raceLogs);
        File.WriteAllText(Path.Combine(raceLogs, "Alpha6OPS-first.jsonl"), "{}");
        File.WriteAllText(Path.Combine(raceLogs, "Alpha6OPS-second.jsonl"), "{}");
        var paths = Directory.EnumerateFiles(raceLogs, "Alpha6OPS-*").ToArray();
        using (var database = new LogFileDatabase(raceRoot))
        {
            var handle = (IntPtr)typeof(LogFileDatabase).GetField("database", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(database)!;
            TraceCallback remove = (_, _, statement, _) =>
            {
                if ((Marshal.PtrToStringUTF8(Sql(statement)) ?? "").StartsWith("INSERT INTO diagnostic_file", StringComparison.Ordinal)) File.Delete(paths[1]);
                return 0;
            };
            Trace(handle, 1, remove, IntPtr.Zero);
            var missing = false;
            try { database.Refresh(raceLogs, Path.Combine(raceRoot, "missing")); }
            catch (FileNotFoundException) { missing = true; }
            finally { Trace(handle, 0, null, IntPtr.Zero); GC.KeepAlive(remove); }
            Check(missing && database.ReadRecent().Count == 1, "disappearing file aborts with earlier row committed");
        }
        // Actual heartbeat, including process/status work, with a separate temporary monitor.
        var monitorFiles = includeLargeWorkload ? 1000 : 20;
        var monitorRoot = Path.Combine(outputDirectory, "index-" + monitorFiles);
        using (var monitor = new ProgramMonitor(Path.Combine(monitorRoot, "TestLogs"), _ => { }, monitorRoot))
        {
            await monitor.WriteHeartbeatAsync();
            var before = GC.GetAllocatedBytesForCurrentThread(); var clock = Stopwatch.StartNew();
            var sentinel = Dispatcher.CurrentDispatcher.InvokeAsync(() => clock.Elapsed.TotalMilliseconds, DispatcherPriority.Send);
            monitor.Update("Connected", "Test aircraft", DateTimeOffset.Parse("2026-09-02T10:00:00Z"), true);
            var heartbeat = monitor.WriteHeartbeatAsync();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            var delay = await sentinel;
            await heartbeat;
            timings.Add(new { files = monitorFiles, workload = "heartbeat", elapsedMs = clock.Elapsed.TotalMilliseconds, dispatcherDelayMs = delay, dispatcherAllocatedBytes = allocated });
            using var status = JsonDocument.Parse(File.ReadAllText(Path.Combine(monitorRoot, "program-status.json")));
            Check(status.RootElement.GetProperty("running").GetBoolean() && status.RootElement.GetProperty("samples").GetInt64() == 1, "heartbeat preserves sample/status fields");
            monitor.Stop("smoke"); await monitor.WriteHeartbeatAsync();
            using var stopped = JsonDocument.Parse(File.ReadAllText(Path.Combine(monitorRoot, "program-status.json")));
            Check(!stopped.RootElement.GetProperty("running").GetBoolean(), "stop remains final and prevents later heartbeat");
        }
        File.WriteAllText(Path.Combine(outputDirectory, "diagnostic-index-smoke.json"), JsonSerializer.Serialize(new { passed = true, count = checks.Count, checks, timings }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Query(IntPtr database, string sql)
    {
        var value = "";
        ExecCallback callback = (_, columns, values, _) => { if (columns > 0) value = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(values)) ?? ""; return 0; };
        var result = Exec(database, sql, callback, IntPtr.Zero, out var error);
        if (error != IntPtr.Zero) Free(error);
        if (result != 0) throw new IOException("Diagnostic test query failed: " + result);
        GC.KeepAlive(callback); return value;
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int TraceCallback(uint mask, IntPtr context, IntPtr statement, IntPtr detail);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ExecCallback(IntPtr context, int columns, IntPtr values, IntPtr names);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_trace_v2", CallingConvention = CallingConvention.Cdecl)] private static extern int Trace(IntPtr database, uint mask, TraceCallback? callback, IntPtr context);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_sql", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr Sql(IntPtr statement);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_exec", CallingConvention = CallingConvention.Cdecl)] private static extern int Exec(IntPtr database, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, ExecCallback callback, IntPtr context, out IntPtr error);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_free", CallingConvention = CallingConvention.Cdecl)] private static extern void Free(IntPtr value);
}
