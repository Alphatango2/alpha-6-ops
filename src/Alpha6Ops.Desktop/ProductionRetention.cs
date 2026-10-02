using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Alpha6Ops.Desktop;

internal sealed record RetentionLimits(
    TimeSpan CompletedAge, int CompletedMinimum,
    TimeSpan InterruptedAge, int InterruptedMinimum,
    TimeSpan RoutineAge, TimeSpan CrashAge, int CrashCount,
    TimeSpan MapAge, TimeSpan SimBriefAge, int SimBriefCount,
    long CompletedBytes, long InterruptedBytes, long RoutineBytes,
    long CrashBytes, long MapBytes, long SimBriefBytes)
{
    internal static RetentionLimits Production { get; } = new(
        TimeSpan.FromDays(14), 3, TimeSpan.FromDays(30), 3,
        TimeSpan.FromDays(14), TimeSpan.FromDays(90), 20,
        TimeSpan.FromDays(30), TimeSpan.FromDays(90), 20,
        512L * 1024 * 1024, 1024L * 1024 * 1024, 100L * 1024 * 1024,
        100L * 1024 * 1024, 500L * 1024 * 1024, 100L * 1024 * 1024);
    internal static RetentionLimits TestDefaults { get; } = Production with
    {
        CompletedBytes = 1024 * 1024,
        InterruptedBytes = 1024 * 1024,
        RoutineBytes = 1024 * 1024,
        CrashBytes = 1024 * 1024,
        MapBytes = 1024 * 1024,
        SimBriefBytes = 1024 * 1024
    };
}

internal sealed record RetentionReport(
    IReadOnlyList<string> Compressed,
    IReadOnlyList<string> Deleted,
    IReadOnlyList<string> Preserved,
    IReadOnlyList<string> Errors);

internal static class ProductionRetention
{
    internal static RetentionReport Apply(ProductStoragePaths paths, DateTimeOffset now, IReadOnlySet<string>? protectedPaths = null) =>
        ApplyForTest(paths, now, protectedPaths, RetentionLimits.Production);

    internal static RetentionReport ApplyForTest(ProductStoragePaths paths, DateTimeOffset now, IReadOnlySet<string>? protectedPaths, RetentionLimits limits)
    {
        var compressed = new List<string>(); var deleted = new List<string>(); var preserved = new List<string>(); var errors = new List<string>();
        var protectedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (protectedPaths is not null) foreach (var path in protectedPaths) protectedSet.Add(Path.GetFullPath(path));
        AddActiveOfpReferences(paths, protectedSet, errors);

        var logs = Path.Combine(paths.Diagnostics, "TestLogs");
        var crashes = Path.Combine(paths.Diagnostics, "CrashReports");
        var map = Path.Combine(paths.Cache, "MapCache");
        var simBrief = Path.Combine(paths.Cache, "SimBrief");
        foreach (var directory in new[] { logs, crashes, map, simBrief }) Directory.CreateDirectory(directory);

        var raw = Directory.EnumerateFiles(logs, "*.jsonl", SearchOption.TopDirectoryOnly).Select(path => new FileInfo(path)).ToArray();
        var interrupted = raw.Where(file => !Completed(file.FullName)).OrderByDescending(file => file.LastWriteTimeUtc).ToArray();
        var newestInterrupted = interrupted.FirstOrDefault()?.FullName;
        if (newestInterrupted is not null) protectedSet.Add(Path.GetFullPath(newestInterrupted));

        foreach (var file in raw.OrderBy(file => file.FullName, StringComparer.OrdinalIgnoreCase))
        {
            var full = Path.GetFullPath(file.FullName);
            if (protectedSet.Contains(full)) { Preserve(full, preserved); continue; }
            var completed = Completed(full);
            var destination = full + (completed ? ".completed.gz" : ".interrupted.gz");
            if (TryCompress(full, destination, errors))
            {
                compressed.Add(destination);
                if (completed)
                {
                    var export = Path.ChangeExtension(full, ".json");
                    if (File.Exists(export) && !protectedSet.Contains(Path.GetFullPath(export)))
                    {
                        var exportDestination = export + ".completed.gz";
                        if (TryCompress(export, exportDestination, errors)) compressed.Add(exportDestination);
                    }
                }
            }
        }

        Prune(Directory.EnumerateFiles(logs, "*.completed.gz"), now, limits.CompletedAge, limits.CompletedMinimum, limits.CompletedBytes, protectedSet, false, false, deleted, preserved, errors);
        var interruptedCompressed = Directory.EnumerateFiles(logs, "*.interrupted.gz").ToArray();
        var newestCompressed = interruptedCompressed.Select(path => new FileInfo(path)).OrderByDescending(file => file.LastWriteTimeUtc).FirstOrDefault()?.FullName;
        if (newestCompressed is not null) protectedSet.Add(Path.GetFullPath(newestCompressed));
        Prune(interruptedCompressed, now, limits.InterruptedAge, limits.InterruptedMinimum, limits.InterruptedBytes, protectedSet, false, true, deleted, preserved, errors);

        var routine = Directory.EnumerateFiles(paths.Diagnostics, "*", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
        Prune(routine, now, limits.RoutineAge, 0, limits.RoutineBytes, protectedSet, false, false, deleted, preserved, errors);
        Prune(Directory.EnumerateFiles(crashes, "*", SearchOption.TopDirectoryOnly), now, limits.CrashAge, limits.CrashCount, limits.CrashBytes, protectedSet, false, true, deleted, preserved, errors);
        Prune(Directory.EnumerateFiles(map, "*", SearchOption.AllDirectories), now, limits.MapAge, 0, limits.MapBytes, protectedSet, true, false, deleted, preserved, errors);
        Prune(Directory.EnumerateFiles(simBrief, "*", SearchOption.TopDirectoryOnly), now, limits.SimBriefAge, limits.SimBriefCount, limits.SimBriefBytes, protectedSet, false, true, deleted, preserved, errors);
        return new(compressed, deleted, preserved.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), errors);
    }

