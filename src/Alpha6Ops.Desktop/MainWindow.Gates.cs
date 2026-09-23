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
        coordinator.ApplyGates(departure,arrival);
    }
}
