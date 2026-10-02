using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;

namespace Alpha6Ops.Desktop;

internal sealed record ProductStoragePaths(
    string Root,
    string Data,
    string Settings,
    string Diagnostics,
    string Cache,
    string State,
    bool UsingLegacyLayout,
    string? MigrationError = null);

internal static class ProductStorage
{
    private const string MarkerName = "migration.json";
    private static readonly object Sync = new();
    private static ProductStoragePaths? current;

    internal static ProductStoragePaths Current => current ?? Initialize();

    internal static ProductStoragePaths Initialize(string? localApplicationData = null)
    {
        lock (Sync)
        {
            if (current is not null && localApplicationData is null) return current;
            var local = Path.GetFullPath(localApplicationData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            var result = Migrate(Path.Combine(local, "Alpha6Designs", "Alpha6OPS"), Path.Combine(local, "Sierra 4"));
            if (localApplicationData is null) current = result;
            return result;
        }
    }

    internal static ProductStoragePaths Migrate(string legacyRoot, string canonicalRoot) =>
        MigrateForTest(legacyRoot, canonicalRoot, static (source, destination) => File.Copy(source, destination, false));

    internal static ProductStoragePaths MigrateForTest(string legacyRoot, string canonicalRoot, Action<string, string> copyFile)
    {
        legacyRoot = Path.GetFullPath(legacyRoot);
        canonicalRoot = Path.GetFullPath(canonicalRoot);
        if (SamePath(legacyRoot, canonicalRoot)) throw new ArgumentException("Legacy and canonical storage roots must be different.");

        if (CompleteCanonical(canonicalRoot))
        {
            var existing = Categorized(canonicalRoot);
            EnsureCategories(existing);
            return existing;
        }

        if (Directory.Exists(canonicalRoot) || File.Exists(canonicalRoot))
            return Flat(Directory.Exists(legacyRoot) ? legacyRoot : canonicalRoot, "Canonical profile exists without a valid completed migration marker.");

        var parent = Path.GetDirectoryName(canonicalRoot) ?? throw new InvalidOperationException("Canonical profile has no parent directory.");
        Directory.CreateDirectory(parent);
        var staging = canonicalRoot + ".migration-" + Guid.NewGuid().ToString("N");
        try
        {
            var staged = Categorized(staging);
            EnsureCategories(staged);
            var sourceFiles = Directory.Exists(legacyRoot)
                ? Directory.EnumerateFiles(legacyRoot, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()
                : [];
            var copies = new List<(string Source, string Destination, long Bytes)>(sourceFiles.Length);
            foreach (var source in sourceFiles)
            {
                var relative = Path.GetRelativePath(legacyRoot, source);
                var destination = MapLegacyFile(staged, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                copyFile(source, destination);
                copies.Add((source, destination, new FileInfo(source).Length));
            }
            foreach (var copy in copies)
            {
                if (!File.Exists(copy.Destination) || new FileInfo(copy.Destination).Length != copy.Bytes ||
                    !SHA256.HashData(File.ReadAllBytes(copy.Source)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(copy.Destination))))
                    throw new InvalidDataException("Migration verification failed for " + Path.GetRelativePath(legacyRoot, copy.Source));
            }
            var marker = new
            {
                schema = "sierra4.storage-migration.v1",
                status = "complete",
                completedAtUtc = DateTimeOffset.UtcNow,
                legacyRoot,
                canonicalRoot,
                sourceFiles = copies.Count,
                sourceBytes = copies.Sum(copy => copy.Bytes)
            };
            File.WriteAllText(Path.Combine(staged.State, MarkerName), JsonSerializer.Serialize(marker, new JsonSerializerOptions { WriteIndented = true }));
            Promote(staging, canonicalRoot);
            return Categorized(canonicalRoot);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            TryDelete(staging);
            return Flat(Directory.Exists(legacyRoot) ? legacyRoot : canonicalRoot, error.GetBaseException().Message);
        }
    }

    private static ProductStoragePaths Categorized(string root) => new(
        Path.GetFullPath(root),
        Path.Combine(Path.GetFullPath(root), "Data"),
        Path.Combine(Path.GetFullPath(root), "Settings"),
        Path.Combine(Path.GetFullPath(root), "Diagnostics"),
        Path.Combine(Path.GetFullPath(root), "Cache"),
        Path.Combine(Path.GetFullPath(root), "State"),
        false);

    private static ProductStoragePaths Flat(string root, string error)
    {
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root)) Directory.CreateDirectory(root);
        return new(root, root, root, root, root, root, true, error);
    }

    private static void EnsureCategories(ProductStoragePaths paths)
    {
        Directory.CreateDirectory(paths.Root);
        foreach (var path in new[] { paths.Data, paths.Settings, paths.Diagnostics, paths.Cache, paths.State }) Directory.CreateDirectory(path);
    }

    private static bool CompleteCanonical(string canonicalRoot)
    {
        var marker = Path.Combine(canonicalRoot, "State", MarkerName);
        if (!File.Exists(marker)) return false;
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(marker));
            return json.RootElement.TryGetProperty("status", out var status) && status.GetString() == "complete";
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException) { return false; }
    }

    private static string MapLegacyFile(ProductStoragePaths paths, string relative)
    {
        var normalized = relative.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var parts = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var first = parts.FirstOrDefault() ?? "legacy-file";
        if (first.Equals("TestLogs", StringComparison.OrdinalIgnoreCase) || first.Equals("CrashReports", StringComparison.OrdinalIgnoreCase) || first.Equals("MapDiagnostics", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(paths.Diagnostics, normalized);
        if (first.Equals("MapCache", StringComparison.OrdinalIgnoreCase) || first.Equals("SimBrief", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(paths.Cache, normalized);
        if (parts.Length == 1 && first is "preferences.json" or "general-settings.json" or "ofp-viewer-state.json")
            return Path.Combine(paths.Settings, first);
        if (parts.Length == 1 && first.Equals("program-status.json", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(paths.State, first);
        if (parts.Length == 1 && (first.StartsWith("Alpha6OPS-logs.sqlite", StringComparison.OrdinalIgnoreCase) || first.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) || first.StartsWith("Alpha6OPS-crash-", StringComparison.OrdinalIgnoreCase)))
            return Path.Combine(paths.Diagnostics, first);
        return Path.Combine(paths.Data, normalized);
    }

    private static bool SamePath(string left, string right) => string.Equals(left.TrimEnd(Path.DirectorySeparatorChar), right.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    private static void Promote(string staging, string canonical)
    {
        Exception? last = null;
        foreach (var delay in new[] { 0, 10, 20, 40, 80 })
        {
            if (delay > 0) Thread.Sleep(delay);
            try { Directory.Move(staging, canonical); return; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (Directory.Exists(canonical) || File.Exists(canonical)) throw;
                last = error;
            }
        }
        throw new IOException("Could not atomically promote the verified Sierra 4 profile.", last);
    }
    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
}
