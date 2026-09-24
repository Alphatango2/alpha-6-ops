using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Alpha6Ops.Desktop.MapV2;

// Immutable presentation data. No operational state or renderer dependencies.
public enum MapEntitySource { Unknown, Observed, Inferred, Simulated, Replay }
public enum MapReliability { Unknown, Current, Stale, Uncertain }
public enum MapAircraftStatus { Unknown, AtGate, Taxi, Airborne, Complete }
public enum MapRouteKind { Planned, CompletedPlan, ObservedTrack, InferredTrack, Diversion }
public enum MapEntityKind { Aircraft, Airport, Route, Region }
public enum MapSeverity { Unknown, Normal, Information, Warning, Critical }
public enum MapLayer { Basemap, DayNight, Weather, Airports, Routes, Aircraft, Disruptions, Selection, Labels }
public enum MapRendererState { Initializing, Ready, Degraded, Unavailable, Failed, Disposed }
public sealed record MapRendererStatus(MapRendererState State,string ReasonCode,string? Detail);
public readonly record struct GeoPoint
{
    public double Longitude { get; }
    public double Latitude { get; }
    public GeoPoint(double longitude,double latitude)
    {
        if(!double.IsFinite(longitude)||!double.IsFinite(latitude)||Math.Abs(longitude)>180||Math.Abs(latitude)>90)
            throw new ArgumentOutOfRangeException(nameof(longitude),"Coordinates must be finite WGS84 degrees.");
        Longitude=longitude; Latitude=latitude;
    }
}
public sealed record MapAircraft(string Id,string? Callsign,string? Registration,GeoPoint Position,
    double? AltitudeFeet,double? HeadingDegrees,double? GroundspeedKnots,MapEntitySource Source,
    DateTimeOffset ObservedAt,MapReliability Reliability,MapAircraftStatus Status,bool Highlighted=false, long SourceGeneration=0, long ContinuityId=0, bool InterpolationAllowed=true);
public sealed record MapWaypoint(string Id,GeoPoint Position,string? Label);
public sealed record MapRoute(string Id,string? OriginId,string? DestinationId,string? RotationId,int? LegIndex,
    MapRouteKind Kind,MapEntitySource Source,ImmutableArray<ImmutableArray<GeoPoint>> Segments,
    ImmutableArray<MapWaypoint> Waypoints,DateTimeOffset? ObservedAt,MapReliability Reliability);
public sealed record MapAirport(string Id,string? Icao,string? Iata,GeoPoint Position,string Name,
    MapSeverity OperationalStatus,DateTimeOffset? StatusAt,double? DelayMinutes,double? CapacityFraction,string? WeatherReference,string? Role=null);
// Resource IDs are resolved through a trusted provider registry, never arbitrary URLs supplied by operational data.
public sealed record MapWeatherLayer(string Id,string ProviderResourceId,DateTimeOffset ValidAt,DateTimeOffset ExpiresAt,double Opacity);
public sealed record MapOperationalRegion(string Id,ImmutableArray<GeoPoint> Boundary,MapSeverity Severity,MapEntitySource Source,DateTimeOffset? ValidUntil);
public sealed record MapSelection(MapEntityKind Kind,string Id);
public sealed record MapCameraState(GeoPoint Center,double Zoom,double Bearing,double Pitch,string? FollowAircraftId);
public sealed record MapSnapshot(int SchemaVersion,long Generation,long Sequence,DateTimeOffset CapturedAt,
    ImmutableArray<MapAircraft> Aircraft,ImmutableArray<MapAirport> Airports,ImmutableArray<MapRoute> Routes,
    ImmutableArray<MapWeatherLayer> Weather,ImmutableArray<MapOperationalRegion> Regions)
{
    public ImmutableArray<double> SunVector { get; init; } = [1, 0, 0];
}
public sealed record MapPresentationState(MapCameraState Camera,MapSelection? Selection,ImmutableHashSet<MapLayer> EnabledLayers,bool ReducedMotion,DateTimeOffset LightingUtc)
{
    public bool RestoreCamera { get; init; }
}



internal static class MapSnapshotValidator
{
    internal const int MaxPayloadBytes = 4 * 1024 * 1024;
    internal const int MaxAircraft = 5000;
    internal const int MaxGeometryPoints = 100000;
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    internal static byte[] SerializeValidated(MapSnapshot snapshot)
    {
        Validate(snapshot);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        Require(bytes.Length <= MaxPayloadBytes, "Map payload budget exceeded.");
        return bytes;
    }

