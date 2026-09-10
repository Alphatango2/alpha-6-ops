using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

public partial class FlightTrackingWorkspace : UserControl
{
    internal event EventHandler? FlightDeckRequested;

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
        TrackingMap.SetRoute(plan.RoutePoints);
        TrackingMap.SetTelemetry(telemetry,routeProgress);
        TrackingSubtitle.Text=connected?"ACTIVE FLIGHT • LIVE TELEMETRY":"ACTIVE ASSIGNMENT • READY FOR SIMULATOR";
        ConnectionText.Text=connected?"LIVE TRACKING":"ASSIGNMENT READY";
        ScheduledOutText.Text=plan.PlannedDepartureUtc.UtcDateTime.ToString("dd MMM • HH:mm'Z'");
        ScheduledInText.Text=plan.PlannedArrivalUtc.UtcDateTime.ToString("dd MMM • HH:mm'Z'");
        ActualOutText.Text=actualOut?.UtcDateTime.ToString("HH:mm:ss'Z'")??"—";
        DepartureGateText.Text=plan.DepartureGate??"—";ArrivalGateText.Text=plan.ArrivalGate??"—";
        var continuousProgress=TrackingMap.HasLiveAircraft?TrackingMap.CompletedFraction*100:progress;
        var fraction=continuousProgress/100;
        var remaining=FlightMetrics.RemainingDistanceNm(plan.RoutePoints,fraction);
        DistanceRemainingText.Text=double.IsFinite(remaining)?$"{remaining:0} NM":"— NM";
        DateTimeOffset? liveEta=null;
        if(telemetry is {OnGround:false,GroundSpeedKnots:>=60}&&double.IsFinite(remaining))liveEta=telemetry.At.AddHours(remaining/Math.Max(telemetry.GroundSpeedKnots,100));
        var arrival=actualIn??liveEta;
        ArrivalText.Text=arrival?.UtcDateTime.ToString("HH:mm:ss'Z'")??"—";
        if(actualIn is not null)ScheduleVarianceText.Text=Variance(actualIn.Value-plan.PlannedArrivalUtc,"ACTUAL");
        else if(liveEta is not null)ScheduleVarianceText.Text=Variance(liveEta.Value-plan.PlannedArrivalUtc,"ESTIMATE");
        else ScheduleVarianceText.Text="WAITING FOR AIRBORNE DATA";
        UpdateProgress(continuousProgress,true);
        var rows=events.Reverse().Take(12).ToArray();EventList.ItemsSource=rows;EventEmptyText.Visibility=rows.Length==0?Visibility.Visible:Visibility.Collapsed;
    }

    private void FlightDeck_Click(object sender,RoutedEventArgs e)=>FlightDeckRequested?.Invoke(this,EventArgs.Empty);
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
