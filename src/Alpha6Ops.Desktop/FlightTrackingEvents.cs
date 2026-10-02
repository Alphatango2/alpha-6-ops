using System;
using System.Collections.Generic;
using System.Linq;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal enum TrackingEventCode { None, BlockOut, Liftoff, ConnectionLost }

internal sealed record TrackingEventEntry(DateTimeOffset At,string Title,string Summary,string Detail,string Kind="operation")
{
    public TrackingEventCode Code { get; init; }
    public string Time => At.UtcDateTime.ToString("HH:mm:ss'Z'");
}

internal sealed record FlightTrackingMonitorState(IReadOnlyList<string> Fired,double? PreviousAltitude,double? InitialFuelPounds,
    bool WasPaused,bool WasSlewing,bool PreviousEngines,bool PreviousBrake,bool HasSample,bool HasTouchedDown,
    int PreviousEngineMask,int ReportedFlaps,double PreviousGear,string? AssignedDestination,int ReportedCruiseLevel,
    double LastAirborneVerticalSpeed=double.NaN,double LastAirborneSpeed=double.NaN,
    IReadOnlyList<int>? EngineStartCounts=null,int PendingEngineMask=-1,int PendingEngineMaskSamples=0,
    bool EngineSignalUnsupported=false,int AirborneEngineOffSamples=0,
    IReadOnlyDictionary<int,EngineCondition>? ObservedEngineStates=null,
    ObservedCruiseTrackerState? ObservedCruise=null);

internal sealed class FlightTrackingEventMonitor
{
    private readonly HashSet<string> fired=new(StringComparer.Ordinal);
    private readonly Dictionary<string,int> candidates=new(StringComparer.Ordinal);
    private double? previousAltitude;
    private double? initialFuelPounds;
    private bool wasPaused,wasSlewing,previousEngines,previousBrake,hasSample,hasTouchedDown;
    private int previousEngineMask=-1,reportedFlaps=-1,flapCandidate=-1,flapCandidateSamples;
    private int pendingEngineMask=-1,pendingEngineMaskSamples;
    private bool engineSignalUnsupported;
    private readonly Dictionary<int,EngineCondition> observedEngineStates=new();
    private int airborneEngineOffSamples;
    private readonly int[] engineStartCounts=new int[4];
    private int reportedCruiseLevel;
    private double previousGear=double.NaN;
    private double lastAirborneVerticalSpeed=double.NaN,lastAirborneSpeed=double.NaN;
    private string? assignedDestination;
    private readonly ObservedCruiseTracker observedCruise;
    internal string AirbornePhase { get; private set; } = "Climb";
    private bool gearWasExtended;

    internal FlightTrackingEventMonitor(FlightTrackingMonitorState? restored=null)
    {
        if(restored is null){observedCruise=new();return;}
        fired.UnionWith(restored.Fired);previousAltitude=restored.PreviousAltitude;initialFuelPounds=restored.InitialFuelPounds;
        wasPaused=restored.WasPaused;wasSlewing=restored.WasSlewing;previousEngines=restored.PreviousEngines;previousBrake=restored.PreviousBrake;
        hasSample=restored.HasSample;hasTouchedDown=restored.HasTouchedDown;previousEngineMask=restored.PreviousEngineMask;
        reportedFlaps=restored.ReportedFlaps;previousGear=restored.PreviousGear;assignedDestination=restored.AssignedDestination;reportedCruiseLevel=restored.ReportedCruiseLevel;
        lastAirborneVerticalSpeed=restored.LastAirborneVerticalSpeed;lastAirborneSpeed=restored.LastAirborneSpeed;
        pendingEngineMask=restored.PendingEngineMask;pendingEngineMaskSamples=restored.PendingEngineMaskSamples;
        engineSignalUnsupported=restored.EngineSignalUnsupported;airborneEngineOffSamples=restored.AirborneEngineOffSamples;
        if(restored.ObservedEngineStates is not null)foreach(var state in restored.ObservedEngineStates)observedEngineStates[state.Key]=state.Value;
        if(restored.EngineStartCounts is not null)for(var index=0;index<Math.Min(engineStartCounts.Length,restored.EngineStartCounts.Count);index++)engineStartCounts[index]=restored.EngineStartCounts[index];
        var cruiseState=restored.ObservedCruise;
        if(cruiseState is null&&fired.Contains("cruise")&&reportedCruiseLevel>=18000)
            cruiseState=new ObservedCruiseTrackerState(ObservedAirborneStage.Cruise,reportedCruiseLevel,DateTimeOffset.UnixEpoch,null,null,0,null,true,0,false,null,0,0,false);
        observedCruise=new(cruiseState);
    }

