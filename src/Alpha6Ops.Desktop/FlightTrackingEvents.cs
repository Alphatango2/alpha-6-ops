using System;
using System.Collections.Generic;
using System.Linq;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal sealed record TrackingEventEntry(DateTimeOffset At,string Title,string Summary,string Detail,string Kind="operation")
{
    public string Time => At.UtcDateTime.ToString("HH:mm:ss'Z'");
}

internal sealed class FlightTrackingEventMonitor
{
    private readonly HashSet<string> fired=new(StringComparer.Ordinal);
    private readonly Dictionary<string,int> candidates=new(StringComparer.Ordinal);
    private double? previousAltitude;
    private bool wasPaused,wasSlewing,previousEngines,previousBrake,hasSample,hasTouchedDown;
    private string? assignedDestination;

    internal IReadOnlyList<TrackingEventEntry> Observe(Telemetry sample,FlightPhase phase,FlightEvent? milestone,double progress,ActiveFlightPlan? plan,string? scenarioEvent)
    {
        var events=new List<TrackingEventEntry>();
        void AddOnce(string key,string title,string summary,string kind="operation")
        {
            if(fired.Add(key))events.Add(Create(sample.At,title,summary,sample,progress,plan,kind));
        }
        void AddStable(string key,bool condition,string title,string summary,int samples=3)
        {
            if(fired.Contains(key))return;
            if(!condition){candidates.Remove(key);return;}
            candidates[key]=candidates.GetValueOrDefault(key)+1;
            if(candidates[key]>=samples){candidates.Remove(key);AddOnce(key,title,summary);}
        }

        AddStable("engine-start",sample.EnginesRunning,"Engines running","Engine start detected",2);
        AddStable("pushback",sample.OnGround&&!sample.ParkingBrake&&sample.GroundSpeedKnots is >=.5 and <10,"Pushback / initial movement",$"Ground movement began at {sample.GroundSpeedKnots:0} kt");
        AddStable("taxi",sample.OnGround&&sample.GroundSpeedKnots>=10,"Taxi started",$"Groundspeed {sample.GroundSpeedKnots:0} kt");
        AddStable("takeoff-roll",sample.OnGround&&phase==FlightPhase.TaxiOut&&sample.GroundSpeedKnots>=60,"Takeoff roll",$"Acceleration through {sample.GroundSpeedKnots:0} kt",2);
        AddStable("initial-climb",!sample.OnGround&&sample.VerticalSpeedFeetPerMinute>=500,"Initial climb",$"Climbing at {sample.VerticalSpeedFeetPerMinute:0} ft/min");
        if(previousAltitude is <10000&&sample.AltitudeFeet>=10000)AddOnce("ten-up","10,000 feet crossed","Climbing through 10,000 ft");
        AddStable("cruise",!sample.OnGround&&sample.AltitudeFeet>=18000&&Math.Abs(sample.VerticalSpeedFeetPerMinute)<300,"Top of climb / cruise established",$"Level at {sample.AltitudeFeet:0} ft");
        AddStable("descent",!sample.OnGround&&sample.VerticalSpeedFeetPerMinute<=-500,"Top of descent",$"Descending at {sample.VerticalSpeedFeetPerMinute:0} ft/min");
        if(previousAltitude is >10000&&sample.AltitudeFeet<=10000&&sample.VerticalSpeedFeetPerMinute<0)AddOnce("ten-down","10,000 feet crossed","Descending through 10,000 ft");
        var agl=double.IsFinite(sample.AltitudeAboveGroundFeet)?sample.AltitudeAboveGroundFeet:sample.AltitudeFeet;
        AddStable("approach",!sample.OnGround&&agl is >0 and <=6000&&sample.VerticalSpeedFeetPerMinute<0,"Approach started",$"Descending through {agl:0} ft AGL");
        AddStable("gear-down",!sample.OnGround&&sample.GearExtendedRatio>=.9,"Landing gear extended","Gear indicates down",2);
        AddStable("final",!sample.OnGround&&agl is >0 and <=2500&&sample.VerticalSpeedFeetPerMinute<0,"Final approach",$"{agl:0} ft AGL • {Speed(sample):0} kt");

        if(milestone is not null)
        {
            switch(milestone.Phase)
            {
                case FlightPhase.TaxiOut:AddOnce("block-out","Block-out / taxi out","Parking brake released and sustained ground movement confirmed");break;
                case FlightPhase.Airborne when hasTouchedDown:AddOnce("go-around","Go-around",$"Airborne again at {Speed(sample):0} kt","alert");break;
                case FlightPhase.Airborne:AddOnce("liftoff","Liftoff",$"Airborne at {Speed(sample):0} kt");break;
                case FlightPhase.TaxiIn:hasTouchedDown=true;AddOnce("touchdown","Touchdown",$"Landing speed {Speed(sample):0} kt");break;
                case FlightPhase.Complete:AddOnce("block-in","Block-in / flight complete","Stopped at gate with parking brake set and engines shut down");break;
            }
        }
        AddStable("runway-vacated",hasTouchedDown&&sample.OnGround&&sample.GroundSpeedKnots<35,"Runway vacated",$"Groundspeed reduced to {sample.GroundSpeedKnots:0} kt");
        AddStable("taxi-in",hasTouchedDown&&sample.OnGround&&sample.GroundSpeedKnots is >=1 and <=25,"Taxi-in",$"Taxiing to gate at {sample.GroundSpeedKnots:0} kt");
        if(hasTouchedDown&&hasSample&&previousEngines&&!sample.EnginesRunning)AddOnce("engine-shutdown","Engines shut down","All monitored engines report stopped");
        if(hasTouchedDown&&hasSample&&!previousBrake&&sample.ParkingBrake)AddOnce("parking-brake","Parking brake set","Aircraft secured at the gate");
        var routeDistance=FlightMetrics.DistanceToRouteNm(plan?.RoutePoints,sample);
        AddStable("route-deviation",double.IsFinite(routeDistance)&&routeDistance>25,"Route deviation",$"Aircraft is {routeDistance:0} NM from the SimBrief route");
        if(fired.Contains("route-deviation"))AddStable("route-rejoin",double.IsFinite(routeDistance)&&routeDistance<10,"Route rejoined",$"Aircraft returned within {routeDistance:0} NM of the SimBrief route");
        if(assignedDestination is null)assignedDestination=plan?.Destination;
        else if(plan is not null&&!plan.Destination.Equals(assignedDestination,StringComparison.OrdinalIgnoreCase)){AddOnce("destination-change-"+plan.Destination,"Destination changed",$"Assignment changed from {assignedDestination} to {plan.Destination}","alert");assignedDestination=plan.Destination;}
        if(sample.Paused&&!wasPaused)AddOnce("paused","Simulator paused","Telemetry phase advancement suspended","system");
        if(!sample.Paused&&wasPaused)AddOnce("resumed","Simulator resumed","Telemetry phase advancement resumed","system");
        if(sample.Slewing&&!wasSlewing)AddOnce("slew","Slew mode detected","Operational event detection suspended","alert");
        if(!sample.Slewing&&wasSlewing)AddOnce("slew-ended","Slew mode ended","Operational event detection resumed","system");
        if(!string.IsNullOrWhiteSpace(scenarioEvent))AddOnce("scenario-"+scenarioEvent,ScenarioTitle(scenarioEvent),scenarioEvent.Replace('_',' '),scenarioEvent.Contains("DIVERSION",StringComparison.OrdinalIgnoreCase)?"alert":"system");
        wasPaused=sample.Paused;wasSlewing=sample.Slewing;previousEngines=sample.EnginesRunning;previousBrake=sample.ParkingBrake;hasSample=true;
        if(double.IsFinite(sample.AltitudeFeet))previousAltitude=sample.AltitudeFeet;
        return events;
    }

