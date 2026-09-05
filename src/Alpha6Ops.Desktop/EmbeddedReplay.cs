using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

// Reads a JSONL flight fixture embedded in this assembly, mirroring Core's file-based JsonReplay
// so the same TimelineBuilder/DebriefSummary/FlightSession work unchanged against either source.
public sealed class EmbeddedReplay(string resourceName) : ISimulatorTelemetry
{
    internal static readonly IReadOnlyList<string> Fixtures = ["delayed-flight.jsonl", "on-time-flight.jsonl"];

    public async IAsyncEnumerable<Telemetry> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException($"The bundled replay '{resourceName}' is missing.");
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            yield return JsonSerializer.Deserialize<Telemetry>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new InvalidDataException("Empty telemetry sample.");
        }
    }
}
