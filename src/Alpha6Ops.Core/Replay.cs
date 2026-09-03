using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Alpha6Ops.Core;

public sealed class JsonReplay(string path) : ISimulatorTelemetry
{
    public async IAsyncEnumerable<Telemetry> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = File.OpenText(path);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            yield return JsonSerializer.Deserialize<Telemetry>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new InvalidDataException("Empty telemetry sample.");
        }
    }
}

public sealed class FlightSession(AircraftRotation rotation)
{
    private readonly PhaseDetector detector = new();
    public AircraftRotation Rotation { get; private set; } = rotation;
    public FlightPhase Phase => detector.Phase;
    public FlightEvent? Observe(Telemetry sample)
    {
        var flightEvent = detector.Observe(sample);
        if (flightEvent is null) return null;
        var legs = Rotation.Legs.ToArray();
        legs[0] = flightEvent.Phase switch
        {
            FlightPhase.TaxiOut => legs[0] with { ActualOut = flightEvent.At },
            FlightPhase.Complete => legs[0] with { ActualIn = flightEvent.At },
            _ => legs[0]
        };
        Rotation = Rotation with { Legs = legs };
        return flightEvent;
    }
}
