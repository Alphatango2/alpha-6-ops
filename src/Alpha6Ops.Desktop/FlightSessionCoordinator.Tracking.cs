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
    private void ResetLiveTrackingState()
    {
        blockInStoppedSince=null;pilotConfirmedBlockIn=false;canConfirmBlockIn=false;estimatedArrival=null;
        liveRecorder=null;liveLast=null;liveAircraft=null;liveTelemetry=null;liveRouteProgress=null;liveRotation=null;liveNeedsBaseline=false;completedFlightHistoryId=null;routeTracker.Reset();arrivalEstimator.Reset();joinedInProgress=false;
        trackingEventMonitor=null;trackingEvents.Clear();milestones.Clear();
    }
    private void RestoreFlightRecovery(FlightRecoveryState? state)
    {
        if(state is null||activePlan is null)return;
        liveLast=state.LastSimulatorUtc;liveAircraft=state.Aircraft;liveTelemetry=state.LastTelemetry;liveRouteProgress=state.RouteProgress;liveSource=state.Source;
        var profile=AircraftGroundProfiles.ForFamily(state.Aircraft);liveRecorder=new TimelineRecorder(new PhaseDetector(profile,state.Phase,state.LastSimulatorUtc),state.PhaseEvents);
        var baseRotation=BuildLiveRotation(activePlan,state.Aircraft,profile);
        if(baseRotation is not null)liveRotation=baseRotation with{Legs=[baseRotation.Legs[0] with{ActualOut=state.ActualOut,ActualIn=state.ActualIn}]};
        trackingEventMonitor=new FlightTrackingEventMonitor(state.MonitorState);foreach(var entry in state.TrackingEvents)trackingEvents.Add(entry);
        foreach(var flightEvent in state.PhaseEvents)milestones.Add($"RECOVERED {flightEvent.At.UtcDateTime:HH:mm:ss}Z   {PhaseLabel(flightEvent.Phase)}");
        liveNeedsBaseline=true;
    }
    private void SaveFlightRecovery()
    {
        if(activePlan is null||liveRecorder is null||liveAircraft is null)return;
        var leg=liveRotation?.Legs[0];
        try{FlightRecoveryStore.Save(new(activePlan.FlightNumber,activePlan.PlannedDepartureUtc,liveSource,liveAircraft,liveRecorder.Phase,liveLast,liveTelemetry,liveRouteProgress,leg?.ActualOut,leg?.ActualIn,liveRecorder.Events.ToArray(),trackingEvents.ToArray(),trackingEventMonitor?.CaptureState(),DateTimeOffset.UtcNow),stateDirectory);}
        catch(Exception error) when(error is IOException or UnauthorizedAccessException){CrashReporter.Write("flight_recovery_save",error);}
    }
    private static AircraftRotation? BuildLiveRotation(ActiveFlightPlan? plan, string? simulatorAircraft, AircraftGroundProfile groundProfile)
    {
        if (plan is null) return null;
        var aircraft = string.IsNullOrWhiteSpace(plan.Registration) ? simulatorAircraft ?? "UNKNOWN" : plan.Registration;
        return new AircraftRotation("alpha6", aircraft, groundProfile.MinimumTurnMinutes, [new(plan.FlightNumber, plan.Origin, plan.Destination, plan.PlannedDepartureUtc, plan.PlannedArrivalUtc)]);
    }
    internal void ObserveLive(LiveReading reading)
    {
        if (liveCancellation is null || liveCancellation.IsCancellationRequested || exiting) return;
        dispatcher.VerifyAccess();
        reading = Freeze(reading);
        var s = reading.Telemetry;
        liveTelemetry=s;liveRouteProgress=reading.RouteProgress;
        liveSource=reading.Source;
        programMonitor?.Update("Connected",reading.Aircraft,s.At,sampleReceived:true);
        
        RecordLog("telemetry", s.At, new { aircraft = reading.Aircraft, onGround = s.OnGround, groundSpeedKnots = s.GroundSpeedKnots, indicatedAirspeedKnots=double.IsFinite(s.IndicatedAirspeedKnots)?(double?)s.IndicatedAirspeedKnots:null, verticalSpeedFeetPerMinute=double.IsFinite(s.VerticalSpeedFeetPerMinute)?(double?)s.VerticalSpeedFeetPerMinute:null, gearExtendedRatio=double.IsFinite(s.GearExtendedRatio)?(double?)s.GearExtendedRatio:null, flapsExtendedRatio=double.IsFinite(s.SelectedFlapRatio)?(double?)s.SelectedFlapRatio:null,
            flapHandleRatio=double.IsFinite(s.SelectedFlapRatio)?(double?)s.SelectedFlapRatio:null,flapHandleIndex=s.FlapHandleIndex,flapHandlePositions=s.FlapHandlePositions,
            leftFlapDegrees=double.IsFinite(s.LeftFlapDegrees)?(double?)s.LeftFlapDegrees:null,rightFlapDegrees=double.IsFinite(s.RightFlapDegrees)?(double?)s.RightFlapDegrees:null,
            leftSlatDegrees=double.IsFinite(s.LeftSlatDegrees)?(double?)s.LeftSlatDegrees:null,rightSlatDegrees=double.IsFinite(s.RightSlatDegrees)?(double?)s.RightSlatDegrees:null,engineDataValid=s.EngineDataValid,engineReadings=s.EngineReadings,engineStates=s.EngineStates, pitchDegrees=double.IsFinite(s.PitchDegrees)?(double?)s.PitchDegrees:null, bankDegrees=double.IsFinite(s.BankDegrees)?(double?)s.BankDegrees:null, fuelTotalWeightPounds=double.IsFinite(s.FuelTotalWeightPounds)?(double?)s.FuelTotalWeightPounds:null, runningEngineCount=s.RunningEngineCount>=0?(int?)s.RunningEngineCount:null, runningEngineMask=s.RunningEngineMask>=0?(int?)s.RunningEngineMask:null, parkingBrake = s.ParkingBrake, enginesRunning = s.EnginesRunning, paused = s.Paused, slewing = s.Slewing, latitude = s.HasPosition?(double?)s.LatitudeDegrees:null, longitude = s.HasPosition?(double?)s.LongitudeDegrees:null, altitudeFeet = double.IsFinite(s.AltitudeFeet)?(double?)s.AltitudeFeet:null, pressureAltitudeFeet=double.IsFinite(s.PressureAltitudeFeet)?(double?)s.PressureAltitudeFeet:null, altitudeAboveGroundFeet=double.IsFinite(s.AltitudeAboveGroundFeet)?(double?)s.AltitudeAboveGroundFeet:null, headingDegrees = double.IsFinite(s.HeadingDegrees)?(double?)s.HeadingDegrees:null, routeProgress=reading.RouteProgress });
        liveDescription = $"{reading.Source} • {reading.Aircraft} • {s.At.UtcDateTime:HH:mm:ss}Z • {s.GroundSpeedKnots:0.0} kt • Brake {(s.ParkingBrake ? "set" : "released")} • {(s.Paused ? "Paused" : s.Slewing ? "Slew" : s.OnGround ? "On ground" : "Airborne")}";
        var continuous=liveNeedsBaseline?(liveAircraft is null||reading.Aircraft==liveAircraft):TelemetryContinuity.IsContinuous(liveLast,liveAircraft,s.At,reading.Aircraft);
        liveNeedsBaseline=false;
        var positionJump = FlightIdentity.PositionJump(currentLiveReading?.Evidence?.Position, reading.Evidence?.Position, s.At - (liveLast ?? s.At));
        if (!continuous || positionJump)
        {
            var reason = reading.Aircraft != liveAircraft ? "Aircraft changed" : positionJump ? "Aircraft position jumped" : "Simulator clock discontinuity";
            RecordLog("monitor_invalidated", s.At, new { reason, previousSimulatorUtc = liveLast, previousAircraft = liveAircraft });
            ResetObservedSession(reason);
            trackingEventMonitor = new();
            trackingEvents.Clear();
        }
        liveLast = s.At; liveAircraft = reading.Aircraft;
        ReconcileLiveReading(reading);
        if (reading.Evidence?.SimulationRunning == false)
        {
            liveWasInMenu = true;
            connectionMessage = "Connected. Waiting for an active simulator flight; tracking is suspended.";
            trackingStatus=connectionMessage; PublishSnapshot();
            return;
        }
        if (liveRecorder is null)
        {
            if (s.Paused || s.Slewing)
            { connectionMessage = "Connected. Waiting for simulation to resume."; trackingStatus=s.Paused ? "Simulator paused" : "Slew mode"; PublishSnapshot(); return; }
            var groundProfile = AircraftGroundProfiles.ForFamily(reading.Aircraft);
            var initialPhase = !s.OnGround ? FlightPhase.Airborne : s.GroundSpeedKnots >= 1 ? FlightPhase.TaxiOut : FlightPhase.AtGate;
            joinedInProgress=initialPhase!=FlightPhase.AtGate;
            liveRecorder = new TimelineRecorder(new PhaseDetector(groundProfile, initialPhase));
            liveRotation = CanRenderAssignedFlight ? BuildLiveRotation(activePlan, reading.Aircraft, groundProfile) : null;
            if(initialPhase!=FlightPhase.AtGate)StartLiveFlightHistory(reading.Aircraft);
            RecordLog("monitor_armed", s.At, new { aircraft = reading.Aircraft, initialPhase, departureObserved = !joinedInProgress, joinedInProgress });
            if(CanRenderAssignedFlight&&activePlan is not null)AddTrackingEvent(trackingEventMonitor?.SystemEvent(s.At,"Assignment loaded",$"{activePlan.FlightNumber} • {activePlan.Origin} to {activePlan.Destination}"));
            var observedRegistration=reading.Evidence?.Registration;AddTrackingEvent(trackingEventMonitor?.SystemEvent(s.At,"Aircraft identified",$"{AircraftIdentity.CanonicalType(activePlan?.AircraftType,reading.Aircraft)}{(string.IsNullOrWhiteSpace(observedRegistration)?"":$" • {observedRegistration}")}"));
            if(joinedInProgress)AddTrackingEvent(trackingEventMonitor?.SystemEvent(s.At,"Tracking joined in progress",$"Connected during {DisplayPhase(initialPhase).ToLowerInvariant()} • earlier OUT and events were not observed","alert"));
            else if(initialPhase==FlightPhase.AtGate){var gate=activePlan?.DepartureGate is {Length:>0}?$"Gate {activePlan.DepartureGate}":"Departure gate unassigned";AddTrackingEvent(trackingEventMonitor?.SystemEvent(s.At,"Preflight at gate",$"{gate} • Parking brake {(s.ParkingBrake?"set":"released")}"));}
            AddTrackingEvent(trackingEventMonitor?.SystemEvent(s.At,"Flight monitoring armed",$"{AircraftIdentity.CanonicalType(activePlan?.AircraftType,reading.Aircraft)} • joined in {PhaseLabel(initialPhase).ToLowerInvariant()}"));
        }
        var stopped=liveRecorder.Phase==FlightPhase.TaxiIn&&s.OnGround&&s.GroundSpeedKnots<.5&&s.ParkingBrake&&!s.Paused&&!s.Slewing;
        if(!stopped){blockInStoppedSince=null;pilotConfirmedBlockIn=false;}else blockInStoppedSince??=s.At;
        var destinationPoint=activePlan?.RoutePoints?.LastOrDefault();
        var destinationPosition=destinationPoint is not null?new GeoPosition(destinationPoint.Latitude,destinationPoint.Longitude):AirportCatalog.Find(activePlan?.Destination)?.Position;
        var observedPosition=s.HasPosition?new GeoPosition(s.LatitudeDegrees,s.LongitudeDegrees):reading.Evidence?.Position;
        var atDestination=destinationPosition is not null&&observedPosition is not null&&observedPosition.DistanceNm(destinationPosition)<=5;
        var eligible=stopped&&blockInStoppedSince is {} stop&&s.At-stop>=TimeSpan.FromSeconds(10);
        canConfirmBlockIn=eligible&&(!s.EngineDataValid||!atDestination);
        var milestone=liveRecorder.Observe(s with{BlockInAllowed=liveRecorder.Phase!=FlightPhase.TaxiIn||atDestination||pilotConfirmedBlockIn,PilotConfirmedBlockIn=eligible&&pilotConfirmedBlockIn});
        if (milestone is not null)
        {
            milestones.Add($"LIVE {milestone.At.UtcDateTime:HH:mm:ss}Z   {PhaseLabel(milestone.Phase)}");
            RecordLog("flight_milestone", milestone.At, new { phase = milestone.Phase.ToString(), label = PhaseLabel(milestone.Phase) });
            if (milestone.Phase is FlightPhase.TaxiIn or FlightPhase.Complete && liveAssociation.Accepted && activePlan is not null)
            {
                var destination = AirportCatalog.Find(activePlan.Destination);
                arrivalMismatch = reading.Evidence?.Position is not { IsValid: true } position || destination is null
                    ? "Arrival airport unverified"
                    : position.DistanceNm(destination.Position) > 5 ? $"Landed away from assigned destination {activePlan.Destination}" : null;
                if (arrivalMismatch is not null) RecordLog("arrival_airport_review", s.At, new { reason = arrivalMismatch, nearby = reading.Evidence?.Nearby });
            }
            if (milestone.Phase == FlightPhase.Airborne) arrivalMismatch = null;
            if (liveRotation is not null && !(joinedInProgress&&milestone.Phase==FlightPhase.TaxiOut) && (milestone.Phase != FlightPhase.Complete || arrivalMismatch is null && liveRotation.Legs[0].ActualOut is not null))
                liveRotation = RotationPlanner.ApplyMilestone(liveRotation, milestone);
            if (milestone.Phase == FlightPhase.Complete) SaveFlightLog();
        }
        routeTracker.SetRoute(activePlan?.RoutePoints?.Select(p=>new GeoPosition(p.Latitude,p.Longitude))??[]);
        if(s.HasPosition)liveRouteProgress=routeTracker.Observe(new(s.LatitudeDegrees,s.LongitudeDegrees),s.At,s.Paused||s.Slewing)?.Fraction??reading.RouteProgress;
        if(liveRecorder.Phase==FlightPhase.AtGate)liveRouteProgress=0;
        if(liveRecorder.Phase==FlightPhase.Complete&&arrivalMismatch is null)liveRouteProgress=1;
        var eventProgress=LiveRouteReliable?liveRouteProgress??0:double.NaN;
        estimatedArrival=LiveEstimatedArrival(s.At,liveRecorder.Phase,ActualFlightOut);
        var eventEstimate=estimatedArrival;
        foreach(var entry in trackingEventMonitor?.Observe(s,liveRecorder.Phase,milestone,eventProgress,activePlan,null,eventEstimate)??[])AddTrackingEvent(entry);
        RecordLog("tracking_state",s.At,new{progress=liveRouteProgress,remainingNm=LiveRemainingDistance,routeReliable=LiveRouteReliable,crossTrackNm=routeTracker.Current?.CrossTrackNm,estimatedIn=eventEstimate,phase=liveRecorder.Phase.ToString()});
        if(milestone?.Phase==FlightPhase.Complete&&liveFlightHistoryId is not null){completedFlightHistoryId=liveFlightHistoryId;EndFlightHistory(liveFlightHistoryId,"Complete");liveFlightHistoryId=null;}
        connectionMessage = $"Connected • {PhaseLabel(liveRecorder.Phase)}. Active flight tracking uses the assignment shown below.";
        var altitude=double.IsFinite(s.AltitudeFeet)?$" • {s.AltitudeFeet:0} ft":"";var heading=double.IsFinite(s.HeadingDegrees)?$" • {((s.HeadingDegrees%360)+360)%360:000}°":"";var brake=s.OnGround?$" • Brake {(s.ParkingBrake ? "set" : "released")}":"";
        trackingStatus=$"Live telemetry • {s.GroundSpeedKnots:0.0} kt{altitude}{heading}{brake}";
        SaveFlightRecovery();
        PublishSnapshot();
    }
    private void StartLog(string mode)
    {
        FinishLog("new_connection");
        try
        {
            flightLog = new TestFlightLog(LogDirectory, Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown", mode);
            lastJournal = flightLog.JournalPath;
            if (activePlan is not null) RecordLog("flight_assignment", null, activePlan);
            logStatus = "Recording test log. Export it here after your flight—or any time something looks wrong.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { logStatus = "Logging unavailable: " + e.Message; }
    }
    private void RecordLog(string kind, DateTimeOffset? at, object detail)
    {
        try { flightLog?.Record(kind, at, detail); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { flightLog?.Dispose(); flightLog = null; logStatus = "Logging stopped: " + e.Message; }
    }
    internal void SaveFlightLog()
    {
        try { flightLog?.SaveExport(); logStatus = "Flight log saved. Click Export test log to save a JSON file to upload."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { logStatus = "Log export failed: " + e.Message; }
    }
    private void FinishLog(string reason)
    {
        if (flightLog is null) return;
        try { flightLog.End(reason); logStatus = "Test log saved. Use Export test log to choose where to save your upload."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { logStatus = "Log finalization failed; the journal may still be available: " + e.Message; }
        finally { flightLog.Dispose(); flightLog = null; }
    }
    private DateTimeOffset? LiveEstimatedArrival(DateTimeOffset? now,FlightPhase? phase,DateTimeOffset? actualOut)
    {
        if(liveTelemetry is not{} sample||activePlan is null||phase is null)return null;
        if(ActualFlightIn is{} actualIn)return actualIn;
        // Hide stale predictions without resetting the established airborne/taxi
        // clocks. A short reconnect must not restart the schedule blend.
        if(!liveDataCurrent||currentLiveReading?.Evidence?.SimulationRunning==false)return null;
        return arrivalEstimator.Observe(sample,phase.Value,LiveRemainingDistance,ActualFlightIn,ScheduleTrusted?activePlan.DispatchEstimatedArrivalUtc??activePlan.PlannedArrivalUtc:null,ScheduleTrusted?activePlan.PlannedDepartureUtc:null,actualOut);
    }
    internal void AddTrackingEvent(TrackingEventEntry? entry)
    {
        if(entry is null)return;
        if(entry.Code==TrackingEventCode.ConnectionLost&&trackingEvents.LastOrDefault() is {} previous&&previous.Code==entry.Code&&previous.Summary==entry.Summary)return;
        trackingEvents.Add(entry);RecordLog("tracking_event",entry.At,new{entry.Title,entry.Summary,entry.Detail,entry.Kind});
        if(liveFlightHistoryId is null&&entry.Code is TrackingEventCode.BlockOut or TrackingEventCode.Liftoff)StartLiveFlightHistory(liveAircraft??activePlan?.AircraftType??"UNKNOWN");
        else if(liveFlightHistoryId is not null)RecordFlightEvent(liveFlightHistoryId,"tracking_event",entry.At,new{entry.Title,entry.Summary,entry.Detail,entry.Kind});
        SaveFlightRecovery();
    }
    private void StartLiveFlightHistory(string aircraft)
    {
        if(liveFlightHistoryId is not null)return;
        var detail=JsonSerializer.Serialize(new{journalPath=lastJournal,jsonPath=lastJournal is null?null:Path.ChangeExtension(lastJournal,".json")});
        liveFlightHistoryId=BeginFlightHistory("live",detail,AircraftIdentity.CanonicalType(activePlan?.AircraftType,aircraft),activePlan?.Origin,activePlan?.Destination,activePlan?.FlightNumber);
        if(liveFlightHistoryId is null)return;
        foreach(var recorded in trackingEvents)RecordFlightEvent(liveFlightHistoryId,"tracking_event",recorded.At,new{recorded.Title,recorded.Summary,recorded.Detail,recorded.Kind});
    }
    internal string? BeginFlightHistory(string source, string? sourceDetail, string aircraft, string? origin, string? destination, string? flightNumber)
    {
        if (flightHistory is null) return null;
        try { return flightHistory.BeginFlight(PilotName, source, sourceDetail, aircraft, origin, destination, flightNumber, Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { CrashReporter.Write("flight_history_begin", error); return null; }
    }
    internal void RecordFlightEvent(string? flightId, string kind, DateTimeOffset? at, object detail)
    {
        if (flightId is null || flightHistory is null) return;
        try { flightHistory.RecordEvent(flightId, kind, at, detail); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { CrashReporter.Write("flight_history_record", error); }
    }
    internal void EndFlightHistory(string? flightId, string finalPhase)
    {
        if (flightId is null || flightHistory is null) return;
        try { flightHistory.EndFlight(flightId, finalPhase); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { CrashReporter.Write("flight_history_end", error); }
    }
}
