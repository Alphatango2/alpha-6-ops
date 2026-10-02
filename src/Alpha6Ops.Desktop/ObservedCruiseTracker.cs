using System;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal enum ObservedAirborneStage { Climb, Cruise, Descent }
internal enum ObservedCruiseTransitionKind { None, CruiseEstablished, StepClimbStarted, CruiseLevelChanged, TopOfDescent }

internal sealed record ObservedCruiseTrackerState(
    ObservedAirborneStage Stage,
    int? EstablishedLevelFeet,
    DateTimeOffset? EstablishedAt,
    int? PriorEstablishedLevelFeet,
    int? CandidateCenterFeet,
    double CandidateObservedSeconds,
    DateTimeOffset? LastSampleAt,
    bool QualifyingClimbSeen,
    double StepClimbObservedSeconds,
    bool StepClimbStarted,
    int? StepCandidateCenterFeet,
    double StepStableObservedSeconds,
    double DescentObservedSeconds,
    bool TopOfDescentObserved);

internal sealed record ObservedCruiseTransition(
    ObservedCruiseTransitionKind Kind,
    DateTimeOffset At,
    int? ObservedLevelFeet=null,
    int? PreviousObservedLevelFeet=null,
    double? FiledLevelFeet=null);

internal sealed record ObservedCruiseObservation(
    ObservedAirborneStage Stage,
    int? ObservedLevelFeet,
    ObservedCruiseTransition Transition);

internal sealed class ObservedCruiseTracker
{
    private ObservedAirborneStage stage=ObservedAirborneStage.Climb;
    private int? establishedLevelFeet,priorEstablishedLevelFeet,candidateCenterFeet,stepCandidateCenterFeet;
    private DateTimeOffset? establishedAt,lastSampleAt;
    private double candidateObservedSeconds,stepClimbObservedSeconds,stepStableObservedSeconds,descentObservedSeconds;
    private bool qualifyingClimbSeen,stepClimbStarted,topOfDescentObserved;

    internal ObservedCruiseTracker(ObservedCruiseTrackerState? restored=null)
    {
        if(restored is null||!Valid(restored))return;
        stage=restored.Stage;establishedLevelFeet=restored.EstablishedLevelFeet;establishedAt=restored.EstablishedAt;
        priorEstablishedLevelFeet=restored.PriorEstablishedLevelFeet;candidateCenterFeet=restored.CandidateCenterFeet;
        candidateObservedSeconds=restored.CandidateObservedSeconds;lastSampleAt=restored.LastSampleAt;
        qualifyingClimbSeen=restored.QualifyingClimbSeen;stepClimbObservedSeconds=restored.StepClimbObservedSeconds;
        stepClimbStarted=restored.StepClimbStarted;stepCandidateCenterFeet=restored.StepCandidateCenterFeet;
        stepStableObservedSeconds=restored.StepStableObservedSeconds;descentObservedSeconds=restored.DescentObservedSeconds;
        topOfDescentObserved=restored.TopOfDescentObserved;
    }

    internal ObservedCruiseTrackerState CaptureState()=>new(stage,establishedLevelFeet,establishedAt,priorEstablishedLevelFeet,
        candidateCenterFeet,candidateObservedSeconds,lastSampleAt,qualifyingClimbSeen,stepClimbObservedSeconds,
        stepClimbStarted,stepCandidateCenterFeet,stepStableObservedSeconds,descentObservedSeconds,topOfDescentObserved);

