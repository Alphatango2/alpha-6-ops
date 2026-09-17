namespace Alpha6Ops.Desktop;

// UI feature availability only. Server membership/role checks remain authoritative.
internal static class PilotFeatureProfile
{
    internal static bool Allows(string module) => module is "PilotLogbook" or "Dispatch" or "FlightTracking" or "Weather" or "Settings";

    internal static readonly DashboardTile[] Tiles =
    [
        new("PilotLogbook", "PILOT LOGBOOK", "Your recorded flights", "Assets/Dashboard/pilot-logbook.png"),
        new("Dispatch", "DISPATCH", "Flight setup & SimBrief import", "Assets/Dashboard/flights-unbranded.png"),
        new("FlightTracking", "FLIGHT TRACKING", "Live flight progress & events", "Assets/Dashboard/hero-unbranded.png"),
        new("Weather", "WEATHER", "Local conditions & observation details", "Assets/Dashboard/weather.png")
    ];
}
