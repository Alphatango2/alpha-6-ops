using System;
using System.Collections.Generic;
using System.Linq;
using Alpha6Ops.Core;
using Alpha6Ops.Desktop.MapV2;

namespace Alpha6Ops.Desktop;

internal enum OperationsProvenance { Observed, Derived, Assignment, Demo, Unavailable, Stale }
internal enum OperationsSeverity { Information, Warning, Critical }
internal sealed record OperationsMetric(string Label, string Value, string Note, OperationsProvenance Source);
internal sealed record OperationsAlert(string Id, string Title, string Detail, OperationsSeverity Severity)
{
    public string Color => Severity switch { OperationsSeverity.Critical => "#F58D8D", OperationsSeverity.Warning => "#ECC66B", _ => "#86BDE8" };
}
internal sealed record OperationsAirport(string Code, string Name, string Role, string Gate);
internal sealed record OperationsActivity(string Time, string Title, string Detail, TrackingEventCode Code);
internal sealed record OperationsOverview(
    string Context, string Freshness, OperationsProvenance Source,
    IReadOnlyList<OperationsMetric> Metrics, IReadOnlyList<OperationsAlert> Alerts,
    IReadOnlyList<OperationsAirport> Airports, IReadOnlyList<OperationsActivity> Activity,
    string WeatherTitle, string WeatherDetail, string MapContext,
    IReadOnlyList<FlightRoutePoint> MapRoute, Telemetry? MapTelemetry, double? MapProgress,
    string MapSessionKey, string FlightsSource)
{
    internal FlightSessionSnapshot? MapState { get; init; }
    internal FlightTrackPresentation? MapTrack { get; init; }
    internal bool MapDemo { get; init; }
}

// Read-only presentation query. No persistence, clock acquisition, simulator access or operational commands.
internal static class OperationsOverviewQuery
{
    internal static string FlightStatus(FlightSessionSnapshot state)
    {
        if(state.Reading is null)return "ASSIGNED";
        if(!state.DataCurrent)return "LAST OBSERVED";
        if(state.Reading.Evidence?.SimulationRunning==false||state.Telemetry?.Paused==true||state.Telemetry?.Slewing==true)return "OBSERVATION SUSPENDED";
        if(!string.IsNullOrWhiteSpace(state.ArrivalMismatch))return "ARRIVAL REVIEW";
        if(!state.Association.Accepted)return "ASSIGNMENT REVIEW";
        if(!state.ScheduleTrusted)return "SCHEDULE REVIEW";
        return state.Phase switch { FlightPhase.AtGate=>"AT GATE",FlightPhase.TaxiOut=>"TAXI OUT",FlightPhase.Airborne=>"AIRBORNE",FlightPhase.TaxiIn=>"TAXI IN",FlightPhase.Complete=>"COMPLETE",_=>"OBSERVING" };
    }

