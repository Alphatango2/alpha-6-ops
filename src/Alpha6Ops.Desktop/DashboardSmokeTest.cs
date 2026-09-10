using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal static class DashboardSmokeTest
{
    internal static async Task RunAsync(MainWindow window, string outputDirectory)
    {
        var checks=new List<string>();
        void Check(bool result,string label) {if(!result)throw new InvalidOperationException(label);checks.Add(label);}
        await HeaderSmokeTest.RunAsync(window, Check);
        await FlightLabSmokeTest.RunAsync(Check);
        await ResponsiveSmokeTest.RunAsync(window, outputDirectory, Check);
        await MonitorSmokeTest.RunAsync(window, outputDirectory, Check);
        window.SetAdvanced(false);window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        Check(window.DashboardFlights.Count==3 && window.HeroFlightText.Text=="A601","Dashboard hero and table use the current rotation");
        Check(window.ModuleTiles.Items.Count==8,"All eight photographic module tiles load");
        Check(window.ConnectionBadgeText.Text.Contains("DISCONNECTED",StringComparison.Ordinal),"Disconnected simulator is never presented as connected");
        Check(window.FleetCountText.Text=="1,006","Fleet chart uses the bundled reference catalog");
        Check(window.VersionText.Text.StartsWith("ALPHA 6 OPS  •  v",StringComparison.Ordinal)&&!window.VersionText.Text.Contains("PREVIEW",StringComparison.Ordinal),"Footer presents a clean product version without preview wording");
        var exitStyle=(Style)window.FindResource("OpsExit");
        Check(window.ExitOpsButton.Style==exitStyle&&window.ExitOpsButton.MinWidth>=88&&window.ExitOpsButton.MinHeight>=34,"Exit OPS is a full themed action button");
        Check(window.ExitOpsButton.BorderBrush.ToString()=="#FF415362"&&exitStyle.Triggers.OfType<Trigger>().Any(t=>t.Property==UIElement.IsMouseOverProperty&&Equals(t.Value,true)&&t.Setters.OfType<Setter>().Any(s=>s.Property==Control.BackgroundProperty&&s.Value is SolidColorBrush brush&&brush.Color==Color.FromRgb(255,218,0))),"Exit OPS uses the panel border and yellow navigation hover treatment");
        Check(window.FooterBrand.FontSize==11&&window.FooterBrand.FontWeight==FontWeights.SemiBold,"Left footer branding matches the updated version treatment");
        Check(window.NavigationTagline.Text=="YOU FLY THE AIRPLANE.\nWE RUN THE AIRLINE."&&window.NavigationTagline.TextWrapping==TextWrapping.NoWrap,"Navigation tagline keeps the pilot phrase on one line");
        Check(window.NavigationProfile.Margin.Top==23&&window.NavigationProfile.Margin.Bottom==-6,"Pilot identity section sits lower in the navigation rail");
        Check(AutomationProperties.GetName(window.FlightTrackingNavButton)=="FLIGHT TRACKING"&&window.HeroFlightDeckButton.Content?.ToString()=="VIEW FLIGHT DECK","Dashboard names the embedded tracker and links to Your Flight Deck");
        window.HeroFlightDetailsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();
        Check(window.DashboardScroll.Visibility==Visibility.Collapsed&&window.FlightTrackingView.Visibility==Visibility.Visible&&window.FlightTrackingView.EmptyState.Visibility==Visibility.Visible,"Flight Tracking opens inside the main application with a No Active Flight state");
        Capture(window,Path.Combine(outputDirectory,"flight-tracking-empty.png"));
        var trackingPlan=new ActiveFlightPlan("FJI911","DQ-FAM","NFFN","YSSY",new DateTimeOffset(2026,9,8,21,0,0,TimeSpan.Zero),new DateTimeOffset(2026,9,9,1,52,0,TimeSpan.Zero),"SimBrief",DepartureGate:"A4",ArrivalGate:"B12",Route:"NFFN NOBAR YSSY",RoutePoints:[new("NFFN",-17.7554,177.4434,"Departure"),new("NOBAR",-25.0,170.0),new("YSSY",-33.9399,151.1753,"Destination")],AircraftType:"A359");
        var trackingEvents=new[]{new TrackingEventEntry(new DateTimeOffset(2026,9,8,21,0,0,TimeSpan.Zero),"Assignment loaded","NFFN to YSSY","SimBrief route loaded")};
        window.FlightTrackingView.Render(trackingPlan,"A350-900 (No Cabin)","AT GATE","Assignment ready",null,null,null,0,trackingEvents,false);window.UpdateLayout();
        Check(window.FlightTrackingView.ActiveState.Visibility==Visibility.Visible&&window.FlightTrackingView.RouteText.Text.Contains("NFFN")&&window.FlightTrackingView.AircraftText.Text=="A359"&&window.FlightTrackingView.EventList.Items.Count==1,"Flight Tracking renders the SimBrief aircraft type, route summary, and chronological events");
        Check(window.FlightTrackingView.ArrivalText.Text=="—","Actual or estimated arrival remains blank before live flight telemetry provides an estimate");
        Check(window.FlightTrackingView.TrackingMap.RoutePointCount==3&&window.FlightTrackingView.TrackingMap.FittedZoom>1,"Flight Tracking plots and frames the saved SimBrief route");
        window.FlightTrackingView.Render(trackingPlan,"Alpha 6 Test A321","CRUISE","Live Flight Lab telemetry",null,null,null,58,trackingEvents,true,new(new DateTimeOffset(2026,9,8,23,0,0,TimeSpan.Zero),false,450,false,true,AltitudeFeet:35000,IndicatedAirspeedKnots:285,VerticalSpeedFeetPerMinute:0,GearExtendedRatio:0),.58);window.UpdateLayout();
        Check(window.FlightTrackingView.TrackingMap.HasLiveAircraft&&window.FlightTrackingView.TrackingMap.CompletedFraction==.58&&window.FlightTrackingView.TrackingMap.TrackPointCount==1,"Live telemetry moves the filled aircraft and splits completed from remaining route");
        Check(window.FlightTrackingView.ProgressAircraftTranslate.X>window.FlightTrackingView.ProgressTrack.ActualWidth*.5,"Filled progress aircraft advances across the operational progress track");
        Check(window.FlightTrackingView.DistanceRemainingText.Text.EndsWith(" NM")&&window.FlightTrackingView.DistanceRemainingText.Text!="— NM"&&window.FlightTrackingView.ArrivalText.Text!="—"&&window.FlightTrackingView.ScheduleVarianceText.Text.StartsWith("ESTIMATE"),"Airborne route progress supplies distance remaining, live ETA, and schedule variance");
        var eventMonitor=new FlightTrackingEventMonitor();var eventTime=new DateTimeOffset(2026,9,8,22,0,0,TimeSpan.Zero);
        var detected=new List<TrackingEventEntry>();for(var second=0;second<3;second++)detected.AddRange(eventMonitor.Observe(new(eventTime.AddSeconds(second),false,285,false,true,AltitudeFeet:12000+second*500,IndicatedAirspeedKnots:290,VerticalSpeedFeetPerMinute:1800,GearExtendedRatio:0),FlightPhase.Airborne,second==0?new(FlightPhase.Airborne,eventTime):null,.2,trackingPlan,null));
        Check(detected.Any(entry=>entry.Title=="Liftoff")&&detected.Any(entry=>entry.Title=="Initial climb")&&detected.All(entry=>entry.Detail.Contains("IAS")&&!entry.Detail.Contains("Route progress")&&!entry.Detail.Contains("Distance remaining")),"Checkpoint 4 events show compact operational telemetry without repeating route progress or distance");
        Capture(window,Path.Combine(outputDirectory,"flight-tracking-live-aircraft.png"));
        var completeHistory=Enumerable.Range(1,18).Select(index=>new TrackingEventEntry(eventTime.AddMinutes(index),$"Flight event {index}","Operational milestone confirmed",$"Operational milestone confirmed\nALT {index*2000:0} FT  •  IAS 285 KT  •  VS +1200 FPM  •  HDG 218°  •  NEAR TEST{index}")).ToArray();
        window.FlightTrackingView.Render(trackingPlan,"A350-900 (No Cabin)","CRUISE","History retention check",null,null,null,58,completeHistory,true,new(eventTime,false,450,false,true),.58);window.UpdateLayout();
        Check(window.FlightTrackingView.EventList.Items.Count==18,"Flight Event Log retains the complete flight beyond the former 12-event limit");
        Capture(window,Path.Combine(outputDirectory,"flight-tracking-full-event-history.png"));
        window.FlightTrackingView.TrackingMap.SetRoute([new("KLAX",33.9425,-118.4081,"Departure"),new("DATELINE",5,179),new("DATELINE2",-5,-179),new("YSSY",-33.9399,151.1753,"Destination")]);
        Check(window.FlightTrackingView.TrackingMap.CrossesDateLine,"Flight route globe unwraps international routes across the date line");
        window.FlightTrackingView.Render(trackingPlan,"A350-900 (No Cabin)","AT GATE","Assignment ready",null,null,null,0,trackingEvents,false);window.UpdateLayout();
        Capture(window,Path.Combine(outputDirectory,"flight-tracking-foundation.png"));
        window.FlightTrackingView.TrackingMap.Zoom(2);window.UpdateLayout();
        Check(window.FlightTrackingView.TrackingMap.ZoomLevel>3&&window.FlightTrackingView.TrackingMap.VisibleWaypointLabelCount>0,"Close route zoom reveals spaced SimBrief waypoint names");
        Capture(window,Path.Combine(outputDirectory,"flight-tracking-waypoint-labels.png"));
        window.FlightTrackingView.TrackingMap.Zoom(3);window.UpdateLayout();
        Check(window.FlightTrackingView.TrackingMap.ZoomLevel>9&&window.FlightTrackingView.TrackingMap.VisibleWaypointLabelCount>0,"Deep route zoom retains readable waypoint names");
        Capture(window,Path.Combine(outputDirectory,"flight-tracking-deep-zoom.png"));
        window.FlightTrackingView.TrackingMap.SetView(65,-110,1.1);window.UpdateLayout();
        Capture(window,Path.Combine(outputDirectory,"flight-tracking-horizon-clipping.png"));
        window.FlightTrackingView.Render(trackingPlan,"A350-900 (No Cabin)","AT GATE","Assignment ready",null,null,null,0,trackingEvents,false);window.UpdateLayout();
        var trackingWidth=window.Width;var trackingHeight=window.Height;
        foreach(var size in new[]{new Size(1920,1080),new Size(2560,1392)})
        {
            window.Width=size.Width;window.Height=size.Height;window.UpdateLayout();
            Check(window.FlightTrackingView.ActualWidth>0&&window.FlightTrackingView.ActualHeight>0&&window.FlightTrackingView.ActiveState.ActualHeight<=window.FlightTrackingView.ActualHeight+1,$"Flight Tracking fits the main workspace at {size.Width:0}x{size.Height:0}");
            Capture(window,Path.Combine(outputDirectory,$"flight-tracking-{size.Width:0}x{size.Height:0}.png"));
        }
        window.Width=trackingWidth;window.Height=trackingHeight;window.UpdateLayout();
        window.ShowDashboard();window.UpdateLayout();
        Check(window.DashboardScroll.Visibility==Visibility.Visible&&window.FlightTrackingView.Visibility==Visibility.Collapsed,"Dashboard navigation restores the main dashboard in place");
        window.OpenTrackerButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(window.FlightTrackingView.Visibility==Visibility.Visible,"Compact dashboard globe opens Flight Tracking");window.ShowDashboard();
        window.HeroFlightDeckButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(window.ToolsOverlay.Visibility==Visibility.Visible,"View Flight Deck opens the existing flight deck");window.ToolsOverlay.Visibility=Visibility.Collapsed;
        var sampleFlight=window.HeroFlightText.Text;
        window.HeroFlightText.Text="DAL742";window.UpdateLayout();
        Check(window.HeroFlightText.FontSize==48,"Combined airline ICAO and flight number use the larger hero treatment");
        Check(window.HeroFlightText.ActualWidth>0 && window.HeroFlightText.Text=="DAL742","Imported flight identifier fits without a separate airline badge");
        window.HeroFlightText.Text=sampleFlight;
        Capture(window,Path.Combine(outputDirectory,"dashboard-default.png"));
        var width=window.Width;var height=window.Height;
        window.Width=1366;window.Height=768;window.UpdateLayout();
        Check(window.DashboardScroll.ScrollableHeight<=1 && window.DashboardScroll.VerticalOffset==0,"1920x1080 and compact displays keep the dashboard stationary");
        Check(window.HeroFlightText.ActualWidth>0 && window.ViewAlertsButton.ActualWidth>0,"Scaled dashboard preserves the hero and alerts");
        Capture(window,Path.Combine(outputDirectory,"dashboard-1366.png"));
        window.Width=1100;window.UpdateLayout();Capture(window,Path.Combine(outputDirectory,"dashboard-1100.png"));
        Check(window.HeaderLogo.ActualWidth >= 250 && window.HeaderLogo.ActualHeight >= 95,"Complete logo has a larger high-quality rendering area");
        Check(window.ClockText.ActualWidth >= 140 && window.LocalClockText.ActualWidth >= 140,"Both clocks retain readable space at minimum window width");
        window.DashboardScroll.ScrollToBottom();window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        Check(window.DashboardScroll.VerticalOffset==0,"Dashboard does not move after a scroll request");
        Check(window.DashboardFlightsGrid.Columns[1].ActualWidth >= 80,"Compact operations table keeps route text visible");
        Capture(window,Path.Combine(outputDirectory,"dashboard-1100-stationary.png"));
        window.DashboardScroll.ScrollToTop();
        window.Width=width;window.Height=height;window.UpdateLayout();

        var first=window.DashboardFlights[0];
        window.ToggleWatch(first);window.SelectFlightTab("watchlist");
        Check(window.DashboardFlightsGrid.Items.Count==1,"Watchlist filters to the selected flight");
        var store=new DashboardStateStore(outputDirectory);
        Check(store.Load().Watchlist.Contains(first.Key),"Watchlist survives a state reload");
        window.ToggleWatch(first);
        Check(window.DashboardFlightsGrid.Items.Count==0 && window.EmptyFlightsText.Visibility==Visibility.Visible,"Empty watchlist has a useful empty state");
        window.SelectFlightTab("all");
        Check(window.DashboardFlightsGrid.Items.Count==3,"All-flights tab restores the rotation");

        var preparation=new FlightPreparationWindow(first,window.LocalDashboardState,()=>store.Save(window.LocalDashboardState),true){Owner=window};
        preparation.Show();preparation.SetCheck(0,true);preparation.SetCheck(1,true);preparation.UpdateLayout();
        Check(OpsUi.UsesWindowTheme(preparation),"Preflight uses the shared Alpha 6 window theme");
        Check(preparation.CompletedCount==2,"Preflight checklist updates completed count");
        Capture(preparation,Path.Combine(outputDirectory,"preflight-preview.png"));preparation.Close();
        Check(store.Load().PreflightChecks[first.Key].Count==2,"Preflight checklist survives a state reload");
        var reopened=new FlightPreparationWindow(first,store.Load(),()=>{},true){Owner=window};reopened.Show();
        Check(reopened.CompletedCount==2,"Reopened preflight shows saved checks");reopened.Close();

        window.AcknowledgeAlert("atl-wx");
        Check(window.AlertsItems.Items.Count==4 && store.Load().AcknowledgedAlerts.Contains("atl-wx"),"Acknowledging a demo alert persists and refills the alert panel");
        window.RouteMap.SelectStation("JFK");window.RouteMap.Zoom(1.25);
        Check(window.RouteMap.SelectedStation=="JFK" && window.RouteMap.ZoomLevel>1,"Map station selection and zoom change the view");
        window.RouteMap.ResetView();
        Check(window.RouteMap.SelectedStation=="ATL" && window.RouteMap.ZoomLevel==1,"Map reset restores its hub and scale");

        foreach(var name in new[]{"Operations","Maintenance","Crews","Passengers","Weather","Dispatch","OCC","Network"})
        {
            var module=DashboardData.Module(name);var desk=new OperationsWorkspaceWindow(module){Owner=window};desk.Show();desk.UpdateLayout();
            Check(OpsUi.UsesWindowTheme(desk),$"{name} workspace uses the shared Alpha 6 window theme");
            Check(desk.VisibleRowCount==module.Rows.Count,$"{name} workspace renders all records");
            Check(desk.Search("zzzz-no-match")==0,$"{name} workspace supports no-results searches");
            desk.Search("");var state=module.Rows[0].State;
            Check(desk.FilterState(state)==module.Rows.Count(r=>r.State==state),$"{name} workspace filters by status");
            desk.FilterState("All states");
            await desk.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            desk.UpdateLayout();
            Check(desk.RecordColumnWidth > 150,$"{name} workspace keeps the item column readable");
            if(name is "Maintenance" or "Weather" or "Operations")Capture(desk,Path.Combine(outputDirectory,name.ToLowerInvariant()+"-workspace.png"));
            desk.Close();
        }
        var flightDesk=new OperationsWorkspaceWindow(window.CreateFlightModule()){Owner=window};flightDesk.Show();flightDesk.UpdateLayout();
        Check(flightDesk.Search("KZZZ")==0 && flightDesk.Search("A601")==1,"Flight workspace searches actual rotation records");
        await flightDesk.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        Capture(flightDesk,Path.Combine(outputDirectory,"flight-workspace.png"));flightDesk.Close();
        var network=new NetworkWindow{Owner=window};network.Show();network.UpdateLayout();Check(OpsUi.UsesWindowTheme(network),"Network window uses the shared Alpha 6 window theme");
        var captions=Descendants<WindowCaptionButton>(network).ToArray();Check(captions.Select(c=>c.Action).Order().SequenceEqual(Enum.GetValues<WindowCaptionAction>()),"Shared title bar exposes all four caption actions");
        captions.Single(c=>c.Action==WindowCaptionAction.Minimize).ExecuteForTest();Check(network.WindowState==WindowState.Minimized,"Subwindow minimize caption action works");
        captions.Single(c=>c.Action==WindowCaptionAction.Restore).ExecuteForTest();Check(network.WindowState==WindowState.Normal,"Subwindow restore caption action works");
        captions.Single(c=>c.Action==WindowCaptionAction.Maximize).ExecuteForTest();Check(network.WindowState==WindowState.Maximized,"Subwindow maximize caption action works");
        captions.Single(c=>c.Action==WindowCaptionAction.Restore).ExecuteForTest();Capture(network,Path.Combine(outputDirectory,"network-preview.png"));captions.Single(c=>c.Action==WindowCaptionAction.Close).ExecuteForTest();Check(!network.IsVisible,"Subwindow close caption action works");
        var notice=new OpsNoticeWindow(window,"Simulator notice","This is a preview of the shared Alpha 6 notice style.");notice.Show();notice.UpdateLayout();Check(OpsUi.UsesWindowTheme(notice),"Application notices use the shared Alpha 6 window theme");Capture(notice,Path.Combine(outputDirectory,"notice-preview.png"));notice.Close();
        var generalSettings=new GeneralSettings(false,false,false,"KG","M","M",false);
        GeneralSettingsStore.Save(generalSettings,outputDirectory);
        Check(GeneralSettingsStore.Load(outputDirectory)==generalSettings,"General settings persist all behavior and unit choices");
        var settings=new SettingsWindow("DISCONNECTED","Test Pilot",()=>{},currentGeneralSettings:generalSettings){Owner=window};settings.Show();settings.UpdateLayout();
        Check(settings.SettingsTabs.Items.Count==7,"Settings workspace organizes all configuration sections");
        Check(settings.MinimizeToTrayToggle.IsChecked==false && settings.FlashNotificationToggle.IsChecked==false &&
            settings.NotificationSoundToggle.IsChecked==false && settings.AdvancedControlsToggle.IsChecked==false,
            "General settings render the saved behavior choices");
        Check(settings.ResetDefaultsButton.Margin.Right==12&&settings.ResetDefaultsButton.Width==128&&settings.SaveSettingsButton.Width==130,
            "General settings header separates and balances its actions");
        Check(new[]{settings.WeightUnitBox,settings.AltitudeUnitBox,settings.LandingDistanceUnitBox}.All(box=>box.Foreground.ToString()=="#FFFFFFFF"&&box.Template is not null),
            "Display-unit selections use readable light text");
        Check(settings.GeneralSettingsScroll.VerticalScrollBarVisibility==ScrollBarVisibility.Disabled,
            "General settings fit without a permanent scroll track");
        Capture(settings,Path.Combine(outputDirectory,"settings-general-preview.png"));
        settings.PluginsTab.IsSelected=true;settings.UpdateLayout();
        Check(settings.SimConnectPluginStatusText.Text=="BUILT IN" && settings.SimBriefPluginStatusText.Text.Length>0,"Plugins tab reports built-in integration status");
        Capture(settings,Path.Combine(outputDirectory,"settings-plugins-preview.png"));
        settings.LogsDiagnosticsTab.IsSelected=true;settings.UpdateLayout();
        Check(settings.FlightHistoryButton.IsVisible && settings.LogDatabaseButton.IsVisible && settings.ExportLogButton.IsVisible,
            "Logs and diagnostics provides flight history, database, and export actions");
        Check(settings.ProgramHealthText.Text.Length>0 && settings.LoggingStatusText.Text.Length>0,
            "Logs and diagnostics presents program-monitor and flight-log status");
        Capture(settings,Path.Combine(outputDirectory,"settings-logs-preview.png"));settings.Close();
        window.ToolsOverlay.Visibility=Visibility.Visible;window.UpdateLayout();Capture(window,Path.Combine(outputDirectory,"flight-tools-preview.png"));
        Check(window.ConnectButton.IsEnabled && window.ConnectFlightLabButton.IsEnabled && !window.DisconnectButton.IsEnabled && !window.LiveTimelineButton.IsEnabled,"Flight tools preserves real and virtual simulator connection guards");
        window.ToolsOverlay.Visibility=Visibility.Collapsed;
        window.SetHeaderWeather(null);window.RenderLocalWeather(DateTimeOffset.UtcNow);
        File.WriteAllText(Path.Combine(outputDirectory,"dashboard-smoke.json"),JsonSerializer.Serialize(new{passed=true,count=checks.Count,checks},new JsonSerializerOptions{WriteIndented=true}));
    }
    internal static void Capture(Window window,string path, double scale = 1)
    {
        window.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth * scale),(int)Math.Ceiling(window.ActualHeight * scale),96 * scale,96 * scale,PixelFormats.Pbgra32);bitmap.Render(window);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T:DependencyObject
    {
        for(var index=0;index<VisualTreeHelper.GetChildrenCount(parent);index++)
        {
            var child=VisualTreeHelper.GetChild(parent,index);if(child is T match)yield return match;
            foreach(var descendant in Descendants<T>(child))yield return descendant;
        }
    }
}
