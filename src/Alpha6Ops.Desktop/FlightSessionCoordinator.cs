using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

// Application lifetime, serialized by the application dispatcher; never owns or
// references a Window/Control. Core algorithms and storage adapters are reused.
internal sealed partial class FlightSessionCoordinator : IDisposable
{
    private readonly IFlightRuntimeDispatcher dispatcher;
    private readonly ISimulatorConnectionSource simulator;
    private readonly string stateDirectory;
    private readonly bool diagnosticMode;
    private readonly CancellationTokenSource lifetime = new();
    private readonly List<string> milestones = [];
    private readonly List<TrackingEventEntry> trackingEvents = [];
    private readonly FlightHistoryDatabase? flightHistory;
    private readonly ProgramMonitor? programMonitor;
    private readonly RouteProgressTracker routeTracker = new();
    private readonly ArrivalEstimator arrivalEstimator = new();
    private CancellationTokenSource? liveCancellation;
    private TimelineRecorder? liveRecorder;
    private DateTimeOffset? liveLast;
    private string? liveAircraft;
    private string liveSource = "MSFS 2024";
    private Telemetry? liveTelemetry;
    private double? liveRouteProgress;
    private DateTimeOffset? blockInStoppedSince;
    private bool pilotConfirmedBlockIn, joinedInProgress, liveNeedsBaseline, exiting, canConfirmBlockIn;
    private FlightTrackingEventMonitor? trackingEventMonitor;
    private TestFlightLog? flightLog;
    private string? lastJournal, liveFlightHistoryId, completedFlightHistoryId;
    private ActiveFlightPlan? activePlan;
    private AircraftRotation? liveRotation;
    private DateTimeOffset? estimatedArrival;
    private string trackingStatus="No active flight", connectionMessage="Disconnected.", liveDescription="", logStatus="";
    private SimulatorConnectionState connectionState;
    private string PilotName="Unspecified";
    private string programHealth="";
    private string LogDirectory => Path.Combine(stateDirectory,"TestLogs");
    private DateTimeOffset? ActualFlightOut => liveRecorder?.Events.FirstOrDefault(e=>e.Phase==FlightPhase.TaxiOut)?.At;
    private DateTimeOffset? ActualFlightIn => liveRecorder?.Events.LastOrDefault(e=>e.Phase==FlightPhase.Complete)?.At;
    private bool ScheduleTrusted => activePlan is not null && (ActualFlightOut is {} at
        ? FlightClock.DepartureScheduleIsPlausible(activePlan,at)
        : liveLast is {} now && FlightClock.ScheduleIsPlausible(activePlan,now));
    private bool LiveRouteReliable => liveRecorder?.Phase==FlightPhase.Complete&&arrivalMismatch is null ||
        liveDataCurrent&&(liveRecorder?.Phase==FlightPhase.AtGate ||
        routeTracker.Current is{IsReliable:true} current&&liveTelemetry is{} sample&&(current.At==sample.At||sample.Paused||sample.Slewing));
    private double? LiveRemainingDistance => LiveRouteReliable&&activePlan is not null&&liveRouteProgress is{} fraction&&
        FlightMetrics.RemainingDistanceNm(activePlan.RoutePoints,fraction) is var distance&&double.IsFinite(distance)?distance:null;

    internal FlightSessionSnapshot Snapshot { get; private set; } = new();
    public event EventHandler? SnapshotChanged;
    public event EventHandler? DiagnosticsChanged;
    internal string ProgramHealth => programHealth;
    internal string CurrentPilotName => PilotName;
    internal FlightHistoryDatabase? History => flightHistory;
    internal ProgramMonitor? Monitor => programMonitor;

    internal FlightSessionCoordinator(string directory, bool diagnostic, IFlightRuntimeDispatcher dispatcher, ISimulatorConnectionSource? simulator=null)
    {
        stateDirectory=directory;diagnosticMode=diagnostic;this.dispatcher=dispatcher;
        this.simulator=simulator??new SimConnectConnectionSource();
        dispatcher.VerifyAccess();
        try { flightHistory=new FlightHistoryDatabase(directory); }
        catch(Exception error){CrashReporter.Write("flight_history_startup",error,directory:directory);}
        activePlan=Freeze(ActiveFlightPlanStore.Load(directory));
        if(activePlan is not null)RestoreFlightRecovery(FlightRecoveryStore.Load(activePlan,directory));
        if(activePlan is not null)trackingStatus="Assignment ready. Connect at the gate to begin tracking.";
        try { programMonitor=new ProgramMonitor(LogDirectory,status=>{if(exiting)return;programHealth=status;Notify(DiagnosticsChanged);},directory); }
        catch(Exception error){CrashReporter.Write("program_monitor_startup",error,directory:directory);programHealth="PROGRAM MONITOR UNAVAILABLE • "+error.GetBaseException().Message;}
        PublishSnapshot();
    }

