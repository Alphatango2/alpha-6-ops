using System;
using System.Windows.Controls;
using Alpha6Ops.Desktop.MapV2;

namespace Alpha6Ops.Desktop;

public partial class MainWindow
{
    private readonly FlightTrackPresentationHistory mapTrackHistory=new();
    // Transitional names for existing current-flight presenters and their regression checks.
    // Every alias targets a visible control in the Sierra workspace; no legacy page is retained.
    internal TextBlock HeroFlightText => OperationsView.HeroFlightText;
    internal TextBlock OriginCodeText => OperationsView.OriginCodeText;
    internal TextBlock DestinationCodeText => OperationsView.DestinationCodeText;
    internal TextBlock OriginCityText => OperationsView.OriginCityText;
    internal TextBlock DestinationCityText => OperationsView.DestinationCityText;
    internal TextBlock HeroDepartureText => OperationsView.HeroDepartureText;
    internal TextBlock HeroArrivalText => OperationsView.HeroArrivalText;
    internal TextBlock HeroDepartureGateText => OperationsView.HeroDepartureGateText;
    internal TextBlock HeroArrivalGateText => OperationsView.HeroArrivalGateText;
    internal TextBlock HeroStatusText => OperationsView.HeroStatusText;
    internal TextBlock HeroTimingText => OperationsView.HeroTimingText;
    internal TextBlock AircraftText => OperationsView.AircraftText;
    internal TextBlock HeroAircraftTypeText => OperationsView.HeroAircraftTypeText;
    internal TextBlock TrackerModeText => OperationsView.TrackerModeText;
    internal TextBlock OperationsFootnote => OperationsView.OperationsFootnote;
    internal TextBlock EmptyFlightsText => OperationsView.EmptyFlightsText;
    internal Border HeroStatusBadge => OperationsView.HeroStatusBadge;
    internal Button HeroFlightDetailsButton => OperationsView.HeroFlightDetailsButton;
    internal Button OpenTrackerButton => OperationsView.OpenTrackerButton;
    internal Button WatchFlightButton => OperationsView.WatchFlightButton;
    internal Button MyFlightsTab => OperationsView.MyFlightsTab;
    internal Button AssignedFlightsTab => OperationsView.AssignedFlightsTab;
    internal Button WatchlistTab => OperationsView.WatchlistTab;
    internal Grid HeroProgressTrack => OperationsView.HeroProgressTrack;
    internal ProgressBar ReplayProgress => OperationsView.ReplayProgress;
    internal DataGrid DashboardFlightsGrid => OperationsView.DashboardFlightsGrid;
    internal ScrollViewer DashboardScroll => OperationsView.Scroller;
    private void InitializeOperations()
    {
        OperationsView.TrackingRequested+=ShowFlightTracking;
        OperationsView.CurrentFlightRequested+=()=>{if(activePlan is null)ShowDispatch();else ShowFlightTracking();};
        OperationsView.FilterRequested+=SelectFlightTab;
        OperationsView.WatchRequested+=()=>{if(SelectedDashboardFlight is {} flight)ToggleWatch(flight);};
        OperationsView.PreflightRequested+=()=>{if(HeroFlight is {} flight)ShowFlight(flight,true);else ShowDispatch();};
        OperationsView.FlightDetailsRequested+=()=>{if(DashboardFlightsGrid.SelectedItem is DashboardFlightRow flight)ShowFlight(flight,false);};
        OperationsView.SelectionChanged+=UpdateWatchButton;
    }
    private void RefreshOperations()
    {
        if(OperationsView is null)return;
        var state=flightState;mapTrackHistory.Observe(state);
        OperationsView.Present(OperationsOverviewQuery.Project(state,!dashboardShowsLive,DashboardFlights,localWeather,weatherRefreshFailed,DateTimeOffset.UtcNow)
            with{MapTrack=mapTrackHistory.Capture()});
    }
}
