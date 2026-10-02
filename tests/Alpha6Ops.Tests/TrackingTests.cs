using Alpha6Ops.Core;

internal static class TrackingTests
{
    internal static void Run(Action<bool,string> check)
    {
        var time=DateTimeOffset.Parse("2026-09-17T23:50:00Z");
        var route=new RouteProgressTracker();
        route.SetRoute([new(0,0),new(0,0),new(0,.005),new(0,1),new(0,10)]);
        route.Observe(new(0,0),time);
        var middle=route.Observe(new(0,5),time.AddMinutes(50))!;
        check(Math.Abs(middle.Fraction-.5)<.001,"short/duplicate legs and reconnect use distance-weighted progress");
        var paused=route.Observe(new(0,8),time.AddMinutes(51),true)!;
        check(paused==middle,"pause does not advance route progress");
        check(route.Observe(new(0,4),time.AddMinutes(49))==middle,"out-of-order telemetry cannot advance progress");
        var off=route.Observe(new(2,5),time.AddMinutes(51))!;
        check(off.Fraction==middle.Fraction&&off.CrossTrackNm>25&&!off.IsReliable,"off-route excursion preserves progress but withdraws distance confidence");
        check(route.Observe(new(0,8),time.AddMinutes(60))!.Fraction>.79,"off-route reconnect reacquires geographical position");
        route.SetRoute([new(0,179),new(0,-179)]);
        check(Math.Abs(route.Observe(new(0,180),time)!.Fraction-.5)<.001,"date-line route projects correctly");
        route.SetRoute([new(0,0),new(0,1),new(1,1),new(1,0),new(0,0),new(-1,0)]);
        route.Observe(new(0,0),time);
        check(route.Observe(new(0,.01),time.AddSeconds(1))!.Fraction<.01,"shared route intersection does not jump to returning leg");
        route.SetRoute([new(0,0),new(0,10)]);
        check(Math.Abs(route.Observe(new(0,4),time)!.Fraction-.4)<.001,"late connection initializes from actual position");
        var estimator=new ArrivalEstimator();
        var shortcut=new RouteProgressTracker();
        shortcut.SetRoute([new(0,0),new(-.3,0),new(-.3,.3),new(0,.3),new(0,4)]);
        shortcut.Observe(new(0,0),time);
        RouteProgress? recovered=null;
        for(var second=1;second<=700;second++) recovered=shortcut.Observe(new(0,second*.001),time.AddSeconds(second));
        check(recovered?.Fraction>.25&&recovered.IsReliable,"continuous shortcut past departure legs reacquires the flown en-route segment");
        var sample=new Telemetry(time,false,300,false,true);
        var eta=estimator.Observe(sample,FlightPhase.Airborne,100,null);
        check(eta==DateTimeOffset.Parse("2026-09-18T00:15:00Z"),"gate ETA crosses UTC midnight and includes taxi allowance");
        check(estimator.Observe(sample,FlightPhase.Airborne,null,null) is null,"invalid route distance immediately withdraws a published ETA even in the same sample");
        estimator.Reset();eta=estimator.Observe(sample,FlightPhase.Airborne,100,null);
        check(estimator.Observe(sample with{At=time.AddSeconds(1)},FlightPhase.Airborne,100-300d/3600,null)==eta,"ETA remains stable while flying at constant speed");
        var actual=time.AddMinutes(23);
        check(estimator.Observe(sample,FlightPhase.Complete,0,actual)==actual,"actual arrival is frozen at confirmed milestone");
        estimator.Reset();
        check(estimator.Observe(sample,FlightPhase.AtGate,100,null) is null,"scheduled arrival is not mislabeled as a live estimate");
        check(estimator.Observe(sample,FlightPhase.Airborne,null,null) is null,"missing route distance yields unavailable estimate");
        estimator.Reset();
        check(estimator.Observe(sample with{At=time.ToOffset(TimeSpan.FromHours(10))},FlightPhase.Airborne,100,null)==eta,"ETA respects timestamp offset without local-clock conversion");
        estimator.Reset();
        var plannedOut=time.AddMinutes(-10);var plannedIn=plannedOut.AddHours(5);
        var departureEta=estimator.Observe(sample with{GroundSpeedKnots=160},FlightPhase.Airborne,2100,null,plannedIn,plannedOut,plannedOut);
        check(departureEta==plannedIn,"five-hour flight starts with block-time estimate instead of takeoff-speed extrapolation");
        DateTimeOffset? longEta=null;
        for(var minute=1;minute<=240;minute++)
            longEta=estimator.Observe(sample with{At=time.AddMinutes(minute),GroundSpeedKnots=450},FlightPhase.Airborne,2100-minute*7.5,null,plannedIn,plannedOut,plannedOut);
        check(longEta is {} predicted && Math.Abs((predicted-time.AddMinutes(285)).TotalMinutes)<2,"five-hour cruise estimate remains stable across UTC midnight");
        estimator.Reset();
        var taxi=estimator.Observe(sample,FlightPhase.TaxiIn,0,null);
        for(var second=1;second<=180;second++)estimator.Observe(sample with{At=time.AddSeconds(second)},FlightPhase.TaxiIn,0,null);
        check(estimator.Observe(sample with{At=time.AddMinutes(3)},FlightPhase.TaxiIn,0,null)==taxi,"taxi allowance stays anchored to touchdown");
        DateTimeOffset? extended=null;
        for(var second=181;second<=540;second++)extended=estimator.Observe(sample with{At=time.AddSeconds(second)},FlightPhase.TaxiIn,0,null);
        check(extended>=time.AddMinutes(9),"extended taxi updates expired estimate");
        ArrivalRecoveryChecks(check,time);
        RouteEndpointChecks(check,time);
        ArrivalValidityChecks(check,time);
    }

