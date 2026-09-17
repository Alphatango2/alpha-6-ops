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
    internal Func<bool>? PirepSubmissionRequested;
    internal event EventHandler? PirepCloseoutCompleted;

    public FlightTrackingWorkspace() => InitializeComponent();

    internal void Render(ActiveFlightPlan? plan, string? simulatorAircraft, string phase, string status,
        DateTimeOffset? actualOut, DateTimeOffset? actualIn, DateTimeOffset? estimatedIn, double progress,
        IEnumerable<TrackingEventEntry> events, bool connected,Telemetry? telemetry=null,double? routeProgress=null)
    {
        EmptyState.Visibility=plan is null?Visibility.Visible:Visibility.Collapsed;
        ActiveState.Visibility=plan is null?Visibility.Collapsed:Visibility.Visible;
        if(plan is null)return;
        FlightNumberText.Text=plan.FlightNumber;
        RouteText.Text=$"{plan.Origin}  →  {plan.Destination}";
        AircraftText.Text=!string.IsNullOrWhiteSpace(plan.AircraftType)?plan.AircraftType:simulatorAircraft??"—";
        PhaseText.Text=phase;
        StatusText.Text=status;
        var complete=phase.Contains("COMPLETE",StringComparison.OrdinalIgnoreCase);
        TrackingMap.SetRoute(plan.RoutePoints);
        TrackingMap.SetTelemetry(telemetry,routeProgress);
        TrackingSubtitle.Text=connected?"ACTIVE FLIGHT • LIVE TELEMETRY":"ACTIVE ASSIGNMENT • READY FOR SIMULATOR";
        ScheduledOutText.Text=FlightClock.FormatScheduled(plan.PlannedDepartureUtc,plan.DepartureUtcOffsetMinutes);
        ScheduledInText.Text=FlightClock.FormatScheduled(plan.PlannedArrivalUtc,plan.ArrivalUtcOffsetMinutes);
        ActualOutText.Text=actualOut?.UtcDateTime.ToString("HH:mm'Z'")??"—";
        DepartureGateText.Text=plan.DepartureGate??"—";ArrivalGateText.Text=plan.ArrivalGate??"—";
        var continuousProgress=complete?100:TrackingMap.HasLiveAircraft?TrackingMap.CompletedFraction*100:progress;
        var fraction=continuousProgress/100;
        var remaining=FlightMetrics.RemainingDistanceNm(plan.RoutePoints,fraction);
        DistanceRemainingText.Text=double.IsFinite(remaining)?$"{remaining:0} NM":"— NM";
        var arrival=actualIn??estimatedIn;
        ArrivalText.Text=arrival?.UtcDateTime.ToString("HH:mm'Z'")??"—";
        var scheduleValid=actualOut is null||FlightClock.DepartureScheduleIsPlausible(plan,actualOut.Value);
        if(!scheduleValid)ScheduleVarianceText.Text="SCHEDULE / SIM CLOCK REVIEW";
        else if(actualIn is not null)ScheduleVarianceText.Text=Variance(actualIn.Value-plan.PlannedArrivalUtc,"ACTUAL");
        else if(estimatedIn is not null)ScheduleVarianceText.Text=Variance(estimatedIn.Value-plan.PlannedArrivalUtc,"ESTIMATE");
        else ScheduleVarianceText.Text="WAITING FOR AIRBORNE DATA";
        UpdateProgress(continuousProgress,true);
        ProgressAircraftIcon.Visibility=complete?Visibility.Collapsed:Visibility.Visible;SubmitPirepButton.Visibility=complete?Visibility.Visible:Visibility.Collapsed;ProgressBar.Foreground=complete?OpsUi.Brush("#55D66B"):(Brush)FindResource("OpsYellow");ProgressText.Foreground=complete?OpsUi.Brush("#55D66B"):(Brush)FindResource("OpsYellow");ProgressBar.Margin=complete?new Thickness(0,0,175,0):new Thickness(0);
        var rows=events.Reverse().ToArray();EventList.ItemsSource=rows;EventEmptyText.Visibility=rows.Length==0?Visibility.Visible:Visibility.Collapsed;
    }

    private void FlightDeck_Click(object sender,RoutedEventArgs e)=>FlightDeckRequested?.Invoke(this,EventArgs.Empty);
    private void Dispatch_Click(object sender,RoutedEventArgs e)=>DispatchRequested?.Invoke(this,EventArgs.Empty);
    private async void SubmitPirep_Click(object sender,RoutedEventArgs e)
    {
        SubmitPirepButton.IsEnabled=false;
        for(var frame=0;frame<7;frame++){SubmitPirepButton.Content="TRANSMITTING"+new string('.',frame%4);await Task.Delay(220);}
        if(PirepSubmissionRequested?.Invoke()!=true){SubmitPirepButton.Content="SUBMISSION FAILED • TRY AGAIN";SubmitPirepButton.IsEnabled=true;return;}
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
        ProgressText.Text=$"{target:0}%";

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
