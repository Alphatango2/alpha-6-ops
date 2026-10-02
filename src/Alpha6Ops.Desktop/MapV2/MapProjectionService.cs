using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop.MapV2;

// Single-worker owned. Only stores a bounded visual trail; never changes operational state.
internal sealed class MapProjectionService
{
    internal const int MaxTrailPoints = 2048;
    private readonly List<List<GeoPoint>> trail = [];
    private DateTimeOffset? previousAt;
    private DateTimeOffset? previousCapture;
    private GeoPoint? previousPosition;
    private string? identity;
    private long connectionGeneration = -1;
    private long rendererGeneration = -1;
    private long continuity;
    private bool previousAllowed;
    private MapEntitySource previousSource;
    internal static string Identity(FlightSessionSnapshot state)=>(state.Aircraft??"")+"|"+(state.Reading?.Evidence?.Registration??"")+"|"+state.ObservedSessionStart?.ToString("O")+"|"+state.Assignment?.FlightNumber+"|"+state.Assignment?.PlannedDepartureUtc.ToString("O");

    internal void Reset()
    {
        trail.Clear();
        BreakContinuity();
    }

    internal void BreakContinuity()
    {
        previousAt = null;
        previousCapture = null;
        previousPosition = null;
        previousAllowed = false;
        continuity++;
    }

    internal MapSnapshot Project(FlightSessionSnapshot state, bool demo, long generation, long sequence, DateTimeOffset now)
        =>Project(state,null,demo,generation,sequence,now);

    internal MapSnapshot Project(FlightSessionSnapshot state, FlightTrackPresentation? presentationTrack, bool demo, long generation, long sequence, DateTimeOffset now)
    {
        now = now.ToUniversalTime();
        if (demo)
        {
            Reset();
            return new(1,generation,sequence,now,[],[],[],[],[]) { SunVector=MapSolar.SunVector(now) };
        }
        var sample = state.Telemetry;
        var source = MapEntitySource.Observed;
        string currentIdentity = Identity(state);
        if (identity != currentIdentity || connectionGeneration != state.ConnectionGeneration || rendererGeneration != generation || previousSource != source)
            Reset();
        identity = currentIdentity;
        connectionGeneration = state.ConnectionGeneration;
        rendererGeneration = generation;
        previousSource = source;
        if (state.SuppressObservedFlight) Reset();
        bool available = sample is { HasPosition: true } && !state.SuppressObservedFlight
            && state.Reading?.Evidence?.SimulationRunning != false;
        bool allowed = available && state.DataCurrent && (state.Connected || demo)
            && !sample!.Paused && !sample.Slewing;
        bool discontinuity = !allowed || !previousAllowed;
        GeoPoint? position = available ? new GeoPoint(sample!.LongitudeDegrees, sample.LatitudeDegrees) : null;
        if (previousAt is {} prior && sample is not null)
        {
            var elapsed = sample.At - prior;
            discontinuity |= elapsed < TimeSpan.Zero || elapsed > TimeSpan.FromSeconds(15);
            if (position is {} current && previousPosition is {} before)
                discontinuity |= MapGeography.Angle(before, current) * 3440.065 > 2 + Math.Max(0, elapsed.TotalHours) * 1500;
        }
        if (previousCapture is {} capture)
            discontinuity |= now < capture || now - capture > TimeSpan.FromSeconds(15);
        if (discontinuity) continuity++;
        if (allowed && position is {} observed && (sample!.At != previousAt || discontinuity))
        {
            if (discontinuity || trail.Count == 0) trail.Add([]);
            trail[^1].Add(observed);
            while (trail.Sum(segment => segment.Count) > MaxTrailPoints)
            {
                trail[0].RemoveAt(0);
                if (trail[0].Count == 0) trail.RemoveAt(0);
            }
        }
        previousAllowed = allowed;
        previousAt = sample?.At;
        previousCapture = now;
        previousPosition = position;
        var aircraft = ImmutableArray.CreateBuilder<MapAircraft>();
        if (position is {} p)
        {
            double? heading = Finite(sample!.HeadingDegrees);
            if (heading is {} h) heading = (h % 360 + 360) % 360;
            aircraft.Add(new("current-aircraft", state.CanRenderAssignment ? state.Assignment?.FlightNumber : state.Aircraft,
                state.Reading?.Evidence?.Registration, p, Finite(sample.AltitudeFeet), heading,
                sample.GroundSpeedKnots >= 0 ? Finite(sample.GroundSpeedKnots) : null,
                source, sample.At.ToUniversalTime(), allowed ? MapReliability.Current : MapReliability.Stale,
                state.Phase switch
                {
                    FlightPhase.AtGate => MapAircraftStatus.AtGate,
                    FlightPhase.TaxiOut or FlightPhase.TaxiIn => MapAircraftStatus.Taxi,
                    FlightPhase.Airborne => MapAircraftStatus.Airborne,
                    FlightPhase.Complete => MapAircraftStatus.Complete,
                    _ => MapAircraftStatus.Unknown
                }, true, state.ConnectionGeneration, continuity, allowed && !discontinuity));
        }
        var routes = ImmutableArray.CreateBuilder<MapRoute>();
        var airports = ImmutableArray.CreateBuilder<MapAirport>();
        AddPlan(state, routes, airports);
        var sourceSegments=presentationTrack?.MaterializeSegments()??trail.Select(segment=>segment.ToImmutableArray()).ToImmutableArray();
        var segments = sourceSegments.SelectMany(segment => MapGeography.SplitDateline(segment)).ToImmutableArray();
        if (segments.Length > 0)
            routes.Add(new("observed-track", null, null, null, null, MapRouteKind.ObservedTrack,
                source, segments, [], sample?.At.ToUniversalTime(), allowed ? MapReliability.Current : MapReliability.Stale));
        var result = new MapSnapshot(1, generation, sequence, now, aircraft.ToImmutable(), airports.ToImmutable(), routes.ToImmutable(), [], [])
        {
            SunVector = MapSolar.SunVector(now)
        };
        MapSnapshotValidator.Validate(result);
        return result;
    }

