namespace Alpha6Ops.Core;

public static class FlightLabProtocol
{
    public const string PipeName = "Alpha6OPS.FlightLab.v2";
    public const int SchemaVersion = 2;
}

public sealed record FlightLabFrame(
    int SchemaVersion,
    string Aircraft,
    DateTimeOffset SimulatorUtc,
    bool OnGround,
    double GroundSpeedKnots,
    bool ParkingBrake,
    bool EnginesRunning,
    bool Paused,
    bool Slewing,
    string Phase,
    string? ScenarioEvent = null,
    double? RouteProgress = null);
