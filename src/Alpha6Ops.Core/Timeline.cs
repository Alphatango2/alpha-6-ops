namespace Alpha6Ops.Core;

public sealed record TimelineSnapshot(int Index, Telemetry Sample, FlightPhase Phase, int EventsFiredCount);
public sealed record FlightTimeline(IReadOnlyList<TimelineSnapshot> Snapshots, IReadOnlyList<FlightEvent> Events, FlightPhase FinalPhase);

// Replays a telemetry source through a fresh, single-pass PhaseDetector and captures one
// immutable snapshot per sample. The detector itself stays forward-only and unmodified;
// seeking is done by indexing this precomputed array, never by re-running the detector.
public static class TimelineBuilder
{
    public static async Task<FlightTimeline> BuildAsync(ISimulatorTelemetry source, CancellationToken cancellationToken = default)
    {
        var detector = new PhaseDetector();
        var snapshots = new List<TimelineSnapshot>();
        var events = new List<FlightEvent>();
        await foreach (var sample in source.ReadAsync(cancellationToken))
        {
            if (detector.Observe(sample) is { } flightEvent) events.Add(flightEvent);
            snapshots.Add(new TimelineSnapshot(snapshots.Count, sample, detector.Phase, events.Count));
        }
        return new FlightTimeline(snapshots, events, detector.Phase);
    }
}
