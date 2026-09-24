using System.Collections.Immutable;
namespace Sierra4.MapPrototype;

// Draft presentation-only API. This project is not referenced by the production solution.
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
    DateTimeOffset ObservedAt,MapReliability Reliability,MapAircraftStatus Status,bool Highlighted=false);
public sealed record MapWaypoint(string Id,GeoPoint Position,string? Label);
public sealed record MapRoute(string Id,string? OriginId,string? DestinationId,string? RotationId,int? LegIndex,
    MapRouteKind Kind,MapEntitySource Source,ImmutableArray<ImmutableArray<GeoPoint>> Segments,
    ImmutableArray<MapWaypoint> Waypoints,DateTimeOffset? ObservedAt,MapReliability Reliability);
public sealed record MapAirport(string Id,string? Icao,string? Iata,GeoPoint Position,string Name,
    MapSeverity OperationalStatus,DateTimeOffset? StatusAt,double? DelayMinutes,double? CapacityFraction,string? WeatherReference);
// Resource IDs are resolved through a trusted provider registry, never arbitrary URLs supplied by operational data.
public sealed record MapWeatherLayer(string Id,string ProviderResourceId,DateTimeOffset ValidAt,DateTimeOffset ExpiresAt,double Opacity);
public sealed record MapOperationalRegion(string Id,ImmutableArray<GeoPoint> Boundary,MapSeverity Severity,MapEntitySource Source,DateTimeOffset? ValidUntil);
public sealed record MapSelection(MapEntityKind Kind,string Id);
public sealed record MapCameraState(GeoPoint Center,double Zoom,double Bearing,double Pitch,string? FollowAircraftId);
public sealed record MapSnapshot(int SchemaVersion,long Generation,long Sequence,DateTimeOffset CapturedAt,
    ImmutableArray<MapAircraft> Aircraft,ImmutableArray<MapAirport> Airports,ImmutableArray<MapRoute> Routes,
    ImmutableArray<MapWeatherLayer> Weather,ImmutableArray<MapOperationalRegion> Regions);
public sealed record MapPresentationState(MapCameraState Camera,MapSelection? Selection,ImmutableHashSet<MapLayer> EnabledLayers,bool ReducedMotion,DateTimeOffset LightingUtc);
public interface IMapRenderer : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken);
    // Latest complete snapshot may replace an earlier visual snapshot. Never blocks the operational producer.
    void Offer(MapSnapshot snapshot);
    void Present(MapPresentationState state);
    event Action<MapSelection?>? SelectionChanged;
    event Action<MapCameraState>? CameraChanged;
    event Action<MapRendererStatus>? StatusChanged;
}