    internal FlightTrackingMonitorState CaptureState()=>new(fired.ToArray(),previousAltitude,initialFuelPounds,wasPaused,wasSlewing,
        previousEngines,previousBrake,hasSample,hasTouchedDown,previousEngineMask,reportedFlaps,previousGear,assignedDestination,reportedCruiseLevel,lastAirborneVerticalSpeed,lastAirborneSpeed,engineStartCounts.ToArray(),pendingEngineMask,pendingEngineMaskSamples,engineSignalUnsupported,airborneEngineOffSamples,new Dictionary<int,EngineCondition>(observedEngineStates),observedCruise.CaptureState());

    internal IReadOnlyList<TrackingEventEntry> Observe(Telemetry sample,FlightPhase phase,FlightEvent? milestone,double progress,ActiveFlightPlan? plan,string? scenarioEvent,DateTimeOffset? estimatedIn=null)
    {
        var events=new List<TrackingEventEntry>();
        var goAround=milestone?.Phase==FlightPhase.Airborne&&hasTouchedDown;
        var cruiseObservation=observedCruise.Observe(sample,plan?.CruiseAltitudeFeet,goAround);
        if(sample.Paused||sample.Slewing)
        {
            candidates.Clear();pendingEngineMaskSamples=0;flapCandidateSamples=0;
            if(sample.Paused&&!wasPaused)events.Add(SystemEvent(sample.At,"Simulator paused","Operational detection suspended"));
            if(sample.Slewing&&!wasSlewing)events.Add(SystemEvent(sample.At,"Slew mode detected","Operational detection suspended"));
            wasPaused=sample.Paused;wasSlewing=sample.Slewing;return events;
        }
        void AddOnce(string key,string title,string summary,string kind="operation")
        {
            var at=key is "block-out" or "liftoff" or "touchdown" or "block-in" or "go-around"?milestone?.At??sample.At:sample.At;
            if(fired.Add(key))events.Add(Create(at,title,summary,sample,progress,plan,kind,estimatedIn) with
                {Code=key switch{"block-out"=>TrackingEventCode.BlockOut,"liftoff"=>TrackingEventCode.Liftoff,_=>TrackingEventCode.None}});
        }
        void AddStable(string key,bool condition,string title,string summary,int samples=3)
        {
            if(fired.Contains(key))return;
            if(!condition){candidates.Remove(key);return;}
            candidates[key]=candidates.GetValueOrDefault(key)+1;
            if(candidates[key]>=samples){candidates.Remove(key);AddOnce(key,title,summary);}
        }

        if(sample.EngineStates is not {Count:>0}&&sample.RunningEngineCount<0)AddStable("engine-start",sample.EnginesRunning,"Engines running","Engine start detected",2);
        AddStable("pushback",sample.OnGround&&!sample.ParkingBrake&&sample.GroundSpeedKnots is >=.5 and <10,"Pushback / initial movement",$"Ground movement began at {sample.GroundSpeedKnots:0} kt");
        AddStable("taxi",sample.OnGround&&sample.GroundSpeedKnots>=10,"Taxi started",$"Groundspeed {sample.GroundSpeedKnots:0} kt");
        AddStable("takeoff-roll",sample.OnGround&&phase==FlightPhase.TaxiOut&&sample.GroundSpeedKnots>=60,"Takeoff roll",$"Acceleration through {sample.GroundSpeedKnots:0} kt",2);
        AddStable("initial-climb",fired.Contains("liftoff")&&!sample.OnGround&&sample.VerticalSpeedFeetPerMinute>=500,"Initial climb",$"Climbing at {sample.VerticalSpeedFeetPerMinute:0} ft/min",2);
        if(previousAltitude is <10000&&sample.AltitudeFeet>=10000)AddOnce("ten-up","10,000 feet crossed","Climbing through 10,000 ft");
        var operationalAltitude=double.IsFinite(sample.PressureAltitudeFeet)?sample.PressureAltitudeFeet:sample.AltitudeFeet;
        var plannedCruise=plan?.CruiseAltitudeFeet;
        var transition=cruiseObservation.Transition;
        if(cruiseObservation.ObservedLevelFeet is { } currentObserved)reportedCruiseLevel=currentObserved;
        switch(transition.Kind)
        {
            case ObservedCruiseTransitionKind.CruiseEstablished:
            {
                var level=transition.ObservedLevelFeet!.Value;var summary=plannedCruise is null
                    ?$"Observed cruise established at FL{level/100:000} • filed cruise unavailable"
                    :Math.Abs(level-plannedCruise.Value)>600
                        ?$"Observed cruise established at FL{level/100:000} • filed FL{plannedCruise.Value/100:000}"
                        :$"Established at FL{level/100:000}";
                AddOnce("cruise","Top of climb / cruise established",summary);break;
            }
            case ObservedCruiseTransitionKind.StepClimbStarted:
                AddOnce($"step-climb-{atKey(sample.At)}","Step climb started",$"Climbing through {operationalAltitude:0} ft at {Speed(sample):0} kt");break;
            case ObservedCruiseTransitionKind.CruiseLevelChanged:
            {
                var level=transition.ObservedLevelFeet!.Value;
                AddOnce($"cruise-level-{level}","Cruise altitude changed",$"Observed cruise established at FL{level/100:000} after step climb");break;
            }
            case ObservedCruiseTransitionKind.TopOfDescent:
                AddOnce("descent","Top of descent",$"Descending from observed FL{transition.ObservedLevelFeet!.Value/100:000} at {sample.VerticalSpeedFeetPerMinute:0} ft/min");break;
        }
        if(previousAltitude is >10000&&sample.AltitudeFeet<=10000&&sample.VerticalSpeedFeetPerMinute<0)AddOnce("ten-down","10,000 feet crossed","Descending through 10,000 ft");
        var agl=double.IsFinite(sample.AltitudeAboveGroundFeet)?sample.AltitudeAboveGroundFeet:sample.AltitudeFeet;
        var destination=plan?.RoutePoints?.LastOrDefault();
        var nearDestination=destination is not null&&sample.HasPosition&&new GeoPosition(sample.LatitudeDegrees,sample.LongitudeDegrees).DistanceNm(new(destination.Latitude,destination.Longitude))<60;
        var origin=plan?.RoutePoints?.FirstOrDefault();
        var awayFromOrigin=origin is not null&&sample.HasPosition&&new GeoPosition(sample.LatitudeDegrees,sample.LongitudeDegrees).DistanceNm(new(origin.Latitude,origin.Longitude))>50;
        var arrivalContext=fired.Contains("descent")||nearDestination&&(awayFromOrigin||fired.Contains("ten-down"));
        AddStable("approach",arrivalContext&&!sample.OnGround&&agl is >0 and <=6000&&sample.VerticalSpeedFeetPerMinute<=-150,"Approach started",$"Descending through {agl:0} ft AGL",10);
        AddStable("gear-down",fired.Contains("gear-up")&&!sample.OnGround&&sample.GearExtendedRatio>=.9,"Landing gear extended","Gear indicates down",2);
        AddStable("final",fired.Contains("approach")&&!sample.OnGround&&agl is >0 and <=2500&&sample.VerticalSpeedFeetPerMinute<0,"Final approach",$"{agl:0} ft AGL • {Speed(sample):0} kt",5);
        if(!sample.OnGround)AirbornePhase=fired.Contains("final")?"Final approach":fired.Contains("approach")?"Approach":cruiseObservation.Stage switch{ObservedAirborneStage.Cruise=>plannedCruise is null?"Level flight":"Cruise",ObservedAirborneStage.Descent=>"Descent",_=>"Climb"};

        if(!sample.OnGround&&sample.GroundSpeedKnots>=80&&sample.RunningEngineCount==0)
        {
            airborneEngineOffSamples++;
            if(airborneEngineOffSamples>=10)engineSignalUnsupported=true;
        }
        else if(sample.RunningEngineCount>0){airborneEngineOffSamples=0;engineSignalUnsupported=false;}

        if(sample.EngineStates is {Count:>0} states)
        {
            foreach(var state in states)
            {
                var previous=observedEngineStates.GetValueOrDefault(state.Index,EngineCondition.Unknown);
                if(state.Condition!=previous&&state.Condition!=EngineCondition.Unknown)
                {
                    var (key,title,summary)=state.Condition switch
                    {
                        EngineCondition.Running=>("running",$"Engine {state.Index} running",$"Engine {state.Index} running confirmed"),
                        EngineCondition.Starting=>("starting",$"Engine {state.Index} starting",$"Engine {state.Index} start sequence detected"),
                        EngineCondition.Stopping=>("shutdown",$"Engine {state.Index} shutdown",$"Engine {state.Index} spool-down detected"),
                        _=>("shut-down",$"Engine {state.Index} shut down",$"Engine {state.Index} shutdown confirmed")
                    };
                    AddOnce($"engine-state-{state.Index}-{key}-{atKey(sample.At)}",title,$"{summary} • {state.Source}");
                }
                observedEngineStates[state.Index]=state.Condition;
            }
        }
        else if(sample.EngineDataValid&&sample.RunningEngineMask>=0&&!engineSignalUnsupported)
        {
            if(previousEngineMask<0){previousEngineMask=sample.RunningEngineMask;pendingEngineMask=-1;pendingEngineMaskSamples=0;}
            else if(sample.RunningEngineMask==previousEngineMask){pendingEngineMask=-1;pendingEngineMaskSamples=0;}
            else
            {
                if(pendingEngineMask==sample.RunningEngineMask)pendingEngineMaskSamples++;else{pendingEngineMask=sample.RunningEngineMask;pendingEngineMaskSamples=1;}
                if(pendingEngineMaskSamples>=2)
                {
                    for(var engine=1;engine<=4;engine++)
                    {
                        var bit=1<<(engine-1);var wasOn=(previousEngineMask&bit)!=0;var isOn=(pendingEngineMask&bit)!=0;if(wasOn==isOn)continue;
                        var repeated=isOn&&engineStartCounts[engine-1]>0;
                        var interrupted=!isOn&&!hasTouchedDown&&phase is FlightPhase.AtGate or FlightPhase.TaxiOut;
                        var title=isOn?(repeated?$"Engine {engine} restarted":$"Engine {engine} on"):(interrupted?$"Engine {engine} start interrupted":$"Engine {engine} off");
                        if(isOn)engineStartCounts[engine-1]++;
                        AddOnce($"engine-{engine}-{(isOn?"on":"off")}-{atKey(sample.At)}",title,$"Engine {engine} state {(isOn?"running":"off")} • {sample.EngineStates?.FirstOrDefault(x=>x.Index==engine)?.Source??"combustion"}");
                    }
                    previousEngineMask=pendingEngineMask;pendingEngineMask=-1;pendingEngineMaskSamples=0;
                }
            }
        }
        if(double.IsFinite(sample.SelectedFlapRatio))
        {
            var a32x=AirbusA32x(plan?.AircraftType);
            var a220Indexed=AirbusA220(plan?.AircraftType)&&sample.FlapHandlePositions is 5 or 6;
            var knownPositions=Boeing737(plan?.AircraftType)?9:Airbus(plan?.AircraftType)||AirbusA220(plan?.AircraftType)?6:0;
            var indexed=sample.FlapHandleIndex>=0&&(a32x||a220Indexed?sample.FlapHandleIndex<=5:sample.FlapHandleIndex<sample.FlapHandlePositions);
            var mapped=indexed&&(a32x||a220Indexed||sample.FlapHandlePositions==knownPositions);
            var setting=indexed?(mapped&&Boeing737(plan?.AircraftType)?Boeing737Detents[sample.FlapHandleIndex]:sample.FlapHandleIndex):FlapSetting(sample.SelectedFlapRatio,plan?.AircraftType);
            var label=indexed&&!mapped?$"Flap handle {setting}":FlapLabel(setting,plan?.AircraftType);
            if(setting==flapCandidate)flapCandidateSamples++;else{flapCandidate=setting;flapCandidateSamples=1;}
            if(reportedFlaps<0)reportedFlaps=setting;
            else if(setting!=reportedFlaps&&flapCandidateSamples>=2){reportedFlaps=setting;AddOnce($"flaps-{setting}-{atKey(sample.At)}",$"{label} selected",label);}
        }
        if(sample.GearExtendedRatio>=.9)gearWasExtended=true;
        AddStable("gear-up",!sample.OnGround&&(gearWasExtended||previousGear>=.9)&&sample.GearExtendedRatio<.1,"Landing gear retracted","Gear indicates up",2);

        if(milestone is not null)
        {
            switch(milestone.Phase)
            {
                case FlightPhase.TaxiOut:
                    AddOnce("block-out","Block-out / taxi out","Parking brake released and sustained ground movement confirmed");break;
                case FlightPhase.Airborne when hasTouchedDown:
                    fired.Remove("approach");fired.Remove("final");fired.Remove("touchdown");hasTouchedDown=false;AirbornePhase="Climb";
                    AddOnce("go-around","Go-around",$"Airborne again at {Speed(sample):0} kt","alert");break;
                case FlightPhase.Airborne:
                    if(double.IsFinite(sample.FuelTotalWeightPounds))initialFuelPounds=sample.FuelTotalWeightPounds;
                    AddOnce("liftoff","Liftoff",$"Airborne at {Speed(sample):0} kt");break;
                case FlightPhase.TaxiIn:
                    hasTouchedDown=true;
                    var touchdownSpeed=double.IsFinite(lastAirborneSpeed)?lastAirborneSpeed:Speed(sample);var touchdownRate=double.IsFinite(lastAirborneVerticalSpeed)?lastAirborneVerticalSpeed:sample.VerticalSpeedFeetPerMinute;
                    var landing=$"Touchdown at {touchdownSpeed:0} kt";
                    if(double.IsFinite(touchdownRate))landing+=$" • {touchdownRate:0} ft/min";
                    if(initialFuelPounds is not null&&double.IsFinite(sample.FuelTotalWeightPounds)&&plan?.PlannedTripFuel is { } plannedFuel)
                    {
                        var plannedPounds=plan.FuelUnits?.StartsWith("KG",StringComparison.OrdinalIgnoreCase)==true?plannedFuel*2.2046226218:plannedFuel;var difference=initialFuelPounds.Value-sample.FuelTotalWeightPounds-plannedPounds;
                        landing+=$" • fuel burn {Math.Abs(difference):0} lb {(difference<0?"below":"above")} plan";
                    }
                    AddOnce("touchdown","Touchdown",landing);break;
                case FlightPhase.Complete:AddOnce("block-in","Block-in / flight complete",CompletionSummary(sample,engineSignalUnsupported));break;
            }
        }
        AddStable("runway-vacated",hasTouchedDown&&sample.OnGround&&sample.GroundSpeedKnots<35,"Landing rollout slowed",$"Groundspeed reduced to {sample.GroundSpeedKnots:0} kt");
        AddStable("taxi-in",hasTouchedDown&&sample.OnGround&&sample.GroundSpeedKnots is >=1 and <=25,"Taxi-in",$"Taxiing to gate at {sample.GroundSpeedKnots:0} kt");
        if(sample.EngineDataValid&&sample.RunningEngineMask<0&&!engineSignalUnsupported&&hasTouchedDown&&hasSample&&previousEngines&&!sample.EnginesRunning)AddOnce("engine-shutdown","Engines shut down","All monitored engines report stopped");
        if(hasTouchedDown&&hasSample&&!previousBrake&&sample.ParkingBrake)AddOnce("parking-brake","Parking brake set","Parking brake set at stand");
        var routeDistance=FlightMetrics.DistanceToRouteNm(plan?.RoutePoints,sample);
        AddStable("route-deviation",double.IsFinite(routeDistance)&&routeDistance>25,"Off planned route",$"{routeDistance:0} NM from the SimBrief route • vectors or route changes may explain this");
        if(fired.Contains("route-deviation"))AddStable("route-rejoin",double.IsFinite(routeDistance)&&routeDistance<10,"Route rejoined",$"Aircraft returned within {routeDistance:0} NM of the SimBrief route");
        if(assignedDestination is null)assignedDestination=plan?.Destination;
        else if(plan is not null&&!plan.Destination.Equals(assignedDestination,StringComparison.OrdinalIgnoreCase)){AddOnce("destination-change-"+plan.Destination,"Destination changed",$"Assignment changed from {assignedDestination} to {plan.Destination}","alert");assignedDestination=plan.Destination;}
        if(sample.Paused&&!wasPaused)AddOnce("paused","Simulator paused","Telemetry phase advancement suspended","system");
        if(!sample.Paused&&wasPaused)AddOnce("resumed","Simulator resumed","Telemetry phase advancement resumed","system");
        if(sample.Slewing&&!wasSlewing)AddOnce("slew","Slew mode detected","Operational event detection suspended","alert");
        if(!sample.Slewing&&wasSlewing)AddOnce("slew-ended","Slew mode ended","Operational event detection resumed","system");
        if(!string.IsNullOrWhiteSpace(scenarioEvent))AddOnce("scenario-"+scenarioEvent,ScenarioTitle(scenarioEvent),scenarioEvent.Replace('_',' '),scenarioEvent.Contains("DIVERSION",StringComparison.OrdinalIgnoreCase)?"alert":"system");
        if(!sample.OnGround&&(double.IsNaN(sample.AltitudeAboveGroundFeet)||sample.AltitudeAboveGroundFeet<=150)){lastAirborneVerticalSpeed=sample.VerticalSpeedFeetPerMinute;lastAirborneSpeed=Speed(sample);}
        wasPaused=sample.Paused;wasSlewing=sample.Slewing;previousEngines=sample.EnginesRunning;previousBrake=sample.ParkingBrake;previousGear=sample.GearExtendedRatio;hasSample=true;
        if(double.IsFinite(sample.AltitudeFeet))previousAltitude=sample.AltitudeFeet;
        return events;
    }