    internal ObservedCruiseObservation Observe(Telemetry sample,double? filedCruiseFeet,bool goAround)
    {
        if(goAround)ResetForGoAround();
        var none=()=>new ObservedCruiseObservation(stage,establishedLevelFeet,new ObservedCruiseTransition(ObservedCruiseTransitionKind.None,sample.At));
        var altitude=double.IsFinite(sample.PressureAltitudeFeet)?sample.PressureAltitudeFeet:sample.AltitudeFeet;
        var valid=!sample.OnGround&&!sample.Paused&&!sample.Slewing&&double.IsFinite(altitude)&&double.IsFinite(sample.VerticalSpeedFeetPerMinute)&&altitude>=18000;
        if(lastSampleAt is{} duplicate&&sample.At==duplicate)return none();
        var stepContinuityLost=lastSampleAt is{} prior&&(sample.At<prior||sample.At-prior>TimeSpan.FromSeconds(3));
        var delta=CountedDelta(sample.At);
        lastSampleAt=sample.At;
        if(!valid)
        {
            ResetPendingLevel();CancelStepClimb();descentObservedSeconds=0;
            return none();
        }
        if(stepContinuityLost&&establishedLevelFeet is not null){CancelStepClimb();descentObservedSeconds=0;}

        if(sample.VerticalSpeedFeetPerMinute>=500)qualifyingClimbSeen=true;

        if(establishedLevelFeet is null)
        {
            if(Math.Abs(sample.VerticalSpeedFeetPerMinute)>=300){ResetPendingLevel();return none();}
            if(candidateCenterFeet is null||Math.Abs(altitude-candidateCenterFeet.Value)>400)
            {
                candidateCenterFeet=(int)Math.Round(altitude);candidateObservedSeconds=0;return none();
            }
            var candidateLevel=RoundLevel(candidateCenterFeet.Value);
            var matchesFiled=filedCruiseFeet is { } filed&&Math.Abs(candidateLevel-filed)<=600;
            if(filedCruiseFeet is null||matchesFiled||qualifyingClimbSeen)candidateObservedSeconds+=delta;
            var required=matchesFiled?30:180;
            if(candidateObservedSeconds<required)return none();
            establishedLevelFeet=candidateLevel;establishedAt=sample.At;stage=ObservedAirborneStage.Cruise;
            ResetPendingLevel();descentObservedSeconds=0;topOfDescentObserved=false;
            return new(stage,establishedLevelFeet,new ObservedCruiseTransition(ObservedCruiseTransitionKind.CruiseEstablished,sample.At,establishedLevelFeet,null,filedCruiseFeet));
        }

        if(stage==ObservedAirborneStage.Descent)return none();

        var descending=altitude<=establishedLevelFeet.Value-500&&sample.VerticalSpeedFeetPerMinute<=-500;
        if(descending)
        {
            descentObservedSeconds+=delta;
            if(!topOfDescentObserved&&descentObservedSeconds>=30)
            {
                topOfDescentObserved=true;stage=ObservedAirborneStage.Descent;
                return new(stage,establishedLevelFeet,new ObservedCruiseTransition(ObservedCruiseTransitionKind.TopOfDescent,sample.At,establishedLevelFeet,null,filedCruiseFeet));
            }
        }
        else descentObservedSeconds=0;

        if(stepClimbStarted)
        {
            stepClimbObservedSeconds+=delta;
            var returned=altitude<=establishedLevelFeet.Value+250&&sample.VerticalSpeedFeetPerMinute<=0;
            if(sample.VerticalSpeedFeetPerMinute<=-300||returned||stepClimbObservedSeconds>=600)
            {CancelStepClimb();return none();}
            if(Math.Abs(sample.VerticalSpeedFeetPerMinute)<300&&altitude>=establishedLevelFeet.Value+750)
            {
                if(stepCandidateCenterFeet is null||Math.Abs(altitude-stepCandidateCenterFeet.Value)>400)
                {
                    stepCandidateCenterFeet=(int)Math.Round(altitude);stepStableObservedSeconds=0;return none();
                }
                stepStableObservedSeconds+=delta;
                if(stepStableObservedSeconds>=60)
                {
                    var previous=establishedLevelFeet.Value;var next=RoundLevel(stepCandidateCenterFeet.Value);
                    if(next>=previous+750)
                    {
                        priorEstablishedLevelFeet=previous;establishedLevelFeet=next;establishedAt=sample.At;stage=ObservedAirborneStage.Cruise;
                        CancelStepClimb();
                        return new(stage,establishedLevelFeet,new ObservedCruiseTransition(ObservedCruiseTransitionKind.CruiseLevelChanged,sample.At,next,previous,filedCruiseFeet));
                    }
                }
            }
            else {stepCandidateCenterFeet=null;stepStableObservedSeconds=0;}
            return none();
        }

        var climbing=altitude>=establishedLevelFeet.Value+100&&sample.VerticalSpeedFeetPerMinute>=500;
        if(climbing)
        {
            stepClimbObservedSeconds+=delta;
            if(stepClimbObservedSeconds>=30)
            {
                stepClimbStarted=true;stage=ObservedAirborneStage.Climb;stepClimbObservedSeconds=0;stepCandidateCenterFeet=null;stepStableObservedSeconds=0;
                return new(stage,establishedLevelFeet,new ObservedCruiseTransition(ObservedCruiseTransitionKind.StepClimbStarted,sample.At,establishedLevelFeet,null,filedCruiseFeet));
            }
        }
        else stepClimbObservedSeconds=0;
        return none();
    }

    internal void ResetForGoAround()
    {
        CancelStepClimb();stage=ObservedAirborneStage.Climb;ResetPendingLevel();descentObservedSeconds=0;topOfDescentObserved=false;
    }

    private double CountedDelta(DateTimeOffset at)
    {
        if(lastSampleAt is not { } last||at<=last)return 0;
        var seconds=(at-last).TotalSeconds;
        return seconds<=3?seconds:0;
    }

    private void ResetPendingLevel(){candidateCenterFeet=null;candidateObservedSeconds=0;}
    private void CancelStepClimb()
    {
        stepClimbObservedSeconds=0;stepClimbStarted=false;stepCandidateCenterFeet=null;stepStableObservedSeconds=0;
        if(establishedLevelFeet is not null&&stage==ObservedAirborneStage.Climb)stage=ObservedAirborneStage.Cruise;
    }
    private static int RoundLevel(double altitude)=>(int)Math.Round(altitude/1000,MidpointRounding.AwayFromZero)*1000;

    private static bool Valid(ObservedCruiseTrackerState state)
    {
        static bool Duration(double value)=>double.IsFinite(value)&&value>=0;
        static bool Level(int? value)=>value is null||value is>=18000 and<=65000;
        if(!Enum.IsDefined(state.Stage)||!Level(state.EstablishedLevelFeet)||!Level(state.PriorEstablishedLevelFeet)||
           !Level(state.CandidateCenterFeet)||!Level(state.StepCandidateCenterFeet)||!Duration(state.CandidateObservedSeconds)||
           !Duration(state.StepClimbObservedSeconds)||!Duration(state.StepStableObservedSeconds)||!Duration(state.DescentObservedSeconds))return false;
        if(state.EstablishedLevelFeet is null&&(state.Stage!=ObservedAirborneStage.Climb||state.EstablishedAt is not null||state.StepClimbStarted||state.TopOfDescentObserved))return false;
        if(state.EstablishedLevelFeet is not null&&state.EstablishedAt is null)return false;
        if(state.CandidateObservedSeconds>0&&state.CandidateCenterFeet is null)return false;
        if(state.StepStableObservedSeconds>0&&state.StepCandidateCenterFeet is null)return false;
        if(state.TopOfDescentObserved&&state.Stage!=ObservedAirborneStage.Descent)return false;
        return true;
    }
}