    private static void ArrivalRecoveryChecks(Action<bool,string> check,DateTimeOffset time)
    {
        var sample=new Telemetry(time,false,300,false,true);
        foreach(var scenario in new[]{("climb",180d,140d),("cruise",450d,240d),("descent",260d,80d)})
        {
            var original=new ArrivalEstimator();var expected=original.Observe(sample with{GroundSpeedKnots=scenario.Item2},FlightPhase.Airborne,scenario.Item3,null);
            var state=original.CaptureState();var restored=new ArrivalEstimator();
            check(restored.TryRestore(state)&&restored.Observe(sample with{At=time.AddSeconds(1),GroundSpeedKnots=scenario.Item2},FlightPhase.Airborne,scenario.Item3-.05,null)==expected,
                $"arrival estimator capture/restore preserves {scenario.Item1} Airborne prediction and anti-churn");
        }
        var taxiOriginal=new ArrivalEstimator();var taxiExpected=taxiOriginal.Observe(sample with{OnGround=true},FlightPhase.TaxiIn,0,null);
        taxiOriginal.Observe(sample with{At=time.AddMinutes(2),OnGround=true},FlightPhase.TaxiIn,0,null);
        var taxiRestored=new ArrivalEstimator();
        check(taxiRestored.TryRestore(taxiOriginal.CaptureState())&&taxiRestored.Observe(sample with{At=time.AddMinutes(2).AddSeconds(1),OnGround=true},FlightPhase.TaxiIn,0,null)==taxiExpected,
            "arrival estimator recovery preserves the original Taxi In allowance anchor");
        var midnightOriginal=new ArrivalEstimator();var midnightExpected=midnightOriginal.Observe(sample,FlightPhase.Airborne,100,null);
        var midnightRestored=new ArrivalEstimator();
        check(midnightRestored.TryRestore(midnightOriginal.CaptureState())&&midnightRestored.Observe(sample with{At=time.AddSeconds(1)},FlightPhase.Airborne,99.95,null)==midnightExpected,
            "arrival estimator recovery preserves an ETA across UTC midnight");
        var unavailableOriginal=new ArrivalEstimator();unavailableOriginal.Observe(sample,FlightPhase.Airborne,100,null);
        check(unavailableOriginal.Observe(sample,FlightPhase.Airborne,null,null) is null,"arrival recovery fixture explicitly withdraws its published estimate");
        var unavailableRestored=new ArrivalEstimator();
        check(unavailableRestored.TryRestore(unavailableOriginal.CaptureState())&&unavailableRestored.Observe(sample with{At=time.AddSeconds(1),Paused=true},FlightPhase.Airborne,null,null) is null
            &&unavailableRestored.Observe(sample with{At=time.AddSeconds(2)},FlightPhase.Airborne,99.9,null) is not null,
            "persisted unavailable ETA stays unavailable until a valid current route estimate returns");
        var valid=new ArrivalEstimator();valid.Observe(sample,FlightPhase.Airborne,100,null);var validState=valid.CaptureState();
        var invalidStates=new[]{
            validState with{AirborneSince=validState.LastSample!.Value.AddSeconds(1)},
            validState with{Published=validState.LastSample!.Value.AddSeconds(-1)},
            validState with{Published=validState.LastSample!.Value.AddHours(37)},
            validState with{SmoothedSpeedKnots=double.NaN},
            validState with{SmoothedSpeedKnots=0},
            validState with{SmoothedSpeedKnots=1600},
            validState with{LastPhase=FlightPhase.AtGate},
            validState with{LastSample=null}
        };
        check(invalidStates.All(state=>!valid.TryRestore(state)&&valid.CaptureState()==validState),
            "invalid future/past anchors, speeds and phase-incoherent ETA states are rejected without mutation");
        var empty=new ArrivalEstimator();check(empty.TryRestore(null)&&empty.CaptureState()==new ArrivalEstimatorState(null,null,null,null,null,null),
            "null ETA recovery state is accepted as an empty backward-compatible checkpoint");
    }