    internal void SetPilotName(string name) { dispatcher.VerifyAccess();PilotName=string.IsNullOrWhiteSpace(name)?"Unspecified":name.Trim(); }

    private void PublishSnapshot()
    {
        dispatcher.VerifyAccess();
        var phase=liveRecorder?.Phase;
        var complete=phase==FlightPhase.Complete;
        var rotation=liveRotation??(activePlan is null?null:BuildLiveRotation(activePlan,liveAircraft,AircraftGroundProfile.Default));
        Snapshot=new()
        {
            Assignment=Freeze(activePlan), Connection=connectionState, ConnectionActive=liveCancellation is not null,
            Connected=liveDataCurrent&&liveCancellation is {IsCancellationRequested:false}, ConnectionMessage=connectionMessage,
            TrackingStatus=trackingStatus, LiveDescription=liveDescription, LogStatus=logStatus, LastJournal=lastJournal,
            Aircraft=liveAircraft, Source=liveSource, Telemetry=Freeze(liveTelemetry), Reading=currentLiveReading is null?null:Freeze(currentLiveReading),
            SimulatorUtc=liveLast, Phase=phase, PhaseLabel=phase is null?"Ready":DisplayPhase(phase.Value),
            RouteProgress=liveRouteProgress, RouteReliable=LiveRouteReliable, RemainingDistanceNm=LiveRemainingDistance,
            EstimatedArrival=ActualFlightIn??(liveDataCurrent&&currentLiveReading?.Evidence?.SimulationRunning!=false?estimatedArrival:null),
            ActualOut=ActualFlightOut, ActualIn=ActualFlightIn, Complete=complete, PirepEligible=complete&&ActualFlightIn is not null,
            CanConfirmBlockIn=canConfirmBlockIn, JoinedInProgress=joinedInProgress, ScheduleTrusted=ScheduleTrusted,
            DataCurrent=liveDataCurrent, CanRenderAssignment=CanRenderAssignedFlight, CanPresentCandidate=CanPresentCandidate,
            SuppressObservedFlight=suppressObservedFlightAfterCloseout, ArrivalMismatch=arrivalMismatch, ObservedSessionStart=observedSessionStart,
            Association=liveAssociation with{Issues=liveAssociation.Issues.ToImmutableArray()},
            Rotation=rotation is null?null:rotation with{Legs=rotation.Legs.ToImmutableArray()}, ContextSummary=LiveContextSummary(),
            PhaseEvents=liveRecorder?.Events.ToImmutableArray()??[], Events=trackingEvents.ToImmutableArray(), Milestones=milestones.ToImmutableArray()
        };
        // Persistence precedes publication. A failed subscriber cannot abort the
        // source loop or prevent another subscriber from receiving this snapshot.
        Notify(SnapshotChanged);
    }

    private void Notify(EventHandler? handlers)
    {
        foreach(var subscriber in handlers?.GetInvocationList()??[])
            try { ((EventHandler)subscriber)(this,EventArgs.Empty); }
            catch(Exception error){CrashReporter.Write("flight_snapshot_subscriber",error,directory:stateDirectory);}
    }

    private static ActiveFlightPlan? Freeze(ActiveFlightPlan? plan)=>plan is null?null:plan with{RoutePoints=plan.RoutePoints?.ToImmutableArray()};
    private static Telemetry? Freeze(Telemetry? sample)=>sample is null?null:sample with{EngineReadings=sample.EngineReadings?.ToImmutableArray(),EngineStates=sample.EngineStates?.ToImmutableArray()};
    private static LiveReading Freeze(LiveReading reading)=>reading with{Telemetry=Freeze(reading.Telemetry)!};
    internal FlightTimeline? ReadTimeline()=>liveRecorder is null?null:new(liveRecorder.Snapshots.ToImmutableArray(),liveRecorder.Events.ToImmutableArray(),liveRecorder.Phase);
    private static string PhaseLabel(FlightPhase phase)=>phase switch
    {
        FlightPhase.AtGate=>"At gate",FlightPhase.TaxiOut=>"Block-out / taxi out",FlightPhase.Airborne=>"Takeoff / airborne",
        FlightPhase.TaxiIn=>"Landing / taxi in",FlightPhase.Complete=>"Block-in / complete",_=>phase.ToString()
    };
    private string DisplayPhase(FlightPhase phase)
    {
        if(phase!=FlightPhase.Airborne||liveTelemetry is not{} sample)return PhaseLabel(phase);
        return trackingEventMonitor?.AirbornePhase??"Airborne";
    }

    public void Dispose()
    {
        dispatcher.VerifyAccess();
        if(exiting)return;
        exiting=true;connectionGeneration++;
        liveCancellation?.Cancel();lifetime.Cancel();
        FinishLog("application_exit");programMonitor?.Stop("application_exit");programMonitor?.Dispose();flightHistory?.Dispose();
        SnapshotChanged=null;DiagnosticsChanged=null;
    }
}
