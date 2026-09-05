namespace Alpha6Ops.Core;

public sealed record PhaseSegment(FlightPhase Phase, DateTimeOffset StartedAt, DateTimeOffset EndedAt);

// Derives named, timed phase segments from a confirmed sequence of flight events. Each segment
// covers the interval between one confirmed phase transition and the next, so it needs nothing
// beyond the events the phase detector already emits — no re-run, no snapshot access.
public static class DebriefSummary
{
    public static IReadOnlyList<PhaseSegment> Segments(IReadOnlyList<FlightEvent> events)
    {
        var segments = new List<PhaseSegment>();
        for (var i = 0; i < events.Count - 1; i++)
            segments.Add(new PhaseSegment(events[i].Phase, events[i].At, events[i + 1].At));
        return segments;
    }
}