    private static void RouteEndpointChecks(Action<bool,string> check,DateTimeOffset time)
    {
        var tracker=new RouteProgressTracker();
        tracker.SetRoute([new(0,0),new(0,10)]);
        var fifty=tracker.Observe(new(0,9.16723),time)!;
        check(fifty.IsReliable&&fifty.RemainingNm is >49.99 and <50.01&&fifty.Fraction is >.916 and <.918,"50 NM enroute remains geographical progress, not completion");
        var twenty=tracker.Observe(new(0,9.666892),time.AddMinutes(5))!;
        check(twenty.IsReliable&&twenty.RemainingNm is >19.99 and <20.01&&twenty.Fraction is >.966 and <.967,"20 NM enroute retains distance and incomplete route progress");
        var cross=tracker.Observe(new(.333108,10),time.AddMinutes(10))!;
        check(cross.IsReliable&&cross.RemainingNm is >19.99 and <20.01&&cross.Fraction is >.966 and <.967,"final-leg cross-track projection cannot consume the 20 NM still to destination");
        var final=tracker.Observe(new(0,9.995),time.AddMinutes(15))!;
        check(final.RemainingNm is >.300 and <.301&&final.Fraction<1,"short final retains sub-mile geographical distance");
        var touchdown=tracker.Observe(new(0,9.998),time.AddSeconds(901))!;
        check(touchdown.RemainingNm is >.120 and <.121&&touchdown.Fraction<1,"touchdown before the airport reference point does not imply route or block-in completion");
        var taxi=tracker.Observe(new(.001,10),time.AddSeconds(902))!;
        check(taxi.RemainingNm is >.060 and <.061&&taxi.Fraction<1,"taxi-in retains geographical distance until separately confirmed completion");
        var endpoint=tracker.Observe(new(0,10),time.AddSeconds(903))!;
        check(endpoint.RemainingNm==0&&endpoint.Fraction==1,"route 100 percent means the geographical endpoint is reached, independently of lifecycle");
        var overshoot=tracker.Observe(new(0,10.333108),time.AddSeconds(904))!;
        check(overshoot.IsReliable&&overshoot.RemainingNm is >19.99 and <20.01&&overshoot.Fraction<endpoint.Fraction,"overshoot or go-around restores remaining distance after reaching the endpoint");
        var unavailable=tracker.Observe(new(1,10),time.AddSeconds(905))!;
        check(!unavailable.IsReliable&&unavailable.Fraction==overshoot.Fraction,"unreliable route excursion preserves only last-known progress");
        var rejoined=tracker.Observe(new(0,9.9),time.AddMinutes(20))!;
        check(rejoined.IsReliable&&rejoined.RemainingNm is >6 and <6.01&&rejoined.Fraction<1,"route rejoin restores a current nonzero distance estimate");
        tracker.SetRoute([new(0,0),new(0,.1)]);
        var shortRoute=tracker.Observe(new(0,.4),time)!;
        check(shortRoute.IsReliable&&shortRoute.RemainingNm is >18 and <18.02&&shortRoute.Fraction==0,"overshoot of a short route can exceed filed length without fabricating completed progress");

        tracker.SetRoute([new(-37.673333,144.843333),new(-37.561861,144.817889),new(-37.196667,143.271611),
            new(-36.846964,142.143735),new(-36.392778,140.741806),new(-36.080556,140.250694),
            new(-35.807963,139.49982),new(-35.650111,139.073083),new(-35.202472,138.511639),
            new(-35.15575,138.453556),new(-35.081667,138.414722),new(-35.009444,138.442778),new(-34.945,138.530556)]);
        var jst776=tracker.Observe(new(-34.95114986094338,138.93988953519647),time)!;
        check(jst776.IsReliable&&jst776.RemainingNm is >20.148 and <20.149&&jst776.Fraction is >.944 and <.946,
            "JST776 recorded 22:11:45 endpoint projection retains its actual 20.148 NM to YPAD");
    }

