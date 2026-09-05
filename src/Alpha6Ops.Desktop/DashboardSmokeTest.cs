using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Alpha6Ops.Desktop;

internal static class DashboardSmokeTest
{
    internal static async Task RunAsync(MainWindow window, string outputDirectory)
    {
        var checks=new List<string>();
        void Check(bool result,string label) {if(!result)throw new InvalidOperationException(label);checks.Add(label);}
        window.SetAdvanced(false);window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        Check(window.DashboardFlights.Count==3 && window.HeroFlightText.Text=="A601","Dashboard hero and table use the current rotation");
        Check(window.ModuleTiles.Items.Count==8,"All eight photographic module tiles load");
        Check(window.ConnectionBadgeText.Text.Contains("DISCONNECTED",StringComparison.Ordinal),"Disconnected simulator is never presented as connected");
        Check(window.FleetCountText.Text=="1,006","Fleet chart uses the bundled reference catalog");
        Capture(window,Path.Combine(outputDirectory,"dashboard-default.png"));
        var width=window.Width;var height=window.Height;
        window.Width=1366;window.Height=768;window.UpdateLayout();
        Check(window.DashboardScroll.ScrollableHeight>0,"Compact displays can scroll to every dashboard panel");
        Check(window.HeroFlightText.ActualWidth>0 && window.ViewAlertsButton.ActualWidth>0,"Compact dashboard preserves the hero and alerts");
        Capture(window,Path.Combine(outputDirectory,"dashboard-1366.png"));
        window.Width=1100;window.UpdateLayout();Capture(window,Path.Combine(outputDirectory,"dashboard-1100.png"));
        window.DashboardScroll.ScrollToBottom();window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        Check(window.DashboardFlightsGrid.Columns[1].ActualWidth >= 80,"Compact operations table keeps route text visible");
        Capture(window,Path.Combine(outputDirectory,"dashboard-1100-lower.png"));
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
        var network=new NetworkWindow{Owner=window};network.Show();network.UpdateLayout();Capture(network,Path.Combine(outputDirectory,"network-preview.png"));network.Close();
        window.ToolsOverlay.Visibility=Visibility.Visible;window.UpdateLayout();Capture(window,Path.Combine(outputDirectory,"flight-tools-preview.png"));
        Check(window.ConnectButton.IsEnabled && !window.DisconnectButton.IsEnabled && !window.LiveTimelineButton.IsEnabled,"Flight tools preserves simulator connection guards");
        window.ToolsOverlay.Visibility=Visibility.Collapsed;
        File.WriteAllText(Path.Combine(outputDirectory,"dashboard-smoke.json"),JsonSerializer.Serialize(new{passed=true,count=checks.Count,checks},new JsonSerializerOptions{WriteIndented=true}));
    }
    internal static void Capture(Window window,string path)
    {
        window.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
}