    internal TrackingEventEntry SystemEvent(DateTimeOffset at,string title,string summary,string kind="system")=>new(at,title,summary,summary,kind);

    private TrackingEventEntry Create(DateTimeOffset at,string title,string summary,Telemetry sample,double progress,ActiveFlightPlan? plan,string kind,DateTimeOffset? estimatedIn)
    {
        var parts=new List<string>();
        if(sample.OnGround)
        {
            parts.Add($"GS {sample.GroundSpeedKnots:0} KT");
            if(title.Contains("brake",StringComparison.OrdinalIgnoreCase)||title.Contains("block",StringComparison.OrdinalIgnoreCase)||title.Contains("pushback",StringComparison.OrdinalIgnoreCase)||title.Contains("preflight",StringComparison.OrdinalIgnoreCase))parts.Add(sample.ParkingBrake?"BRAKE SET":"BRAKE RELEASED");
            if(title=="Block-in / flight complete")parts.AddRange(CompletionStatus(sample,engineSignalUnsupported));
            else if(!title.StartsWith("Engine ",StringComparison.OrdinalIgnoreCase))parts.Add(EngineStatus(sample,engineSignalUnsupported));
        }
        else
        {
            if(double.IsFinite(sample.AltitudeFeet))parts.Add($"ALT {sample.AltitudeFeet:0} FT");
            if(double.IsFinite(sample.AltitudeAboveGroundFeet)&&sample.AltitudeAboveGroundFeet<10000)parts.Add($"AGL {sample.AltitudeAboveGroundFeet:0} FT");
            if(double.IsFinite(sample.IndicatedAirspeedKnots))parts.Add($"IAS {sample.IndicatedAirspeedKnots:0} KT");
            if(double.IsFinite(sample.VerticalSpeedFeetPerMinute))parts.Add($"VS {sample.VerticalSpeedFeetPerMinute:+0;-0;0} FPM");
            if(double.IsFinite(sample.HeadingDegrees))parts.Add($"HDG {Normalize(sample.HeadingDegrees):000}°");
            if(double.IsFinite(sample.PitchDegrees))parts.Add($"PITCH {sample.PitchDegrees:0}°");
            if(double.IsFinite(sample.BankDegrees))parts.Add($"BANK {sample.BankDegrees:0}°");
        }
        if(sample.HasPosition&&title.Contains("Route",StringComparison.OrdinalIgnoreCase))parts.Add($"POS {sample.LatitudeDegrees:0.0000}, {sample.LongitudeDegrees:0.0000}");
        var remaining=FlightMetrics.RemainingDistanceNm(plan?.RoutePoints,progress,sample);
        if(double.IsFinite(remaining)&&(title.Contains("approach",StringComparison.OrdinalIgnoreCase)||title.Contains("final",StringComparison.OrdinalIgnoreCase)))parts.Add($"{remaining:0} NM TO GO");
        if(double.IsFinite(sample.FuelTotalWeightPounds)&&(title.Contains("engine",StringComparison.OrdinalIgnoreCase)||title.Contains("preflight",StringComparison.OrdinalIgnoreCase)||title.Contains("block",StringComparison.OrdinalIgnoreCase)||title.Contains("Touchdown",StringComparison.OrdinalIgnoreCase)))parts.Add($"FUEL {sample.FuelTotalWeightPounds:0} LB");
        if(estimatedIn is not null&&!sample.OnGround&&plan is not null&&FlightClock.ScheduleIsPlausible(plan,sample.At)&&
           (title.Contains("cruise",StringComparison.OrdinalIgnoreCase)||title.Contains("descent",StringComparison.OrdinalIgnoreCase)||title.Contains("approach",StringComparison.OrdinalIgnoreCase)))
        {
            var eta=estimatedIn.Value;var variance=(int)Math.Round((eta-plan.PlannedArrivalUtc).TotalMinutes);
            parts.Add($"ETA {eta.UtcDateTime:HH:mm}Z • {(variance==0?"ON TIME":$"{Math.Abs(variance)} MIN {(variance<0?"EARLY":"LATE")}")}");
        }
        return new(at,title,summary,parts.Count==0?summary:$"{summary}\n{string.Join("  •  ",parts)}",kind);
    }

