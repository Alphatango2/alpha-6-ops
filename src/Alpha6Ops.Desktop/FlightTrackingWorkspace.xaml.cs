using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Alpha6Ops.Core;
using System.Threading.Tasks;

namespace Alpha6Ops.Desktop;

public partial class FlightTrackingWorkspace : UserControl
{
    internal event EventHandler? FlightDeckRequested;
    internal event EventHandler? DispatchRequested;
    internal event EventHandler? GatesRequested;
    internal event EventHandler? ConfirmBlockInRequested;
    internal event EventHandler? ScheduleRequested;
    private void ConfirmBlockIn_Click(object sender,RoutedEventArgs e)=>ConfirmBlockInRequested?.Invoke(this,EventArgs.Empty);
    private void Schedule_Click(object sender,RoutedEventArgs e)=>ScheduleRequested?.Invoke(this,EventArgs.Empty);
    internal Func<bool>? PirepSubmissionRequested;
    internal event EventHandler? PirepCloseoutCompleted;

    public FlightTrackingWorkspace() => InitializeComponent();

    internal void Render(ActiveFlightPlan? plan, string? simulatorAircraft, string phase, string status,
        DateTimeOffset? actualOut, DateTimeOffset? actualIn, DateTimeOffset? estimatedIn, double progress,
        IEnumerable<TrackingEventEntry> events, bool connected,Telemetry? telemetry=null,double? routeProgress=null,bool assignmentVerified=true,bool routeReliable=true,bool complete=false,bool pirepReady=false)
    {
        EmptyState.Visibility=plan is null?Visibility.Visible:Visibility.Collapsed;
        ActiveState.Visibility=plan is null?Visibility.Collapsed:Visibility.Visible;
        if(plan is null)return;
        FlightNumberText.Text=plan.FlightNumber;
        RouteText.Text=$"{plan.Origin}  →  {plan.Destination}";
        AircraftText.Text=AircraftIdentity.CanonicalType(plan.AircraftType,simulatorAircraft);
        PhaseText.Text=phase;
        StatusText.Text=status;
        TrackingMap.SetRoute(plan.RoutePoints);
        TrackingMap.SetTelemetry(telemetry,routeProgress);
        TrackingSubtitle.Text=connected?(assignmentVerified?"ACTIVE FLIGHT • LIVE TELEMETRY":"ASSIGNMENT REVIEW • OBSERVED LIVE TELEMETRY"):telemetry is not null?"LAST OBSERVATION • TELEMETRY UNAVAILABLE":"ACTIVE ASSIGNMENT • READY FOR SIMULATOR";
        ScheduledOutText.Text=FlightClock.FormatScheduled(plan.PlannedDepartureUtc,null);
        ScheduledInText.Text=FlightClock.FormatScheduled(plan.PlannedArrivalUtc,null);
        ActualOutText.Text=actualOut?.UtcDateTime.ToString("HH:mm'Z'")??"—";
        DepartureGateText.Text=plan.DepartureGate??"—";ArrivalGateText.Text=plan.ArrivalGate??"—";
        DepartureGateText.ToolTip=ArrivalGateText.ToolTip=plan.GateAssignmentSource??"Unassigned";
        var continuousProgress=routeProgress is{} fractionValue?fractionValue*100:progress;
        var fraction=continuousProgress/100;
        var remaining=routeReliable?FlightMetrics.RemainingDistanceNm(plan.RoutePoints,fraction,telemetry,complete):double.NaN;
        DistanceRemainingText.Text=!double.IsFinite(remaining)?"— NM":remaining is >0 and <.1?"<0.1 NM":remaining is >=.1 and <1?$"{remaining:0.0} NM":$"{remaining:0} NM";
        DistanceRemainingText.ToolTip="Estimated geographical distance to destination. Confirmed block-in closes the flight.";
        var arrival=actualIn??estimatedIn;
        ArrivalText.Text=arrival?.UtcDateTime.ToString("HH:mm'Z'")??"—";
        var scheduleValid=actualOut is {} outAt?FlightClock.DepartureScheduleIsPlausible(plan,outAt):telemetry is {} t&&FlightClock.ScheduleIsPlausible(plan,t.At);
        ScheduleReviewButton.Visibility=telemetry is not null&&!scheduleValid?Visibility.Visible:Visibility.Collapsed;
        ScheduleReviewButton.ToolTip=telemetry is {} clock?$"Simulator {clock.At:dd MMM yyyy HH:mm}Z • Scheduled OUT {plan.PlannedDepartureUtc:dd MMM yyyy HH:mm}Z":null;
        if(!scheduleValid)ScheduleVarianceText.Text="SCHEDULE / SIM CLOCK REVIEW";
        else if(actualIn is not null)ScheduleVarianceText.Text=Variance(actualIn.Value-plan.PlannedArrivalUtc,"ACTUAL");
        else if(estimatedIn is not null)ScheduleVarianceText.Text=Variance(estimatedIn.Value-plan.PlannedArrivalUtc,"ESTIMATE");
        else ScheduleVarianceText.Text=complete?"ACTUAL IN UNAVAILABLE":!connected&&telemetry is not null?"TELEMETRY UNAVAILABLE • ESTIMATE PENDING":!routeReliable?"ROUTE POSITION REVIEW • ESTIMATE PENDING":phase.Contains("TAXI IN",StringComparison.OrdinalIgnoreCase)?"ARRIVAL ESTIMATE PENDING":"WAITING FOR AIRBORNE DATA";
        UpdateProgress(continuousProgress,true);
        if(!routeReliable&&!complete)ProgressText.Text="—";
        ProgressText.ToolTip=ProgressBar.ToolTip=complete?"Flight complete • actual block-in confirmed":routeReliable
            ?"Geographical route progress. 100% means the route endpoint is reached; flight completion requires block-in. Progress may decrease during vectors or a go-around."
            :"Route position unavailable • bar shows the last known geographical estimate.";
        ProgressAircraftIcon.Visibility=complete?Visibility.Collapsed:Visibility.Visible;SubmitPirepButton.Visibility=pirepReady?Visibility.Visible:Visibility.Collapsed;ProgressBar.Foreground=complete?OpsUi.Brush("#55D66B"):(Brush)FindResource("OpsYellow");ProgressText.Foreground=complete?OpsUi.Brush("#55D66B"):(Brush)FindResource("OpsYellow");ProgressBar.Margin=pirepReady?new Thickness(0,0,175,0):new Thickness(0);
        var rows=events.Reverse().OrderByDescending(entry=>entry.At).ToArray();EventList.ItemsSource=rows;EventEmptyText.Visibility=rows.Length==0?Visibility.Visible:Visibility.Collapsed;
    }

