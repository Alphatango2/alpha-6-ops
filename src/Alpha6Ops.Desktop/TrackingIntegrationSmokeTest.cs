using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal static class TrackingIntegrationSmokeTest
{
    internal static async Task RunAsync(MainWindow window,string directory)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        void Set(string name,object? value)=>FlightSessionDiagnosticAccess.Set(window,name,value);
        var reset=(Action)(()=>FlightSessionDiagnosticAccess.Invoke(window,"ResetLiveTrackingState"));
        var resetIdentity=(Action)(()=>FlightSessionDiagnosticAccess.Invoke(window,"ResetLiveIdentity"));
        var observe=(Action<LiveReading>)(value=>FlightSessionDiagnosticAccess.Invoke(window,"ObserveLive",value));
        var checks=new List<string>();
        void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);checks.Add(label);}
        reset();resetIdentity();
        var at=DateTimeOffset.Parse("2026-09-17T02:05:00Z");
        var plan=new ActiveFlightPlan("JST721","VH-TEST","YSSY","YMHB",at,at.AddMinutes(128),RoutePoints:[new("YSSY",-33.946,151.177111),new("TESAT",-33.943806,151.182583),new("YMHB",-42.836111,147.510278)],AircraftType:"FENIX A320",CruiseAltitudeFeet:32000,DepartureUtcOffsetMinutes:600,ArrivalUtcOffsetMinutes:600);
        Set("activePlan",plan);Set("trackingEventMonitor",new FlightTrackingEventMonitor());
        using var cancellation=new CancellationTokenSource();Set("liveCancellation",cancellation);
        try
        {
            window.ShowDashboard();
            for(var i=0;i<=40;i++)
            {
                var fraction=i*.01;var p=new GeoPosition(-33.946+(-42.836111+33.946)*fraction,151.177111+(147.510278-151.177111)*fraction);
                var sample=new Telemetry(at.AddMinutes(i),false,450,false,true,LatitudeDegrees:p.Latitude,LongitudeDegrees:p.Longitude,AltitudeFeet:32000,PressureAltitudeFeet:32000,AltitudeAboveGroundFeet:30000,VerticalSpeedFeetPerMinute:0,GearExtendedRatio:0);
                observe(new("FenixA320 IAE SL",sample,new(p,"VH-TEST",new("YSSY","YMHB"),true)));
            }
            var value=window.RecoveredRouteProgress??0;
            Check(value is >.35 and <.45,"native null-progress readings advance before tracking page is opened");
            Check(Math.Abs((double)window.ReplayProgress.GetAnimationBaseValue(RangeBase.ValueProperty)-value*100)<.01,"hero consumes shared geographic progress");
            window.ShowFlightTracking();window.UpdateLayout();
            Check(window.FlightTrackingView.ProgressText.Text==$"{value*100:0}%","tracking footer agrees with hero progress after navigation");
            Check(window.FlightTrackingView.ActualOutText.Text=="—","late connection does not invent an actual OUT time");
            Check(window.FlightTrackingView.ArrivalText.Text.TrimEnd('Z')==window.HeroArrivalText.Text,"hero and tracking footer share published gate-IN estimate");
            window.ApplyDispatchGates("24","A12");
            Check(window.FlightTrackingView.DepartureGateText.Text=="24"&&window.FlightTrackingView.ArrivalGateText.Text=="A12","dispatch gate assignments render compactly during flight");
            Check(window.RecoveredRouteProgress==value,"gate assignment preserves in-flight progress");
            Check(ActiveFlightPlanStore.Load(directory)?.ArrivalGate=="A12","dispatch gate assignment persists locally");
            var order=new[]{new TrackingEventEntry(at.AddSeconds(2),"Later","",""),new TrackingEventEntry(at,"Earlier","","")};
            window.FlightTrackingView.Render(plan,null,"CLIMB","",null,null,null,40,order,true);
            Check(((TrackingEventEntry)window.FlightTrackingView.EventList.Items[0]).Title=="Later","backdated milestones display in timestamp order");
            var shortPlan=plan with{RoutePoints=[new("START",0,0),new("END",0,.1)]};
            var shortTelemetry=new Telemetry(at,false,140,false,true,LatitudeDegrees:0,LongitudeDegrees:.4);
            window.FlightTrackingView.Render(shortPlan,null,"AIRBORNE","",null,null,null,0,[],true,shortTelemetry,0);
            Check(window.FlightTrackingView.DistanceRemainingText.Text=="18 NM","tracking footer preserves distance beyond filed length after a short-route overshoot");
            shortTelemetry=shortTelemetry with{LongitudeDegrees=.09999};
            window.FlightTrackingView.Render(shortPlan,null,"FINAL","",null,null,null,99.99,[],true,shortTelemetry,.9999);
            Check(window.FlightTrackingView.ProgressText.Text=="99.9%"&&window.FlightTrackingView.DistanceRemainingText.Text=="<0.1 NM","short final cannot round geographical progress to 100 percent or positive distance to zero");
            window.FlightTrackingView.Render(shortPlan,null,"AIRBORNE","",null,null,null,100,[],true,shortTelemetry,1,routeReliable:false);
            Check(window.FlightTrackingView.ProgressText.Text=="—"&&window.FlightTrackingView.DistanceRemainingText.Text=="— NM","unreliable route position withdraws current progress and distance labels");
            window.FlightTrackingView.Render(shortPlan,null,"COMPLETE","",at,at.AddHours(1),null,100,[],true,shortTelemetry,1,complete:true);
            Check(window.FlightTrackingView.ProgressText.Text=="100%"&&window.FlightTrackingView.DistanceRemainingText.Text=="0 NM","confirmed block-in closes route progress and distance independently of airport reference-point offset");
            window.ShowFlightTracking();
            Check(!window.FlightTrackingView.ScheduledOutText.Text.Contains('\n') && window.FlightTrackingView.ScheduledOutText.Text.EndsWith("Z"),"tracking schedule displays concise UTC only");
            foreach(var width in new[]{1920,1366,1280,1100})
            {
                window.WindowState=WindowState.Normal;window.Width=width;window.Height=900;window.UpdateLayout();
                await window.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                var scheduled=window.FlightTrackingView.ScheduledOutText;var actual=window.FlightTrackingView.ActualOutText;
                var scheduledRight=scheduled.TranslatePoint(new Point(scheduled.ActualWidth,0),window).X;
                var actualLeft=actual.TranslatePoint(new Point(0,0),window).X;
                Check(scheduledRight<actualLeft,$"scheduled and actual columns do not overlap at {width} DIP");
                DashboardSmokeTest.Capture(window,Path.Combine(directory,$"tracking-7.14-{width}.png"));
            }
            // Exercise the actual rejected-assignment UI path exposed by ANG2.
            reset();resetIdentity();
            var mismatch=plan with{FlightNumber="ANG2",Registration="C-SYNC",AircraftType="Airbus A220-300",PlannedDepartureUtc=at.AddDays(1),PlannedArrivalUtc=at.AddDays(1).AddHours(4)};
            Set("activePlan",mismatch);Set("trackingEventMonitor",new FlightTrackingEventMonitor());
            void Sample(int seconds,bool ground,double speed,bool brake,double latitude,double longitude,bool engineValid=false,double fuel=double.NaN)
            {
                var point=new GeoPosition(latitude,longitude);var telemetry=new Telemetry(at.AddSeconds(seconds),ground,speed,brake,false,LatitudeDegrees:latitude,LongitudeDegrees:longitude,AltitudeFeet:ground?20:32000,IndicatedAirspeedKnots:speed,VerticalSpeedFeetPerMinute:ground?0:500,FuelTotalWeightPounds:fuel,EngineDataValid:engineValid);
                observe(new("A220-300",telemetry,new(point,"C-SYNC",new("YSSY","YMHB"),true)));
            }
            Sample(0,true,0,true,-33.946,151.177111,fuel:6200);
            Check(!window.FlightTrackingView.EventList.Items.Cast<TrackingEventEntry>().Any(x=>x.Title=="Preflight at gate"&&x.Summary.Contains("Fuel",StringComparison.OrdinalIgnoreCase)),"Preflight event omits unstable loading fuel");
            for(var i=1;i<=5;i++)Sample(i,true,3,false,-33.946,151.177111);
            Check(window.FlightTrackingView.ActualOutText.Text=="02:05Z","schedule mismatch preserves observed OUT in tracking");
            Check(window.HeroFlightText.Text=="ANG2"&&window.TrackerModeText.Text.Contains("REVIEW"),"hero preserves candidate flight with explicit assignment review");
            Check(window.FlightTrackingView.ScheduleVarianceText.Text.Contains("REVIEW"),"wrong date suppresses misleading early/late variance");
            for(var i=6;i<=10;i++)Sample(i,false,450,false,-33.95,151.17);
            var beforeCorrection=window.RecoveredRouteProgress;
            window.ApplyScheduleCorrection(at,at.AddHours(4));
            Check(window.RecoveredRouteProgress==beforeCorrection&&window.FlightTrackingView.ActualOutText.Text=="02:05Z","schedule correction preserves milestones and progress");
            var map=window.FlightTrackingView.TrackingMap;map.ToggleFollow();var zoom=map.ZoomLevel;map.Zoom(1.2);
            Check(map.Following&&map.ZoomLevel>zoom,"map follow persists while zooming");map.ResetView();Check(!map.Following,"route fit disables follow");
            // Allow plausible elapsed time in increments below the continuity threshold.
            for(var i=60;i<=240;i+=60)Sample(i,false,450,false,-34,151);
            // Test landing/completion without using a position teleport through ObserveLive.
            var destination=new GeoPosition(-42.836111,147.510278);
            Set("liveLast",at.AddHours(2));Set("currentLiveReading",null);
            // This fixture installs a landed state, not a teleport. Recovery now
            // also checks the saved telemetry position when no live reading exists.
            Set("liveTelemetry",new Telemetry(at.AddHours(2),true,8,false,false,
                LatitudeDegrees:destination.Latitude,LongitudeDegrees:destination.Longitude,EngineDataValid:false));
            Set("liveRecorder",new TimelineRecorder(new PhaseDetector(initialPhase:FlightPhase.TaxiIn),new[]{new FlightEvent(FlightPhase.TaxiOut,at.AddSeconds(1)),new FlightEvent(FlightPhase.Airborne,at.AddSeconds(6)),new FlightEvent(FlightPhase.TaxiIn,at.AddHours(2))}));
            for(var i=7201;i<=7214;i++)Sample(i,true,0,true,destination.Latitude,destination.Longitude);
            Check(window.FlightTrackingView.ConfirmBlockInButton.Visibility==Visibility.Visible,"unknown engine data offers parked confirmation after sustained stop");
            FlightSessionDiagnosticAccess.Invoke(window,"ConfirmBlockIn");
            for(var i=7215;i<=7220;i++)Sample(i,true,0,true,destination.Latitude,destination.Longitude);
            Check(window.FlightTrackingView.ArrivalText.Text!="—"&&!window.FlightTrackingView.ScheduleVarianceText.Text.Contains("WAITING"),"confirmed block-in exposes actual IN with unavailable engines");
            Check((double?)typeof(MainWindow).GetProperty("LiveRemainingDistance",flags)!.GetValue(window)==0&&window.FlightTrackingView.DistanceRemainingText.Text=="0 NM","completion publishes zero remaining distance to both journal state and footer");
            var history=new GateHistoryStore(directory);history.Remember(mismatch with{DepartureGate="b7",ArrivalGate="NA"});history.Remember(mismatch with{DepartureGate=" B7 ",ArrivalGate=null});
            Check(history.Load().FindAll(x=>x.Airport=="YSSY"&&x.Gate=="B7").Count==1,"gate history normalizes duplicate entries");
            Check(!history.Load().Exists(x=>x.Gate=="NA"),"placeholder gates are not saved");
            Check(history.Load().Single(x=>x.Airport=="YSSY"&&x.Gate=="B7").UseCount==1,"repeated saves for one flight do not inflate learned gate confidence");
            var laterFlight=mismatch with{PlannedDepartureUtc=mismatch.PlannedDepartureUtc.AddDays(1),PlannedArrivalUtc=mismatch.PlannedArrivalUtc.AddDays(1),DepartureGate="B7"};history.Remember(laterFlight);
            var recommendation=history.Recommend(mismatch,"YSSY","Departure");
            Check(recommendation?.Gate=="B7"&&recommendation.UseCount==2&&recommendation.Reason.Contains("LOCAL HISTORY"),"gate history recommends a repeatedly confirmed airline and airport match");
            Check(history.Recommend(mismatch with{FlightNumber="QFA2"},"YSSY","Departure") is null,"gate history suppresses weak cross-airline recommendations");
            var otherType=mismatch with{AircraftType="Airbus A320",DepartureGate="C9"};history.Remember(otherType);history.Remember(otherType with{PlannedDepartureUtc=otherType.PlannedDepartureUtc.AddDays(1),PlannedArrivalUtc=otherType.PlannedArrivalUtc.AddDays(1)});history.Remember(otherType with{PlannedDepartureUtc=otherType.PlannedDepartureUtc.AddDays(2),PlannedArrivalUtc=otherType.PlannedArrivalUtc.AddDays(2)});
            Check(history.Recommend(mismatch with{AircraftType="Airbus A220-100"},"YSSY","Departure")?.Gate=="B7","gate recommendation prioritizes the matching aircraft family over a more frequent unrelated type");
            window.ShowDispatch();window.DispatchView.PresentDraft(new(mismatch,at,"Airbus A220-300","DCT",36000,10000,"KG",false));
            window.DispatchView.ReviewGates.Departure="B7";window.DispatchView.ReviewGates.Arrival="12";
            window.DispatchView.PresentDraft(new(mismatch,at,"Airbus A220-300","DCT",36000,10000,"KG",false));
            Check(window.DispatchView.ReviewGates.Departure=="B7","same-release refresh preserves unsaved gate entry");
            window.DispatchView.ReviewGates.Departure="";window.DispatchView.ReviewGates.Present(mismatch,false,true);
            Check(window.DispatchView.ReviewGates.RecommendedDeparture=="B7"&&window.DispatchView.ReviewGates.Departure=="","local-history recommendation waits for pilot confirmation");
            window.DispatchView.ReviewGates.ConfirmDepartureRecommendation();Check(window.DispatchView.ReviewGates.Departure=="B7","pilot confirmation applies the learned departure gate");
            foreach(var width in new[]{1920,1366,1100}){window.Width=width;window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);DashboardSmokeTest.Capture(window,Path.Combine(directory,$"dispatch-7.15-{width}.png"));}
            window.DispatchView.Render(mismatch,"FLIGHT CLOSEOUT");Set("activePlan",mismatch);FlightSessionDiagnosticAccess.Invoke(window,"FinalizeFlight");
            Check(!window.DispatchView.HasActiveRelease,"PIREP closeout immediately clears the rendered Dispatch release");
            reset();resetIdentity();
            var bypass=plan with{Registration="C-SYNC",AircraftType="Airbus A220-300",RoutePoints=[new("YSSY",-33.946,151.177111),new("SID1",-34.246,151.177111),new("SID2",-34.246,151.477111),new("JOIN",-33.946,151.477111),new("END",-33.946,155.177111)]};
            Set("activePlan",bypass);Set("trackingEventMonitor",new FlightTrackingEventMonitor());
            void BypassSample(int second,double latitude,double longitude,bool ground=false,double groundSpeed=0,bool brake=true)
            {
                var position=new GeoPosition(latitude,longitude);
                observe(new("A220-300",new(at.AddSeconds(second),ground,ground?groundSpeed:450,ground&&brake,!ground,LatitudeDegrees:latitude,LongitudeDegrees:longitude,AltitudeFeet:ground?20:32000,AltitudeAboveGroundFeet:ground?0:30000,VerticalSpeedFeetPerMinute:0),new(position,null,null,true)));
            }
            BypassSample(0,-33.946,151.177111,true);
            Check(window.TrackerModeText.Text.Contains("VERIFIED")&&window.HeroFlightText.Text=="JST721","missing simulator registration retains independently verified departure assignment in the live UI");
            for(var second=1;second<=4;second++)BypassSample(second,-33.946,151.177111,true,3,false);
            for(var second=5;second<=750;second+=5)BypassSample(second,-33.946,151.177111+second*.001);
            window.ShowFlightTracking();
            var bypassProgress=window.RecoveredRouteProgress??0;
            Check(bypassProgress>.25&&window.FlightTrackingView.ArrivalText.Text!="—",$"live UI recovers progress and ETA after bypassing departure legs (progress {bypassProgress:0.000}, arrival {window.FlightTrackingView.ArrivalText.Text}, phase {window.FlightTrackingView.PhaseText.Text}, state {window.FlightTrackingView.ScheduleVarianceText.Text})");
            Check(Math.Abs((double)window.ReplayProgress.GetAnimationBaseValue(RangeBase.ValueProperty)-bypassProgress*100)<.01&&window.FlightTrackingView.ProgressText.Text==$"{bypassProgress*100:0}%","hero and footer agree after continuous route reacquisition");
            for(var step=1;step<=100;step++)BypassSample(750+step*5,-33.946+step*.01,151.927111);
            Check(window.FlightTrackingView.ArrivalText.Text=="—"&&window.FlightTrackingView.DistanceRemainingText.Text=="— NM"&&window.FlightTrackingView.ScheduleVarianceText.Text.Contains("ROUTE POSITION REVIEW"),"off-route excursion withdraws stale distance, ETA and delay claims in the live UI");
            for(var step=1;step<=100;step++)BypassSample(1250+step*5,-32.946-step*.01,151.927111);
            Check(window.FlightTrackingView.ArrivalText.Text!="—"&&window.FlightTrackingView.DistanceRemainingText.Text!="— NM","continuous route rejoin restores the live estimate");
            var beforeDisconnectEstimate=window.FlightTrackingView.ArrivalText.Text;
            FlightSessionDiagnosticAccess.Invoke(window,"MarkLiveUnavailable");
            window.ShowFlightTracking();
            Check(window.FlightTrackingView.ArrivalText.Text=="—"&&!window.FlightTrackingView.ScheduleVarianceText.Text.Contains("MIN "),"telemetry loss withdraws an otherwise fresh last-sample estimate and schedule variance");
            BypassSample(1755,-33.946,151.927111);
            Check(window.FlightTrackingView.ArrivalText.Text!="—","reconnect restores the live estimate without restarting the flight");
            var estimateShift=DateTime.ParseExact(window.FlightTrackingView.ArrivalText.Text,"HH:mm'Z'",System.Globalization.CultureInfo.InvariantCulture)-DateTime.ParseExact(beforeDisconnectEstimate,"HH:mm'Z'",System.Globalization.CultureInfo.InvariantCulture);
            Check(Math.Abs(estimateShift.TotalMinutes)<2,"short reconnect preserves established ETA instead of restarting schedule blending");
            var pausedSample=((Telemetry)FlightSessionDiagnosticAccess.Get(window,"liveTelemetry")!) with{At=at.AddSeconds(1760),Paused=true};
            observe(new("A220-300",pausedSample,new(new(-33.946,151.927111),null,null,true)));
            FlightSessionDiagnosticAccess.Invoke(window,"MarkLiveUnavailable");
            Check(window.FlightTrackingView.ArrivalText.Text=="—"&&window.FlightTrackingView.DistanceRemainingText.Text=="— NM","disconnect while paused also withdraws the cached estimate and distance");
            File.WriteAllText(Path.Combine(directory,"tracking-integration.json"),JsonSerializer.Serialize(new{passed=true,checks},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally{Set("liveCancellation",null);reset();resetIdentity();}
    }
}
