using System;
using System.IO;
using System.Text.Json;

namespace Alpha6Ops.Desktop;

internal record ActiveFlightPlan(string FlightNumber, string Registration, string Origin, string Destination,
    DateTimeOffset PlannedDepartureUtc, DateTimeOffset PlannedArrivalUtc)
{
    internal TimeSpan PlannedDuration => PlannedArrivalUtc - PlannedDepartureUtc;
}

internal static class ActiveFlightPlanStore
{
    private static string PathName => Path.Combine(CrashReporter.RootDirectory, "active-flight.json");
    internal static ActiveFlightPlan? Load()
    {
        try { return File.Exists(PathName) ? JsonSerializer.Deserialize<ActiveFlightPlan>(File.ReadAllText(PathName)) : null; }
        catch (Exception error) when (error is IOException or JsonException) { CrashReporter.Write("active_flight_load", error); return null; }
    }
    internal static void Save(ActiveFlightPlan plan)
    {
        Directory.CreateDirectory(CrashReporter.RootDirectory);
        var temporary = PathName + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, PathName, true);
    }
}
