namespace Alpha6Ops.Core;

public sealed record TimelineSnapshot(int Index, Telemetry Sample, FlightPhase Phase, int EventsFiredCount);
public sealed record FlightTimeline(IReadOnlyList<TimelineSnapshot> Snapshots, IReadOnlyList<FlightEvent> Events, FlightPhase FinalPhase);

// Wraps one PhaseDetector and accumulates one immutable snapshot per observed sample. This is
// the single place that turns detector output into a scrubbable/debriefable timeline, whether
// samples arrive all at once (a replayed fixture) or one at a time as they're observed live —
// either way the detector is driven forward exactly once per sample, never re-run.
public sealed class TimelineRecorder(PhaseDetector detector)
{
    private readonly List<TimelineSnapshot> snapshots = [];
    private readonly List<FlightEvent> events = [];
    public FlightPhase Phase => detector.Phase;
    public IReadOnlyList<TimelineSnapshot> Snapshots => snapshots;
    public IReadOnlyList<FlightEvent> Events => events;

    public FlightEvent? Observe(Telemetry sample)
    {
        var flightEvent = detector.Observe(sample);
        if (flightEvent is not null) events.Add(flightEvent);
        snapshots.Add(new TimelineSnapshot(snapshots.Count, sample, detector.Phase, events.Count));
        return flightEvent;
    }

    public FlightTimeline ToTimeline() => new(snapshots, events, detector.Phase);
}

public static class TimelineBuilder
{
    public static async Task<FlightTimeline> BuildAsync(ISimulatorTelemetry source, CancellationToken cancellationToken = default)
    {
        var recorder = new TimelineRecorder(new PhaseDetector());
        await foreach (var sample in source.ReadAsync(cancellationToken))
            recorder.Observe(sample);
        return recorder.ToTimeline();
    }
}
