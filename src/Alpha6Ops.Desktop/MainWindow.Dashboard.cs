using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.Windows.Shapes.Path;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

public partial class MainWindow
{
    private DashboardState dashboardState = new();
    private DashboardStateStore? dashboardStore;
    private readonly DispatcherTimer dashboardClock = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool dashboardShowsLive;
    private string flightTab = "all";
    internal IReadOnlyList<DashboardFlightRow> DashboardFlights { get; private set; } = [];
    internal DashboardState LocalDashboardState => dashboardState;
    internal bool DashboardIsLive => dashboardShowsLive;
    private DashboardFlightRow? HeroFlight => DashboardFlights.FirstOrDefault(f => !f.Leg.Completed) ?? DashboardFlights.LastOrDefault();
    private DashboardFlightRow? SelectedDashboardFlight => DashboardFlightsGrid.SelectedItem as DashboardFlightRow ?? HeroFlight;

    private void InitializeDashboard(string? diagnosticDirectory)
    {
        dashboardStore = new DashboardStateStore(diagnosticDirectory ?? CrashReporter.RootDirectory);
        try { dashboardState = dashboardStore.Load(); }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        { StatusText.Text = "Could not load dashboard preferences: " + error.Message; }
        ModuleTiles.ItemsSource = DashboardData.Tiles;
        RefreshAlerts(); RefreshMessages(); RefreshFleetSummary();
        UpdateClock(); dashboardClock.Tick += (_,_) => UpdateClock(); dashboardClock.Start();
        SizeChanged += (_,_) => UpdateFlightTabs();
        PreviewKeyDown += (_,e) => { if(e.Key == Key.Escape && ToolsOverlay.Visibility == Visibility.Visible) { ToolsOverlay.Visibility = Visibility.Collapsed; e.Handled=true; } };
        RefreshDashboardFlight(false);
        if (activePlan is not null) RefreshLiveTracker(null,null,null,"Assignment ready. Connect at the gate to begin tracking.");
    }
    private void UpdateClock()
    {
        var now=DateTimeOffset.UtcNow;
        ClockText.Text=now.ToString("HH:mm")+"Z";
        ClockDateText.Text=now.ToString("ddd, dd MMM yyyy").ToUpperInvariant();
        ClockText.ToolTip="Current real-world UTC. Replay and flight schedules use their own dated simulator clock.";
    }
    private void SaveDashboard()
    {
        try { dashboardStore?.Save(dashboardState); }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException)
        { MessageBox.Show(this,"Could not save dashboard preferences: "+error.Message,"Alpha 6 OPS",MessageBoxButton.OK,MessageBoxImage.Warning); }
    }
    private void PilotName_Changed(object sender, TextChangedEventArgs e)
    {
        if(PilotDisplayText is null || PilotNameBox is null)return;
        var name=PilotNameBox.Text.Trim();
        PilotDisplayText.Text=name.Length==0?"Your flight deck":name;
        PilotDisplayText.ToolTip=name;
        PilotInitialsText.Text=name.Length==0?"A6":string.Concat(name.Split(' ',StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p=>char.ToUpperInvariant(p[0])));
    }
    private void RefreshFleetSummary()
    {
        try
        {
            var fleet=FleetDatabase.Read();var active=fleet.Count(f=>f.Status=="Active");var parked=fleet.Count(f=>f.Status=="Parked");
            FleetCountText.Text=fleet.Count.ToString("N0");FleetActiveText.Text=$"■  ACTIVE       {active:N0}";FleetParkedText.Text=$"■  PARKED          {parked:N0}";
            FleetFamiliesText.Text=$"{fleet.Select(f=>f.Family).Distinct().Count()} aircraft families";
            FleetRing.Children.Clear();
            var ring=new Ellipse { Width=132,Height=132,Stroke=OpsUi.Brush("#63C848"),StrokeThickness=16 };
            Canvas.SetLeft(ring,9);Canvas.SetTop(ring,9);FleetRing.Children.Add(ring);
            var fraction=parked/(double)fleet.Count;var end=new Point(75+66*Math.Sin(fraction*2*Math.PI),75-66*Math.Cos(fraction*2*Math.PI));
            FleetRing.Children.Add(new Path {Data=new PathGeometry([new PathFigure(new Point(75,9),[new ArcSegment(end,new Size(66,66),0,fraction>.5,SweepDirection.Clockwise,true)],false)]),Stroke=OpsUi.Brush("#FFDA00"),StrokeThickness=16});
        }
        catch(Exception error) when(error is IOException or DllNotFoundException or EntryPointNotFoundException)
        {FleetCountText.Text="—";FleetActiveText.Text="Catalog unavailable";FleetParkedText.Text="";FleetFamiliesText.Text=error.Message;}
    }
    private void RefreshDashboardFlight(bool live)
    {
        dashboardShowsLive=live;
        var rotation=live ? liveRotation ?? (activePlan is null ? null : BuildLiveRotation(activePlan,liveAircraft,AircraftGroundProfile.Default)) : session.Rotation;
        DashboardFlights=rotation is null ? [] : RotationPlanner.Project(rotation).Select(l=>new DashboardFlightRow(l,rotation.AircraftId)).ToArray();
        var hero=HeroFlight;
        DataModeText.Text=live?"ACTIVE ASSIGNMENT  /  SIMULATOR UTC":"REPLAY SCENARIO  /  02 SEP 2026";
        if(hero is null)
        {
            HeroFlightText.Text="NO FLIGHT";OriginCodeText.Text=DestinationCodeText.Text="—";OriginCityText.Text="SET AN";DestinationCityText.Text="ASSIGNMENT";
            HeroDepartureText.Text=HeroArrivalText.Text="—";HeroTimingText.Text="Open Flight tools to enter your flight";HeroStatusText.Text="●  WAITING";HeroAircraftTypeText.Text="NO ASSIGNMENT";
        }
        else
        {
            HeroFlightText.Text=hero.Id;OriginCodeText.Text=DashboardData.AirportCode(hero.Leg.Origin);DestinationCodeText.Text=DashboardData.AirportCode(hero.Leg.Destination);
            OriginCityText.Text=DashboardData.City(hero.Leg.Origin);DestinationCityText.Text=DashboardData.City(hero.Leg.Destination);
            HeroDepartureText.Text=hero.Out;HeroArrivalText.Text=hero.In;HeroStatusText.Text="●  "+hero.Status;
            HeroStatusText.Foreground=OpsUi.Brush(hero.StatusColor);HeroStatusBadge.Background=OpsUi.Brush(hero.StatusBackground);
            HeroTimingText.Text=hero.Leg.Completed?"Flight complete":$"{(hero.Leg.EstimatedIn-hero.Leg.EstimatedOut).TotalMinutes:0} min block  •  {hero.Leg.EstimatedOut:dd MMM}  •  {hero.Out}Z";
            AircraftText.Text=rotation!.AircraftId;
            HeroAircraftTypeText.Text=live?"ACTIVE ASSIGNMENT":"SAMPLE ASSIGNMENT";
            TrackerModeText.Text=live?"ACTIVE FLIGHT • SIMCONNECT":"REPLAY • SAMPLE DATA";
        }
        RefreshFlightsTable();
        if(live)
        {
            RotationGrid.ItemsSource=DashboardFlights.Select(f=>new {f.Id,f.Route,f.Out,f.In,Delay=$"{f.Leg.DepartureDelayMinutes:0} / {f.Leg.ArrivalDelayMinutes:0} min",Status=f.Leg.Completed?"Actual":"Projected"}).ToArray();
            OperationsFootnote.Text=hero is null?"NO ACTIVE ASSIGNMENT":$"{hero.Leg.ScheduledOut:dd MMM yyyy}  •  ALL TIMES UTC".ToUpperInvariant();
        }
        else OperationsFootnote.Text="02 SEP 2026  •  ALL TIMES UTC";
    }
    private void RefreshFlightsTable()
    {
        var selected=(DashboardFlightsGrid.SelectedItem as DashboardFlightRow)?.Key;
        var visible=DashboardFlights.Where(f=>flightTab=="all"||(flightTab=="assigned"&&!f.Leg.Completed)||(flightTab=="watchlist"&&dashboardState.Watchlist.Contains(f.Key))).ToArray();
        DashboardFlightsGrid.ItemsSource=visible;DashboardFlightsGrid.SelectedItem=visible.FirstOrDefault(f=>f.Key==selected)??visible.FirstOrDefault();
        EmptyFlightsText.Visibility=visible.Length==0?Visibility.Visible:Visibility.Collapsed;
        EmptyFlightsText.Text=flightTab=="watchlist"?"Select a flight in My flights, then add it to your watchlist.":"No flights in this view.";
        UpdateFlightTabs();
        WatchFlightButton.IsEnabled=visible.Length>0;UpdateWatchButton();
    }
    private void UpdateFlightTabs()
    {
        if (DashboardFlightsGrid is null) return;
        var compact = ActualWidth < 1400;
        MyFlightsTab.Content = compact ? "ALL" : "MY FLIGHTS";
        AssignedFlightsTab.Content = $"{(compact ? "NEXT" : "ASSIGNED")} ({DashboardFlights.Count(f => !f.Leg.Completed)})";
        WatchlistTab.Content = $"{(compact ? "WATCH" : "WATCHLIST")} ({DashboardFlights.Count(f => dashboardState.Watchlist.Contains(f.Key))})";
    }
    internal void SelectFlightTab(string tab)
    {
        flightTab=tab;
        MyFlightsTab.Foreground=OpsUi.Brush(tab=="all"?"#FFDA00":"#A7B7C5");AssignedFlightsTab.Foreground=OpsUi.Brush(tab=="assigned"?"#FFDA00":"#A7B7C5");WatchlistTab.Foreground=OpsUi.Brush(tab=="watchlist"?"#FFDA00":"#A7B7C5");
        RefreshFlightsTable();
    }
    private void FlightsTab_Click(object sender,RoutedEventArgs e)=>SelectFlightTab((string)((Button)sender).Tag);
    private void FlightSelection_Changed(object sender,SelectionChangedEventArgs e)=>UpdateWatchButton();
    private void UpdateWatchButton()
    {
        if(WatchFlightButton is null)return;
        WatchFlightButton.Content=DashboardFlightsGrid.SelectedItem is DashboardFlightRow f && dashboardState.Watchlist.Contains(f.Key)?"★  UNWATCH":"☆  WATCH SELECTED";
    }
    internal void ToggleWatch(DashboardFlightRow flight)
    {
        if(!dashboardState.Watchlist.Add(flight.Key))dashboardState.Watchlist.Remove(flight.Key);
        SaveDashboard();RefreshFlightsTable();
    }
    private void WatchFlight_Click(object sender,RoutedEventArgs e){if(SelectedDashboardFlight is {} f)ToggleWatch(f);}
    private void FlightRow_DoubleClick(object sender,MouseButtonEventArgs e){if(DashboardFlightsGrid.SelectedItem is DashboardFlightRow f)ShowFlight(f,false);}
    private void FlightDetails_Click(object sender,RoutedEventArgs e){if(HeroFlight is {} f)ShowFlight(f,false);else OpenTools();}
    private void Preflight_Click(object sender,RoutedEventArgs e){if(HeroFlight is {} f)ShowFlight(f,true);else OpenTools();}
    private void ShowFlight(DashboardFlightRow flight,bool preflight)=>new FlightPreparationWindow(flight,dashboardState,SaveDashboard,preflight,()=>SetFlight_Click(this,new RoutedEventArgs())){Owner=this}.ShowDialog();
    private void RefreshAlerts()
    {
        var active=DashboardData.Alerts.Where(a=>!dashboardState.AcknowledgedAlerts.Contains(a.Id)).ToArray();
        AlertsItems.ItemsSource=active.Take(4).ToArray();ViewAlertsButton.Content=$"VIEW ALL ({active.Length})";
    }
    internal void AcknowledgeAlert(string id)
    {
        if(!DashboardData.Alerts.Any(a=>a.Id==id))return;
        dashboardState.AcknowledgedAlerts.Add(id);SaveDashboard();RefreshAlerts();
    }
    private void Alert_Click(object sender,RoutedEventArgs e)
    {
        if(((Button)sender).Tag is not DashboardAlert alert)return;
        var module=new OpsModule("ALERT DETAIL",alert.Title,"DEMONSTRATION SCENARIO • LOCAL ACKNOWLEDGMENT",
            [new("PRIORITY",alert.Color=="#FF484E"?"HIGH":"REVIEW","Scenario alert"),new("AGE",alert.Age,"Relative to the fixed demo"),new("STATE",dashboardState.AcknowledgedAlerts.Contains(alert.Id)?"ACKNOWLEDGED":"OPEN","Saved on this computer")],
            [new(alert.Id,alert.Title,alert.Age,"Review",alert.Detail)]);
        OperationsWorkspaceWindow? window=null;
        window=new OperationsWorkspaceWindow(module,"ACKNOWLEDGE",_=>{AcknowledgeAlert(alert.Id);window!.Close();}){Owner=this};window.ShowDialog();
    }
    private void AllAlerts_Click(object sender,RoutedEventArgs e)
    {
        var module=new OpsModule("ALERT CENTER","Review all demonstration alerts, including local acknowledgments","DEMONSTRATION SCENARIO • NO LIVE ADVISORIES",
            [new("TOTAL",DashboardData.Alerts.Length.ToString(),"Demonstration alerts"),new("OPEN",DashboardData.Alerts.Count(a=>!dashboardState.AcknowledgedAlerts.Contains(a.Id)).ToString(),"Awaiting local review"),new("ACKNOWLEDGED",dashboardState.AcknowledgedAlerts.Count.ToString(),"Saved on this computer")],
            DashboardData.Alerts.Select(a=>new OpsRow(a.Id,a.Title,a.Age,dashboardState.AcknowledgedAlerts.Contains(a.Id)?"Acknowledged":"Open",a.Detail)).ToArray());
        OperationsWorkspaceWindow? window=null;
        window=new OperationsWorkspaceWindow(module,"ACKNOWLEDGE",r=>{AcknowledgeAlert(r.Reference);window!.Close();}){Owner=this};window.ShowDialog();
    }
    private void RefreshMessages()=>MessagesItems.ItemsSource=DashboardData.Messages.Select(m=>m with{ReadLabel=dashboardState.ReadMessages.Contains(m.Id)?"READ":"NEW"}).ToArray();
    private void Message_Click(object sender,RoutedEventArgs e)
    {
        if(((Button)sender).Tag is not DashboardMessage message)return;
        dashboardState.ReadMessages.Add(message.Id);SaveDashboard();RefreshMessages();
        new OperationsWorkspaceWindow(new("COMPANY MESSAGE",message.Title,"LOCAL PRODUCT BRIEFING",
            [new("SOURCE","ALPHA 6 OPS","Bundled product notes"),new("STATUS","READ","Saved on this computer"),new("DELIVERY","LOCAL","No company messaging service")],
            [new(message.Id,message.Title,"Product briefing","Read",message.Body)])){Owner=this}.ShowDialog();
    }
    private void Dashboard_Click(object sender,RoutedEventArgs e){ToolsOverlay.Visibility=Visibility.Collapsed;DashboardScroll.ScrollToTop();}
    private void OpenTools(){ToolsOverlay.Visibility=Visibility.Visible;PilotNameBox.Focus();}
    private void FlightTools_Click(object sender,RoutedEventArgs e)=>OpenTools();
    private void CloseTools_Click(object sender,RoutedEventArgs e)=>ToolsOverlay.Visibility=Visibility.Collapsed;
    internal OpsModule CreateFlightModule() => new("FLIGHTS & ROTATIONS","The aircraft's day, calculated from the current flight session",dashboardShowsLive?"ACTIVE ASSIGNMENT • SIMULATOR UTC":"RECORDED SCENARIO • 02 SEP 2026",
        [new("LEGS",DashboardFlights.Count.ToString(),"Current rotation"),new("COMPLETED",DashboardFlights.Count(f=>f.Leg.Completed).ToString(),"Confirmed block-in"),new("TURNAROUND",(dashboardShowsLive?liveRotation?.MinimumTurnMinutes??35:session.Rotation.MinimumTurnMinutes)+" MIN","Minimum aircraft turn")],
        DashboardFlights.Select(f=>new OpsRow(f.Id,$"{f.Route} / {f.Aircraft}",$"{f.Out} – {f.In} UTC",f.Status,
            $"Scheduled: {f.Leg.ScheduledOut:HH:mm}Z to {f.Leg.ScheduledIn:HH:mm}Z. Projected/actual: {f.Out}Z to {f.In}Z. Departure delay {f.Leg.DepartureDelayMinutes:0} min; arrival delay {f.Leg.ArrivalDelayMinutes:0} min. "+(f.Leg.Completed?"Confirmed completion recorded.":"This leg is projected from the current actuals and minimum turnaround."))).ToArray(),"hero");
    private void Module_Click(object sender,RoutedEventArgs e)
    {
        var name=(string)((Button)sender).Tag;
        switch(name)
        {
            case "Settings": OpenTools();return;
            case "Aircraft": Fleet_Click(sender,e);return;
            case "Network": new NetworkWindow{Owner=this}.ShowDialog();return;
            case "Flights":
                new OperationsWorkspaceWindow(CreateFlightModule(),"FLIGHT DETAILS",r=>{var f=DashboardFlights.First(x=>x.Id==r.Reference);ShowFlight(f,false);}){Owner=this}.ShowDialog();return;
            case "Reports":
                var flights=flightHistory?.ReadRecentFlights()??[];
                var report=new OpsModule("FLIGHT REPORTS","Local flight history and recorded session results","LOCAL FLIGHT HISTORY • REPLAY RUNS",
                    [new("RECENT FLIGHTS",flights.Count.ToString(),"Up to 200 recent records"),new("COMPLETED",flights.Count(f=>f.FinalPhase=="Complete").ToString(),"Recorded completion"),new("EVENTS",flights.Sum(f=>f.EventCount).ToString(),"Stored milestones")],
                    flights.Select((f,i)=>new OpsRow((i+1).ToString("D3"),$"{f.Aircraft} / {f.Route}",f.StartedUtc,f.FinalPhase??"In progress",$"Pilot: {f.Pilot}. Source: {f.Source}. Started: {f.StartedUtc}. Ended: {f.EndedUtc??"Not recorded"}. Events: {f.EventCount}. Live simulator journals can be exported separately through Flight tools.")).ToArray());
                new OperationsWorkspaceWindow(report){Owner=this}.ShowDialog();return;
            case "Messages":
                var messages=new OpsModule("COMPANY MESSAGES","Product briefings for your flight deck","LOCAL PRODUCT NOTES",
                    [new("BRIEFINGS","4","Bundled with the application"),new("UNREAD",DashboardData.Messages.Count(m=>!dashboardState.ReadMessages.Contains(m.Id)).ToString(),"Local reading status"),new("DELIVERY","LOCAL","No messaging server")],
                    DashboardData.Messages.Select(m=>new OpsRow(m.Id,m.Title,m.Subtitle,dashboardState.ReadMessages.Contains(m.Id)?"Read":"New",m.Body)).ToArray());
                OperationsWorkspaceWindow? messagesWindow = null;
                messagesWindow = new OperationsWorkspaceWindow(messages,"MARK READ",r=>{dashboardState.ReadMessages.Add(r.Reference);SaveDashboard();RefreshMessages();messagesWindow!.Close();}){Owner=this};
                messagesWindow.ShowDialog();return;
            default:new OperationsWorkspaceWindow(DashboardData.Module(name)){Owner=this}.ShowDialog();return;
        }
    }
}