    internal static void Validate(MapSnapshot s)
    {
        Require(s is not null, "Snapshot required.");
        Require(s!.SchemaVersion == 1 && s.Generation > 0 && s.Sequence > 0, "Invalid map version/generation/sequence.");
        Utc(s.CapturedAt);
        Require(!s.Aircraft.IsDefault && s.Aircraft.Length <= MaxAircraft, "Aircraft budget.");
        Require(!s.Airports.IsDefault && s.Airports.Length <= 2048, "Airport budget.");
        Require(!s.Routes.IsDefault && s.Routes.Length <= 2048, "Route budget.");
        Require(!s.Weather.IsDefault && s.Weather.Length == 0 && !s.Regions.IsDefault && s.Regions.Length == 0,
            "Weather/regions are not supported by the production data provider.");
        Require(!s.SunVector.IsDefault && s.SunVector.Length == 3 && s.SunVector.All(double.IsFinite)
            && Math.Abs(s.SunVector.Sum(x => x * x) - 1) < 0.00001, "Invalid sun vector.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in s.Aircraft)
        {
            ArgumentNullException.ThrowIfNull(a);
            Id(a.Id, ids); Text(a.Callsign); Text(a.Registration); Point(a.Position); Utc(a.ObservedAt);
            Require(Enum.IsDefined(a.Source) && Enum.IsDefined(a.Reliability) && Enum.IsDefined(a.Status), "Aircraft enum.");
            Require(a.SourceGeneration >= 0 && a.ContinuityId >= 0, "Aircraft continuity.");
            Number(a.AltitudeFeet); Number(a.HeadingDegrees); Number(a.GroundspeedKnots);
            Require(a.HeadingDegrees is null or >= 0 and < 360 && a.GroundspeedKnots is null or >= 0, "Aircraft numeric bounds.");
        }
        ids.Clear();
        foreach (var a in s.Airports)
        {
            ArgumentNullException.ThrowIfNull(a);
            Id(a.Id, ids); Point(a.Position); Text(a.Name); Text(a.Icao); Text(a.Iata); Text(a.Role); Text(a.WeatherReference);
            if (a.StatusAt is {} at) Utc(at);
            Number(a.DelayMinutes); Number(a.CapacityFraction);
            Require(Enum.IsDefined(a.OperationalStatus) && a.CapacityFraction is null or >= 0 and <= 1, "Airport status.");
        }
        ids.Clear();
        int points = 0;
        foreach (var r in s.Routes)
        {
            ArgumentNullException.ThrowIfNull(r);
            Id(r.Id, ids); Text(r.OriginId); Text(r.DestinationId); Text(r.RotationId);
            Require(Enum.IsDefined(r.Source) && Enum.IsDefined(r.Kind) && Enum.IsDefined(r.Reliability), "Route enum.");
            Require(r.Kind != MapRouteKind.ObservedTrack || r.Source is MapEntitySource.Observed or MapEntitySource.Replay or MapEntitySource.Simulated,
                "Observed track requires an explicit observation source.");
            Require(r.Kind != MapRouteKind.InferredTrack || r.Source == MapEntitySource.Inferred, "Inferred track source.");
            if (r.ObservedAt is {} at) Utc(at);
            Require(!r.Segments.IsDefault && !r.Waypoints.IsDefault && r.Waypoints.Length <= 2048, "Route arrays.");
            var waypointIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var waypoint in r.Waypoints) { ArgumentNullException.ThrowIfNull(waypoint);Id(waypoint.Id, waypointIds); Point(waypoint.Position); Text(waypoint.Label); points++; }
            Require(points <= MaxGeometryPoints, "Geometry budget.");
            foreach (var segment in r.Segments)
            {
                Require(!segment.IsDefault && segment.Length >= 2, "Route segment length.");
                points += segment.Length;
                Require(points <= MaxGeometryPoints, "Geometry budget.");
                for (int i = 0; i < segment.Length; i++)
                {
                    Point(segment[i]);
                    if (i > 0) Require(Math.Abs(segment[i].Longitude - segment[i - 1].Longitude) <= 180, "Unsplit dateline crossing.");
                }
            }
        }
    }

    private static void Id(string value, HashSet<string> ids)
    {
        Require(!string.IsNullOrWhiteSpace(value) && value.Length <= 160 && !value.Any(char.IsControl) && ids.Add(value), "Invalid or duplicate ID.");
    }
    private static void Text(string? value) => Require(value is null || value.Length <= 512, "Text budget.");
    private static void Number(double? value) => Require(value is null || double.IsFinite(value.Value), "Nonfinite number.");
    private static void Utc(DateTimeOffset value) => Require(value.Offset == TimeSpan.Zero && value.Year >= 1900, "UTC timestamp required.");
    private static void Point(GeoPoint p) => Require(double.IsFinite(p.Longitude) && double.IsFinite(p.Latitude)
        && Math.Abs(p.Longitude) <= 180 && Math.Abs(p.Latitude) <= 90, "Invalid coordinate.");
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new ArgumentException(message);
    }
}
