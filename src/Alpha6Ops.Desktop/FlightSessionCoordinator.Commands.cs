using System;
using System.IO;
using System.Linq;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal sealed partial class FlightSessionCoordinator
{
    internal void AcceptAssignment(ActiveFlightPlan plan)
    {
        dispatcher.VerifyAccess();ObjectDisposedException.ThrowIf(exiting,this);
        if(liveCancellation is not null)throw new InvalidOperationException("Disconnect the simulator before accepting a different flight.");
        activePlan=Freeze(plan);suppressObservedFlightAfterCloseout=false;
        ActiveFlightPlanStore.Save(activePlan!,stateDirectory);FlightRecoveryStore.Delete(stateDirectory);
        ResetLiveTrackingState();ResetLiveIdentity();
        // Gate history is an application operation, not part of acceptance animation.
        new GateHistoryStore(stateDirectory).Remember(activePlan!);
        RecordLog("flight_assignment_changed",null,activePlan!);
        trackingStatus="Flight accepted by Dispatch. Select View Flight Tracking when ready.";
        PublishSnapshot();
    }

    internal void ClearAssignment()
    {
        dispatcher.VerifyAccess();
        if(liveCancellation is not null)throw new InvalidOperationException("Disconnect the simulator before clearing the active flight.");
        ActiveFlightPlanStore.Delete(stateDirectory);FlightRecoveryStore.Delete(stateDirectory);activePlan=null;
        ResetLiveTrackingState();ResetLiveIdentity();trackingStatus="No active flight";PublishSnapshot();
    }

    internal void ApplyGates(string? departure,string? arrival)
    {
        dispatcher.VerifyAccess();if(activePlan is null)return;
        var updated=activePlan with{DepartureGate=departure,ArrivalGate=arrival,GateAssignmentSource="Dispatch",GateAssignmentConfidence="Assigned"};
        ActiveFlightPlanStore.Save(updated,stateDirectory);activePlan=updated;
        new GateHistoryStore(stateDirectory).Remember(updated);
        RecordLog("gate_assignment_changed",liveLast,new{updated.DepartureGate,updated.ArrivalGate,source="Dispatch"});
        if(liveLast is {} at)AddTrackingEvent(new(at,"Dispatch gates updated",$"{updated.Origin} {departure??"—"} · {updated.Destination} {arrival??"—"}",$"{updated.Origin} {departure??"—"} · {updated.Destination} {arrival??"—"}","system"));
        SaveFlightRecovery();PublishSnapshot();
    }

    internal void CorrectSchedule(DateTimeOffset departure,DateTimeOffset arrival)
    {
        dispatcher.VerifyAccess();
        if(activePlan is null||arrival<=departure)throw new ArgumentException("Arrival must follow departure.");
        var original=activePlan;var updated=activePlan with{PlannedDepartureUtc=departure.ToUniversalTime(),PlannedArrivalUtc=arrival.ToUniversalTime(),DispatchEstimatedArrivalUtc=null};
        ActiveFlightPlanStore.Save(updated,stateDirectory);activePlan=updated;
        RecordLog("schedule_corrected",liveLast,new{originalOut=original.PlannedDepartureUtc,originalIn=original.PlannedArrivalUtc,correctedOut=updated.PlannedDepartureUtc,correctedIn=updated.PlannedArrivalUtc,source="Pilot UTC correction"});
        arrivalEstimator.Reset();if(currentLiveReading is {} reading)ReconcileLiveReading(reading);
        if(liveRotation is not null){var leg=liveRotation.Legs[0] with{ScheduledOut=departure,ScheduledIn=arrival};liveRotation=liveRotation with{Legs=new[]{leg}};}
        estimatedArrival=LiveEstimatedArrival(liveLast,liveRecorder?.Phase,ActualFlightOut);
        SaveFlightRecovery();trackingStatus="Schedule updated • recorded actuals preserved";PublishSnapshot();
    }

    internal void ConfirmBlockIn()
    {
        dispatcher.VerifyAccess();
        if(currentLiveReading is null||blockInContext is null||!blockInPolicy.TryConfirm(CurrentBlockInContext(currentLiveReading),DateTimeOffset.UtcNow))return;
        canConfirmBlockIn=false;
        pilotConfirmedBlockIn=true;RecordLog("block_in_confirmation",liveLast,new{source="Pilot",destination=activePlan?.Destination});
        PublishSnapshot();
    }

    internal bool SubmitPirep()
    {
        dispatcher.VerifyAccess();
        if(activePlan is null||flightHistory is null||liveRecorder?.Phase!=FlightPhase.Complete||ActualFlightIn is null)return false;
        // A flight number can recur. Only the exact live/recovered history
        // identity is authority to submit; legacy checkpoints without it stay open.
        var flightId=completedFlightHistoryId;
        if(flightId is null)return false;
        try
        {
            var submitted=flightHistory.SubmitPirep(flightId,new{activePlan.FlightNumber,activePlan.Origin,activePlan.Destination,activePlan.AircraftType,activePlan.Route,actualOut=ActualFlightOut,actualOff=liveRecorder.Events.FirstOrDefault(e=>e.Phase==FlightPhase.Airborne)?.At,actualOn=liveRecorder.Events.LastOrDefault(e=>e.Phase==FlightPhase.TaxiIn)?.At,actualIn=ActualFlightIn,actualSource="Observed simulator milestones",pilotConfirmedBlockIn,submittedUtc=DateTimeOffset.UtcNow,status="Completed",eventCount=trackingEvents.Count});
            if(submitted)FinalizeFlight();
            return submitted;
        }
        catch(Exception error)when(error is IOException or UnauthorizedAccessException){CrashReporter.Write("pirep_submit",error);return false;}
    }

    private void FinalizeFlight()
    {
        if(activePlan is null)return;
        RecordLog("pirep_closeout_complete",null,new{activePlan.FlightNumber});FinishLog("pirep_submitted");
        ActiveFlightPlanStore.Delete(stateDirectory);FlightRecoveryStore.Delete(stateDirectory);activePlan=null;completedFlightHistoryId=null;
        ResetLiveTrackingState();ResetLiveIdentity();suppressObservedFlightAfterCloseout=true;
        trackingStatus="PIREP complete • active flight cleared";PublishSnapshot();
    }

    internal void ExportFlightLog(string journal,string destination)=>TestFlightLog.Export(journal,destination);
}
