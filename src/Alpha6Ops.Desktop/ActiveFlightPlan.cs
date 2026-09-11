using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Alpha6Ops.Core;

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
    internal bool IsValid => !string.IsNullOrWhiteSpace(FlightNumber) && FlightIdentity.IsAirportId(Origin)
        && FlightIdentity.IsAirportId(Destination) && PlannedArrivalUtc > PlannedDepartureUtc;
}

internal static class ActiveFlightPlanStore
{
    private static string PathName(string? root = null) => Path.Combine(root ?? CrashReporter.RootDirectory, "active-flight.json");
    internal static ActiveFlightPlan? Load(string? root = null)
    {
        try
        {
            var path=PathName(root);var plan = File.Exists(path) ? JsonSerializer.Deserialize<ActiveFlightPlan>(File.ReadAllText(path)) : null;
            if (plan is not null && !plan.IsValid) throw new InvalidDataException("Saved assignment has invalid flight identification, airports or UTC times.");
            return plan;
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException) { CrashReporter.Write("active_flight_load", error,directory:root); return null; }
    }
    internal static void Save(ActiveFlightPlan plan,string? root = null)
    {
        if (!plan.IsValid) throw new ArgumentException("The flight assignment has invalid airports or UTC times.", nameof(plan));
        var directory=root ?? CrashReporter.RootDirectory;Directory.CreateDirectory(directory);
        var path=PathName(root);var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
    internal static void Delete(string? root=null){var path=PathName(root);if(File.Exists(path))File.Delete(path);var temporary=path+".tmp";if(File.Exists(temporary))File.Delete(temporary);}
}