    internal TrackingEventEntry SystemEvent(DateTimeOffset at,string title,string summary,string kind="system")=>new(at,title,summary,summary,kind);

    private static TrackingEventEntry Create(DateTimeOffset at,string title,string summary,Telemetry sample,double progress,ActiveFlightPlan? plan,string kind)
    {
        var parts=new List<string>();
        if(sample.OnGround)
        {
            parts.Add($"GS {sample.GroundSpeedKnots:0} KT");
            parts.Add(sample.ParkingBrake?"BRAKE SET":"BRAKE RELEASED");
            parts.Add(sample.EnginesRunning?"ENGINES RUNNING":"ENGINES OFF");
        }
        else
        {
            if(double.IsFinite(sample.AltitudeFeet))parts.Add($"ALT {sample.AltitudeFeet:0} FT");
            if(double.IsFinite(sample.AltitudeAboveGroundFeet)&&sample.AltitudeAboveGroundFeet<10000)parts.Add($"AGL {sample.AltitudeAboveGroundFeet:0} FT");
            if(double.IsFinite(sample.IndicatedAirspeedKnots))parts.Add($"IAS {sample.IndicatedAirspeedKnots:0} KT");
            if(double.IsFinite(sample.VerticalSpeedFeetPerMinute))parts.Add($"VS {sample.VerticalSpeedFeetPerMinute:+0;-0;0} FPM");
            if(double.IsFinite(sample.HeadingDegrees))parts.Add($"HDG {Normalize(sample.HeadingDegrees):000}°");
        }
        if(sample.HasPosition&&title.Contains("Route",StringComparison.OrdinalIgnoreCase))parts.Add($"POS {sample.LatitudeDegrees:0.0000}, {sample.LongitudeDegrees:0.0000}");
        var remaining=FlightMetrics.RemainingDistanceNm(plan?.RoutePoints,progress);
        if(double.IsFinite(remaining)&&!sample.OnGround&&sample.GroundSpeedKnots>=60&&plan is not null&&
           (title.Contains("cruise",StringComparison.OrdinalIgnoreCase)||title.Contains("descent",StringComparison.OrdinalIgnoreCase)||title.Contains("approach",StringComparison.OrdinalIgnoreCase)))
        {
            var eta=sample.At.AddHours(remaining/Math.Max(sample.GroundSpeedKnots,100));var variance=(int)Math.Round((eta-plan.PlannedArrivalUtc).TotalMinutes);
            parts.Add($"ETA {eta.UtcDateTime:HH:mm}Z • {(variance==0?"ON TIME":$"{Math.Abs(variance)} MIN {(variance<0?"EARLY":"LATE")}")}");
        }
        var nearby=FlightMetrics.NearestWaypoint(plan?.RoutePoints,sample);
        if(nearby is not null)parts.Add($"NEAR {nearby}");
        return new(at,title,summary,parts.Count==0?summary:$"{summary}\n{string.Join("  •  ",parts)}",kind);
    }

