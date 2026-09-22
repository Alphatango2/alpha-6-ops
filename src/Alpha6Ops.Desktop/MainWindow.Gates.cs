using System;

namespace Alpha6Ops.Desktop;

public partial class MainWindow
{
    private void AssignGates()
    {
        if(activePlan is null)return;
        var dialog=new GateAssignmentWindow(activePlan){Owner=this};
        if(dialog.ShowDialog()!=true||dialog.Plan is null)return;
        ApplyDispatchGates(dialog.Plan.DepartureGate,dialog.Plan.ArrivalGate);
    }

    internal void ApplyDispatchGates(string? departure,string? arrival)
    {
        if(activePlan is null)return;
        var updated=activePlan with{DepartureGate=departure,ArrivalGate=arrival,GateAssignmentSource="Dispatch",GateAssignmentConfidence="Assigned"};
        ActiveFlightPlanStore.Save(updated,stateDirectory);activePlan=updated;
        new GateHistoryStore(stateDirectory??CrashReporter.RootDirectory).Remember(updated);
        RecordLog("gate_assignment_changed",liveLast,new{updated.DepartureGate,updated.ArrivalGate,source="Dispatch"});
        if(liveLast is {} at)AddTrackingEvent(new(at,"Dispatch gates updated",$"{updated.Origin} {departure??"—"} · {updated.Destination} {arrival??"—"}",$"{updated.Origin} {departure??"—"} · {updated.Destination} {arrival??"—"}","system"));
        SaveFlightRecovery();RefreshLiveTracker(liveAircraft,liveLast,liveRecorder?.Phase,FlightTrackingView.StatusText.Text);DispatchView.Render(activePlan,"VIEW RELEASE");
    }
}
