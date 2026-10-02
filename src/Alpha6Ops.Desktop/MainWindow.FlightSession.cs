using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

public partial class MainWindow
{
    internal static readonly System.Diagnostics.ActivitySource PresentationTiming = new("Alpha6Ops.Presentation");
    private readonly FlightSessionCoordinator coordinator;
    private FlightSessionSnapshot flightState => coordinator.Snapshot;
    private ActiveFlightPlan? activePlan => flightState.Assignment;
    private string? liveAircraft => flightState.Aircraft;
    private DateTimeOffset? liveLast => flightState.SimulatorUtc;
    private Telemetry? liveTelemetry => flightState.Telemetry;
    private double? liveRouteProgress => flightState.RouteProgress;
    private AircraftRotation? liveRotation => flightState.Rotation;
    private LiveReading? currentLiveReading => flightState.Reading;
    private FlightAssociation liveAssociation => flightState.Association;
    private DateTimeOffset? observedSessionStart => flightState.ObservedSessionStart;
    private bool liveDataCurrent => flightState.DataCurrent;
    private string? arrivalMismatch => flightState.ArrivalMismatch;
    private bool suppressObservedFlightAfterCloseout => flightState.SuppressObservedFlight;
    private bool CanRenderAssignedFlight => flightState.CanRenderAssignment;
    private bool CanPresentCandidate => flightState.CanPresentCandidate;
    private bool ScheduleTrusted => flightState.ScheduleTrusted;
    private bool joinedInProgress => flightState.JoinedInProgress;
    private DateTimeOffset? ActualFlightOut => flightState.ActualOut;
    private DateTimeOffset? ActualFlightIn => flightState.ActualIn;
    private IReadOnlyList<TrackingEventEntry> trackingEvents => flightState.Events;
    private FlightHistoryDatabase? flightHistory => coordinator.History;
    private ProgramMonitor? programMonitor => coordinator.Monitor;
    private string? lastJournal => flightState.LastJournal;
    private string LiveContextSummary()=>flightState.ContextSummary;
    // The initial XAML already presents Disconnected; keep its resting palette.
    private SimulatorConnectionState renderedConnection=SimulatorConnectionState.Disconnected;
    private bool presentationDisposed;

    private void RuntimeChanged(object? sender,EventArgs e)
    {
        using var timing = PresentationTiming.StartActivity("RuntimeChanged");
        if(presentationDisposed||exiting)return;
        var snapshot=flightState;
        RenderConnection(snapshot);
        LiveText.Text=snapshot.LiveDescription;
        if(snapshot.LogStatus.Length>0)LogStatusText.Text=snapshot.LogStatus;
        LiveTimelineButton.IsEnabled=LiveDebriefButton.IsEnabled=snapshot.Phase is not null;
        FlightTrackingView.ConfirmBlockInButton.Visibility=snapshot.CanConfirmBlockIn?Visibility.Visible:Visibility.Collapsed;
        if(!milestones.SequenceEqual(snapshot.Milestones)){milestones.Clear();foreach(var item in snapshot.Milestones)milestones.Add(item);}
        if(snapshot.Phase is {} phase)tray.Text="Sierra 4 — Live "+phase;
        RefreshLiveTracker(snapshot.Aircraft,snapshot.SimulatorUtc,snapshot.Phase,snapshot.TrackingStatus);
        RefreshOperations();
    }

    private void RenderConnection(FlightSessionSnapshot snapshot)
    {
        Shell.PresentConnection(snapshot);
        // Initial connection presentation also applies without an active flight;
        // it must not replace the independent fixture-preview dashboard.
        ConnectionText.Text=snapshot.ConnectionMessage;
        ConnectButton.IsEnabled=ReplayButton.IsEnabled=ResetButton.IsEnabled=!snapshot.ConnectionActive&&!running;
        DisconnectButton.IsEnabled=snapshot.ConnectionActive;
        if(renderedConnection!=snapshot.Connection)
        {
            renderedConnection=snapshot.Connection;
            var (label,color,background)=snapshot.Connection switch
            {
                SimulatorConnectionState.Launching=>("LAUNCHING MSFS","#FFCA45","#433817"),
                SimulatorConnectionState.Connecting=>("CONNECTING TO SIMULATOR","#FFCA45","#433817"),
                SimulatorConnectionState.Connected=>("SIMULATOR CONNECTED","#65E697","#173D27"),
                SimulatorConnectionState.Reconnecting=>("SIMULATOR CONNECTION LOST — RETRYING","#FFCA45","#433817"),
                SimulatorConnectionState.Failed=>("SIMULATOR CONNECTION FAILED","#FF9690","#512621"),
                _=>("SIMULATOR DISCONNECTED","#A9AD9F","#30342C")
            };
            SetConnectionBadge(label,color,background);
        }
    }

    private void DisposePresentation()
    {
        if(presentationDisposed)return;
        presentationDisposed=true;coordinator.SnapshotChanged-=RuntimeChanged;coordinator.DiagnosticsChanged-=DiagnosticsChanged;
        dashboardClock.Stop();lifetime.Cancel();
        tray.Visible=false;tray.ContextMenuStrip?.Dispose();tray.Dispose();trayIcon.Dispose();
    }

    private void DiagnosticsChanged(object? sender,EventArgs e)
    {
        if(!presentationDisposed&&!exiting)ProgramHealthText.Text=coordinator.ProgramHealth;
    }

    private string? BeginFlightHistory(string source,string? detail,string aircraft,string? origin,string? destination,string? number)
        =>coordinator.BeginFlightHistory(source,detail,aircraft,origin,destination,number);
    private void RecordFlightEvent(string? id,string kind,DateTimeOffset? at,object detail)=>coordinator.RecordFlightEvent(id,kind,at,detail);
    private void EndFlightHistory(string? id,string phase)=>coordinator.EndFlightHistory(id,phase);
}