    private static string EngineStatus(Telemetry sample,bool unsupported)
    {
        if(unsupported)return "ENGINE DATA UNAVAILABLE";
        if(sample.EngineStates is {Count:>0} states)
        {
            if(states.Any(x=>x.Condition==EngineCondition.Starting))return "ENGINE START IN PROGRESS";
            if(states.Any(x=>x.Condition==EngineCondition.Stopping))return "ENGINE SHUTDOWN IN PROGRESS";
            var running=states.Count(x=>x.Condition==EngineCondition.Running);
            if(running>0)return running==1?"1 ENGINE RUNNING":$"{running} ENGINES RUNNING";
            if(states.All(x=>x.Condition==EngineCondition.Off))return "ENGINES OFF";
            return "ENGINE DATA UNAVAILABLE";
        }
        return !sample.EngineDataValid?"ENGINE DATA UNAVAILABLE":sample.EnginesRunning?"ENGINES RUNNING":"ENGINES OFF";
    }

    private static bool EnhancedCompletion(Telemetry sample)=>sample.TerminalBlockInEligible==true
        &&sample.TerminalShutdown is {Support:ShutdownIntentSupport.Enhanced,Qualified:true};
    private static bool ManualCompletion(Telemetry sample)=>sample.TerminalBlockInEligible==true&&sample.PilotConfirmedBlockIn
        &&sample.TerminalShutdown?.Support==ShutdownIntentSupport.Limited;
    private static string CompletionSummary(Telemetry sample,bool unsupported)
    {
        const string parked="Stopped at gate with parking brake set";
        if(EnhancedCompletion(sample))return parked+" • terminal shutdown confirmed";
        if(ManualCompletion(sample))return parked+" • pilot-confirmed Block In";
        return (unsupported||!sample.EngineDataValid)?parked+" • engine data unavailable":parked+(sample.EnginesRunning?"":" and engines shut down");
    }
    private static IEnumerable<string> CompletionStatus(Telemetry sample,bool unsupported)
    {
        if(EnhancedCompletion(sample))
        {
            yield return "TERMINAL SHUTDOWN CONFIRMED";
            var states=sample.EngineStates;
            if(states?.Any(x=>x.Condition==EngineCondition.Stopping)==true)yield return "MECHANICAL SPOOL-DOWN CONTINUES";
            if(!sample.EngineDataValid||states is null||states.Any(x=>x.Condition==EngineCondition.Unknown))yield return "PARTIAL MECHANICAL ENGINE DATA";
            else if(states.Count>0&&states.All(x=>x.Condition==EngineCondition.Off))yield return "MECHANICAL ENGINES OFF";
            yield break;
        }
        if(ManualCompletion(sample))
        {
            yield return "PILOT-CONFIRMED BLOCK IN";
            yield return "AUTOMATIC SHUTDOWN INTENT UNAVAILABLE";
            yield break;
        }
        yield return EngineStatus(sample,unsupported);
    }

