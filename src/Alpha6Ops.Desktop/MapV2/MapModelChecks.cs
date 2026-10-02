using System;
using System.Collections.Immutable;
using System.Linq;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop.MapV2;

internal static class MapModelChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var now = new DateTimeOffset(2024, 6, 21, 12, 0, 0, TimeSpan.Zero);
        var projection = new MapProjectionService();
        var state = new FlightSessionSnapshot
        {
            Connected = true, DataCurrent = true, Aircraft = "Test aircraft", ConnectionGeneration = 1,
            Telemetry = new(now, false, 250, false, true, LatitudeDegrees: 40, LongitudeDegrees: -80, HeadingDegrees: 350)
        };
        MapSnapshot Project(FlightSessionSnapshot value, long sequence) => projection.Project(value, false, 1, sequence, now.AddSeconds(sequence));
        var first = Project(state, 1);
        check(first.Aircraft is [{ Source: MapEntitySource.Observed }] && first.Routes.IsEmpty, "Free flight renders observed position without inventing route.");
        check(first.Aircraft[0].AltitudeFeet is null && state.Telemetry.AltitudeFeet is double.NaN, "Nonfinite optional input becomes unavailable without mutating telemetry.");
        var second = Project(state with { Telemetry = state.Telemetry with { At = now.AddSeconds(1), LongitudeDegrees = -79.999 } }, 2);
        check(second.Routes.Single().Kind == MapRouteKind.ObservedTrack && second.Routes[0].Segments[0].Length == 2, "Consecutive observations create observed segment.");
        check(first.Routes.IsEmpty, "Retained snapshots own immutable trail copies.");
        check(Project(state with { SuppressObservedFlight = true }, 3).Routes.IsEmpty, "Closed session suppression clears retained observed track.");
        projection.Reset();
        Project(state, 1);
        Project(state with { Telemetry = state.Telemetry with { At = now.AddSeconds(1) } }, 2);
        var paused = Project(state with { Telemetry = state.Telemetry with { At = now.AddSeconds(2), Paused = true } }, 3);
        check(!paused.Aircraft[0].InterpolationAllowed && paused.Aircraft[0].Reliability == MapReliability.Stale, "Pause freezes interpolation.");
        var resumed = Project(state with { Telemetry = state.Telemetry with { At = now.AddSeconds(3) } }, 4);
        check(resumed.Routes[0].Segments.Length == 1 && resumed.Routes[0].Segments[0].Length == 2, "Resume does not join unobserved pause interval.");
        projection.Reset();
        check(Project(state, 5).Routes.IsEmpty, "Hidden/reset discards trail continuity.");
        Project(state with { Telemetry = state.Telemetry with { At = now.AddSeconds(1) } }, 6);
        check(Project(state with { ConnectionGeneration = 2 }, 7).Routes.IsEmpty, "Reconnect discards previous trail.");
        check(Project(state with { Aircraft = "Other" }, 8).Routes.IsEmpty, "Aircraft identity change discards previous trail.");
        check(Project(state with { Telemetry = null }, 9).Aircraft.IsEmpty, "Unknown position does not fabricate aircraft.");
        check(projection.Project(state, true, 2, 1, now).Aircraft.IsEmpty, "Replay preview cannot reclassify the live aircraft as simulated traffic.");
        foreach (var invalid in new[] { first with { Sequence = 0 }, first with { Aircraft = [first.Aircraft[0], first.Aircraft[0]] },
            first with { Aircraft = [first.Aircraft[0] with { HeadingDegrees = double.NaN }] },
            first with { CapturedAt = now.ToOffset(TimeSpan.FromHours(1)) },
            first with { Aircraft = [first.Aircraft[0] with { Id = "" }] },
            first with { Aircraft = Enumerable.Range(0, 5001).Select(i => first.Aircraft[0] with { Id = i.ToString() }).ToImmutableArray() } })
        {
            bool rejected = false;
            try { MapSnapshotValidator.Validate(invalid); } catch (ArgumentException) { rejected = true; }
            check(rejected, "Validator rejects malformed or over-budget snapshot.");
        }
        var largeRoute = new MapRoute("budget-route", null, null, null, null, MapRouteKind.Planned,
            MapEntitySource.Inferred, [Enumerable.Repeat(new GeoPoint(0, 0), MapSnapshotValidator.MaxGeometryPoints + 1).ToImmutableArray()],
            [], null, MapReliability.Uncertain);
        bool geometryRejected = false;
        try { MapSnapshotValidator.Validate(first with { Routes = [largeRoute] }); } catch (ArgumentException) { geometryRejected = true; }
        check(geometryRejected, "Geometry point budget is enforced.");
        var largePayload = first with
        {
            Aircraft = Enumerable.Range(0, 5000).Select(i => first.Aircraft[0] with
            {
                Id = "load-" + i, Callsign = new string('A', 512), Registration = new string('B', 512)
            }).ToImmutableArray()
        };
        bool payloadRejected = false;
        try { MapSnapshotValidator.SerializeValidated(largePayload); } catch (ArgumentException) { payloadRejected = true; }
        check(payloadRejected, "Actual UTF8 payload byte budget is enforced.");
        var dateline = MapGeography.GreatCircle(new(170, 60), new(-170, 60));
        check(dateline.Length == 2 && dateline.All(s => s.Zip(s.Skip(1)).All(pair => Math.Abs(pair.First.Longitude - pair.Second.Longitude) <= 180)), "Dateline great circle splits into geographic segments.");
        check(MapGeography.Interpolate(new(-60, 80), new(60, 80), .5).Latitude > 84, "High latitude path follows great circle.");
        var plan = new ActiveFlightPlan("TEST1", "N123", "AAA", "BBB", now, now.AddHours(2),
            RoutePoints: [new("AAA", 40, -80), new("BBB", 40, -70)]);
        var planned = Project(state with { Assignment = plan, CanPresentCandidate = true }, 10);
        check(planned.Routes.Any(r => r.Kind == MapRouteKind.Planned) && planned.Airports.Length == 2, "Candidate plan has geography and truthful endpoint roles.");
        var progress = Project(state with { Assignment = plan, CanRenderAssignment = true, RouteReliable = true, RouteProgress = .5 }, 11);
        check(progress.Routes.Any(r => r.Kind == MapRouteKind.CompletedPlan) && progress.Routes.Any(r => r.Kind == MapRouteKind.Planned), "Authoritative progress splits completed and planned geography.");
        var noProgress = Project(state with { Assignment = plan, CanRenderAssignment = true, RouteProgress = .5 }, 12);
        check(!noProgress.Routes.Any(r => r.Kind == MapRouteKind.CompletedPlan), "Unreliable progress cannot imply completion.");
        projection.Reset();
        var endpointsOnly=Project(state with{Assignment=plan with{Origin="YSSY",Destination="YMHB",RoutePoints=[]},CanPresentCandidate=true},13);
        check(endpointsOnly.Airports.Length==2&&endpointsOnly.Routes.IsEmpty,"Known airports without supplied route do not invent a direct flight path.");
        foreach (var interruption in new[]
        {
            state with { DataCurrent = false },
            state with { Telemetry = state.Telemetry with { Slewing = true } },
            state with { Reading = new LiveReading("Test aircraft", state.Telemetry, new FlightEvidence(SimulationRunning: false)) },
            state with { Telemetry = state.Telemetry with { At = now.AddSeconds(-1) } },
            state with { Telemetry = state.Telemetry with { At = now.AddMinutes(10) } },
            state with { Telemetry = state.Telemetry with { LongitudeDegrees = 80 } }
        })
        {
            projection.Reset();
            Project(state, 1);
            Project(state with { Telemetry = state.Telemetry with { At = now.AddSeconds(1) } }, 2);
            var gap = Project(interruption, 3);
            check(gap.Aircraft.IsEmpty || !gap.Aircraft[0].InterpolationAllowed, "Stale/slew/menu/time reversal/time jump/teleport freezes interpolation.");
            var after = Project(state with { Telemetry = state.Telemetry with { At = now.AddSeconds(4) } }, 4);
            check(after.Routes.All(r => r.Segments.All(s => s.Length <= 2)), "Discontinuity never bridges an unobserved gap.");
        }
        projection.Reset();
        for (int i = 0; i < MapProjectionService.MaxTrailPoints + 10; i++)
            Project(state with { Telemetry = state.Telemetry with { At = now.AddSeconds(i) } }, i + 1);
        var bounded = Project(state with { Telemetry = state.Telemetry with { At = now.AddSeconds(2100) } }, 2101);
        check(bounded.Routes.SelectMany(r => r.Segments).Sum(s => s.Length) <= MapProjectionService.MaxTrailPoints, "Visual trail remains bounded.");
        // Independent reference: astronomical equinox/solstice declinations (Earth obliquity),
        // not values generated by NOAA's fractional-year series used in production.
        foreach (var (date, expected) in new[] { ("2024-03-20T12:00:00Z", 0d), ("2024-06-21T12:00:00Z", 23.44),
            ("2024-09-22T12:00:00Z", 0d), ("2024-12-21T12:00:00Z", -23.44), ("2024-02-29T12:00:00Z", -7.8) })
        {
            var vector = MapSolar.SunVector(DateTimeOffset.Parse(date));
            check(Math.Abs(Math.Asin(vector[2]) * 180 / Math.PI - expected) < 1,
                "Independent seasonal/leap-year reference: " + date);
            check(Math.Abs(vector.Sum(v => v * v) - 1) < 1e-12, "Sun vector is normalized.");
        }
        var summer = MapSolar.SunVector(now);
        // Independent orbital approximation from USNO (not the production NOAA series).
        // Sun coordinates: https://aa.usno.navy.mil/faq/sun_approx
        // Greenwich sidereal time: https://aa.usno.navy.mil/faq/GAST
        // Fixed expected declination and RA-GMST longitude, degrees, UT1 approximated by UTC.
        foreach (var (date, latitude, longitude) in new[] {
            ("2024-03-20T12:00:00Z", .147536, 1.828772),
            ("2024-06-21T12:00:00Z", 23.434408, .480127),
            ("2024-09-22T12:00:00Z", .011265, -1.867981),
            ("2024-12-21T12:00:00Z", -23.435666, -.424586),
            ("2024-02-29T12:00:00Z", -7.669732, 3.102056),
            ("2024-06-21T00:00:00Z", 23.435723, -179.547054) })
        {
            var sun = MapSolar.SunVector(DateTimeOffset.Parse(date));
            double lat = Math.Asin(sun[2]) * 180 / Math.PI;
            double lon = Math.Atan2(sun[1], sun[0]) * 180 / Math.PI;
            double longitudeError = Math.Abs(Math.IEEERemainder(lon - longitude, 360));
            check(Math.Abs(lat - latitude) < 1 && longitudeError < 1,
                "Independent USNO subsolar latitude/longitude within one degree: " + date);
        }
        var winter = MapSolar.SunVector(now.AddMonths(6));
        check(summer[2] > 0 && winter[2] < 0, "North and south polar seasons reverse.");
        check(MapSolar.SunVector(now.AddHours(12))[0] < 0 && summer[0] > 0, "UTC midnight shifts illumination toward dateline.");
        check(MapSolar.SunVector(now.ToOffset(TimeSpan.FromHours(9))).SequenceEqual(summer), "Solar time uses UTC independent of caller offset.");
        check(MapSnapshotValidator.SerializeValidated(first).Length < MapSnapshotValidator.MaxPayloadBytes, "Valid snapshot serializes inside payload budget.");
        FlightTrackPresentationChecks.Run(check);
    }
}
