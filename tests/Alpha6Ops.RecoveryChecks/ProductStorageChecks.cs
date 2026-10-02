using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using Alpha6Ops.Desktop;

internal static class ProductStorageChecks
{
    private static readonly List<object> Results = [];
    private static int failed;

    internal static int Run(string[] args)
    {
        var root = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine("work", "storage-checks", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        Check("FRESH_PROFILE_CREATES_CATEGORIZED_ROOTS", () =>
        {
            var paths = ProductStorage.Migrate(Path.Combine(root, "fresh-legacy"), Path.Combine(root, "fresh-canonical"));
            Require(!paths.UsingLegacyLayout, "Fresh profile fell back to a legacy layout");
            foreach (var path in new[] { paths.Data, paths.Settings, paths.Diagnostics, paths.Cache, paths.State })
                Require(Directory.Exists(path), "Missing category directory " + path);
            Require(File.Exists(Path.Combine(paths.State, "migration.json")), "Fresh profile has no completion marker");
        });
        Check("LEGACY_COPY_IS_BYTE_EXACT_AND_CATEGORIZED", () =>
        {
            var legacy = SeedLegacy(Path.Combine(root, "categorized-legacy"));
            var canonical = Path.Combine(root, "categorized-canonical");
            var before = HashTree(legacy);
            var paths = ProductStorage.Migrate(legacy, canonical);
            Require(!paths.UsingLegacyLayout, paths.MigrationError ?? "Migration unexpectedly fell back");
            Require(File.Exists(Path.Combine(paths.Data, "active-flight.json")), "Flight data was not routed to Data");
            Require(File.Exists(Path.Combine(paths.Data, "Alpha6OPS-flights.sqlite-wal")), "SQLite sidecar was not preserved with its database");
            Require(File.Exists(Path.Combine(paths.Settings, "preferences.json")), "Preferences were not routed to Settings");
            Require(File.Exists(Path.Combine(paths.Diagnostics, "TestLogs", "flight.jsonl")), "Journal was not routed to Diagnostics");
            Require(File.Exists(Path.Combine(paths.Diagnostics, "CrashReports", "crash.json")), "Crash report was not routed to Diagnostics");
            Require(File.Exists(Path.Combine(paths.Cache, "MapCache", "tile.bin")), "Map cache was not routed to Cache");
            Require(File.Exists(Path.Combine(paths.Cache, "SimBrief", "latest-ofp.json")), "SimBrief cache was not routed to Cache");
            Require(File.Exists(Path.Combine(paths.State, "program-status.json")), "Program state was not routed to State");
            Require(before.SequenceEqual(HashTree(legacy)), "Migration modified the legacy source");
            Require(File.ReadAllBytes(Path.Combine(paths.Data, "active-flight.json")).SequenceEqual(File.ReadAllBytes(Path.Combine(legacy, "active-flight.json"))), "Migrated bytes changed");
        });
        Check("MIGRATION_PRESERVES_SQLITE_DATABASE_AND_SIDECARS", () =>
        {
            var legacy = SeedLegacy(Path.Combine(root, "sqlite-legacy"));
            var paths = ProductStorage.Migrate(legacy, Path.Combine(root, "sqlite-canonical"));
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                var name = "Alpha6OPS-flights.sqlite" + suffix;
                Require(SHA256.HashData(File.ReadAllBytes(Path.Combine(legacy, name))).SequenceEqual(SHA256.HashData(File.ReadAllBytes(Path.Combine(paths.Data, name)))), "SQLite file hash changed: " + name);
            }
        });
        Check("SECOND_LAUNCH_IS_IDEMPOTENT", () =>
        {
            var legacy = SeedLegacy(Path.Combine(root, "repeat-legacy"));
            var canonical = Path.Combine(root, "repeat-canonical");
            var first = ProductStorage.Migrate(legacy, canonical);
            var marker = Path.Combine(first.State, "migration.json");
            var markerHash = SHA256.HashData(File.ReadAllBytes(marker));
            File.WriteAllText(Path.Combine(legacy, "late-file.txt"), "must remain legacy-only");
            var second = ProductStorage.Migrate(legacy, canonical);
            Require(second == first, "Second launch returned a different storage identity");
            Require(markerHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(marker))), "Second launch rewrote the completion marker");
            Require(!File.Exists(Path.Combine(second.Data, "late-file.txt")), "Completed migration recopied later legacy changes");
        });
        Check("CANONICAL_CONFLICT_FALLS_BACK_WITHOUT_OVERWRITE", () =>
        {
            var legacy = SeedLegacy(Path.Combine(root, "conflict-legacy"));
            var canonical = Path.Combine(root, "conflict-canonical");
            Directory.CreateDirectory(canonical); File.WriteAllText(Path.Combine(canonical, "owner.txt"), "foreign");
            var paths = ProductStorage.Migrate(legacy, canonical);
            Require(paths.UsingLegacyLayout && paths.Root == Path.GetFullPath(legacy), "Conflict did not retain the legacy profile");
            Require(File.ReadAllText(Path.Combine(canonical, "owner.txt")) == "foreign", "Conflict content was overwritten");
            Require(File.Exists(Path.Combine(legacy, "active-flight.json")), "Legacy profile was changed during conflict handling");
        });
        Check("INTERRUPTED_COPY_FALLS_BACK_AND_CLEANS_STAGING", () =>
        {
            var legacy = SeedLegacy(Path.Combine(root, "interrupt-legacy"));
            var canonical = Path.Combine(root, "interrupt-canonical");
            var before = HashTree(legacy); var copied = 0;
            var paths = ProductStorage.MigrateForTest(legacy, canonical, (source, destination) =>
            {
                if (copied++ == 1) throw new IOException("injected copy failure");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source, destination, true);
            });
            Require(paths.UsingLegacyLayout && paths.Root == Path.GetFullPath(legacy), "Interrupted copy did not fall back to legacy");
            Require(!Directory.Exists(canonical), "Interrupted copy published a canonical profile");
            Require(!Directory.EnumerateDirectories(Path.GetDirectoryName(canonical)!, Path.GetFileName(canonical) + ".migration-*").Any(), "Interrupted copy left staging data");
            Require(before.SequenceEqual(HashTree(legacy)), "Interrupted copy modified the legacy source");
        });
        Check("MIGRATION_MARKER_DESCRIBES_VERIFIED_SOURCE", () =>
        {
            var legacy = SeedLegacy(Path.Combine(root, "marker-legacy"));
            var paths = ProductStorage.Migrate(legacy, Path.Combine(root, "marker-canonical"));
            using var marker = JsonDocument.Parse(File.ReadAllText(Path.Combine(paths.State, "migration.json")));
            var json = marker.RootElement;
            Require(json.GetProperty("status").GetString() == "complete", "Marker is not complete");
            Require(json.GetProperty("sourceFiles").GetInt32() == HashTree(legacy).Length, "Marker source count is wrong");
            Require(Path.GetFullPath(json.GetProperty("legacyRoot").GetString()!) == Path.GetFullPath(legacy), "Marker legacy root is wrong");
        });
        Check("LEGACY_INSTANCE_IDENTITY_BLOCKS_SIERRA_4", () => Require(BlockedByIdentity(root, "legacy"), "Legacy Alpha6 process identity did not block Sierra 4"));
        Check("SIERRA_4_INSTANCE_IDENTITY_BLOCKS_SECOND_INSTANCE", () => Require(BlockedByIdentity(root, "sierra4"), "Sierra 4 process identity did not block a second instance"));
        File.WriteAllText(Path.Combine(root, "storage-summary.json"), JsonSerializer.Serialize(new { executed = Results.Count, passed = Results.Count - failed, failed, checks = Results }, new JsonSerializerOptions { WriteIndented = true }));
        return failed == 0 ? 0 : 1;
    }

    internal static int HoldIdentity(string[] args)
    {
        var directory = Path.GetFullPath(args[1]); var identity = args[2];
        Directory.CreateDirectory(directory);
        var name = identity == "legacy" ? "Local\\Alpha6Designs.Alpha6OPS.SingleInstance" : "Local\\Sierra4.SingleInstance";
        using var mutex = new Mutex(true, name, out var created);
        File.WriteAllText(Path.Combine(directory, "ready"), created ? "ready" : "unavailable");
        if (!created) return 2;
        if (!SpinWait.SpinUntil(() => File.Exists(Path.Combine(directory, "release")), 10000)) return 3;
        mutex.ReleaseMutex(); return 0;
    }

    private static bool BlockedByIdentity(string root, string identity)
    {
        var directory = Path.Combine(root, "identity-" + identity); Directory.CreateDirectory(directory);
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "--storage-instance-child", directory, identity }) info.ArgumentList.Add(argument);
        using var child = Process.Start(info) ?? throw new Exception("Could not start identity holder");
        Require(SpinWait.SpinUntil(() => File.Exists(Path.Combine(directory, "ready")) || child.HasExited, 5000), "Identity holder did not start");
        Require(!child.HasExited && File.ReadAllText(Path.Combine(directory, "ready")) == "ready", "Identity holder could not own the named mutex");
        var acquired = SingleInstance.TryAcquire(); SingleInstance.Release();
        File.WriteAllText(Path.Combine(directory, "release"), "release");
        Require(child.WaitForExit(5000) && child.ExitCode == 0, "Identity holder did not exit cleanly");
        return !acquired;
    }

    private static string SeedLegacy(string path)
    {
        Directory.CreateDirectory(path);
        void Put(string relative, string value) { var file = Path.Combine(path, relative); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, value); }
        Put("active-flight.json", "{\"flight\":\"SWA1\"}"); Put("active-flight-state.json", "state");
        Put("Alpha6OPS-flights.sqlite", "db"); Put("Alpha6OPS-flights.sqlite-wal", "wal"); Put("Alpha6OPS-flights.sqlite-shm", "shm");
        Put("preferences.json", "preferences"); Put("general-settings.json", "settings"); Put("ofp-viewer-state.json", "viewer");
        Put("program-status.json", "status"); Put("TestLogs/flight.jsonl", "journal"); Put("CrashReports/crash.json", "crash");
        Put("MapCache/tile.bin", "map"); Put("SimBrief/latest-ofp.json", "ofp"); Put("unknown.txt", "preserve");
        return path;
    }

    private static string[] HashTree(string root) => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/') + "|" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))
        .OrderBy(value => value, StringComparer.Ordinal).ToArray();
    private static void Require(bool valid, string reason) { if (!valid) throw new Exception(reason); }
    private static void Check(string name, Action action)
    {
        string? error = null; var watch = System.Diagnostics.Stopwatch.StartNew();
        try { action(); } catch (Exception exception) { failed++; error = exception.ToString(); }
        Results.Add(new { name, passed = error is null, milliseconds = watch.Elapsed.TotalMilliseconds, error });
        Console.WriteLine($"{(error is null ? "PASS" : "FAIL")} {name}{(error is null ? "" : ": " + error.Split('\n')[0])}");
    }
}