    private static bool Completed(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var length = Math.Min(stream.Length, 128 * 1024); stream.Position = stream.Length - length;
            var bytes = new byte[(int)length]; _ = stream.Read(bytes, 0, bytes.Length);
            return Regex.IsMatch(Encoding.UTF8.GetString(bytes), @"""kind""\s*:\s*""session_ended""", RegexOptions.CultureInvariant);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return false; }
    }

    private static bool TryCompress(string source, string destination, List<string> errors)
    {
        var temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            var sourceInfo = new FileInfo(source);
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true)) input.CopyTo(gzip);
                output.Flush(flushToDisk: true);
            }
            byte[] originalHash; byte[] packedHash;
            using (var original = File.OpenRead(source)) originalHash = SHA256.HashData(original);
            using (var packed = new GZipStream(File.OpenRead(temporary), CompressionMode.Decompress)) packedHash = SHA256.HashData(packed);
            if (!originalHash.SequenceEqual(packedHash)) throw new InvalidDataException("Compressed verification failed.");
            File.Move(temporary, destination, false);
            File.SetLastWriteTimeUtc(destination, sourceInfo.LastWriteTimeUtc);
            File.Delete(source);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            errors.Add(source + ": " + error.GetBaseException().Message);
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
            return false;
        }
    }

    private static void Prune(IEnumerable<string> paths, DateTimeOffset now, TimeSpan age, int keepNewestForAge, long maxBytes,
        HashSet<string> protectedSet, bool accessOrderForCap, bool preserveNewestForCap,
        List<string> deleted, List<string> preserved, List<string> errors)
    {
        var files = paths.Where(File.Exists).Select(path => new FileInfo(path)).OrderByDescending(file => file.LastWriteTimeUtc).ToList();
        var ageKeep = files.Take(keepNewestForAge).Select(file => file.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files.Where(file => file.LastWriteTimeUtc < now.UtcDateTime - age).OrderBy(file => file.LastWriteTimeUtc).ToArray())
        {
            if (ageKeep.Contains(file.FullName) || protectedSet.Contains(Path.GetFullPath(file.FullName))) { Preserve(file.FullName, preserved); continue; }
            Delete(file.FullName, deleted, errors);
        }
        files = files.Where(file => File.Exists(file.FullName)).ToList();
        var newest = preserveNewestForCap ? files.OrderByDescending(file => file.LastWriteTimeUtc).FirstOrDefault()?.FullName : null;
        long bytes = files.Sum(file => file.Length);
        foreach (var file in (accessOrderForCap ? files.OrderBy(file => file.LastAccessTimeUtc) : files.OrderBy(file => file.LastWriteTimeUtc)).ToArray())
        {
            if (bytes <= maxBytes) break;
            var full = Path.GetFullPath(file.FullName);
            if (protectedSet.Contains(full) || string.Equals(file.FullName, newest, StringComparison.OrdinalIgnoreCase)) { Preserve(file.FullName, preserved); continue; }
            if (Delete(file.FullName, deleted, errors)) bytes -= file.Length;
        }
        foreach (var file in files.Where(file => File.Exists(file.FullName) && protectedSet.Contains(Path.GetFullPath(file.FullName)))) Preserve(file.FullName, preserved);
    }

    private static bool Delete(string path, List<string> deleted, List<string> errors)
    {
        try { File.Delete(path); deleted.Add(path); return true; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { errors.Add(path + ": " + error.GetBaseException().Message); return false; }
    }
    private static void Preserve(string path, List<string> preserved) { if (!preserved.Contains(path, StringComparer.OrdinalIgnoreCase)) preserved.Add(path); }

    private static void AddActiveOfpReferences(ProductStoragePaths paths, HashSet<string> protectedSet, List<string> errors)
    {
        var assignment = Path.Combine(paths.Data, "active-flight.json");
        if (!File.Exists(assignment)) return;
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(assignment));
            if (!json.RootElement.TryGetProperty("OfpCacheKey", out var keyElement)) return;
            var key = keyElement.GetString(); if (string.IsNullOrWhiteSpace(key)) return;
            var directory = Path.Combine(paths.Cache, "SimBrief");
            if (!Directory.Exists(directory)) return;
            foreach (var path in Directory.EnumerateFiles(directory, "ofp-" + key + ".*")) protectedSet.Add(Path.GetFullPath(path));
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException) { errors.Add(assignment + ": " + error.GetBaseException().Message); }
    }
}
