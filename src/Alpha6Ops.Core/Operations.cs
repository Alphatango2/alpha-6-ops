namespace Alpha6Ops.Core;

public enum FlightPhase { AtGate, TaxiOut, Airborne, TaxiIn, Complete }
public enum TenantRole { Pilot, Dispatcher, Administrator }
public record Telemetry(DateTimeOffset At, bool OnGround, double GroundSpeedKnots,
    bool ParkingBrake, bool EnginesRunning, bool Paused = false, bool Slewing = false);
public record FlightEvent(FlightPhase Phase, DateTimeOffset At);

// A flight-scoped state machine. Timestamps are simulator UTC, never wall-clock time.
public sealed class PhaseDetector
{
    public FlightPhase Phase { get; private set; } = FlightPhase.AtGate;
    private DateTimeOffset? last;
    private FlightPhase? candidate;
    private DateTimeOffset candidateSince;
    public FlightEvent? Observe(Telemetry sample)
    {
        if (!double.IsFinite(sample.GroundSpeedKnots) || sample.GroundSpeedKnots < 0)
            throw new ArgumentException("Ground speed must be finite and nonnegative.");
        if (last is not null && sample.At <= last) return null; // replay duplicates/out-of-order
        var gap = last is not null && sample.At - last > TimeSpan.FromSeconds(15);
        last = sample.At;
        if (sample.Paused || sample.Slewing || gap) { candidate = null; return null; }
        FlightPhase? next = Phase switch
        {
            FlightPhase.AtGate when sample.OnGround && !sample.ParkingBrake && sample.GroundSpeedKnots >= 1 => FlightPhase.TaxiOut,
            FlightPhase.TaxiOut when !sample.OnGround => FlightPhase.Airborne,
            FlightPhase.Airborne when sample.OnGround => FlightPhase.TaxiIn,
            FlightPhase.TaxiIn when !sample.OnGround => FlightPhase.Airborne, // bounce/go-around
            FlightPhase.TaxiIn when sample.OnGround && sample.GroundSpeedKnots < 0.5 && sample.ParkingBrake && !sample.EnginesRunning => FlightPhase.Complete,
            _ => null
        };
        if (next is null) { candidate = null; return null; }
        if (next != candidate) { candidate = next; candidateSince = sample.At; return null; }
        if (sample.At - candidateSince < TimeSpan.FromSeconds(3)) return null;
        Phase = next.Value;
        candidate = null;
        return new FlightEvent(Phase, candidateSince);
    }
}

public record FlightLeg(string Id, string Origin, string Destination,
    DateTimeOffset ScheduledOut, DateTimeOffset ScheduledIn,
    DateTimeOffset? ActualOut = null, DateTimeOffset? ActualIn = null);
public record LegProjection(string Id, string Origin, string Destination,
    DateTimeOffset ScheduledOut, DateTimeOffset ScheduledIn,
    DateTimeOffset EstimatedOut, DateTimeOffset EstimatedIn,
    double DepartureDelayMinutes, double ArrivalDelayMinutes, bool Completed);
public record AircraftRotation(string TenantId, string AircraftId, int MinimumTurnMinutes, IReadOnlyList<FlightLeg> Legs);

public static class RotationPlanner
{
    // The single place a confirmed milestone is allowed to touch a rotation's actuals, so a live
    // SimConnect flight and a replayed one update the same way and Project never diverges between them.
    public static AircraftRotation ApplyMilestone(AircraftRotation rotation, FlightEvent milestone)
    {
        var legs = rotation.Legs.ToArray();
        legs[0] = milestone.Phase switch
        {
            FlightPhase.TaxiOut => legs[0] with { ActualOut = milestone.At },
            FlightPhase.Complete => legs[0] with { ActualIn = milestone.At },
            _ => legs[0]
        };
        return rotation with { Legs = legs };
    }

    public static IReadOnlyList<LegProjection> Project(AircraftRotation rotation)
    {
        if (rotation.MinimumTurnMinutes < 0 || string.IsNullOrWhiteSpace(rotation.TenantId))
            throw new ArgumentException("A tenant and nonnegative turnaround are required.");
        var result = new List<LegProjection>();
        DateTimeOffset? available = null;
        FlightLeg? previous = null;
        var ids = new HashSet<string>();
        foreach (var leg in rotation.Legs)
        {
            if (!ids.Add(leg.Id) || leg.ScheduledIn <= leg.ScheduledOut ||
                (previous is not null && (previous.Destination != leg.Origin || previous.ScheduledOut >= leg.ScheduledOut)) ||
                (leg.ActualIn is not null && (leg.ActualOut is null || leg.ActualIn < leg.ActualOut)) ||
                (leg.ActualOut is not null && available is not null && leg.ActualOut < available))
                throw new ArgumentException("Invalid or physically inconsistent aircraft rotation.");
            var departure = leg.ActualOut ?? (available > leg.ScheduledOut ? available.Value : leg.ScheduledOut);
            var arrival = leg.ActualIn ?? departure + (leg.ScheduledIn - leg.ScheduledOut);
            result.Add(new(leg.Id, leg.Origin, leg.Destination, leg.ScheduledOut, leg.ScheduledIn,
                departure, arrival, (departure - leg.ScheduledOut).TotalMinutes,
                (arrival - leg.ScheduledIn).TotalMinutes, leg.ActualIn is not null));
            available = arrival.AddMinutes(rotation.MinimumTurnMinutes);
            previous = leg;
        }
        return result;
    }
}

public static class Demo
{
    public static AircraftRotation Rotation() => new("alpha6", "N600A6", 35, [
        new("A601", "KORD", "KDTW", DateTimeOffset.Parse("2026-09-02T10:00:00Z"), DateTimeOffset.Parse("2026-09-02T11:15:00Z")),
        new("A602", "KDTW", "KJFK", DateTimeOffset.Parse("2026-09-02T11:50:00Z"), DateTimeOffset.Parse("2026-09-02T13:30:00Z")),
        new("A603", "KJFK", "KORD", DateTimeOffset.Parse("2026-09-02T14:30:00Z"), DateTimeOffset.Parse("2026-09-02T17:00:00Z"))]);
}
