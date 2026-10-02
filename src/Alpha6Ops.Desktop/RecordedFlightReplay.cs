using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

// Runs the production progress, arrival, phase and event components against untouched journals.
// Explicit diagnostic entry point; never writes into the pilot's active flight or logbook.
internal static class RecordedFlightReplay
{
    internal static void Run(string manifest, string output)
    {
        Directory.CreateDirectory(output);
        var reports = new List<object>();
        var allPassed=true;
        foreach(var file in JsonSerializer.Deserialize<string[]>(File.ReadAllText(manifest))!)
        {
            // New live journals retain the normalized policy decision separately
            // from raw mechanical telemetry. Old journals deliberately retain
            // their original phase semantics; never manufacture command intent.
            var terminalDecisions=ReadTerminalDecisions(file);
            ActiveFlightPlan? plan=null;PhaseDetector? detector=null;var engineEvidence=new EngineEvidenceTracker();
            var tracker=new RouteProgressTracker();var estimator=new ArrivalEstimator();var monitor=new FlightTrackingEventMonitor();
            var entries=new List<TrackingEventEntry>();var milestones=new List<FlightEvent>();
            var snapshots=new List<object>();var samples=0;var maximumJump=0d;var prior=0d;var maxAirborne=0d;DateTimeOffset? actualIn=null;
            var prematureEndpointSamples=0;var understatedRemainingSamples=0;var expiredEstimateSamples=0;var completionMismatchSamples=0;
            var maximumRemainingShortfallNm=0d;var maximumExpiredSeconds=0d;
            var calculationFailures=new List<object>();
            foreach(var line in File.ReadLines(file))
            {
                using var document=JsonDocument.Parse(line);var root=document.RootElement;var kind=root.GetProperty("kind").GetString();var d=root.GetProperty("detail");
                if(kind is "flight_assignment" or "flight_assignment_changed")
                {
                    plan=d.Deserialize<ActiveFlightPlan>();tracker.SetRoute(plan?.RoutePoints?.Select(p=>new GeoPosition(p.Latitude,p.Longitude))??[]);continue;
                }
                if(kind!="telemetry"||plan is null)continue;
                var at=root.GetProperty("simulatorUtc").GetDateTimeOffset();
                double N(string key)=>d.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.Number?v.GetDouble():double.NaN;
                bool B(string key)=>d.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.True;
                int I(string key)=>double.IsFinite(N(key))?(int)N(key):-1;
                var engineReadings=d.TryGetProperty("engineReadings",out var er)?er.Deserialize<EngineReading[]>():null;
                var engineStates=engineReadings is {Length:>0}?engineEvidence.Observe(engineReadings,at):d.TryGetProperty("engineStates",out var es)?es.Deserialize<EngineAssessment[]>():null;
                var sample=new Telemetry(at,B("onGround"),N("groundSpeedKnots"),B("parkingBrake"),B("enginesRunning"),B("paused"),B("slewing"),
                    N("latitude"),N("longitude"),N("altitudeFeet"),N("headingDegrees"),N("indicatedAirspeedKnots"),N("verticalSpeedFeetPerMinute"),N("gearExtendedRatio"),N("altitudeAboveGroundFeet"),N("flapsExtendedRatio"),N("pitchDegrees"),N("bankDegrees"),N("fuelTotalWeightPounds"),I("runningEngineCount"),I("runningEngineMask"),N("pressureAltitudeFeet"),EngineDataValid:!d.TryGetProperty("engineDataValid",out var validity)||validity.ValueKind==JsonValueKind.True,FlapHandleRatio:N("flapHandleRatio"),FlapHandleIndex:I("flapHandleIndex"),FlapHandlePositions:I("flapHandlePositions"),LeftFlapDegrees:N("leftFlapDegrees"),RightFlapDegrees:N("rightFlapDegrees"),LeftSlatDegrees:N("leftSlatDegrees"),RightSlatDegrees:N("rightSlatDegrees"),EngineReadings:engineReadings,EngineStates:engineStates);
                if(d.TryGetProperty("terminalPolicyVersion",out _))
                {
                    terminalDecisions.TryGetValue(root.GetProperty("sequence").GetInt64(),out var decision);
                    sample=sample with{TerminalBlockInEligible=decision.Eligible,PilotConfirmedBlockIn=decision.Manual};
                }
                detector??=new PhaseDetector(AircraftGroundProfiles.ForFamily(d.GetProperty("aircraft").GetString()),!sample.OnGround?FlightPhase.Airborne:sample.GroundSpeedKnots>=1?FlightPhase.TaxiOut:FlightPhase.AtGate);
                var milestone=detector.Observe(sample);if(milestone is not null){milestones.Add(milestone);if(milestone.Phase==FlightPhase.Complete)actualIn=milestone.At;}
                var progress=tracker.Observe(new(sample.LatitudeDegrees,sample.LongitudeDegrees),at,sample.Paused||sample.Slewing);
                var fraction=detector.Phase==FlightPhase.AtGate?0:detector.Phase==FlightPhase.Complete?1:progress?.Fraction??0;
                var reliable=detector.Phase is FlightPhase.AtGate or FlightPhase.Complete || progress is{IsReliable:true}&&(progress.At==at||sample.Paused||sample.Slewing);
                double? remaining=reliable?FlightMetrics.RemainingDistanceNm(plan.RoutePoints,fraction,sample,detector.Phase==FlightPhase.Complete):null;
                var estimate=estimator.Observe(sample,detector.Phase,remaining,actualIn,plan.DispatchEstimatedArrivalUtc??plan.PlannedArrivalUtc,plan.PlannedDepartureUtc,milestones.FirstOrDefault(m=>m.Phase==FlightPhase.TaxiOut)?.At);
                var destinationDistance=sample.HasPosition&&plan.RoutePoints is {Count:>0} points
                    ?new GeoPosition(sample.LatitudeDegrees,sample.LongitudeDegrees).DistanceNm(new(points[^1].Latitude,points[^1].Longitude)):double.NaN;
                // Route progress is geographical, so vectors/go-arounds may reduce it.
                // Check every sample (not only saved minute snapshots) against actual
                // destination geometry; lifecycle Complete separately closes the leg.
                var premature=false;var understated=false;
                if(reliable&&detector.Phase is not (FlightPhase.AtGate or FlightPhase.Complete)&&!sample.Paused&&!sample.Slewing&&double.IsFinite(destinationDistance))
                {
                    premature=fraction>=1&&destinationDistance>1e-6;
                    understated=remaining is {} distance&&distance+1e-6<destinationDistance;
                    if(premature)prematureEndpointSamples++;
                    if(understated){understatedRemainingSamples++;maximumRemainingShortfallNm=Math.Max(maximumRemainingShortfallNm,destinationDistance-remaining!.Value);}
                }
                var expired=actualIn is null&&estimate is {} predicted&&predicted<=at;
                if(expired){expiredEstimateSamples++;maximumExpiredSeconds=Math.Max(maximumExpiredSeconds,(at-estimate!.Value).TotalSeconds);}
                var completionMismatch=detector.Phase==FlightPhase.Complete&&(fraction!=1||remaining!=0||estimate!=actualIn);
                if(completionMismatch)completionMismatchSamples++;
                if((premature||understated||expired||completionMismatch)&&calculationFailures.Count<20)
                    calculationFailures.Add(new{at,phase=detector.Phase.ToString(),fraction,remaining,destinationDistance,estimate,premature,understated,expired,completionMismatch});
                var events=monitor.Observe(sample,detector.Phase,milestone,reliable?fraction:double.NaN,plan,null,estimate);entries.AddRange(events);
                if(samples>0)maximumJump=Math.Max(maximumJump,fraction-prior);prior=fraction;
                if(!sample.OnGround)maxAirborne=Math.Max(maxAirborne,fraction);
                if(samples++%60==0||milestone is not null||events.Count>0)snapshots.Add(new{at,phase=detector.Phase.ToString(),fraction,remaining,destinationDistance,reliable,estimate});
            }
            if(plan is null||samples==0)throw new InvalidDataException("No flight telemetry: "+file);
            var failures=new List<string>();
            if(prematureEndpointSamples>0)failures.Add($"Premature geographical endpoint completion in {prematureEndpointSamples} samples.");
            if(understatedRemainingSamples>0)failures.Add($"Remaining distance below actual destination distance in {understatedRemainingSamples} samples.");
            if(expiredEstimateSamples>0)failures.Add($"Expired gate-IN prediction in {expiredEstimateSamples} samples.");
            if(completionMismatchSamples>0)failures.Add($"Completed progress/distance/actual IN mismatch in {completionMismatchSamples} samples.");
            if(maxAirborne<.8)failures.Add("Airborne progress did not reach 80%.");
            if(maximumJump>.1)failures.Add("Progress jumped more than 10 percentage points in one sample.");
            if(!entries.Any(e=>e.Title=="Approach started"))failures.Add("Approach event missing.");
            if(!entries.Any(e=>e.Title=="Final approach"))failures.Add("Final approach event missing.");
            if(actualIn is null)failures.Add("Block-in missing.");
            if(plan.FlightNumber=="QFA124"&&entries.Any(e=>(e.Title.Contains("cruise",StringComparison.OrdinalIgnoreCase)||e.Title=="Sustained level flight"||e.Title=="Step climb started")&&e.At<DateTimeOffset.Parse("2026-09-16T21:45:00Z")))failures.Add("Departure level-off misidentified as cruise/step climb.");
            if(plan.FlightNumber=="JST721")
            {
                if(!entries.Any(e=>e.Title=="Landing gear retracted")||!entries.Any(e=>e.Title=="Landing gear extended"))failures.Add("Gear cycle missing.");
                if(actualIn?.ToString("HH:mm")!="03:57")failures.Add("Actual IN differs from recorded flight.");
                foreach(var pair in new[]{(FlightPhase.TaxiOut,"Block-out / taxi out"),(FlightPhase.Airborne,"Liftoff"),(FlightPhase.TaxiIn,"Touchdown"),(FlightPhase.Complete,"Block-in / flight complete")})
                    if(entries.FirstOrDefault(e=>e.Title==pair.Item2)?.At!=milestones.FirstOrDefault(e=>e.Phase==pair.Item1)?.At)failures.Add("Milestone timestamp differs: "+pair.Item2);
            }
            if(plan.FlightNumber=="JST760")
            {
                if(actualIn?.ToString("yyyy-MM-dd HH:mm:ss")!="2026-09-20 23:06:32")failures.Add("Actual IN differs from JST760 recording.");
                if(entries.Any(e=>e.Summary.StartsWith("Flap handle ",StringComparison.OrdinalIgnoreCase)))failures.Add("A32x raw flap handle label remained in JST760 replay.");
                if(!entries.Any(e=>e.Title=="Flaps 2 selected"))failures.Add("JST760 Flaps 2 selection missing.");
                if(!entries.Any(e=>e.Title is "Engine 1 shutdown" or "Engine 2 shutdown"))failures.Add("JST760 shutdown transition missing.");
                if(entries.Any(e=>e.Title=="Parking brake set"&&e.Summary.Contains("secured",StringComparison.OrdinalIgnoreCase)))failures.Add("Parking brake event claims premature secured state.");
            }
            if(plan.FlightNumber=="QLK1274")
            {
                if(actualIn?.ToString("yyyy-MM-dd HH:mm:ss")!="2026-09-21 23:22:22")failures.Add("QLK1274 actual IN changed.");
                var final=entries.FirstOrDefault(e=>e.Title=="Final approach");
                if(final is null||!final.Detail.Contains("7 NM TO GO")||!final.Detail.Contains("ETA 23:20Z"))failures.Add("QLK1274 final approach distance or ETA did not converge.");
                if(entries.Any(e=>e.Summary.StartsWith("Flap handle ")||e.Detail.Contains("SURFACE POSITION")||e.Summary.Contains("combustion loss")))failures.Add("QLK1274 unsupported flap or engine claims remain.");
                if(!entries.Any(e=>e.Title=="Flaps 5 selected")||!entries.Any(e=>e.Title=="Flaps UP selected"))failures.Add("QLK1274 full flap selection cycle missing.");
            }
            if(plan.FlightNumber=="DAL40")
            {
                if(samples!=54061)failures.Add($"DAL40 endurance sample count changed: {samples}.");
                var observedLevels=entries.Where(e=>e.Title is "Top of climb / cruise established" or "Cruise altitude changed")
                    .Select(e=>new[]{"FL350","FL370","FL390","FL410"}.FirstOrDefault(level=>e.Summary.Contains(level,StringComparison.Ordinal)))
                    .Where(level=>level is not null).ToArray();
                if(!observedLevels.SequenceEqual(new[]{"FL350","FL370","FL390","FL410"}))failures.Add("DAL40 observed-cruise provenance is not FL350, FL370, FL390, FL410.");
                if(entries.FirstOrDefault(e=>e.Title=="Top of descent")?.Summary.Contains("FL410",StringComparison.Ordinal)!=true)failures.Add("DAL40 TOD does not derive from observed FL410.");
                if(!milestones.Select(e=>e.Phase).SequenceEqual(new[]{FlightPhase.TaxiOut,FlightPhase.Airborne,FlightPhase.TaxiIn,FlightPhase.Complete}))failures.Add("DAL40 OOOI sequence changed.");
                if(actualIn!=DateTimeOffset.Parse("2026-10-01T13:21:14.171875Z"))failures.Add("DAL40 actual IN changed.");
            }
            var calculationChecks=new{prematureEndpointSamples,understatedRemainingSamples,expiredEstimateSamples,completionMismatchSamples,maximumRemainingShortfallNm,maximumExpiredSeconds};
            var report=new{flight=plan.FlightNumber,samples,passed=failures.Count==0,failures,maximumJump,maxAirborne,actualIn,calculationChecks,calculationFailures,milestones,events=entries,snapshots};
            File.WriteAllText(Path.Combine(output,plan.FlightNumber+".json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
            reports.Add(new{flight=plan.FlightNumber,samples,passed=failures.Count==0,failures,maximumJump,maxAirborne,actualIn,calculationChecks});
            allPassed&=failures.Count==0;
        }
        File.WriteAllText(Path.Combine(output,"summary.json"),JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
        if(!allPassed)throw new InvalidOperationException("Recorded flight regression failed; see summary.json.");
    }
    internal static Dictionary<long,(bool Eligible,bool Manual)> ReadTerminalDecisions(string file)
    {
        var result=new Dictionary<long,(bool,bool)>();long? telemetry=null;
        foreach(var line in File.ReadLines(file))
        {
            using var document=JsonDocument.Parse(line);var root=document.RootElement;var kind=root.GetProperty("kind").GetString();var detail=root.GetProperty("detail");
            if(kind=="telemetry")telemetry=detail.TryGetProperty("terminalPolicyVersion",out var version)&&version.GetInt32()==1?root.GetProperty("sequence").GetInt64():null;
            if(kind=="tracking_state"&&telemetry is {} sequence&&detail.TryGetProperty("terminalPolicyVersion",out var policy)&&policy.GetInt32()==1)
            {
                result[sequence]=(detail.TryGetProperty("TerminalBlockInEligible",out var allowed)&&allowed.ValueKind==JsonValueKind.True,
                    detail.TryGetProperty("PilotConfirmedBlockIn",out var manual)&&manual.ValueKind==JsonValueKind.True);
                telemetry=null;
            }
        }
        return result;
    }
}
