using System;
using System.Linq;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

public partial class MainWindow
{
    public static readonly System.Windows.DependencyProperty SimulatorReadyActionProperty = System.Windows.DependencyProperty.Register(
        nameof(SimulatorReadyAction), typeof(string), typeof(MainWindow), new System.Windows.PropertyMetadata("LAUNCH & CONNECT"));
    public string SimulatorReadyAction
    {
        get => (string)GetValue(SimulatorReadyActionProperty);
        set => SetValue(SimulatorReadyActionProperty, value);
    }
    private DateTime nextSimulatorProcessCheck;
    private void UpdateSimulatorLaunchAction()
    {
        if (DateTime.UtcNow < nextSimulatorProcessCheck) return;
        nextSimulatorProcessCheck = DateTime.UtcNow.AddSeconds(5);
        SimulatorReadyAction = !diagnosticMode && SimulatorLauncher.IsRunning() ? "CLICK TO CONNECT" : "LAUNCH & CONNECT";
    }
    private LiveReading? currentLiveReading;
    private FlightAssociation liveAssociation = new(false, "FREE FLIGHT", []);
    private DateTimeOffset? observedSessionStart;
    private GeoPosition? observedStartPosition;
    private bool observedStartOnGround;
    private string? observedRegistration;
    private bool liveWasInMenu;
    private bool liveDataCurrent;
    private DateTime liveReceivedAt;
    private string? arrivalMismatch;
    private bool CanRenderAssignedFlight => liveAssociation.Accepted && liveDataCurrent &&
        currentLiveReading?.Evidence is { SimulationRunning: true, Position.IsValid: true };

