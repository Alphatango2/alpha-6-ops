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
    }
}
