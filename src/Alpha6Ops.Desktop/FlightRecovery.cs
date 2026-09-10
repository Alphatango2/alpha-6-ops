using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal sealed record FlightRecoveryState(
    string FlightNumber, DateTimeOffset PlannedDepartureUtc, string Source, string Aircraft,
    FlightPhase Phase, DateTimeOffset? LastSimulatorUtc, Telemetry? LastTelemetry, double? RouteProgress,
    DateTimeOffset? ActualOut, DateTimeOffset? ActualIn, IReadOnlyList<FlightEvent> PhaseEvents,
    IReadOnlyList<TrackingEventEntry> TrackingEvents, FlightTrackingMonitorState? MonitorState,
    DateTimeOffset SavedUtc);

internal static class FlightRecoveryStore
{
    private static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true,NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals};
    private static string PathName(string? root=null)=>Path.Combine(root??CrashReporter.RootDirectory,"active-flight-state.json");
    internal static FlightRecoveryState? Load(ActiveFlightPlan plan,string? root=null)
    {
        try
        {
            var path=PathName(root);if(!File.Exists(path))return null;
            var state=JsonSerializer.Deserialize<FlightRecoveryState>(File.ReadAllText(path),JsonOptions);
            return state is not null&&state.FlightNumber.Equals(plan.FlightNumber,StringComparison.OrdinalIgnoreCase)&&state.PlannedDepartureUtc==plan.PlannedDepartureUtc?state:null;
        }
        catch(Exception error) when(error is IOException or JsonException or UnauthorizedAccessException){CrashReporter.Write("flight_recovery_load",error,directory:root);return null;}
    }
    internal static void Save(FlightRecoveryState state,string? root=null)
    {
        var directory=root??CrashReporter.RootDirectory;Directory.CreateDirectory(directory);var path=PathName(root);var temporary=path+".tmp";
        File.WriteAllText(temporary,JsonSerializer.Serialize(state,JsonOptions));File.Move(temporary,path,true);
    }
    internal static void Delete(string? root=null){var path=PathName(root);if(File.Exists(path))File.Delete(path);var temporary=path+".tmp";if(File.Exists(temporary))File.Delete(temporary);}
}
