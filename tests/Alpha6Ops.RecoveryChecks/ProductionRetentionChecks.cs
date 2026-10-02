using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using Alpha6Ops.Desktop;

internal static class ProductionRetentionChecks
{
    private static readonly List<object> Results = [];
    private static int failed;
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 15, 0, 0, TimeSpan.Zero);

    internal static int Run(string[] args)
    {
        var root = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine("work", "retention-checks", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        Check("COMPLETED_JOURNAL_COMPRESSES_ATOMICALLY", () =>
        {
            var paths = Paths(Path.Combine(root, "completed")); var journal = Journal(paths, "complete.jsonl", true, Now.AddDays(-1));
            var original = File.ReadAllBytes(journal);
            var report = ProductionRetention.ApplyForTest(paths, Now, null, RetentionLimits.TestDefaults);
            var compressed = journal + ".completed.gz";
            Require(!File.Exists(journal) && File.Exists(compressed), "Completed journal was not replaced by gzip");
            using var gzip = new GZipStream(File.OpenRead(compressed), CompressionMode.Decompress); using var output = new MemoryStream(); gzip.CopyTo(output);
            Require(original.SequenceEqual(output.ToArray()), "Compressed journal does not reproduce the source bytes");
            Require(report.Compressed.Contains(compressed) && !Directory.EnumerateFiles(paths.Diagnostics, "*.tmp-*", SearchOption.AllDirectories).Any(), "Atomic compression report or staging cleanup is wrong");
        });
        Check("ACTIVE_AND_NEWEST_INTERRUPTED_JOURNALS_ARE_PRESERVED", () =>
        {
            var paths = Paths(Path.Combine(root, "protected"));
            var active = Journal(paths, "active.jsonl", false, Now.AddDays(-40));
            var older = Journal(paths, "older.jsonl", false, Now.AddDays(-35));
            var newest = Journal(paths, "newest.jsonl", false, Now.AddDays(-2));
            var report = ProductionRetention.ApplyForTest(paths, Now, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { active }, RetentionLimits.TestDefaults);
            Require(File.Exists(active) && File.Exists(newest), "Active or newest unresolved journal was removed or compressed");
            Require(File.Exists(older + ".interrupted.gz") || !File.Exists(older), "Older interruption was neither compressed nor expired");
            Require(report.Preserved.Contains(active) && report.Preserved.Contains(newest), "Protected journals were not reported");
        });
        Check("AGE_AND_COUNT_WINDOWS_ARE_ENFORCED", () =>
        {
            var paths = Paths(Path.Combine(root, "age"));
            for (var i = 0; i < 4; i++) GzipFile(Path.Combine(paths.Diagnostics, "TestLogs", $"completed-{i}.jsonl.completed.gz"), Now.AddDays(-20 - i));
            for (var i = 0; i < 4; i++) GzipFile(Path.Combine(paths.Diagnostics, "TestLogs", $"incident-{i}.jsonl.interrupted.gz"), Now.AddDays(-40 - i));
            for (var i = 0; i < 21; i++) Sized(Path.Combine(paths.Diagnostics, "CrashReports", $"Alpha6OPS-crash-{i:D2}.json"), 8, Now.AddDays(-100 - i));
            for (var i = 0; i < 21; i++) Sized(Path.Combine(paths.Cache, "SimBrief", $"ofp-{i:D2}.txt"), 8, Now.AddDays(-100 - i));
            ProductionRetention.ApplyForTest(paths, Now, null, RetentionLimits.TestDefaults);
            Require(Directory.GetFiles(Path.Combine(paths.Diagnostics, "TestLogs"), "*.completed.gz").Length == 3, "Completed flight last-three floor is wrong");
            Require(Directory.GetFiles(Path.Combine(paths.Diagnostics, "TestLogs"), "*.interrupted.gz").Length == 3, "Interrupted incident last-three floor is wrong");
            Require(Directory.GetFiles(Path.Combine(paths.Diagnostics, "CrashReports")).Length == 20, "Crash report 90-day/20-item rule is wrong");
            Require(Directory.GetFiles(Path.Combine(paths.Cache, "SimBrief")).Length == 20, "SimBrief 90-day/latest-20 rule is wrong");
        });
        Check("BYTE_CAPS_DELETE_OLDEST_ELIGIBLE_FILES", () =>
        {
            var paths = Paths(Path.Combine(root, "caps"));
            var limits = RetentionLimits.TestDefaults with { CompletedBytes = 24, InterruptedBytes = 24, RoutineBytes = 24, CrashBytes = 24, MapBytes = 24, SimBriefBytes = 24 };
            for (var i = 0; i < 4; i++) Sized(Path.Combine(paths.Diagnostics, "TestLogs", $"cap-{i}.jsonl.completed.gz"), 12, Now.AddMinutes(-i));
            for (var i = 0; i < 4; i++) Sized(Path.Combine(paths.Diagnostics, "TestLogs", $"incident-cap-{i}.jsonl.interrupted.gz"), 12, Now.AddMinutes(-i));
            for (var i = 0; i < 4; i++) Sized(Path.Combine(paths.Diagnostics, $"routine-{i}.log"), 12, Now.AddMinutes(-i));
            for (var i = 0; i < 4; i++) Sized(Path.Combine(paths.Diagnostics, "CrashReports", $"crash-{i}.json"), 12, Now.AddMinutes(-i));
            for (var i = 0; i < 4; i++) Sized(Path.Combine(paths.Cache, "SimBrief", $"ofp-cap-{i}.txt"), 12, Now.AddMinutes(-i));
            var newestIncident = Path.Combine(paths.Diagnostics, "TestLogs", "incident-cap-0.jsonl.interrupted.gz");
            ProductionRetention.ApplyForTest(paths, Now, null, limits);
            Require(Bytes(Path.Combine(paths.Diagnostics, "TestLogs"), "*.completed.gz") <= 24, "Completed pool exceeds cap");
            Require(Bytes(Path.Combine(paths.Diagnostics, "TestLogs"), "*.interrupted.gz") <= 24 && File.Exists(newestIncident), "Interrupted pool cap removed newest unresolved evidence");
            Require(Bytes(paths.Diagnostics, "*.log") <= 24, "Routine log pool exceeds cap");
            Require(Bytes(Path.Combine(paths.Diagnostics, "CrashReports"), "*") <= 24, "Crash pool exceeds cap");
            Require(Bytes(Path.Combine(paths.Cache, "SimBrief"), "*") <= 24, "SimBrief pool exceeds cap");
        });
        Check("MAP_CACHE_USES_AGE_AND_LRU_CAP", () =>
        {
            var paths = Paths(Path.Combine(root, "map")); var map = Path.Combine(paths.Cache, "MapCache");
            var old = Sized(Path.Combine(map, "old.bin"), 12, Now.AddDays(-31), Now.AddDays(-1));
            var least = Sized(Path.Combine(map, "least.bin"), 12, Now.AddDays(-1), Now.AddHours(-4));
            var recent = Sized(Path.Combine(map, "recent.bin"), 12, Now.AddDays(-1), Now.AddHours(-1));
            ProductionRetention.ApplyForTest(paths, Now, null, RetentionLimits.TestDefaults with { MapBytes = 12 });
            Require(!File.Exists(old) && !File.Exists(least) && File.Exists(recent), "Map cleanup did not expire by age then least-recent access");
        });
        Check("PROTECTED_SIMBRIEF_REFERENCE_SURVIVES_QUOTA", () =>
        {
            var paths = Paths(Path.Combine(root, "simbrief-protected"));
            var referenced = Sized(Path.Combine(paths.Cache, "SimBrief", "referenced.pdf"), 40, Now.AddDays(-120));
            Sized(Path.Combine(paths.Cache, "SimBrief", "new.pdf"), 40, Now.AddDays(-1));
            ProductionRetention.ApplyForTest(paths, Now, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { referenced }, RetentionLimits.TestDefaults with { SimBriefBytes = 20 });
            Require(File.Exists(referenced), "Active/recovery OFP was removed to satisfy quota");
        });
        Check("DIAGNOSTIC_INDEX_REMOVES_MISSING_FILES", () =>
        {
            var paths = Paths(Path.Combine(root, "index")); var logs = Path.Combine(paths.Diagnostics, "TestLogs"); var crashes = Path.Combine(paths.Diagnostics, "CrashReports");
            var file = Sized(Path.Combine(logs, "Alpha6OPS-index.jsonl"), 8, Now);
            using var database = new LogFileDatabase(paths.Diagnostics);
            Require(database.Refresh(logs, crashes) == 1, "Initial diagnostic file was not indexed");
            File.Delete(file);
            Require(database.Refresh(logs, crashes) == 0 && database.ReadRecent().Count == 0, "Missing file remained in the derived index");
        });
        File.WriteAllText(Path.Combine(root, "retention-summary.json"), JsonSerializer.Serialize(new { executed = Results.Count, passed = Results.Count - failed, failed, checks = Results }, new JsonSerializerOptions { WriteIndented = true }));
        return failed == 0 ? 0 : 1;
    }

    private static ProductStoragePaths Paths(string root)
    {
        var paths = new ProductStoragePaths(root, Path.Combine(root, "Data"), Path.Combine(root, "Settings"), Path.Combine(root, "Diagnostics"), Path.Combine(root, "Cache"), Path.Combine(root, "State"), false);
        foreach (var path in new[] { paths.Data, paths.Settings, paths.Diagnostics, paths.Cache, paths.State, Path.Combine(paths.Diagnostics, "TestLogs"), Path.Combine(paths.Diagnostics, "CrashReports"), Path.Combine(paths.Cache, "MapCache"), Path.Combine(paths.Cache, "SimBrief") }) Directory.CreateDirectory(path);
        return paths;
    }
    private static string Journal(ProductStoragePaths paths, string name, bool complete, DateTimeOffset modified)
    {
        var path = Path.Combine(paths.Diagnostics, "TestLogs", name);
        File.WriteAllText(path, "{\"kind\":\"session_started\"}\n" + (complete ? "{\"kind\":\"session_ended\"}\n" : "{\"kind\":\"telemetry\"}\n"));
        File.SetLastWriteTimeUtc(path, modified.UtcDateTime); return path;
    }
    private static string GzipFile(string path, DateTimeOffset modified)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); using (var gzip = new GZipStream(File.Create(path), CompressionLevel.SmallestSize)) gzip.Write(Encoding.UTF8.GetBytes("evidence"));
        File.SetLastWriteTimeUtc(path, modified.UtcDateTime); return path;
    }
    private static string Sized(string path, long bytes, DateTimeOffset modified, DateTimeOffset? accessed = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read)) stream.SetLength(bytes);
        File.SetLastWriteTimeUtc(path, modified.UtcDateTime); File.SetLastAccessTimeUtc(path, (accessed ?? modified).UtcDateTime); return path;
    }
    private static long Bytes(string directory, string pattern) => Directory.Exists(directory) ? Directory.GetFiles(directory, pattern).Sum(path => new FileInfo(path).Length) : 0;
    private static void Require(bool valid, string reason) { if (!valid) throw new Exception(reason); }
    private static void Check(string name, Action action)
    {
        string? error = null; var watch = System.Diagnostics.Stopwatch.StartNew();
        try { action(); } catch (Exception exception) { failed++; error = exception.ToString(); }
        Results.Add(new { name, passed = error is null, milliseconds = watch.Elapsed.TotalMilliseconds, error });
        Console.WriteLine($"{(error is null ? "PASS" : "FAIL")} {name}{(error is null ? "" : ": " + error.Split('\n')[0])}");
    }
}