    private static bool Airbus(string? aircraft)=>aircraft?.Contains("A3",StringComparison.OrdinalIgnoreCase)==true||aircraft?.Contains("A380",StringComparison.OrdinalIgnoreCase)==true||aircraft?.Contains("AIRBUS",StringComparison.OrdinalIgnoreCase)==true||aircraft?.Contains("FENIX",StringComparison.OrdinalIgnoreCase)==true;
    private static bool AirbusA220(string? aircraft)=>aircraft?.Contains("A220",StringComparison.OrdinalIgnoreCase)==true||aircraft?.Contains("A22",StringComparison.OrdinalIgnoreCase)==true;
    private static bool AirbusA32x(string? aircraft)=>Airbus(aircraft)&&!AirbusA220(aircraft)&&aircraft?.Contains("A380",StringComparison.OrdinalIgnoreCase)!=true;
    private static bool Boeing737(string? aircraft)=>aircraft?.Contains("737",StringComparison.OrdinalIgnoreCase)==true||aircraft?.Contains("B738",StringComparison.OrdinalIgnoreCase)==true;
    private static readonly int[] Boeing737Detents=[0,1,2,5,10,15,25,30,40];
    private static int FlapSetting(double ratio,string? aircraft)
    {
        if(AirbusA220(aircraft)||Airbus(aircraft))return Math.Clamp((int)Math.Round(ratio*5,MidpointRounding.AwayFromZero),0,5);
        if(Boeing737(aircraft))return Boeing737Detents[Math.Clamp((int)Math.Round(ratio*(Boeing737Detents.Length-1),MidpointRounding.AwayFromZero),0,Boeing737Detents.Length-1)];
        return (int)Math.Clamp(Math.Round(ratio*100/5)*5,0,100);
    }
    private static string FlapLabel(int setting,string? aircraft)=>AirbusA220(aircraft)?setting==0?"Flaps UP":$"Flaps {setting}":Airbus(aircraft)?setting switch{0=>"Flaps UP",1=>"Flaps 1",2=>"Flaps 1+F",3=>"Flaps 2",4=>"Flaps 3",_=>"Flaps FULL"}:Boeing737(aircraft)?setting==0?"Flaps UP":$"Flaps {setting}":$"Flaps set to {setting}%";