    private static double? Finite(double number) => double.IsFinite(number) ? number : null;

    private static void AddPlan(FlightSessionSnapshot state, ImmutableArray<MapRoute>.Builder routes, ImmutableArray<MapAirport>.Builder airports)
    {
        var plan = state.Assignment;
        if (plan is null || !(state.CanRenderAssignment || state.CanPresentCandidate)) return;
        var points = plan.RoutePoints?.ToList() ?? [];
        if (points.Count > 2048) return;
        var origin = AirportCatalog.Find(plan.Origin);
        var destination = AirportCatalog.Find(plan.Destination);
        foreach (var (code, role, reference) in new[] { (plan.Origin,"origin",origin),(plan.Destination,"destination",destination) })
        {
            var endpoint=points.FirstOrDefault(p=>string.Equals(p.Ident,code,StringComparison.OrdinalIgnoreCase));
            var coordinate=endpoint is not null?new GeoPosition(endpoint.Latitude,endpoint.Longitude):reference?.Position;
            if(coordinate is {} valid&&valid.IsValid&&!airports.Any(a=>a.Id==code))
                airports.Add(new(code,code,null,new(valid.Longitude,valid.Latitude),reference?.Name??code,MapSeverity.Unknown,null,null,null,null,role));
        }
        // Airport endpoints alone do not establish an authoritative route.
        if(points.Count<2)return;
        if ((points.Count == 0 || !string.Equals(points[0].Ident, plan.Origin, StringComparison.OrdinalIgnoreCase)) && origin is not null)
            points.Insert(0, new(plan.Origin, origin.Position.Latitude, origin.Position.Longitude));
        if ((points.Count == 0 || !string.Equals(points[^1].Ident, plan.Destination, StringComparison.OrdinalIgnoreCase)) && destination is not null)
            points.Add(new(plan.Destination, destination.Position.Latitude, destination.Position.Longitude));
        if (points.Count is < 2 or > 2048) return;
        // Reject the whole route on missing geometry; filtering bad points would invent continuity.
        if (points.Any(p => !new GeoPosition(p.Latitude, p.Longitude).IsValid)) return;
        var positions = points.Select(p => new GeoPoint(p.Longitude, p.Latitude)).ToArray();
        var waypoints = points.Select((p, i) => new MapWaypoint($"waypoint-{i}", positions[i], p.Ident)).ToImmutableArray();
        var planned = ImmutableArray.CreateBuilder<ImmutableArray<GeoPoint>>();
        var completed = ImmutableArray.CreateBuilder<ImmutableArray<GeoPoint>>();
        var lengths = positions.Zip(positions.Skip(1), MapGeography.Angle).ToArray();
        double total = lengths.Sum();
        // Only the authoritative fraction determines completion; geographic distances merely locate that fraction on the display.
        double fraction = state.CanRenderAssignment && state.RouteReliable && state.RouteProgress is {} progress && double.IsFinite(progress)
            ? Math.Clamp(progress, 0, 1) : 0;
        double boundary = total * fraction, traveled = 0;
        try
        {
            for (int i = 0; i < positions.Length - 1; i++)
            {
                double length = lengths[i];
                if (traveled + length <= boundary && fraction > 0)
                    completed.AddRange(MapGeography.GreatCircle(positions[i], positions[i + 1]));
                else if (traveled >= boundary || length < 1e-8)
                    planned.AddRange(MapGeography.GreatCircle(positions[i], positions[i + 1]));
                else
                {
                    var split = MapGeography.Interpolate(positions[i], positions[i + 1], (boundary - traveled) / length);
                    completed.AddRange(MapGeography.GreatCircle(positions[i], split));
                    planned.AddRange(MapGeography.GreatCircle(split, positions[i + 1]));
                }
                traveled += length;
            }
        }
        catch (ArgumentException) { return; } // Ambiguous antipodal path cannot be truthfully interpolated.
        if (planned.Count > 0) routes.Add(new("planned-route", plan.Origin, plan.Destination, null, null,
            MapRouteKind.Planned, MapEntitySource.Inferred, planned.ToImmutable(), waypoints, null, MapReliability.Uncertain));
        if (completed.Count > 0) routes.Add(new("completed-plan", plan.Origin, plan.Destination, null, null,
            MapRouteKind.CompletedPlan, MapEntitySource.Inferred, completed.ToImmutable(), [], null, MapReliability.Uncertain));
    }
}