    private static void ArrivalValidityChecks(Action<bool,string> check,DateTimeOffset time)
    {
        var estimator=new ArrivalEstimator();
        var sample=new Telemetry(time,false,300,false,true);
        var airborne=estimator.Observe(sample,FlightPhase.Airborne,5,null);
        var touchdown=estimator.Observe(sample with{At=time.AddSeconds(1),OnGround=true},FlightPhase.TaxiIn,0,null);
        check(airborne==time.AddMinutes(6)&&touchdown==time.AddMinutes(5),"touchdown publishes the taxi prediction even within ordinary ETA hysteresis");
        var expired=0;
        for(var second=2;second<=900;second++)
        {
            var now=time.AddSeconds(second);
            var estimate=estimator.Observe(sample with{At=now,OnGround=true},FlightPhase.TaxiIn,0,null);
            if(estimate is null||estimate<=now)expired++;
        }
        check(expired==0,"every extended taxi prediction remains strictly future despite minute rounding and publication hysteresis");
        estimator.Reset();
        var stalePlan=estimator.Observe(sample,FlightPhase.Airborne,100,null,time.AddMinutes(-5),time.AddHours(-2),time.AddHours(-2));
        check(stalePlan==time.AddMinutes(25),"expired schedule blend falls back to the future observed airborne gate estimate");
        estimator.Reset();
        estimator.Observe(sample,FlightPhase.TaxiIn,0,null);
        var paused=estimator.Observe(sample with{At=time.AddMinutes(6),Paused=true},FlightPhase.TaxiIn,0,null);
        check(paused is null,"paused telemetry cannot present an expired prediction as current");
        var actual=time.AddMinutes(7).AddSeconds(12);
        check(estimator.Observe(sample with{At=time.AddMinutes(10)},FlightPhase.Complete,0,actual)==actual,"completed ETA is exact actual IN and may correctly be in the past");
        check(estimator.Observe(sample with{At=time.AddMinutes(10)},FlightPhase.Complete,0,null) is null,"completed flight without actual IN has no fabricated estimate");
    }
}
