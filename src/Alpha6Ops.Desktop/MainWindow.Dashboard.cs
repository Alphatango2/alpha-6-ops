using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

    internal void UpdateHeroProgress(double value,bool animate=true)
    {
        var target=Math.Clamp(value,ReplayProgress.Minimum,ReplayProgress.Maximum);var current=ReplayProgress.Value;
        ReplayProgress.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty,null);
        ReplayProgress.Value=target;
        if(!animate||Math.Abs(target-current)<.001)return;
        var duration=TimeSpan.FromMilliseconds(850);var easing=new QuadraticEase{EasingMode=EasingMode.EaseOut};
        ReplayProgress.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty,new DoubleAnimation(current,target,duration){EasingFunction=easing,FillBehavior=FillBehavior.Stop});
    }

    private void InitializeDashboard(string? diagnosticDirectory)
    {
        dashboardStore = new DashboardStateStore(diagnosticDirectory ?? CrashReporter.RootDirectory);
        try { dashboardState = dashboardStore.Load(); }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        { StatusText.Text = "Could not load dashboard preferences: " + error.Message; }
        InitializeOperations();
        RefreshMessages();
        UpdateClock(); dashboardClock.Tick += (_,_) => UpdateClock(); dashboardClock.Start();
        InitializeLocalWeather(diagnosticDirectory is not null);
        SizeChanged += (_,_) => { UpdateResponsiveLayout(); UpdateFlightTabs(); };
        UpdateResponsiveLayout();
        FlightTrackingView.FlightDeckRequested += (_,_)=>OpenTools();
        FlightTrackingView.DispatchRequested += (_,_)=>ShowDispatch();
        FlightTrackingView.GatesRequested += (_,_)=>AssignGates();
        DispatchView.GatesRequested += (_,_)=>AssignGates();
        DispatchView.ConfigureGates(stateDirectory);
        DispatchView.GateValuesSaved+=(_,p)=>ApplyDispatchGates(p.DepartureGate,p.ArrivalGate);
        DispatchView.ScheduleRequested+=(_,_)=>CorrectSchedule();
        FlightTrackingView.ConfirmBlockInRequested+=(_,_)=>{if(flightState.Phase==FlightPhase.TaxiIn&&OpsConfirmWindow.Ask(this,"Confirm parked at gate","Confirm you have reached your parking stand. Automatic engine/destination verification is incomplete.")){coordinator.ConfirmBlockIn();}};
        FlightTrackingView.ScheduleRequested+=(_,_)=>CorrectSchedule();
        FlightTrackingView.PirepSubmissionRequested=SubmitCompletedPirep;
        FlightTrackingView.PirepCloseoutCompleted+=(_,_)=>CompletePirepCloseout();
        PilotLogbookView.DashboardRequested+=(_,_)=>ShowDashboard();
        DispatchView.DashboardRequested+=(_,_)=>ShowDashboard();
        DispatchView.ManualRequested+=(_,_)=>{SetFlight_Click(this,new RoutedEventArgs());UpdateDispatchState();};
        DispatchView.FlightAccepted+=(_,plan)=>AcceptDispatchFlight(plan);
        DispatchView.TrackingRequested+=(_,_)=>ShowFlightTracking();
        PreviewKeyDown += (_,e) => { if(e.Key == Key.Escape && ToolsOverlay.Visibility == Visibility.Visible) { ToolsOverlay.Visibility = Visibility.Collapsed; e.Handled=true; } };
        RefreshDashboardFlight(false);
        if (activePlan is not null) RefreshLiveTracker(null,null,null,"Assignment ready. Connect at the gate to begin tracking.");
        else{if(!diagnosticMode)RefreshDashboardFlight(true);RefreshFlightTrackingWorkspace(null,null,"No active flight");}
        ShowDashboard();
    }
    private void UpdateClock()
    {
        var now=DateTimeOffset.UtcNow;
        RenderClocks(now, TimeZoneInfo.Local);
        RenderLocalWeather(now);
        RefreshOperations();
        ClockText.ToolTip="Current real-world UTC. Replay and flight schedules use their own dated simulator clock.";
    }
    private void SaveDashboard()
    {
        try { dashboardStore?.Save(dashboardState); }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException)
        { OpsNoticeWindow.Show(this,"Alpha 6 OPS","Could not save dashboard preferences: "+error.Message,true); }
    }
    private void PilotName_Changed(object sender, TextChangedEventArgs e)
    {
        if(Shell is null || PilotNameBox is null)return;
        var name=PilotNameBox.Text.Trim();
        Shell.PresentProfile(name);
        if(!initializingPresentation)coordinator.SetPilotName(name);
    }
    private void RefreshDashboardFlight(bool live)
    {
        dashboardShowsLive=live;
        var rotation=live ? flightState.Rotation : session.Rotation;
        DashboardFlights=rotation is null ? [] : RotationPlanner.Project(rotation).Select(l=>new DashboardFlightRow(l,rotation.AircraftId,
            ScheduleComparisonAvailable:!live || flightState.ScheduleTrusted && flightState.DataCurrent,
            AircraftType:live?activePlan?.AircraftType:null,
            OperationalStatus:live&&l.Id==activePlan?.FlightNumber?OperationsOverviewQuery.FlightStatus(flightState):null)).ToArray();
        var hero=HeroFlight;
        if(hero is null)
        {
            HeroFlightText.Text="NO ACTIVE FLIGHT";OriginCodeText.Text=DestinationCodeText.Text="—";OriginCityText.Text="SET AN";DestinationCityText.Text="ASSIGNMENT";
            HeroDepartureText.Text=HeroArrivalText.Text=HeroDepartureGateText.Text=HeroArrivalGateText.Text="—";HeroTimingText.Text="Open Flight tools to enter your flight";HeroStatusText.Text="●  WAITING";HeroAircraftTypeText.Text="NO ASSIGNMENT";
            AircraftText.Text="—";TrackerModeText.Text="NO ACTIVE ASSIGNMENT";
        }
        else
        {
            HeroFlightText.Text=hero.Id;OriginCodeText.Text=DashboardData.AirportCode(hero.Leg.Origin);DestinationCodeText.Text=DashboardData.AirportCode(hero.Leg.Destination);
            OriginCityText.Text=DashboardData.AirportName(hero.Leg.Origin);DestinationCityText.Text=DashboardData.AirportName(hero.Leg.Destination);
            HeroDepartureText.Text=hero.Out;HeroArrivalText.Text=hero.In;HeroStatusText.Text="●  "+hero.Status;
            var planMatches=live&&activePlan is not null&&hero.Id.Equals(activePlan.FlightNumber,StringComparison.OrdinalIgnoreCase);
            HeroDepartureGateText.Text=planMatches?activePlan!.DepartureGate??"—":"—";HeroArrivalGateText.Text=planMatches?activePlan!.ArrivalGate??"—":"—";
            var gateTip=planMatches?$"{activePlan!.GateAssignmentSource??"Unassigned"} • {activePlan.GateAssignmentConfidence??"None"}":"No gate assignment has been supplied.";
            HeroDepartureGateText.ToolTip=HeroArrivalGateText.ToolTip=gateTip;
            HeroStatusText.Foreground=OpsUi.Brush(hero.StatusColor);HeroStatusBadge.Background=OpsUi.Brush(hero.StatusBackground);
            HeroTimingText.Text=hero.Leg.Completed?"Flight complete":$"{(hero.Leg.EstimatedIn-hero.Leg.EstimatedOut).TotalMinutes:0} min block  •  {hero.Leg.EstimatedOut:dd MMM}  •  {hero.Out}Z";
            AircraftText.Text=AircraftIdentity.CanonicalType(planMatches?activePlan!.AircraftType:null,rotation!.AircraftId);
            HeroAircraftTypeText.Text=live?"ACTIVE ASSIGNMENT":"SAMPLE ASSIGNMENT";
            TrackerModeText.Text=live?"ACTIVE FLIGHT • SIMCONNECT":"REPLAY • SAMPLE DATA";
        }
        RefreshFlightsTable();
        UpdateDispatchState();
        if(live)
        {
            RotationGrid.ItemsSource=DashboardFlights.Select(f=>new {f.Id,f.Route,f.Out,f.In,Delay=$"{f.Leg.DepartureDelayMinutes:0} / {f.Leg.ArrivalDelayMinutes:0} min",Status=f.Leg.Completed?"Actual":"Projected"}).ToArray();
            OperationsFootnote.Text=hero is null?"NO ACTIVE ASSIGNMENT":$"{hero.Leg.ScheduledOut:dd MMM yyyy}  •  ALL TIMES UTC".ToUpperInvariant();
        }
        else OperationsFootnote.Text="02 SEP 2026  •  ALL TIMES UTC";
        RefreshOperations();
    }
    private void RefreshFlightsTable()
    {
        var selected=(DashboardFlightsGrid.SelectedItem as DashboardFlightRow)?.Key;
        var visible=DashboardFlights.Where(f=>flightTab=="all"||(flightTab=="assigned"&&!f.Leg.Completed)||(flightTab=="watchlist"&&dashboardState.Watchlist.Contains(f.Key))).ToArray();
        // Resetting an unchanged DataGrid source rebuilds its star-width cells and
        // briefly adds a horizontal scrollbar on every live snapshot. Compare the
        // complete immutable rows so genuine data changes still bind immediately.
        if(DashboardFlightsGrid.ItemsSource is not IEnumerable<DashboardFlightRow> existing || !existing.SequenceEqual(visible))
        {
            DashboardFlightsGrid.ItemsSource=visible;
            DashboardFlightsGrid.SelectedItem=visible.FirstOrDefault(f=>f.Key==selected)??visible.FirstOrDefault();
        }
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
    private void FlightDetails_Click(object sender,RoutedEventArgs e){if(activePlan is null)ShowDispatch();else ShowFlightTracking();}
    private void Preflight_Click(object sender,RoutedEventArgs e){if(HeroFlight is {} f)ShowFlight(f,true);else OpenTools();}
    private void FlightDeck_Click(object sender,RoutedEventArgs e)=>OpenTools();
    private void OpenTracker_Click(object sender,RoutedEventArgs e)=>ShowFlightTracking();
    private void ShowFlight(DashboardFlightRow flight,bool preflight)=>new FlightPreparationWindow(flight,dashboardState,SaveDashboard,preflight,()=>SetFlight_Click(this,new RoutedEventArgs())){Owner=this}.ShowDialog();
    internal void AcknowledgeAlert(string id)
    {
        if(!DashboardData.Alerts.Any(a=>a.Id==id))return;
        dashboardState.AcknowledgedAlerts.Add(id);SaveDashboard();
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
    private void RefreshMessages(){Shell.PresentMessages(DashboardData.Messages.Count(m=>!dashboardState.ReadMessages.Contains(m.Id)));}
    private void Message_Click(object sender,RoutedEventArgs e)
    {
        if(((Button)sender).Tag is not DashboardMessage message)return;
        dashboardState.ReadMessages.Add(message.Id);SaveDashboard();RefreshMessages();
        new OperationsWorkspaceWindow(new("COMPANY MESSAGE",message.Title,"LOCAL PRODUCT BRIEFING",
            [new("SOURCE","ALPHA 6 OPS","Bundled product notes"),new("STATUS","READ","Saved on this computer"),new("DELIVERY","LOCAL","No company messaging service")],
            [new(message.Id,message.Title,"Product briefing","Read",message.Body)])){Owner=this}.ShowDialog();
    }
    private void Dashboard_Click(object sender,RoutedEventArgs e)=>ShowDashboard();
    internal void ShowDashboard()=>Shell.Navigation.Request(ShellSection.Operations);
    internal void ShowFlightTracking()=>Shell.Navigation.Request(ShellSection.Flights);
    internal void ShowPilotLogbook()=>Shell.Navigation.Request(ShellSection.Logbook);
    internal void ShowDispatch()=>Shell.Navigation.Request(ShellSection.Dispatch);
    private void AcceptDispatchFlight(ActiveFlightPlan plan)
    {
        if(running)throw new InvalidOperationException("Finish the replay before accepting a different flight.");
        coordinator.AcceptAssignment(plan);
    }
    private bool SubmitCompletedPirep() => coordinator.SubmitPirep();
    private void CompletePirepCloseout() => ShowDashboard();
    private void UpdateDispatchState()
    {
        var state=activePlan is null?"IMPORT FLIGHT":flightState.Phase==FlightPhase.Complete?"FLIGHT CLOSEOUT":"VIEW RELEASE";
        DispatchSubtitleText.Text="FLIGHT DESK";HeroFlightDetailsButton.Content=activePlan is null?"OPEN DISPATCH    →":"VIEW FLIGHT TRACKING    →";DispatchView.Render(activePlan,state);
    }
    internal void OpenTools(){ToolsOverlay.Visibility=Visibility.Visible;PilotNameBox.Focus();}
    private void CloseTools_Click(object sender,RoutedEventArgs e)=>ToolsOverlay.Visibility=Visibility.Collapsed;
    internal OpsModule CreateFlightModule() => new("FLIGHTS & ROTATIONS","The aircraft's day, calculated from the current flight session",dashboardShowsLive?"ACTIVE ASSIGNMENT • SIMULATOR UTC":"RECORDED SCENARIO • 02 SEP 2026",
        [new("LEGS",DashboardFlights.Count.ToString(),"Current rotation"),new("COMPLETED",DashboardFlights.Count(f=>f.Leg.Completed).ToString(),"Confirmed block-in"),new("TURNAROUND",(dashboardShowsLive?liveRotation?.MinimumTurnMinutes??35:session.Rotation.MinimumTurnMinutes)+" MIN","Minimum aircraft turn")],
        DashboardFlights.Select(f=>new OpsRow(f.Id,$"{f.Route} / {f.Aircraft}",$"{f.Out} – {f.In} UTC",f.Status,
            $"Scheduled: {f.Leg.ScheduledOut:HH:mm}Z to {f.Leg.ScheduledIn:HH:mm}Z. Projected/actual: {f.Out}Z to {f.In}Z. Departure delay {f.Leg.DepartureDelayMinutes:0} min; arrival delay {f.Leg.ArrivalDelayMinutes:0} min. "+(f.Leg.Completed?"Confirmed completion recorded.":"This leg is projected from the current actuals and minimum turnaround."))).ToArray(),"flights-unbranded");
    private void Module_Click(object sender,RoutedEventArgs e)
    {
        Shell.Navigation.Request(ShellNavigation.FromLegacy((string)((Button)sender).Tag));
    }
    private void OpenLegacyModule(string name)
    {
        switch(name)
        {
            case "AlertsDemo": AllAlerts_Click(this,new RoutedEventArgs());return;
            case "Settings": new SettingsWindow(ConnectionBadgeText.Text, PilotNameBox.Text, OpenTools,
                () => FlightHistory_Click(this, new RoutedEventArgs()),
                () => Logs_Click(this, new RoutedEventArgs()),
                () => ExportLog_Click(this, new RoutedEventArgs()), MinimizeToTray,
                ProgramHealthText.Text, LogStatusText.Text, generalSettings, ApplyGeneralSettings) { Owner = this }.ShowDialog(); return;
            case "Aircraft": Fleet_Click(this,new RoutedEventArgs());return;
            case "Network": new NetworkWindow{Owner=this}.ShowDialog();return;
            case "FlightTracking": ShowFlightTracking();return;
            case "PilotLogbook":ShowPilotLogbook();return;
            case "Dispatch":ShowDispatch();return;
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