    private void ResetLiveIdentity()
    {
        currentLiveReading = null; observedSessionStart = null; observedStartPosition = null;
        observedRegistration = null; liveWasInMenu = false; liveDataCurrent = false; arrivalMismatch = null;
        liveAssociation = new(false, "FREE FLIGHT", []);
    }
    private void ResetObservedSession(string reason)
    {
        RecordLog("observed_session_reset", liveLast, new { reason });
        liveRecorder = null; liveRotation = null; liveLast = null; liveAircraft = null;
        milestones.Clear(); LiveTimelineButton.IsEnabled = LiveDebriefButton.IsEnabled = false;
        ResetLiveIdentity();
    }
    private void ReconcileLiveReading(LiveReading reading)
    {
        var evidence = reading.Evidence ?? new();
        if (observedSessionStart is null && evidence.SimulationRunning == true && evidence.Position is { IsValid: true })
        {
            observedSessionStart = reading.Telemetry.At;
            observedStartPosition = evidence.Position;
            observedStartOnGround = reading.Telemetry.OnGround;
        }
        var plan = activePlan is null ? null : new FlightAssignment(activePlan.FlightNumber, activePlan.Registration,
            activePlan.Origin, activePlan.Destination, activePlan.PlannedDepartureUtc, activePlan.PlannedArrivalUtc);
        var association = FlightIdentity.Reconcile(plan, evidence with { Position = evidence.Position is null ? null : observedStartPosition },
            observedSessionStart ?? reading.Telemetry.At, observedStartOnGround, AirportCatalog.Find(activePlan?.Origin), liveAssociation.Accepted);
        if (association.Accepted != liveAssociation.Accepted || association.Explanation != liveAssociation.Explanation)
            RecordLog("flight_identification", reading.Telemetry.At, new { association.Mode, association.Accepted, association.Issues,
                evidence.Registration, evidence.Route, position = evidence.Position, nearby = evidence.Nearby, evidence.PlanStatus });
        liveAssociation = association;
        if (!association.Accepted) liveRotation = null;
        currentLiveReading = reading;
        liveReceivedAt = DateTime.UtcNow; liveDataCurrent = true;
    }
    private string LiveContextSummary()
    {
        var evidence = currentLiveReading?.Evidence;
        if (!liveDataCurrent) return "Telemetry unavailable — showing last observation";
        if (evidence?.SimulationRunning != true) return "Waiting for an active flight";
        if (arrivalMismatch is not null) return arrivalMismatch;
        if (liveAssociation.Issues.Count > 0) return liveAssociation.Accepted ? "Flight context needs review" : "Saved assignment needs review";
        if (evidence?.Position is null) return "Position unavailable";
        return evidence?.Nearby is { } nearby ? $"Near {nearby.Airport.Ident} · {nearby.DistanceNm:0.0} nm" : "Airport vicinity unknown";
    }
    private void MarkLiveUnavailable()
    {
        liveDataCurrent = false;
        if (currentLiveReading is not null) RefreshLiveTracker(liveAircraft, liveLast, liveRecorder?.Phase, "Telemetry unavailable. Waiting for reconnection.");
    }
    private void RenderObservedFlight()
    {
        if (currentLiveReading is not { } reading) return;
        var evidence = reading.Evidence ?? new();
        var route = evidence.Route;
        var nearby = evidence.Nearby;
        ApplyAirlineBrand(null);
        AirlineBrandNameText.Text = "LIVE AIRCRAFT";
        HeroFlightText.Text = route is null ? "FREE FLIGHT" : "MSFS FLIGHT";
        OriginCodeText.Text = route?.Origin ?? nearby?.Airport.Ident ?? "—";
        OriginCityText.Text = route is not null ? AirportCatalog.Find(route.Origin)?.City.ToUpperInvariant() ?? "PLANNED DEPARTURE"
            : nearby is not null ? "NEAR " + (string.IsNullOrWhiteSpace(nearby.Airport.City) ? nearby.Airport.Ident : nearby.Airport.City).ToUpperInvariant() : "LOCATION UNKNOWN";
        DestinationCodeText.Text = route?.Destination ?? "—";
        DestinationCityText.Text = route is not null ? AirportCatalog.Find(route.Destination)?.City.ToUpperInvariant() ?? "PLANNED DESTINATION" : "DESTINATION UNKNOWN";
        OriginCodeText.ToolTip = nearby is null ? evidence.PlanStatus : $"{nearby.Airport.Name} · {nearby.DistanceNm:0.0} nm · {nearby.Airport.Source}. Proximity does not establish the departure airport.";
        HeroDepartureText.Text = liveRecorder?.Events.FirstOrDefault(e => e.Phase == FlightPhase.TaxiOut)?.At.ToString("HH:mm") ?? "—";
        HeroArrivalText.Text = liveRecorder?.Events.FirstOrDefault(e => e.Phase == FlightPhase.Complete)?.At.ToString("HH:mm") ?? "—";
        HeroDepartureGateText.Text = HeroArrivalGateText.Text = "—";
        HeroDepartureGateText.ToolTip = HeroArrivalGateText.ToolTip = "No verified gate assignment";
        HeroStatusText.Text = !liveDataCurrent ? "LAST OBSERVED" : evidence.SimulationRunning != true ? "WAITING" : reading.Telemetry.Paused ? "PAUSED" : reading.Telemetry.Slewing ? "SLEW" : reading.Telemetry.OnGround ? "ON GROUND" : "AIRBORNE";
        HeroStatusText.Foreground = OpsUi.Brush("#FFDA00"); HeroStatusBadge.Background = OpsUi.Brush("#433817");
        HeroTimingText.Text = LiveContextSummary();
        HeroTimingText.ToolTip = string.Join(" ", new[] { liveAssociation.Explanation, evidence.PlanStatus, evidence.Warning, arrivalMismatch }.Where(s => !string.IsNullOrWhiteSpace(s)));
        AircraftText.Text = string.Join(" · ", new[] { reading.Aircraft, evidence.Registration }.Where(s => !string.IsNullOrWhiteSpace(s)));
        HeroAircraftTypeText.Text = liveDataCurrent ? "OBSERVED IN MSFS" : "LAST OBSERVED IN MSFS";
        TrackerModeText.Text = liveDataCurrent ? "LIVE FLIGHT · " + liveAssociation.Mode : "LAST OBSERVATION · TELEMETRY UNAVAILABLE";
        OperationsFootnote.Text = "NO VERIFIED SCHEDULE · LIVE SESSION RECORDED IN DIAGNOSTICS";
        EmptyFlightsText.Text = activePlan is null ? "Free flight is being observed. No airline schedule is assigned." : "Saved assignment is not linked to this flight. Open flight details to review the differences.";
    }
    private void ShowObservedFlightDetails()
    {
        if (currentLiveReading is not { } reading) { OpenTools(); return; }
        var evidence = reading.Evidence ?? new();
        var rows = new System.Collections.Generic.List<OpsRow>
        {
            new("AIRCRAFT", reading.Aircraft, evidence.Registration ?? "Registration unavailable", "MSFS", "Live aircraft identity; saved registration is never substituted."),
            new("POSITION", evidence.Position is { } p ? $"{p.Latitude:0.00000}, {p.Longitude:0.00000}" : "Unavailable", evidence.Nearby?.Airport.Name ?? "Airport unknown", evidence.Nearby?.Airport.Source ?? "MSFS", "Nearby airport is an approximation, not proof of departure or intended destination."),
            new("SIM PLAN", evidence.Route is { } route ? $"{route.Origin} → {route.Destination}" : "No readable route", evidence.PlanStatus, "MSFS active plan", "Some aircraft and activities do not publish a readable simulator flight plan; destination remains unknown."),
            new("ASSIGNMENT", activePlan is null ? "None" : $"{activePlan.FlightNumber} · {activePlan.Origin} → {activePlan.Destination}", liveAssociation.Accepted ? "Verified" : "Not linked", activePlan?.Source ?? "Pilot entry", liveAssociation.Explanation),
            new("SESSION", LiveContextSummary(), reading.Telemetry.At.ToString("u"), liveDataCurrent ? "Receiving data" : "Last observation", arrivalMismatch ?? evidence.Warning ?? "Aircraft swaps, position jumps and simulator time changes start a new observed session.")
        };
        new OperationsWorkspaceWindow(new("LIVE FLIGHT DETAILS", "Aircraft, location and flight-plan checks", "OBSERVATIONS AND SAVED ASSIGNMENT",
            [new("MODE", liveAssociation.Mode, "Live flight classification"), new("SCHEDULE", liveAssociation.Accepted ? "VERIFIED" : "NOT LINKED", "Requires matching live evidence"), new("DATA", liveDataCurrent ? "CURRENT" : "UNAVAILABLE", "Simulator telemetry")], rows.ToArray()),
            "REVIEW ASSIGNMENT", _ => SetFlight_Click(this, new System.Windows.RoutedEventArgs())) { Owner = this }.ShowDialog();
    }
}
