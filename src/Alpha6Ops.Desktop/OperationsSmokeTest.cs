using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal static class OperationsSmokeTest
{
    internal static async Task RunAsync(MainWindow window,string directory,Action<bool,string> check)
    {
        var at=new DateTimeOffset(2026,9,24,2,30,0,TimeSpan.Zero);
        var plan=new ActiveFlightPlan("JST721","VH-VGF","YSSY","YMHB",at.AddMinutes(-25),at.AddMinutes(87),"Diagnostic fixture",DepartureGate:"54",ArrivalGate:"3",AircraftType:"A320",
            RoutePoints:[new("YSSY",-33.94,151.18,"Departure"),new("OTKED",-37,149),new("YMHB",-42.84,147.51,"Arrival")]);
        var telemetry=new Telemetry(at,false,430,false,true,LatitudeDegrees:-35.6,LongitudeDegrees:150.1,AltitudeFeet:34000,HeadingDegrees:195);
        var state=new FlightSessionSnapshot{Assignment=plan,Connection=SimulatorConnectionState.Connected,Connected=true,ConnectionActive=true,
            Reading=new("Airbus A320",telemetry),Telemetry=telemetry,Aircraft="Airbus A320",SimulatorUtc=at,DataCurrent=true,
            CanRenderAssignment=true,CanPresentCandidate=true,Association=new(true,"VERIFIED",[]),ScheduleTrusted=true,
            Phase=FlightPhase.Airborne,EstimatedArrival=plan.PlannedArrivalUtc.AddMinutes(2),ActualOut=at.AddMinutes(-20),RouteProgress=.23,RouteReliable=true,
            Events=[new(at.AddMinutes(-12),"10,000 feet crossed","Climbing through 10,000 ft",""),new(at.AddMinutes(-18),"Liftoff","Departure recorded",""){Code=TrackingEventCode.Liftoff}]};
        OperationsOverview Project(FlightSessionSnapshot value,bool demo=false,LocalWeather? weather=null,bool failed=false)
            =>OperationsOverviewQuery.Project(value,demo,[],weather,failed,at);
        var empty=Project(new());
        check(empty.Metrics.All(m=>m.Value=="—")&&empty.Activity.Count==0&&empty.Alerts.Count==0,"Empty Operations invents no airline figures, alerts or activity");
        var live=Project(state);
        check(live.Metrics[0].Value=="1"&&live.Metrics[1].Value=="1"&&live.Metrics[2].Value=="0"&&live.Metrics[4].Value=="1","Trusted session supplies scoped active/arrival/aircraft KPIs");
        check(live.Metrics[3].Source==OperationsProvenance.Unavailable&&live.Metrics[5].Value=="—","Risk and passenger connections remain unavailable");
        check(Project(state with{EstimatedArrival=plan.PlannedArrivalUtc.AddMinutes(7)}).Metrics[2].Value=="1","Delayed metric follows typed dated arrival comparison");
        check(Project(state with{ScheduleTrusted=false}).Metrics[1].Value=="—"&&Project(state with{ScheduleTrusted=false}).Alerts.Any(a=>a.Id=="schedule"),"Untrusted dates suppress schedule KPIs and explain the review");
        var mismatch=Project(state with{CanRenderAssignment=false,CanPresentCandidate=false,Association=new(false,"FREE FLIGHT",["Different aircraft"])});
        check(mismatch.MapRoute.Count==0&&mismatch.MapTelemetry is null&&mismatch.Metrics[1].Value=="—"&&mismatch.Alerts.Any(a=>a.Id=="association"),"Unverified assignment cannot supply live route truth or claim an unsupported free-flight map marker");
        var stale=Project(state with{DataCurrent=false,Connection=SimulatorConnectionState.Reconnecting});
        check(stale.Source==OperationsProvenance.Stale&&stale.MapTelemetry is null&&stale.Metrics[0].Value=="—"&&stale.Alerts.Any(a=>a.Severity==OperationsSeverity.Warning),"Stale telemetry is retained as history rather than a live aircraft");
        check(Project(state with{Connection=SimulatorConnectionState.Failed,DataCurrent=false}).Alerts.Any(a=>a.Severity==OperationsSeverity.Critical),"Connection failure receives typed critical severity");
        foreach(var suspended in new[]{state with{Reading=state.Reading with{Evidence=new(){SimulationRunning=false}}},state with{Telemetry=telemetry with{Paused=true}},state with{Telemetry=telemetry with{Slewing=true}}})
            check(Project(suspended).Metrics[0].Value=="—"&&Project(suspended).MapTelemetry is null&&Project(suspended).Alerts.Any(a=>a.Id=="suspended"),"Menu, pause and slew states suspend live totals despite retained current sample flag");
        check(Project(state with{ArrivalMismatch="Wrong arrival airport",ActualIn=at}).Metrics[1].Value=="—","Wrong-airport block-in cannot count as an on-time assigned arrival");
        check(OperationsOverviewQuery.FlightStatus(state)=="AIRBORNE"&&OperationsOverviewQuery.FlightStatus(state with{ArrivalMismatch="Wrong airport"})=="ARRIVAL REVIEW"&&OperationsOverviewQuery.FlightStatus(state with{ScheduleTrusted=false})=="SCHEDULE REVIEW","Flight table uses typed phase and review precedence instead of schedule projection wording");
        var demo=Project(state,true);
        check(demo.Metrics.All(m=>m.Value=="—")&&demo.Activity.Count==0&&demo.MapTelemetry is null&&demo.Context.Contains("SAMPLE"),"Replay context is explicit and cannot leak live metrics or network traffic");
        check(live.Activity[0].Time.Contains("02:18")&&live.Activity[1].Code==TrackingEventCode.Liftoff,"Timeline retains event identities and descending timestamp order");
        check(live.Airports.Count==2&&live.Airports[0].Code=="YSSY"&&live.Airports[0].Gate=="Gate 54","Airports use existing catalog and assigned gates");
        var row=new DashboardFlightRow(new("JST721","YSSY","YMHB",at,at.AddHours(2),at,at.AddHours(2),0,0,false),"VH-VGF",false,"A320");
        check(row.Status=="SCHEDULE UNVERIFIED"&&row.DisplayAircraft=="Airbus A320"&&row.Key.StartsWith("VH-VGF|"),"Flight table suppresses unverified schedule status and preserves watchlist identity when displaying aircraft type");
        check((row with{Leg=row.Leg with{ScheduledOut=new DateTimeOffset(2026,9,24,7,0,0,TimeSpan.FromHours(10))}}).ScheduledOut=="23 Sep 21:00Z","Table dates convert timezone offsets to dated UTC across midnight");
        var weather=Project(state,weather:new("Sydney",21,3,at.AddHours(-2)));
        check(weather.WeatherDetail.Contains("Last available")&&weather.WeatherDetail.Contains("not airport weather"),"Old city weather is labeled stale and is never presented as airport observations");
        check(Project(state,weather:new("Sydney",21,3,at),failed:true).WeatherDetail.Contains("Last available"),"Failed weather refresh preserves the last observation with a stale label");
        check(Project(state with{Telemetry=telemetry with{LatitudeDegrees=double.NaN,LongitudeDegrees=double.NaN}}).MapTelemetry is null,"Progress alone cannot fabricate an observed map track");
        var json=new JsonSerializerOptions{NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals};
        var before=JsonSerializer.Serialize(state,json);for(var i=0;i<20;i++)Project(state);
        check(before==JsonSerializer.Serialize(state,json),"Repeated Operations aggregation does not mutate its session snapshot");
        check(Project(state with{PhaseLabel="Changed display wording",Events=[new(at,"Changed title","Changed summary",""){Code=TrackingEventCode.Liftoff}]}).Metrics.SequenceEqual(live.Metrics),"Display wording has no effect on typed KPI decisions");

        var host=new OperationsMapHost();host.Present(live);var renderer=host.Renderer;host.Present(live);
        check(ReferenceEquals(renderer,host.Renderer)&&renderer.HasLiveAircraft&&renderer.TrackPointCount==1,"Repeated map presentation preserves the view without adding duplicate track points");
        host.Present(live with{MapRoute=[new("YSSY",-33.94,151.18),new("YMHB",-42.84,147.51)]});check(host.Renderer.HasLiveAircraft,"Route replacement reapplies an unchanged current telemetry sample");
        host.Present(stale);check(!host.Renderer.HasLiveAircraft&&host.Renderer.TrackPointCount==0,"Stale transition removes a formerly live map marker");
        host.Present(live);host.Present(live with{MapSessionKey="new session"});check(!ReferenceEquals(renderer,host.Renderer)&&host.Renderer.TrackPointCount==1,"New session cannot inherit the previous presentation track");
        host.EndVisibleTrail();host.Present(live with{MapSessionKey="new session",MapTelemetry=telemetry with{LatitudeDegrees=-36.4}});
        check(host.Renderer.TrackPointCount==1,"Reopened Operations cannot draw a flown segment across a hidden interval");
        var detached=new OperationsWorkspace();var initial=detached.MapHost.Renderer;detached.Present(live);
        check(ReferenceEquals(initial,detached.MapHost.Renderer)&&!initial.HasLiveAircraft,"Hidden workspace does not run geographic redraws");
        window.ShowDashboard();window.UpdateLayout();
        check(window.LegacyViewport.Visibility==Visibility.Collapsed&&window.OperationsView.IsVisible,"Operations is natively hosted outside the legacy minimum-width viewport");
        check(!Descendants(window.OperationsView).OfType<System.Windows.Controls.Border>().Any(b=>b.Background is ImageBrush),"Operations contains no photographic hero or launcher tile background");
        foreach(var section in new[]{ShellSection.OperationsDemo,ShellSection.AlertsDemo,ShellSection.Passengers,ShellSection.Weather,ShellSection.Occ})
            check(window.Shell.Navigation.Destinations.Any(d=>d.Section==section)&&window.Shell.Navigation.Search("demo").Any(d=>d.Section==section),$"Removed tile destination {section} remains reachable through explicitly labeled workspace search");
        // Actual production view with explicitly injected diagnostic data; never a production demo feed.
        var saved=window.OperationsView.Overview!;var width=window.Width;var height=window.Height;
        var texts=new[]{window.HeroFlightText,window.OriginCodeText,window.DestinationCodeText,window.OriginCityText,window.DestinationCityText,window.HeroStatusText,window.HeroTimingText,window.AircraftText,window.HeroAircraftTypeText,window.TrackerModeText,window.HeroDepartureText,window.HeroArrivalText,window.HeroDepartureGateText,window.HeroArrivalGateText};
        var values=texts.Select(t=>t.Text).ToArray();var oldItems=window.DashboardFlightsGrid.ItemsSource;
        var oldAction=window.HeroFlightDetailsButton.Content;var oldAssigned=window.AssignedFlightsTab.Content;var oldFootnote=window.OperationsFootnote.Text;
        window.Shell.PresentConnection(state);window.HeroFlightDetailsButton.Content="VIEW FLIGHT TRACKING    →";window.AssignedFlightsTab.Content="ASSIGNED (1)";window.OperationsFootnote.Text="24 SEP 2026 · ALL TIMES UTC";
        window.HeroFlightText.Text="JST721";window.OriginCodeText.Text="YSSY";window.DestinationCodeText.Text="YMHB";
        window.OriginCityText.Text="SYDNEY KINGSFORD SMITH INTERNATIONAL AIRPORT";window.DestinationCityText.Text="HOBART INTERNATIONAL AIRPORT";
        window.HeroStatusText.Text="ON TIME";window.HeroTimingText.Text="23% complete · 87 min to planned arrival";window.AircraftText.Text="Airbus A320 · VH-VGF";
        window.HeroAircraftTypeText.Text="DIAGNOSTIC FIXTURE";window.TrackerModeText.Text="OBSERVED POSITION";window.HeroDepartureText.Text="02:10";window.HeroArrivalText.Text="03:59";window.HeroDepartureGateText.Text="54";window.HeroArrivalGateText.Text="3";
        window.DashboardFlightsGrid.ItemsSource=new[]{new DashboardFlightRow(new("JST721","YSSY","YMHB",plan.PlannedDepartureUtc,plan.PlannedArrivalUtc,plan.PlannedDepartureUtc,plan.PlannedArrivalUtc,0,0,false),"Airbus A320")};
        foreach(var size in new[]{(1920,1080),(2560,1440)})
        {
            window.Width=size.Item1;window.Height=size.Item2;window.OperationsView.Present(live);window.UpdateLayout();
            DashboardSmokeTest.Capture(window,Path.Combine(directory,$"operations-observed-{size.Item1}x{size.Item2}.png"));
        }
        window.OperationsView.Present(stale);window.UpdateLayout();DashboardSmokeTest.Capture(window,Path.Combine(directory,"operations-stale.png"));
        foreach(var text in texts)text.Text="—";window.HeroFlightText.Text="NO ACTIVE FLIGHT";window.HeroTimingText.Text="Open Dispatch to prepare a release";window.DashboardFlightsGrid.ItemsSource=Array.Empty<DashboardFlightRow>();
        window.Shell.PresentConnection(new());window.HeroFlightDetailsButton.Content="OPEN DISPATCH    →";window.AssignedFlightsTab.Content="ASSIGNED (0)";window.OperationsFootnote.Text="NO ACTIVE ASSIGNMENT";
        window.OperationsView.Present(empty);window.UpdateLayout();DashboardSmokeTest.Capture(window,Path.Combine(directory,"operations-empty-query.png"));
        for(var i=0;i<texts.Length;i++)texts[i].Text=values[i];window.DashboardFlightsGrid.ItemsSource=oldItems;
        window.HeroFlightDetailsButton.Content=oldAction;window.AssignedFlightsTab.Content=oldAssigned;window.OperationsFootnote.Text=oldFootnote;
        window.Width=width;window.Height=height;window.OperationsView.Present(saved);window.UpdateLayout();
        await OperationsLayoutChecks.RunAsync(window,check);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) {var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var value in Descendants(child))yield return value;}
    }
}
