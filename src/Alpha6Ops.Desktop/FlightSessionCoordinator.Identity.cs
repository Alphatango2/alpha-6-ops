using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal sealed partial class FlightSessionCoordinator
{
    private LiveReading? currentLiveReading;
    private FlightAssociation liveAssociation = new(false, "FREE FLIGHT", []);
    private DateTimeOffset? observedSessionStart;
    private GeoPosition? observedStartPosition;
    private bool observedStartOnGround;
    private bool liveWasInMenu;
    private bool liveDataCurrent;
    private DateTime liveReceivedAt;
    private string? arrivalMismatch;
    private bool suppressObservedFlightAfterCloseout;
    private bool CanRenderAssignedFlight => liveAssociation.Accepted && liveDataCurrent &&
          currentLiveReading?.Evidence is { SimulationRunning: true, Position.IsValid: true };

    private bool CanPresentCandidate
    {
        get
        {
            if(activePlan is null)return false;
            if(liveAssociation.Accepted)return true;
            if(currentLiveReading?.Evidence?.Route is {} route&&route.Origin==activePlan.Origin&&route.Destination==activePlan.Destination)return true;
            var origin=AirportCatalog.Find(activePlan.Origin)?.Position;
            if(origin is null&&activePlan.RoutePoints?.FirstOrDefault() is {} point)origin=new(point.Latitude,point.Longitude);
            return origin is not null&&observedStartPosition is not null&&observedStartOnGround&&observedStartPosition.DistanceNm(origin)<=5;
        }
    }

    private void ResetLiveIdentity()
    {
        currentLiveReading = null; observedSessionStart = null; observedStartPosition = null;
        liveWasInMenu = false; liveDataCurrent = false; arrivalMismatch = null;
        liveAssociation = new(false, "FREE FLIGHT", []);
    }
    private void ResetObservedSession(string reason)
    {
        ResetTerminalConfirmation();
        pendingCompletedFlightHistoryId=null;
        routeTracker.Reset();arrivalEstimator.Reset();estimatedArrival=null;liveRouteProgress=null;
        RecordLog("observed_session_reset", liveLast, new { reason });
        if(liveFlightHistoryId is not null){RecordFlightEvent(liveFlightHistoryId,"invalidated",liveLast,new{reason});EndFlightHistory(liveFlightHistoryId,"Invalid");liveFlightHistoryId=null;}
        liveRecorder = null; liveRotation = null; liveLast = null; liveAircraft = null;
        milestones.Clear();
        ResetLiveIdentity();
    }
    private void ReconcileLiveReading(LiveReading reading)
    {
        var evidence = reading.Evidence ?? new();
        if (liveWasInMenu && evidence.SimulationRunning == true)
        {
            observedSessionStart = null;
            observedStartPosition = null;
            liveWasInMenu = false;
        }
        if (observedSessionStart is null && evidence.SimulationRunning == true &&
            evidence.Position is { IsValid: true })
        {
            observedSessionStart = reading.Telemetry.At;
            observedStartPosition = evidence.Position;
            observedStartOnGround = reading.Telemetry.OnGround;
        }
        var plan = activePlan is null ? null : new FlightAssignment(activePlan.FlightNumber, activePlan.Registration,
            activePlan.Origin, activePlan.Destination, activePlan.PlannedDepartureUtc, activePlan.PlannedArrivalUtc);
        var plannedOrigin=AirportCatalog.Find(activePlan?.Origin);
        if(plannedOrigin is null&&activePlan?.RoutePoints?.FirstOrDefault() is{} routeOrigin)
            plannedOrigin=new(activePlan.Origin,"Assigned departure",activePlan.Origin,new(routeOrigin.Latitude,routeOrigin.Longitude),"SimBrief route");
        var association = FlightIdentity.Reconcile(plan, evidence with { Position = evidence.Position is null ? null : observedStartPosition },
                observedSessionStart ?? reading.Telemetry.At, observedStartOnGround, plannedOrigin, liveAssociation.Accepted);
        if (association.Accepted != liveAssociation.Accepted || association.Explanation != liveAssociation.Explanation)
            RecordLog("flight_identification", reading.Telemetry.At, new { association.Mode, association.Accepted, association.Issues,
                evidence.Registration, evidence.Route, position = evidence.Position, nearby = evidence.Nearby, evidence.PlanStatus });
        liveAssociation = association;
        if (!association.Accepted) liveRotation = null;
        currentLiveReading = reading;
        liveReceivedAt = DateTime.UtcNow; liveDataCurrent = true;
        if (association.Accepted && liveRotation is null && liveRecorder is not null && activePlan is not null)
        {
            var rotation = BuildLiveRotation(activePlan, reading.Aircraft, AircraftGroundProfiles.ForFamily(reading.Aircraft));
            if(rotation is not null)foreach (var milestone in liveRecorder.Events) if(milestone.Phase!=FlightPhase.Complete||rotation.Legs[0].ActualOut is not null)rotation = RotationPlanner.ApplyMilestone(rotation, milestone);
            liveRotation=rotation;
        }
    }
    private string LiveContextSummary()
    {
        var evidence = currentLiveReading?.Evidence;
        if (!liveDataCurrent) return "Telemetry unavailable — showing last observation";
        if (evidence?.SimulationRunning != true) return "Waiting for an active flight";
        if (arrivalMismatch is not null) return arrivalMismatch;
        if (liveAssociation.Issues.Count > 0) return liveAssociation.Accepted ? "Flight context needs review" : "Saved assignment needs review";
        if (evidence?.Position is null) return "Position unavailable";
        return evidence?.Nearby is { } nearby ? $"Near {nearby.Airport.Ident} · {nearby.DistanceNm:0.0} nm" : "Airport vicinity unknown";
    }
    internal void MarkLiveUnavailable()
    {
        ResetTerminalConfirmation();
        liveDataCurrent = false;
        if (currentLiveReading is not null) trackingStatus="Telemetry unavailable. Waiting for reconnection.";
        PublishSnapshot();
    }
}
