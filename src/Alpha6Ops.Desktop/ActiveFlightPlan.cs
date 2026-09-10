using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Alpha6Ops.Desktop;

internal record FlightRoutePoint(string Ident,double Latitude,double Longitude,string Kind="Waypoint");

internal record ActiveFlightPlan(string FlightNumber, string Registration, string Origin, string Destination,
    DateTimeOffset PlannedDepartureUtc, DateTimeOffset PlannedArrivalUtc, string? Source = null,
    string? SimBriefUsername = null, DateTimeOffset? ImportedAtUtc = null, string? DepartureGate = null,
    string? ArrivalGate = null, string? GateAssignmentSource = null, string? GateAssignmentConfidence = null,
    string? Route = null,IReadOnlyList<FlightRoutePoint>? RoutePoints = null,string? AircraftType = null,
    double? PlannedTripFuel = null,string? FuelUnits = null)
{
    internal TimeSpan PlannedDuration => PlannedArrivalUtc - PlannedDepartureUtc;
}

internal static class ActiveFlightPlanStore
{
    private static string PathName(string? root = null) => Path.Combine(root ?? CrashReporter.RootDirectory, "active-flight.json");
    internal static ActiveFlightPlan? Load(string? root = null)
    {
        try { return File.Exists(PathName(root)) ? JsonSerializer.Deserialize<ActiveFlightPlan>(File.ReadAllText(PathName(root))) : null; }
        catch (Exception error) when (error is IOException or JsonException) { CrashReporter.Write("active_flight_load", error); return null; }
    }
    internal static void Save(ActiveFlightPlan plan,string? root = null)
    {
        var directory=root ?? CrashReporter.RootDirectory;Directory.CreateDirectory(directory);
        var path=PathName(root);var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
    internal static void Delete(string? root=null){var path=PathName(root);if(File.Exists(path))File.Delete(path);var temporary=path+".tmp";if(File.Exists(temporary))File.Delete(temporary);}
}