    internal static OperationsOverview Project(FlightSessionSnapshot state, bool demo,
        IReadOnlyList<DashboardFlightRow> flights, LocalWeather? weather, bool weatherFailed, DateTimeOffset now)
    {
        var observed = state.Reading is not null && !state.SuppressObservedFlight;
        var suspended = state.Reading?.Evidence?.SimulationRunning == false || state.Telemetry?.Paused == true || state.Telemetry?.Slewing == true;
        var current = observed && state.DataCurrent && !suspended;
        var source = demo ? OperationsProvenance.Demo : observed
            ? current ? OperationsProvenance.Observed : OperationsProvenance.Stale
            : state.Assignment is not null ? OperationsProvenance.Assignment : OperationsProvenance.Unavailable;
        var alerts = new List<OperationsAlert>();
        if (!demo)
        {
            if (state.Connection == SimulatorConnectionState.Failed)
                alerts.Add(new("connection", "Simulator connection failed", state.ConnectionMessage, OperationsSeverity.Critical));
            else if (state.Connection == SimulatorConnectionState.Reconnecting)
                alerts.Add(new("connection", "Reconnecting to simulator", "Last observations remain available. Waiting for fresh telemetry.", OperationsSeverity.Warning));
            else if (observed && suspended)
                alerts.Add(new("suspended", "Flight observation suspended", "Simulator menu, pause or slew is active. Live totals are unavailable until normal observation resumes.", OperationsSeverity.Information));
            else if (observed && !current)
                alerts.Add(new("stale", "Telemetry is not current", "Last observed flight state is retained; live totals are unavailable.", OperationsSeverity.Warning));
            if (observed && state.Assignment is not null && !state.Association.Accepted)
                alerts.Add(new("association", "Assignment needs review", state.Association.Explanation, OperationsSeverity.Warning));
            if (observed && state.Assignment is not null && !state.ScheduleTrusted)
                alerts.Add(new("schedule", "Schedule needs review", "Simulator and assignment dates must align before schedule comparisons are available.", OperationsSeverity.Warning));
            if (!string.IsNullOrWhiteSpace(state.ArrivalMismatch))
                alerts.Add(new("arrival", "Arrival airport needs review", state.ArrivalMismatch, OperationsSeverity.Warning));
        }
        var active = current && state.Phase is FlightPhase.TaxiOut or FlightPhase.Airborne or FlightPhase.TaxiIn;
        var comparable = current && state.Association.Accepted && state.ScheduleTrusted && state.Assignment is not null && string.IsNullOrWhiteSpace(state.ArrivalMismatch)
            && (state.ActualIn ?? state.EstimatedArrival) is not null;
        var delay = comparable ? ((state.ActualIn ?? state.EstimatedArrival)!.Value - state.Assignment!.PlannedArrivalUtc).TotalMinutes : (double?)null;
        var metrics = new[] {
            new OperationsMetric("ACTIVE",demo ? "—" : current ? active ? "1" : "0" : "—",demo ? "Replay scenario" : current ? "Monitored flight" : "Awaiting telemetry",demo ? OperationsProvenance.Demo : current ? OperationsProvenance.Derived : OperationsProvenance.Unavailable),
            new OperationsMetric("ON TIME",!demo && comparable ? delay <= 5 ? "1" : "0" : "—","Current flight · arrival",comparable && !demo ? OperationsProvenance.Derived : OperationsProvenance.Unavailable),
            new OperationsMetric("DELAYED",!demo && comparable ? delay > 5 ? "1" : "0" : "—","Current flight · >5 min",comparable && !demo ? OperationsProvenance.Derived : OperationsProvenance.Unavailable),
            new OperationsMetric("AT RISK","—","Network analysis unavailable",OperationsProvenance.Unavailable),
            new OperationsMetric("AIRCRAFT",!demo && current ? "1" : "—","Observed · not fleet availability",!demo && current ? OperationsProvenance.Observed : OperationsProvenance.Unavailable),
            new OperationsMetric("CONNECTIONS","—","Passenger data unavailable",OperationsProvenance.Unavailable)
        };
        var plan = state.Assignment;
        var airports = new List<OperationsAirport>();
        if (!demo && plan is not null)
        {
            airports.Add(Airport(plan.Origin,"Assigned departure",plan.DepartureGate));
            airports.Add(Airport(plan.Destination,"Assigned arrival",plan.ArrivalGate));
        }
        else if (demo && flights.FirstOrDefault() is {} first)
        {
            airports.Add(Airport(first.Leg.Origin,"Sample departure",null));
            airports.Add(Airport(first.Leg.Destination,"Sample arrival",null));
        }
        var activity = demo ? Array.Empty<OperationsActivity>() : state.Events
            .OrderByDescending(e=>e.At).Take(8).Select(e=>new OperationsActivity(e.At.ToString("dd MMM · HH:mm:ss'Z'"), e.Title,e.Summary,e.Code)).ToArray();
        var weatherStale = weatherFailed || weather is not null && now-weather.ObservedAt>TimeSpan.FromMinutes(45);
        var weatherTitle = weather is null ? "Local weather unavailable" : $"{weather.City} · {weather.Temperature(false)}";
        var weatherDetail = weather is null ? "Airport observations are not connected." :
            $"{(weatherStale ? "Last available · " : "")}{weather.Condition} · {weather.ObservedAt:dd MMM HH:mm}Z\nOpen-Meteo · approximate IP city, not airport weather";
        var mapRoute = !demo && state.CanRenderAssignment ? plan?.RoutePoints?.ToArray() ?? [] : Array.Empty<FlightRoutePoint>();
        // Do not infer a flown track from progress when position telemetry is missing.
        // The retained renderer draws aircraft only with a route. Do not claim a
        // displayed position for free-flight observations it cannot currently draw.
        var telemetry = !demo && current && mapRoute.Length>=2 && state.Telemetry?.HasPosition == true ? state.Telemetry : null;
        var mapContext = demo ? "Replay preview · no network traffic shown" : telemetry is not null
            ? "Observed position · assigned route"
            : mapRoute.Length>1 ? "Planned route · no current position" : "No verified route or current position";
        return new(
            demo ? "REPLAY SCENARIO · SAMPLE ROTATION" : "YOUR FLIGHT SESSION · NOT NETWORK-WIDE OPERATIONS",
            demo ? "Recorded scenario preview" : state.SimulatorUtc is {} at && observed
                ? $"{(current ? "Observed" : "Last observed")} {at:dd MMM yyyy · HH:mm:ss}Z · simulator time"
                : plan is not null ? "Assignment loaded · awaiting live observations" : "No active flight · open Dispatch to prepare a release",
            source,metrics,alerts,airports,activity,weatherTitle,weatherDetail,mapContext,mapRoute,telemetry,
            state.CanRenderAssignment && state.RouteReliable ? state.RouteProgress : null,
            $"{plan?.FlightNumber}|{plan?.PlannedDepartureUtc:O}|{state.ObservedSessionStart:O}|{demo}|{state.CanRenderAssignment}",
            demo ? "Sample rotation · scheduled UTC" : observed && !state.CanPresentCandidate ? "No verified assignment · observed flight only"
            : plan is null ? "No assigned rotation" : "Assigned rotation · scheduled UTC · estimates in flight summary")
        { MapState=state, MapDemo=demo };
    }

    private static OperationsAirport Airport(string code,string role,string? gate)
        => new(code,AirportCatalog.Find(code)?.Name ?? "Airport name unavailable",role,gate is null ? "Gate —" : "Gate " + gate);
}
