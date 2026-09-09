namespace Alpha6Ops.Core;

public record GeoPosition(double Latitude, double Longitude)
{
    public bool IsValid => double.IsFinite(Latitude) && double.IsFinite(Longitude) && Math.Abs(Latitude) <= 90 && Math.Abs(Longitude) <= 180;
    public double DistanceNm(GeoPosition other)
    {
        if (!IsValid || !other.IsValid) return double.PositiveInfinity;
        const double radians = Math.PI / 180;
        var a = Math.Pow(Math.Sin((other.Latitude - Latitude) * radians / 2), 2)
            + Math.Cos(Latitude * radians) * Math.Cos(other.Latitude * radians)
            * Math.Pow(Math.Sin((other.Longitude - Longitude) * radians / 2), 2);
        return 3440.065 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }
}

public record AirportReference(string Ident, string Name, string City, GeoPosition Position, string Source);
public record NearbyAirport(AirportReference Airport, double DistanceNm);
public record SimulatorRoute(string Origin, string Destination);
public record FlightEvidence(GeoPosition? Position = null, string? Registration = null,
    SimulatorRoute? Route = null, bool? SimulationRunning = null, NearbyAirport? Nearby = null,
    string PlanStatus = "Simulator plan unavailable", string? Warning = null);
public record FlightAssignment(string FlightNumber, string Registration, string Origin, string Destination,
    DateTimeOffset Departure, DateTimeOffset Arrival);
public record FlightAssociation(bool Accepted, string Mode, IReadOnlyList<string> Issues)
{
    public string Explanation => string.Join(" ", Issues);
}

// A briefing is a proposed assignment. Only independent live evidence can attach it to a session.
// Absence of evidence is deliberately different from a confirmed mismatch.
public static class FlightIdentity
{
    public static string Normalize(string? value) => new((value ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    public static bool IsAirportId(string? value) => value is { Length: >= 3 and <= 8 } && value.All(char.IsAsciiLetterOrDigit);

    public static FlightAssociation Reconcile(FlightAssignment? plan, FlightEvidence evidence,
        DateTimeOffset sessionStart, bool onGroundAtStart, AirportReference? plannedOrigin, bool alreadyAccepted = false)
    {
        var issues = new List<string>();
        var fallback = evidence.Route is null ? "FREE FLIGHT" : "SIMULATOR FLIGHT PLAN";
        if (plan is null) return new(false, fallback, []);
        if (!IsAirportId(plan.Origin) || !IsAirportId(plan.Destination) || plan.Arrival <= plan.Departure || string.IsNullOrWhiteSpace(plan.FlightNumber))
            return new(false, fallback, ["Saved assignment is invalid; review its airports and UTC times."]);
        if (Math.Abs((sessionStart - plan.Departure).TotalHours) > 18)
            issues.Add("Saved assignment is outside this session's 18-hour departure window.");
        var expectedReg = Normalize(plan.Registration);
        var actualReg = Normalize(evidence.Registration);
        if (expectedReg.Length > 0 && actualReg.Length > 0 && expectedReg != actualReg)
            issues.Add($"Aircraft registration differs: simulator {evidence.Registration}, assignment {plan.Registration}.");
        var routeMatches = evidence.Route is { } route &&
            Normalize(route.Origin) == Normalize(plan.Origin) && Normalize(route.Destination) == Normalize(plan.Destination);
        if (evidence.Route is { } simulatorRoute && !routeMatches)
            issues.Add($"Simulator route {simulatorRoute.Origin}–{simulatorRoute.Destination} differs from assignment {plan.Origin}–{plan.Destination}.");
        var originMatches = onGroundAtStart && evidence.Position is { IsValid: true } position && plannedOrigin is not null && position.DistanceNm(plannedOrigin.Position) <= 5;
        if (!alreadyAccepted && onGroundAtStart && plannedOrigin is not null && evidence.Position is { IsValid: true } point && !originMatches)
            issues.Add($"Aircraft is {point.DistanceNm(plannedOrigin.Position):0} nm from assigned departure {plan.Origin}.");
        if (issues.Count > 0) return new(false, fallback, issues);
        if (alreadyAccepted && (evidence.SimulationRunning != true || evidence.Position is not { IsValid: true }))
            return new(true, "VERIFIED ASSIGNMENT", ["Live context temporarily unavailable; schedule display is suspended."]);
        if (evidence.SimulationRunning != true || evidence.Position is not { IsValid: true })
            return new(false, fallback, ["Assignment unverified: waiting for active simulation and a valid live position."]);
        if (!alreadyAccepted && (!routeMatches && !originMatches || expectedReg.Length > 0 && actualReg.Length == 0))
            return new(false, fallback, ["Assignment unverified: waiting for matching route/location and aircraft identification."]);
        return new(true, "VERIFIED ASSIGNMENT", []);
    }

    public static bool PositionJump(GeoPosition? previous, GeoPosition? current, TimeSpan elapsed) =>
        previous is { IsValid: true } && current is { IsValid: true } &&
        previous.DistanceNm(current) > 2 + Math.Max(0, elapsed.TotalHours) * 1500;
}
