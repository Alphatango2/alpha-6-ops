namespace Alpha6Ops.Core;

public static class FlightLabProtocol
{
    public const string PipeName = "Alpha6OPS.FlightLab.v3";
    public const int SchemaVersion = 3;
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
    double? RouteProgress = null,
    double AltitudeFeet = 0,
    double IndicatedAirspeedKnots = 0,
    double VerticalSpeedFeetPerMinute = 0,
    double GearExtendedRatio = 1,
    double AltitudeAboveGroundFeet = 0,
    double FlapsExtendedRatio = 0,
    double PitchDegrees = 0,
    double BankDegrees = 0,
    double FuelTotalWeightPounds = 30000,
    int RunningEngineCount = 0,
    int RunningEngineMask = 0);
