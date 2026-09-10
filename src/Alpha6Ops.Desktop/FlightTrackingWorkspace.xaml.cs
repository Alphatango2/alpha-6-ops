using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

public partial class FlightTrackingWorkspace : UserControl
{
    internal event EventHandler? FlightDeckRequested;

    public FlightTrackingWorkspace() => InitializeComponent();

    internal void Render(ActiveFlightPlan? plan, string? simulatorAircraft, string phase, string status,
        DateTimeOffset? actualOut, DateTimeOffset? actualIn, DateTimeOffset? estimatedIn, double progress,
        IEnumerable<string> events, bool connected,Telemetry? telemetry=null,double? routeProgress=null)
    {
        EmptyState.Visibility=plan is null?Visibility.Visible:Visibility.Collapsed;
        ActiveState.Visibility=plan is null?Visibility.Collapsed:Visibility.Visible;
        if(plan is null)return;
        FlightNumberText.Text=plan.FlightNumber;
        RouteText.Text=$"{plan.Origin}  →  {plan.Destination}";
        AircraftText.Text=string.Join(" • ",new[]{simulatorAircraft,plan.Registration}.Where(value=>!string.IsNullOrWhiteSpace(value)));
        PhaseText.Text=phase;
        StatusText.Text=status;
        TrackingMap.SetRoute(plan.RoutePoints);
        TrackingMap.SetTelemetry(telemetry,routeProgress);
        TrackingSubtitle.Text=connected?"ACTIVE FLIGHT • LIVE TELEMETRY":"ACTIVE ASSIGNMENT • READY FOR SIMULATOR";
        ConnectionText.Text=connected?"LIVE TRACKING":"ASSIGNMENT READY";
        ScheduledOutText.Text=plan.PlannedDepartureUtc.UtcDateTime.ToString("dd MMM • HH:mm'Z'");
        ScheduledInText.Text=plan.PlannedArrivalUtc.UtcDateTime.ToString("dd MMM • HH:mm'Z'");
        ActualOutText.Text=actualOut?.UtcDateTime.ToString("HH:mm:ss'Z'")??"—";
        ArrivalText.Text=(actualIn??estimatedIn)?.UtcDateTime.ToString("HH:mm:ss'Z'")??"—";
        DepartureGateText.Text=plan.DepartureGate??"—";ArrivalGateText.Text=plan.ArrivalGate??"—";
        ProgressBar.Value=Math.Clamp(progress,0,100);ProgressText.Text=$"{ProgressBar.Value:0}%";
        UpdateProgressMarker();
        var rows=events.Reverse().Take(12).ToArray();EventList.ItemsSource=rows;EventEmptyText.Visibility=rows.Length==0?Visibility.Visible:Visibility.Collapsed;
    }

    private void FlightDeck_Click(object sender,RoutedEventArgs e)=>FlightDeckRequested?.Invoke(this,EventArgs.Empty);
    private void ProgressTrack_SizeChanged(object sender,SizeChangedEventArgs e)=>UpdateProgressMarker();
    private void UpdateProgressMarker()=>ProgressAircraftTranslate.X=Math.Max(0,ProgressTrack.ActualWidth-ProgressAircraftIcon.Width)*ProgressBar.Value/100;
}
