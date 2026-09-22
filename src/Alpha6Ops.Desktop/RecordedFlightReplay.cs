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
            ActiveFlightPlan? plan=null;PhaseDetector? detector=null;var engineEvidence=new EngineEvidenceTracker();
            var tracker=new RouteProgressTracker();var estimator=new ArrivalEstimator();var monitor=new FlightTrackingEventMonitor();
            var entries=new List<TrackingEventEntry>();var milestones=new List<FlightEvent>();
            var snapshots=new List<object>();var samples=0;var maximumJump=0d;var prior=0d;var maxAirborne=0d;DateTimeOffset? actualIn=null;
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
                detector??=new PhaseDetector(AircraftGroundProfiles.ForFamily(d.GetProperty("aircraft").GetString()),!sample.OnGround?FlightPhase.Airborne:sample.GroundSpeedKnots>=1?FlightPhase.TaxiOut:FlightPhase.AtGate);
                var milestone=detector.Observe(sample);if(milestone is not null){milestones.Add(milestone);if(milestone.Phase==FlightPhase.Complete)actualIn=milestone.At;}
                var progress=tracker.Observe(new(sample.LatitudeDegrees,sample.LongitudeDegrees),at,sample.Paused||sample.Slewing);
                var fraction=detector.Phase==FlightPhase.AtGate?0:progress?.Fraction??0;
                var remaining=FlightMetrics.RemainingDistanceNm(plan.RoutePoints,fraction);
                var estimate=estimator.Observe(sample,detector.Phase,remaining,actualIn,plan.PlannedArrivalUtc,plan.PlannedDepartureUtc,milestones.FirstOrDefault(m=>m.Phase==FlightPhase.TaxiOut)?.At);
                var events=monitor.Observe(sample,detector.Phase,milestone,fraction,plan,null,estimate);entries.AddRange(events);
                if(samples>0)maximumJump=Math.Max(maximumJump,fraction-prior);prior=fraction;
                if(!sample.OnGround)maxAirborne=Math.Max(maxAirborne,fraction);
                if(samples++%60==0||milestone is not null||events.Count>0)snapshots.Add(new{at,phase=detector.Phase.ToString(),fraction,remaining,estimate});
            }
            if(plan is null||samples==0)throw new InvalidDataException("No flight telemetry: "+file);
            var failures=new List<string>();
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
                if(!entries.Any(e=>e.Summary=="Flaps 2"&&e.Detail.Contains("FLAP SURFACES CONFIRMED",StringComparison.Ordinal)))failures.Add("JST760 Flaps 2 surface confirmation missing.");
                if(!entries.Any(e=>e.Title is "Engine 1 shutdown" or "Engine 2 shutdown"))failures.Add("JST760 shutdown transition missing.");
                if(entries.Any(e=>e.Title=="Parking brake set"&&e.Summary.Contains("secured",StringComparison.OrdinalIgnoreCase)))failures.Add("Parking brake event claims premature secured state.");
            }
            var report=new{flight=plan.FlightNumber,samples,passed=failures.Count==0,failures,maximumJump,maxAirborne,actualIn,milestones,events=entries,snapshots};
            File.WriteAllText(Path.Combine(output,plan.FlightNumber+".json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
            reports.Add(new{flight=plan.FlightNumber,samples,passed=failures.Count==0,failures,maximumJump,maxAirborne,actualIn});
            allPassed&=failures.Count==0;
        }
        File.WriteAllText(Path.Combine(output,"summary.json"),JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
        if(!allPassed)throw new InvalidOperationException("Recorded flight regression failed; see summary.json.");
    }
}
