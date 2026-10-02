using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Alpha6Ops.Desktop;

public partial class OperationsWorkspace : UserControl
{
    internal event Action? TrackingRequested, CurrentFlightRequested, WatchRequested, PreflightRequested, FlightDetailsRequested, SelectionChanged;
    internal event Action<string>? FilterRequested;
    internal OperationsOverview? Overview { get; private set; }
    public OperationsWorkspace()
    {
        InitializeComponent();SizeChanged+=(_,_)=>Reflow();Loaded+=(_,_)=>Reflow();
        MapHost.ContextChanged+=text=>MapContextText.Text=text;
        MapHost.LayoutRequested+=Reflow;
        IsVisibleChanged+=(_,_)=>{if(IsVisible && Overview is {} model)MapHost.Present(model);};
    }
    internal void Present(OperationsOverview overview)
    {
        // These rows are immutable value records. Retain WPF containers when a new
        // telemetry projection has the same visible values; changed rows still bind normally.
        Overview=overview;PresentRows(Metrics,overview.Metrics);PresentRows(AlertsList,overview.Alerts);
        PresentRows(AirportsList,overview.Airports);PresentRows(ActivityList,overview.Activity);
        ContextText.Text=overview.Context;FreshnessText.Text=overview.Freshness;
        ScopeText.Text=overview.Source==OperationsProvenance.Demo?"REPLAY SCENARIO":overview.Source==OperationsProvenance.Stale?"LAST OBSERVED":"LOCAL OPERATIONS";
        CurrentFlightBody.Visibility=overview.Source==OperationsProvenance.Unavailable?Visibility.Collapsed:Visibility.Visible;
        CurrentFlightEmpty.Visibility=overview.Source==OperationsProvenance.Unavailable?Visibility.Visible:Visibility.Collapsed;
        MapContextText.Text=overview.MapContext;FlightsSourceText.Text=overview.FlightsSource;
        WeatherTitleText.Text=overview.WeatherTitle;WeatherDetailText.Text=overview.WeatherDetail;
        AlertsEmpty.Visibility=overview.Alerts.Count==0?Visibility.Visible:Visibility.Collapsed;
        AirportsEmpty.Visibility=overview.Airports.Count==0?Visibility.Visible:Visibility.Collapsed;
        ActivityEmpty.Visibility=overview.Activity.Count==0?Visibility.Visible:Visibility.Collapsed;
        ActivityEmpty.Message=overview.Source==OperationsProvenance.Demo ? "Sample rotation preview. Run a recorded scenario from Flight tools to inspect its milestones." : "Recorded milestones appear here as the session progresses. No automated OCC decisions are generated.";
        // Hidden maps retain only the newest immutable overview; the host owns
        // suspension and never projects this background stream into the browser.
        if(IsVisible||MapHost.UseV2)MapHost.Present(overview);
        Reflow();
    }
    private static void PresentRows<T>(ItemsControl control,IReadOnlyList<T> rows)
    {
        if(control.ItemsSource is not IEnumerable<T> existing || !existing.SequenceEqual(rows))control.ItemsSource=rows;
    }
    private void Reflow()
    {
        var width=Math.Max(0,ActualWidth-36);var narrow=width<1050;var bottomStack=width<1400;
        MainLayout.ColumnDefinitions[1].Width=new GridLength(narrow?0:width>=1900?390:320);
        Grid.SetColumn(SideRail,narrow?0:1);Grid.SetRow(SideRail,narrow?1:0);
        MapPanel.Margin=new Thickness(0,0,narrow?0:14,narrow?14:0);
        MapPanel.Height=width>=1900?620:width>=1400?430:510;
        if(MapHost.UseV2)MapPanel.Height=Math.Max(MapPanel.Height,550);
        SideRail.Height=narrow?double.NaN:MapPanel.Height;
        SideRail.RowDefinitions[0].Height=narrow?GridLength.Auto:new GridLength(.85,GridUnitType.Star);
        SideRail.RowDefinitions[1].Height=narrow?GridLength.Auto:new GridLength(1.15,GridUnitType.Star);
        SideRail.Margin=new Thickness(0);
        BottomLayout.ColumnDefinitions[1].Width=bottomStack?new GridLength(0):new GridLength(1,GridUnitType.Star);
        Grid.SetColumn(ActivityPanel,bottomStack?0:1);Grid.SetRow(ActivityPanel,bottomStack?1:0);
        FlightsPanel.Margin=new Thickness(0,0,bottomStack?0:14,bottomStack?14:0);
        ScopeBadge.Visibility=width<800?Visibility.Collapsed:Visibility.Visible;
        MapContextText.Visibility=width<800?Visibility.Collapsed:Visibility.Visible;
        Grid.SetColumn(HeroFlightDetailsButton,width<600?0:1);
        HeroFlightDetailsButton.HorizontalAlignment=width<600?HorizontalAlignment.Right:HorizontalAlignment.Stretch;
        // On compact windows the shell's Dispatch destination remains available;
        // keep the contextual action on its own line instead of obscuring the identifier.
        HeroFlightDetailsButton.Margin=width<600?new Thickness(0,38,0,0):new Thickness(0);
        foreach(var grid in FindVisual<UniformGrid>(Metrics))grid.Columns=width>=1400?6:width>=750?3:2;
    }
    private static System.Collections.Generic.IEnumerable<T> FindVisual<T>(DependencyObject parent) where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        { var child=VisualTreeHelper.GetChild(parent,i);if(child is T match)yield return match;foreach(var nested in FindVisual<T>(child))yield return nested; }
    }
    private void Tracking_Click(object sender,RoutedEventArgs e)=>TrackingRequested?.Invoke();
    private void CurrentFlight_Click(object sender,RoutedEventArgs e)=>CurrentFlightRequested?.Invoke();
    private void Filter_Click(object sender,RoutedEventArgs e)=>FilterRequested?.Invoke((string)((Button)sender).Tag);
    private void Watch_Click(object sender,RoutedEventArgs e)=>WatchRequested?.Invoke();
    private void Preflight_Click(object sender,RoutedEventArgs e)=>PreflightRequested?.Invoke();
    private void Selection_Changed(object sender,SelectionChangedEventArgs e)=>SelectionChanged?.Invoke();
    private void Flight_DoubleClick(object sender,MouseButtonEventArgs e)=>FlightDetailsRequested?.Invoke();
}