    private static double Speed(Telemetry sample)=>double.IsFinite(sample.IndicatedAirspeedKnots)&&sample.IndicatedAirspeedKnots>0?sample.IndicatedAirspeedKnots:sample.GroundSpeedKnots;
    private static double Normalize(double heading)=>(heading%360+360)%360;
    private static string ScenarioTitle(string value)=>value.Contains("DIVERSION",StringComparison.OrdinalIgnoreCase)?"Diversion declared":value.Contains("CLOCK",StringComparison.OrdinalIgnoreCase)?"Simulator clock changed":value.Contains("AIRCRAFT",StringComparison.OrdinalIgnoreCase)?"Aircraft changed":"Flight Lab event";
}

internal static class FlightMetrics
{
    private const double EarthRadiusNm=3440.065;
    internal static double RouteDistanceNm(IReadOnlyList<FlightRoutePoint>? route)
    {
        if(route is null||route.Count<2)return double.NaN;
        double total=0;for(var index=1;index<route.Count;index++)total+=Distance(route[index-1].Latitude,route[index-1].Longitude,route[index].Latitude,route[index].Longitude);return total;
    }
    internal static double RemainingDistanceNm(IReadOnlyList<FlightRoutePoint>? route,double progress)
    {
        var total=RouteDistanceNm(route);return double.IsFinite(total)?total*(1-Math.Clamp(progress,0,1)):double.NaN;
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
