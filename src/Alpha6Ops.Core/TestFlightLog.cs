using System.Text.Json;

namespace Alpha6Ops.Core;

// Append-and-flush diagnostic journal. Simulator time and real receipt time stay separate.
public sealed class TestFlightLog : IDisposable
{
    private readonly StreamWriter writer;
    private long sequence;
    private bool ended;
    public string JournalPath { get; }
    public string ExportPath => Path.ChangeExtension(JournalPath, ".json");
    public TestFlightLog(string directory, string appVersion, string mode)
    {
        Directory.CreateDirectory(directory);
        JournalPath = Path.Combine(directory, $"Alpha6OPS-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.jsonl");
        writer = new StreamWriter(new FileStream(JournalPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        try { Record("session_started", null, new { appVersion, mode, schemaVersion = 1,
            timing = "simulatorUtc is the in-sim clock; recordedAtUtc is the computer UTC clock" }); }
        catch { writer.Dispose(); throw; }
    }
    public void Record(string kind, DateTimeOffset? simulatorUtc, object detail)
    {
        if (ended) return;
        writer.WriteLine(JsonSerializer.Serialize(new { sequence = ++sequence, kind, recordedAtUtc = DateTimeOffset.UtcNow, simulatorUtc, detail }));
    }
    public void SaveExport() => Export(JournalPath, ExportPath);
    public void End(string reason)
    {
        if (ended) return;
        try { Record("session_ended", null, new { reason }); }
        finally { ended = true; writer.Dispose(); }
        SaveExport();
    }
    public void Dispose() { if (!ended) { ended = true; writer.Dispose(); } }

    public static void Export(string journal, string destination)
    {
        if (Path.GetFullPath(journal).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            throw new IOException("Choose a different filename for the JSON export.");
        var events = new List<JsonElement>();
        bool incompleteLastLine = false;
        using (var input = new StreamReader(new FileStream(journal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
        {
            while (input.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try { using var json = JsonDocument.Parse(line); events.Add(json.RootElement.Clone()); }
                catch (JsonException) when (input.EndOfStream) { incompleteLastLine = true; }
            }
        }
        var temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, JsonSerializer.Serialize(new {
            schemaVersion = 1, exportedAtUtc = DateTimeOffset.UtcNow, incompleteLastLine,
            sessionEnded = events.Any(e => e.GetProperty("kind").GetString() == "session_ended"),
            eventCount = events.Count, events
        }, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, destination, overwrite: true);
    }
}
