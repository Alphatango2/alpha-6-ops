using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

// Explicit smoke-test coverage: observe transient WPF changes, not just final layout.
internal static class OperationsLayoutChecks
{
    internal static async Task RunAsync(MainWindow window,Action<bool,string> check)
    {
        var oldRows=window.DashboardFlights;var oldTab=(string)FlightSessionDiagnosticAccess.Get(window,"flightTab")!;
        var oldSelection=window.DashboardFlightsGrid.SelectedItem as DashboardFlightRow;
        var oldOverview=window.OperationsView.Overview;
        var width=window.Width;var height=window.Height;
        var property=typeof(MainWindow).GetProperty("DashboardFlights",BindingFlags.Instance|BindingFlags.NonPublic)!;
        void Rows(params DashboardFlightRow[] rows)=>property.SetValue(window,rows);
        var at=new DateTimeOffset(2026,9,27,1,30,0,TimeSpan.Zero);
        var row=new DashboardFlightRow(new("JST941","YBCS","YMML",at,at.AddHours(3),at,at.AddHours(3),0,0,false),"G-FENX",true,"A320","AT GATE");
        try
        {
            window.ShowDashboard();window.Width=3440;window.Height=1440;
            Rows(row);window.SelectFlightTab("all");
            await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            var grid=window.DashboardFlightsGrid;var viewer=Descendants(grid).OfType<ScrollViewer>().First();
            var source=grid.ItemsSource;var selected=grid.SelectedItem;var bars=0;var sizes=0;
            var descriptor=DependencyPropertyDescriptor.FromProperty(ScrollViewer.ComputedHorizontalScrollBarVisibilityProperty,typeof(ScrollViewer));
            EventHandler changed=(_,_)=>bars++;
            SizeChangedEventHandler resized=(_,_)=>sizes++;
            descriptor.AddValueChanged(viewer,changed);grid.SizeChanged+=resized;
            try
            {
                for(var i=0;i<8;i++)
                {
                    Rows(row with{Leg=row.Leg with{}});window.SelectFlightTab("all");
                    await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                }
                check(bars==0&&sizes==0,$"Equal flight-row refreshes cause no transient scrollbar or height oscillation (bars {bars}, sizes {sizes})");
                check(ReferenceEquals(source,grid.ItemsSource)&&ReferenceEquals(selected,grid.SelectedItem),"Equal immutable flight rows retain containers and selected row");
            }
            finally{descriptor.RemoveValueChanged(viewer,changed);grid.SizeChanged-=resized;}
            var updated=row with{OperationalStatus="AIRBORNE",Leg=row.Leg with{EstimatedIn=at.AddHours(4)}};
            Rows(updated);window.SelectFlightTab("all");
            await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            check(grid.Items.Cast<DashboardFlightRow>().Single()==updated&&((DashboardFlightRow)grid.SelectedItem).Status=="AIRBORNE","Changed flight data and status replace the displayed row while preserving selection by key");
            // Full record equality matters even when formatted text has not changed.
            var subMinute=updated with{Leg=updated.Leg with{EstimatedIn=updated.Leg.EstimatedIn.AddSeconds(1)}};
            Rows(subMinute);window.SelectFlightTab("all");
            check(grid.Items.Cast<DashboardFlightRow>().Single()==subMinute,"Same-text changes still refresh complete typed flight data");
            window.SelectFlightTab("watchlist");
            check(grid.Items.Count==0,"Changing to an empty watchlist still updates visible membership");
            window.SelectFlightTab("all");
            check(grid.Items.Count==1,"Returning to all flights restores the current row");
            Rows();window.SelectFlightTab("all");
            check(grid.Items.Count==0&&grid.SelectedItem is null,"Clearing flight rows removes stale selection and table data");
        }
        finally
        {
            property.SetValue(window,oldRows);window.SelectFlightTab(oldTab);window.Width=width;window.Height=height;
            window.DashboardFlightsGrid.SelectedItem=window.DashboardFlightsGrid.Items.Cast<DashboardFlightRow>().FirstOrDefault(r=>r.Key==oldSelection?.Key);
            if(oldOverview is not null)window.OperationsView.Present(oldOverview);
        }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}
    }
}