    private void FlightDeck_Click(object sender,RoutedEventArgs e)=>FlightDeckRequested?.Invoke(this,EventArgs.Empty);
    private void Gates_Click(object sender,RoutedEventArgs e)=>GatesRequested?.Invoke(this,EventArgs.Empty);
    private void Dispatch_Click(object sender,RoutedEventArgs e)=>DispatchRequested?.Invoke(this,EventArgs.Empty);
    private async void SubmitPirep_Click(object sender,RoutedEventArgs e)
    {
        SubmitPirepButton.IsEnabled=false;
        if(PirepSubmissionRequested?.Invoke()!=true){SubmitPirepButton.Content="SUBMISSION FAILED • TRY AGAIN";SubmitPirepButton.IsEnabled=true;return;}
        // Persistence and closeout have already finished in the command.
        for(var frame=0;frame<7;frame++){SubmitPirepButton.Content="TRANSMITTING"+new string('.',frame%4);await Task.Delay(220);}
        SubmitPirepButton.Content="PIREP COMPLETE";ProgressText.Text="COMPLETE";await Task.Delay(1100);PirepCloseoutCompleted?.Invoke(this,EventArgs.Empty);
    }
    private void ProgressTrack_SizeChanged(object sender,SizeChangedEventArgs e)=>PositionProgressMarker(ProgressBar.Value);

    private void UpdateProgress(double value,bool animate)
    {
        var target=Math.Clamp(value,0,100);
        var current=ProgressBar.Value;
        var currentX=ProgressAircraftTranslate.X;
        var targetX=MarkerPosition(target);

        ProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty,null);
        ProgressAircraftTranslate.BeginAnimation(TranslateTransform.XProperty,null);
        ProgressBar.Value=target;
        ProgressAircraftTranslate.X=targetX;
        // Preserve ordinary whole-percent presentation, but never round a value
        // below the geographical endpoint up to a completed route.
        ProgressText.Text=target is >=99.5 and <100?$"{Math.Floor(target*10)/10:0.0}%":$"{target:0}%";

        if(!animate||Math.Abs(target-current)<.01)return;
        var duration=TimeSpan.FromMilliseconds(850);
        var easing=new QuadraticEase{EasingMode=EasingMode.EaseOut};
        ProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty,new DoubleAnimation(current,target,duration){EasingFunction=easing,FillBehavior=FillBehavior.Stop});
        ProgressAircraftTranslate.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(currentX,targetX,duration){EasingFunction=easing,FillBehavior=FillBehavior.Stop});
    }

    private void PositionProgressMarker(double value)
    {
        ProgressAircraftTranslate.BeginAnimation(TranslateTransform.XProperty,null);
        ProgressAircraftTranslate.X=MarkerPosition(value);
    }

    private double MarkerPosition(double value)=>Math.Max(0,ProgressTrack.ActualWidth-ProgressAircraftIcon.Width)*Math.Clamp(value,0,100)/100;
    private static string Variance(TimeSpan difference,string prefix)
    {
        var minutes=(int)Math.Round(difference.TotalMinutes);return minutes==0?$"{prefix} • ON TIME":$"{prefix} • {Math.Abs(minutes)} MIN {(minutes<0?"EARLY":"LATE")}";
    }
}
