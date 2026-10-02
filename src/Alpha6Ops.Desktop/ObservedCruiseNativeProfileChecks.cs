using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal static class ObservedCruiseNativeProfileChecks
{
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var checks=new List<object>();var failed=0;
        void Check(bool passed,string name){checks.Add(new{passed,name});if(!passed)failed++;}
        var at=DateTimeOffset.Parse("2026-09-30T23:50:00Z");
        var tracker=new ObservedCruiseTracker();var transitions=new List<ObservedCruiseTransition>();var second=0;
        void Observe(double altitude,double verticalSpeed,bool paused=false,bool slewing=false,bool goAround=false,int advance=1)
        {
            second+=advance;var result=tracker.Observe(Sample(at.AddSeconds(second),altitude,verticalSpeed,paused,slewing),35000,goAround);
            if(result.Transition.Kind!=ObservedCruiseTransitionKind.None)transitions.Add(result.Transition);
        }
        for(var index=0;index<=30;index++)Observe(35000,0);
        Check(transitions.Count(t=>t.Kind==ObservedCruiseTransitionKind.CruiseEstablished&&t.ObservedLevelFeet==35000)==1,"DAL40 initial FL350 is established independently from filed provenance");

        ShallowClimb(35000,37000,242,50,Observe);Stable(37000,61,Observe);
        Check(transitions.Any(t=>t.Kind==ObservedCruiseTransitionKind.CruiseLevelChanged&&t.PreviousObservedLevelFeet==35000&&t.ObservedLevelFeet==37000),"DAL40 shallow FL350 to FL370 establishes FL370");

        ShallowClimb(37000,39000,231,50,Observe);Stable(39000,61,Observe);
        Check(transitions.Any(t=>t.Kind==ObservedCruiseTransitionKind.CruiseLevelChanged&&t.PreviousObservedLevelFeet==37000&&t.ObservedLevelFeet==39000),"DAL40 FL370 to FL390 preserves sequential provenance");

        ShallowClimb(39000,41000,253,50,Observe);Stable(41000,61,Observe);
        Check(transitions.Any(t=>t.Kind==ObservedCruiseTransitionKind.CruiseLevelChanged&&t.PreviousObservedLevelFeet==39000&&t.ObservedLevelFeet==41000),"DAL40 shallow FL390 to FL410 establishes FL410");
        Check(transitions.Where(t=>t.Kind is ObservedCruiseTransitionKind.CruiseEstablished or ObservedCruiseTransitionKind.CruiseLevelChanged)
            .Select(t=>t.ObservedLevelFeet).SequenceEqual(new int?[]{35000,37000,39000,41000}),"Observed cruise history is sequential FL350 FL370 FL390 FL410");
        Check(transitions.Where(t=>t.Kind is ObservedCruiseTransitionKind.CruiseEstablished or ObservedCruiseTransitionKind.CruiseLevelChanged)
            .All(t=>t.FiledLevelFeet==35000),"Filed FL350 remains immutable across observed step-climb history");

        for(var index=1;index<=150;index++)Observe(41000-index*10,-600);
        Check(transitions.Any(t=>t.Kind==ObservedCruiseTransitionKind.TopOfDescent&&t.ObservedLevelFeet==41000),"TOD derives naturally from the latest observed FL410");

        var shortClimb=Established(at);var shortTransitions=new List<ObservedCruiseTransition>();
        for(var index=1;index<=45;index++)Collect(shortClimb,Sample(at.AddSeconds(30+index),35000+index*10,550),shortTransitions);
        for(var index=1;index<=90;index++)Collect(shortClimb,Sample(at.AddSeconds(75+index),35450-index*5,-300),shortTransitions);
        Check(!shortTransitions.Any(t=>t.Kind==ObservedCruiseTransitionKind.CruiseLevelChanged),"Temporary climb that never reaches plus 750 feet cannot commit");
        Check(!shortClimb.CaptureState().StepClimbStarted,"Return toward the old level cancels pending step intent");

        var unstable=Established(at);var unstableTransitions=new List<ObservedCruiseTransition>();
        for(var index=1;index<=45;index++)Collect(unstable,Sample(at.AddSeconds(30+index),35000+index*20,600),unstableTransitions);
        for(var index=1;index<=650;index++)Collect(unstable,Sample(at.AddSeconds(75+index),35900+index*.5,350),unstableTransitions);
        Check(!unstableTransitions.Any(t=>t.Kind==ObservedCruiseTransitionKind.CruiseLevelChanged),"A displaced climb without a stable level cannot commit");
        Check(!unstable.CaptureState().StepClimbStarted,"Unstable pending step intent expires within a bounded observation window");

        foreach(var invalidation in new[]{"pause","slew","invalid","gap","descent","go-around"})
        {
            var protectedTracker=Established(at);var protectedTransitions=new List<ObservedCruiseTransition>();
            for(var index=1;index<=40;index++)Collect(protectedTracker,Sample(at.AddSeconds(30+index),35000+index*15,600),protectedTransitions);
            var time=at.AddSeconds(71);var sample=Sample(time,35600,100);
            switch(invalidation)
            {
                case "pause":sample=sample with{Paused=true};break;
                case "slew":sample=sample with{Slewing=true};break;
                case "invalid":sample=sample with{AltitudeFeet=double.NaN,PressureAltitudeFeet=double.NaN,VerticalSpeedFeetPerMinute=double.NaN};break;
                case "gap":sample=sample with{At=time.AddSeconds(10)};break;
                case "descent":sample=sample with{VerticalSpeedFeetPerMinute=-600,PressureAltitudeFeet=35400,AltitudeFeet=35400};break;
                case "go-around":Collect(protectedTracker,sample,protectedTransitions,true);sample=sample with{At=sample.At.AddSeconds(1)};break;
            }
            Collect(protectedTracker,sample,protectedTransitions);
            Check(!protectedTracker.CaptureState().StepClimbStarted,$"{invalidation} cancels pending step intent");
        }

        var recovery=Established(at);var recoveryTransitions=new List<ObservedCruiseTransition>();var recoverySecond=30;
        for(var index=1;index<=242;index++)Collect(recovery,Sample(at.AddSeconds(++recoverySecond),35000+2000d*index/242,index<=50?550:435),recoveryTransitions);
        for(var index=1;index<=61;index++)Collect(recovery,Sample(at.AddSeconds(++recoverySecond),37000,0),recoveryTransitions);
        var recoveryTime=at.AddSeconds(recoverySecond);var restored=new ObservedCruiseTracker(recovery.CaptureState());var restoredTransitions=new List<ObservedCruiseTransition>();var restoredAt=recoveryTime.AddMinutes(10);
        for(var index=1;index<=50;index++)Collect(restored,Sample(restoredAt.AddSeconds(index),37000+index*18,600),restoredTransitions);
        for(var index=1;index<=61;index++)Collect(restored,Sample(restoredAt.AddSeconds(50+index),39000,0),restoredTransitions);
        Check(restoredTransitions.Any(t=>t.Kind==ObservedCruiseTransitionKind.CruiseLevelChanged&&t.PreviousObservedLevelFeet==37000&&t.ObservedLevelFeet==39000),"Recovered observed cruise preserves step provenance for the next level");

        var pending=Established(at);for(var index=1;index<=40;index++)pending.Observe(Sample(at.AddSeconds(30+index),35000+index*15,600),35000,false);
        var pendingRestored=new ObservedCruiseTracker(pending.CaptureState());pendingRestored.Observe(Sample(at.AddMinutes(20),36000,100),35000,false);
        Check(!pendingRestored.CaptureState().StepClimbStarted,"Restart downtime supplies zero step-intent credit and an excessive gap cancels pending intent");

        File.WriteAllText(Path.Combine(directory,"observed-cruise-native.json"),JsonSerializer.Serialize(new{passed=failed==0,executed=checks.Count,failed,checks},new JsonSerializerOptions{WriteIndented=true}));
        return failed==0?0:1;
    }

    private static ObservedCruiseTracker Established(DateTimeOffset at)
    {
        var tracker=new ObservedCruiseTracker();for(var index=0;index<=30;index++)tracker.Observe(Sample(at.AddSeconds(index),35000,0),35000,false);return tracker;
    }
    private static void ShallowClimb(double from,double to,int duration,int fastSeconds,Action<double,double,bool,bool,bool,int> observe)
    {
        for(var index=1;index<=duration;index++)observe(from+(to-from)*index/duration,index<=fastSeconds?550:435,false,false,false,1);
    }
    private static void Stable(double altitude,int seconds,Action<double,double,bool,bool,bool,int> observe)
    {for(var index=0;index<seconds;index++)observe(altitude,0,false,false,false,1);}
    private static void Collect(ObservedCruiseTracker tracker,Telemetry sample,List<ObservedCruiseTransition> transitions,bool goAround=false)
    {var transition=tracker.Observe(sample,35000,goAround).Transition;if(transition.Kind!=ObservedCruiseTransitionKind.None)transitions.Add(transition);}
    private static Telemetry Sample(DateTimeOffset at,double altitude,double verticalSpeed,bool paused=false,bool slewing=false)
        =>new(at,false,470,false,true,AltitudeFeet:altitude,PressureAltitudeFeet:altitude,VerticalSpeedFeetPerMinute:verticalSpeed,Paused:paused,Slewing:slewing);
}
