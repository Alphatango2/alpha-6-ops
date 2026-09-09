using System;
using System.IO;
using System.Text.Json;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal record ActiveFlightPlan(string FlightNumber, string Registration, string Origin, string Destination,
    DateTimeOffset PlannedDepartureUtc, DateTimeOffset PlannedArrivalUtc, string? Source = null,
    string? SimBriefUsername = null, DateTimeOffset? ImportedAtUtc = null, string? DepartureGate = null,
    string? ArrivalGate = null, string? GateAssignmentSource = null, string? GateAssignmentConfidence = null,
    string? AirlineIcao = null)
{
    internal TimeSpan PlannedDuration => PlannedArrivalUtc - PlannedDepartureUtc;
    internal bool IsValid => !string.IsNullOrWhiteSpace(FlightNumber) && FlightIdentity.IsAirportId(Origin)
        && FlightIdentity.IsAirportId(Destination) && PlannedArrivalUtc > PlannedDepartureUtc;
}

internal static class ActiveFlightPlanStore
{
    private static string PathName => Path.Combine(CrashReporter.RootDirectory, "active-flight.json");
    internal static ActiveFlightPlan? Load()
    {
        try
        {
            var plan = File.Exists(PathName) ? JsonSerializer.Deserialize<ActiveFlightPlan>(File.ReadAllText(PathName)) : null;
            if (plan is not null && !plan.IsValid) throw new InvalidDataException("Saved assignment has invalid flight identification, airports or UTC times.");
            return plan;
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException) { CrashReporter.Write("active_flight_load", error); return null; }
    }
    internal static void Save(ActiveFlightPlan plan)
    {
        if (!plan.IsValid) throw new ArgumentException("The flight assignment has invalid airports or UTC times.", nameof(plan));
        Directory.CreateDirectory(CrashReporter.RootDirectory);
        var temporary = PathName + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, PathName, true);
    }
}
