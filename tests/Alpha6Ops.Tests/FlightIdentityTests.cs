using Alpha6Ops.Core;

internal static class FlightIdentityTests
{
    internal static void Run(Action<bool, string> check)
    {
        var at = DateTimeOffset.Parse("2026-09-08T22:00:00Z");
        var sydney = new AirportReference("YSSY", "Sydney", "Sydney", new(-33.9461, 151.1772), "test reference");
        var lax = new AirportReference("KLAX", "Los Angeles", "Los Angeles", new(33.9425, -118.4081), "test reference");
        var flight = new FlightAssignment("JBU124", "G-FBIG", "KLAX", "KJFK", at.AddDays(-5), at.AddDays(-5).AddHours(5));
        var observed = new FlightEvidence(sydney.Position, "VH-TEST", SimulationRunning: true, Nearby: new(sydney, 0), PlanStatus: "No simulator flight plan");
        var mismatch = FlightIdentity.Reconcile(flight, observed, at, true, lax);
        check(!mismatch.Accepted && mismatch.Mode == "FREE FLIGHT" && mismatch.Issues.Count == 3, "Sydney free flight rejects stale JBU assignment, registration and departure airport independently");
        check(!FlightIdentity.Reconcile(flight with { Departure = at, Arrival = at.AddHours(5) }, observed, at, true, lax).Accepted, "current report with wrong airport is not attached");
        check(!FlightIdentity.Reconcile(flight with { Registration = "VH-TEST" }, observed, at, true, lax).Accepted, "matching registration cannot rescue an unrelated route/date");
        check(!FlightIdentity.Reconcile(flight, observed with { Route = new("YSSY", "YSBK") }, at, true, lax).Accepted, "simulator route conflict rejects briefing");
        var local = new FlightAssignment("LOCAL1", "VH TEST", "YSSY", "YSBK", at, at.AddHours(1));
        check(FlightIdentity.Reconcile(local, observed, at, true, sydney).Accepted, "live position and normalized registration verify a local assignment without a simulator plan");
        check(!FlightIdentity.Reconcile(local, observed with { Registration = null }, at, true, sydney).Accepted, "missing registration does not borrow identity from assignment");
        check(!FlightIdentity.Reconcile(local, observed with { Position = null }, at, true, sydney).Accepted, "missing position leaves new assignment unverified");
        check(!FlightIdentity.Reconcile(local, observed with { SimulationRunning = null }, at, true, sydney).Accepted, "unknown simulation state does not arm assignment");
        check(!FlightIdentity.Reconcile(local, observed with { SimulationRunning = false }, at, true, sydney).Accepted, "menu does not arm assignment");
        check(!FlightIdentity.Reconcile(local, observed, at, false, sydney).Accepted, "joining airborne does not infer departure from nearby airport");
        check(FlightIdentity.Reconcile(local, observed with { Route = new("YSSY", "YSBK") }, at, false, sydney).Accepted, "published matching simulator route can identify an airborne assignment");
        check(FlightIdentity.Reconcile(local, observed with { Position = lax.Position }, at, false, sydney, true).Accepted, "accepted airborne session is not compared with departure location every second");
        check(!FlightIdentity.Reconcile(local, observed with { Route = new("YSSY", "YMML") }, at, false, sydney, true).Accepted, "route change revokes an existing assignment association");
        check(FlightIdentity.Reconcile(local, observed with { Position = null }, at, true, sydney, true).Accepted, "temporary context outage retains established identity for reconnect without reusing it as fresh evidence");
        check(!FlightIdentity.Reconcile(local with { Departure = at.AddDays(2), Arrival = at.AddDays(2).AddHours(1) }, observed, at, true, sydney).Accepted, "future assignment outside window is not attached");
        check(!FlightIdentity.Reconcile(local with { Arrival = at.AddMinutes(-1) }, observed, at, true, sydney).Accepted, "invalid schedule is rejected before rotation projection");
        check(!FlightIdentity.Reconcile(local with { Origin = "?" }, observed, at, true, sydney).Accepted, "malformed saved airport is rejected");
        check(FlightIdentity.Reconcile(local with { Destination = "YSSY" }, observed, at, true, sydney).Accepted, "round-trip assignment is supported");
        check(FlightIdentity.Reconcile(null, observed, at, true, sydney).Mode == "FREE FLIGHT", "no briefing or simulator route is a valid free flight");
        check(FlightIdentity.Reconcile(null, observed with { Route = new("YSSY", "YSBK") }, at, true, sydney).Mode == "SIMULATOR FLIGHT PLAN", "simulator route works independently of SimBrief");
        check(sydney.Position.DistanceNm(lax.Position) > 6000, "cross-hemisphere distance detects Sydney versus Los Angeles");
        check(new GeoPosition(0, 179.9).DistanceNm(new(0, -179.9)) < 13, "airport distances cross the international date line correctly");
        check(!new GeoPosition(double.NaN, 0).IsValid && !new GeoPosition(0, 181).IsValid, "invalid coordinates are not usable evidence");
        check(FlightIdentity.PositionJump(sydney.Position, lax.Position, TimeSpan.FromSeconds(1)), "teleport starts a new observed session");
        check(!FlightIdentity.PositionJump(sydney.Position, new(-33.945,151.1772), TimeSpan.FromSeconds(1)), "ordinary movement does not reset session");
        check(!FlightIdentity.PositionJump(null, sydney.Position, TimeSpan.FromSeconds(1)), "first position is not a teleport");
        var airborne = new PhaseDetector(initialPhase: FlightPhase.Airborne);
        check(airborne.Observe(new(at, false, 120, false, true)) is null, "midair connection does not invent takeoff or block-out");
        airborne.Observe(new(at.AddSeconds(1), true, 60, false, true));
        check(airborne.Observe(new(at.AddSeconds(4), true, 50, false, true))?.Phase == FlightPhase.TaxiIn, "midair observation can detect a later landing");
    }
}