    private static double Speed(Telemetry sample)=>double.IsFinite(sample.IndicatedAirspeedKnots)&&sample.IndicatedAirspeedKnots>0?sample.IndicatedAirspeedKnots:sample.GroundSpeedKnots;
    private static double Normalize(double heading)=>(heading%360+360)%360;
    private static long atKey(DateTimeOffset at)=>at.ToUnixTimeSeconds();
    private static string ScenarioTitle(string value)=>value.Contains("DIVERSION",StringComparison.OrdinalIgnoreCase)?"Diversion declared":value.Contains("CLOCK",StringComparison.OrdinalIgnoreCase)?"Simulator clock changed":value.Contains("AIRCRAFT",StringComparison.OrdinalIgnoreCase)?"Aircraft changed":"Simulation event";
}

internal static class FlightMetrics
{
    private const double EarthRadiusNm=3440.065;
    internal static double RouteDistanceNm(IReadOnlyList<FlightRoutePoint>? route)
    {
        if(route is null||route.Count<2)return double.NaN;
        double total=0;for(var index=1;index<route.Count;index++)total+=Distance(route[index-1].Latitude,route[index-1].Longitude,route[index].Latitude,route[index].Longitude);return total;
    }
    // Progress is geographical, not a lifecycle percentage. Reapply the direct
    // distance floor when reconstructing from a fraction, including short routes
    // where remaining distance can exceed the filed length and fraction is zero.
    internal static double RemainingDistanceNm(IReadOnlyList<FlightRoutePoint>? route,double progress,Telemetry? sample=null,bool complete=false)
    {
        if(complete)return 0;
        var total=RouteDistanceNm(route);
        if(!double.IsFinite(total)||!double.IsFinite(progress))return double.NaN;
        var remaining=total*(1-Math.Clamp(progress,0,1));
        return sample is {HasPosition:true}&&route is {Count:>0}
            ?Math.Max(remaining,Distance(sample.LatitudeDegrees,sample.LongitudeDegrees,route[^1].Latitude,route[^1].Longitude)):remaining;
    }
    internal static string? NearestWaypoint(IReadOnlyList<FlightRoutePoint>? route,Telemetry sample)
    {
        if(route is null||!sample.HasPosition)return null;
        return route.OrderBy(point=>Distance(sample.LatitudeDegrees,sample.LongitudeDegrees,point.Latitude,point.Longitude)).FirstOrDefault()?.Ident;
    }
    internal static double DistanceToRouteNm(IReadOnlyList<FlightRoutePoint>? route,Telemetry sample)
    {
        if(route is null||route.Count<2||!sample.HasPosition)return double.NaN;
        var best=double.PositiveInfinity;
        for(var index=1;index<route.Count;index++)
        {
            var a=route[index-1];var b=route[index];var referenceLatitude=(a.Latitude+b.Latitude+sample.LatitudeDegrees)/3*Math.PI/180;
            static double Wrap(double value){while(value>180)value-=360;while(value< -180)value+=360;return value;}
            var bx=Wrap(b.Longitude-a.Longitude)*Math.Cos(referenceLatitude);var by=b.Latitude-a.Latitude;
            var px=Wrap(sample.LongitudeDegrees-a.Longitude)*Math.Cos(referenceLatitude);var py=sample.LatitudeDegrees-a.Latitude;
            var length=bx*bx+by*by;var t=length<=0?0:Math.Clamp((px*bx+py*by)/length,0,1);
            var dx=px-t*bx;var dy=py-t*by;best=Math.Min(best,Math.Sqrt(dx*dx+dy*dy)*60);
        }
        return best;
    }
    private static double Distance(double lat1,double lon1,double lat2,double lon2)
    {
        static double Rad(double value)=>value*Math.PI/180;
        var dLat=Rad(lat2-lat1);var dLon=Rad(lon2-lon1);var a=Math.Pow(Math.Sin(dLat/2),2)+Math.Cos(Rad(lat1))*Math.Cos(Rad(lat2))*Math.Pow(Math.Sin(dLon/2),2);return 2*EarthRadiusNm*Math.Asin(Math.Min(1,Math.Sqrt(a)));
    }
}
