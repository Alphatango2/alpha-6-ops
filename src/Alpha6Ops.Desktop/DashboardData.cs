using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal sealed record DashboardTile(string Name, string Label, string Description, string Image);
internal sealed record DashboardAlert(string Id, string Title, string Summary, string Detail, string Color, string Age, string Icon = "\uE7BA");
internal sealed record DashboardMessage(string Id, string Title, string Subtitle, string Body, string Icon, string Color, string ReadLabel = "NEW");
internal sealed record OpsRow(string Reference, string Item, string Timing, string State, string Detail);
internal sealed record OpsMetric(string Label, string Value, string Note);
internal sealed record OpsModule(string Title, string Subtitle, string Source, IReadOnlyList<OpsMetric> Metrics, IReadOnlyList<OpsRow> Rows, string Image = "occ");
internal sealed record DashboardFlightRow(LegProjection Leg, string Aircraft)
{
    public string Id => Leg.Id;
    public string Route => $"{DashboardData.AirportCode(Leg.Origin)} → {DashboardData.AirportCode(Leg.Destination)}";
    public string Out => Leg.EstimatedOut.UtcDateTime.ToString("HH:mm");
    public string In => Leg.EstimatedIn.UtcDateTime.ToString("HH:mm");
    public string Status => Leg.Completed ? "COMPLETE" : Leg.DepartureDelayMinutes > 0 ? $"+{Leg.DepartureDelayMinutes:0} MIN" : "ON TIME";
    public string StatusColor => Leg.Completed ? "#60B7FF" : Leg.DepartureDelayMinutes > 0 ? "#FFDE36" : "#82DF69";
    public string StatusBackground => Leg.Completed ? "#142D45" : Leg.DepartureDelayMinutes > 0 ? "#3D3510" : "#193821";
    public string Key => $"{Aircraft}|{Leg.ScheduledOut:yyyy-MM-dd}|{Id}";
}

// Local convenience state only. These are pilot annotations, never dispatch authorizations.
internal sealed class DashboardState
{
    public HashSet<string> Watchlist { get; set; } = [];
    public HashSet<string> AcknowledgedAlerts { get; set; } = [];
    public HashSet<string> ReadMessages { get; set; } = [];
    public Dictionary<string, HashSet<string>> PreflightChecks { get; set; } = [];
}
internal sealed class DashboardStateStore(string root)
{
    internal string FilePath => Path.Combine(root, "dashboard-state.json");
    internal DashboardState Load()
    {
        if (!File.Exists(FilePath)) return new();
        var state = JsonSerializer.Deserialize<DashboardState>(File.ReadAllText(FilePath)) ?? new();
        state.Watchlist ??= []; state.AcknowledgedAlerts ??= []; state.ReadMessages ??= []; state.PreflightChecks ??= [];
        return state;
    }
    internal void Save(DashboardState state)
    {
        Directory.CreateDirectory(root);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, true);
    }
}

internal static class DashboardData
{
    internal static string AirportCode(string code) => code.Length == 4 && code.StartsWith('K') ? code[1..] : code;
    internal static string City(string code) => AirportCode(code) switch
    {
        "ORD" => "CHICAGO", "DTW" => "DETROIT", "JFK" => "NEW YORK", "ATL" => "ATLANTA", "MKE" => "MILWAUKEE",
        "SEA" => "SEATTLE", "LAX" => "LOS ANGELES", "DEN" => "DENVER", "MIA" => "MIAMI", "MSP" => "MINNEAPOLIS",
        "BOS" => "BOSTON", "SLC" => "SALT LAKE CITY", _ => code
    };
    internal static readonly DashboardTile[] Tiles =
    [
        new("Flights", "FLY", "View flights & prepare", "Assets/Dashboard/hero.png"),
        new("Operations", "OPERATIONS", "Network, IRROPS, diversions", "Assets/Dashboard/operations.png"),
        new("Aircraft", "AIRCRAFT", "Registrations, types, fleet", "Assets/Dashboard/aircraft.png"),
        new("Maintenance", "MAINTENANCE", "Discrepancies, MEL, service", "Assets/Dashboard/maintenance.png"),
        new("Crews", "CREWS", "Pairings, schedules, reserve", "Assets/Dashboard/crews.png"),
        new("Passengers", "PASSENGERS", "Connections, loads, rebooking", "Assets/Dashboard/passengers.png"),
        new("Weather", "WEATHER", "Airport conditions & outlook", "Assets/Dashboard/weather.png"),
        new("OCC", "OCC", "Operations control overview", "Assets/Dashboard/occ.png")
    ];
    internal static readonly DashboardAlert[] Alerts =
    [
        new("atl-wx", "ATL – THUNDERSTORMS", "Arrival rate reduced to 48/hr\nExpect delays 15–30 min", "Demonstration scenario: convective weather is affecting the Atlanta arrival bank. Review the weather desk and the inbound connection bank before considering a recovery plan. This is sample scenario data; no live weather or ATC feed is connected.", "#FF484E", "17m ago"),
        new("hold", "DL2147   MSP → ATL", "Holding 19 min\nDiversion review requested", "Demonstration scenario: DL2147 is holding on the Atlanta arrival. Dispatcher review includes the current briefing, crew status, airport conditions and an alternate. Fuel, diversion decisions and clearances are not calculated by this preview.", "#FFDC29", "41m ago"),
        new("service", "N358DN – MAINTENANCE", "A-check scheduled\nATL 22:30 – 05:15", "Demonstration maintenance entry: a planned overnight service window is shown against N358DN at ATL. The entry is illustrative and does not describe the maintenance status of a real aircraft.", "#42A8FF", "2h ago", "\uE946"),
        new("crew", "CREW DUTY REVIEW", "Potential pairing conflict on DL285\nReserve crew being evaluated", "Demonstration crew entry: a delay may conflict with the example pairing schedule. This preview does not calculate crew legality or assign reserve crew. Use the crew desk to inspect the sample pairing.", "#FFDC29", "3h ago"),
        new("connections", "ATL CONNECTION BANK", "18 example connections under review", "Demonstration passenger entry: the inbound bank contains a group with a short onward connection. Connection protection and rebooking require a future passenger operations service.", "#FFDC29", "3h ago"),
        new("gate", "MKE GATE COORDINATION", "Example gate change awaiting review", "Demonstration airport entry: a gate-change scenario has been added for review. No live gate allocation is connected.", "#42A8FF", "4h ago", "\uE946"),
        new("briefing", "PREFLIGHT BRIEFING", "Review your active assignment", "Use Flight tools to enter a flight or import your latest SimBrief briefing. The preflight checklist is a personal preparation aid, saved locally for each flight.", "#42A8FF", "TODAY", "\uE946")
    ];
    internal static readonly DashboardMessage[] Messages =
    [
        new("welcome", "Welcome to Alpha 6 OPS", "Your airline day starts here", "The dashboard brings your next flight, aircraft rotation, alerts and reference fleet together. Flight tools contains SimBrief import, simulator connection, recorded replay, timeline and debrief. The operations, crew, maintenance and weather desks currently contain clearly labeled demonstration scenarios.", "\uE8F2", "#F2F5F8"),
        new("weather-note", "Weather desk preview", "Explore the Atlanta scenario", "Open Weather to compare illustrative airport observations, visibility, wind and arrival-bank impacts. All weather entries are example data, not current observations or flight-planning information.", "\uE946", "#42A8FF"),
        new("brief", "Operations briefing", "How delay moves through a rotation", "Run the delayed fixture from Flight tools. A601 arrives 30 minutes late, A602 inherits 30 minutes, and A603 recovers to a five-minute delay. Open the debrief to inspect the actual milestones behind those projections.", "\uE8A5", "#EAF0F4"),
        new("workflow", "Make it your flight deck", "Watchlists & preflight preparation", "Select a flight and choose Watch selected. Your watchlist, read messages, acknowledged demo alerts and preflight checks are saved on this computer. They do not alter a flight assignment or authorize a departure.", "\uE734", "#FFDC29")
    ];
    internal static OpsModule Module(string name) => name switch
    {
        "Operations" => new("OPERATIONS", "Disruptions, station coordination and recovery review", "DEMONSTRATION SCENARIO • NO LIVE OPERATIONS FEED",
            [new("ACTIVE REVIEWS","4","Across the example network"),new("ATL ARRIVAL RATE","48 / HR","Illustrative weather restriction"),new("RECOVERY WINDOW","15–30 MIN","Scenario estimate")],
            [
                new("IRR-001","ATL arrival-bank restriction","14:00–16:00Z","Monitoring","Thunderstorm scenario. Review inbound aircraft, downstream rotations and connecting passengers together. The example recovery estimate is not a live operational forecast."),
                new("DL2147","MSP → ATL / arrival holding","19 min","Review","Illustrative holding event. Coordinate a review with the weather and dispatch desks. No clearance or diversion command is issued."),
                new("DL285","SEA → ATL / pairing impact","17:30Z","Crew review","Example flight with a potential pairing conflict. Reserve availability is shown on the crew desk as sample data."),
                new("STN-MKE","Milwaukee station / gate coordination","Before departure","Pending","Example gate coordination task. Confirm actual gate information with the relevant flight briefing."),
                new("BANK-ATL","Atlanta / onward connection bank","18 connections","Monitoring","Illustrative group of onward connections. Compare incoming delay and scheduled connections before a recovery decision.")
            ],"operations"),
        "Maintenance" => new("MAINTENANCE CONTROL", "Example work orders, service windows and discrepancy review", "DEMONSTRATION DATA • NOT REAL AIRCRAFT MAINTENANCE STATUS",
            [new("PLANNED WORK","4","Illustrative work orders"),new("OVERNIGHT WINDOW","22:30–05:15","ATL scenario"),new("RELEASE AUTHORITY","NOT CONNECTED","No dispatch release is issued")],
            [
                new("WO-1042","N358DN / A321 / overnight A-check","ATL 22:30–05:15","Scheduled","Sample planned service window. Work scope: routine inspection, servicing and documentation review. Completion and return-to-service approval require a maintenance system."),
                new("WO-1043","N372DA / A321 / cabin discrepancy","DTW 18:00Z","Review","Illustrative cabin discrepancy. Review the defect report and assigned work package. No MEL classification or dispatch permission is inferred."),
                new("WO-1044","N414DZ / A330 / inspection planning","JFK overnight","Planned","Illustrative planning entry only. Reference registration details are available in the aircraft catalog; they do not establish maintenance condition."),
                new("WO-1045","N319US / A320 / parts coordination","MSP 19:30Z","Awaiting review","Sample parts coordination request. No procurement or external communication takes place.")
            ],"maintenance"),
        "Crews" => new("CREW OPERATIONS", "Pairings, reporting times and reserve coverage", "DEMONSTRATION DATA • NO CREW LEGALITY ENGINE",
            [new("PAIRINGS","4","Example duty sequences"),new("RESERVE GROUPS","2","Illustrative coverage"),new("REQUIRES REVIEW","1","DL285 pairing scenario")],
            [
                new("PAIR-101","MKE → ATL → MKE","Report 06:50Z","Assigned","Example pairing: two pilots and four cabin crew. Review the flight sequence and local reporting procedures. These entries do not identify real crew members."),
                new("PAIR-204","SEA → ATL","Report 15:45Z","Review","Example delay conflict on DL285. No duty limit or legality conclusion is calculated. Reserve substitution requires a future crew service."),
                new("RSV-ATL","Atlanta / flight deck reserve","14:00–22:00Z","Reserve","Illustrative reserve group for exploring recovery workflow. Not a confirmed staffing commitment."),
                new("RSV-DTW","Detroit / cabin reserve","12:00–20:00Z","Reserve","Illustrative cabin reserve group. Assignments and notifications are not transmitted.")
            ],"crews"),
        "Passengers" => new("PASSENGER CONNECTIONS", "Example loads, onward connections and recovery cases", "DEMONSTRATION DATA • NO PASSENGER RECORDS OR LIVE BOOKINGS",
            [new("FLIGHT LOAD","178 / 191","Example A321 cabin"),new("CONNECTION REVIEW","18","Illustrative ATL bank"),new("RECOVERY CASES","3","For workflow exploration")],
            [
                new("DL1482","MKE → ATL / example load","178 / 191 seats","Boarding plan","Illustrative cabin load: 178 travelers and 13 open seats. No real passenger manifest, personal data or booking feed is present."),
                new("CX-001","ATL → MIA / onward group","8 connections","Review","Example short connection group. Consider terminal transfer time and actual onward boarding status in a future connection engine."),
                new("CX-002","ATL → JFK / onward group","6 connections","Review","Example onward group affected by the arrival-bank scenario. No ticket or itinerary is changed."),
                new("CX-003","ATL → BOS / onward group","4 connections","Review","Example rebooking review. Search and select cases to inspect how a future recovery desk will present context."),
                new("BAG-ATL","ATL transfer-bag coordination","Inbound bank","Monitoring","Illustrative baggage coordination task. No baggage tracking system is connected.")
            ],"passengers"),
        "Weather" => new("WEATHER DESK", "Airport conditions and arrival-bank outlook", "DEMONSTRATION WEATHER • NOT CURRENT OBSERVATIONS",
            [new("ATL","TSRA / 25°C","Illustrative convective weather"),new("MKE","VFR / 21°C","Illustrative departure conditions"),new("NETWORK STATIONS","8","Static scenario observations")],
            [
                new("KATL","Atlanta / thunderstorms and rain","Wind 240° / 12 kt","TSRA","Sample: 25°C, visibility 4 statute miles, broken cloud at 2,500 ft. Scenario arrival rate: 48 aircraft/hour. Not a live METAR or forecast."),
                new("KMKE","Milwaukee / scattered cloud","Wind 190° / 8 kt","VFR","Sample: 21°C, visibility 10 statute miles, scattered cloud at 4,500 ft. Verify real weather in your briefing."),
                new("KORD","Chicago / broken cloud","Wind 220° / 10 kt","VFR","Sample: 23°C and visibility 10 statute miles. No runway assignment or dispatch interpretation is provided."),
                new("KDTW","Detroit / few clouds","Wind 200° / 7 kt","VFR","Sample: 24°C and visibility 10 statute miles. This entry is independent of simulator weather."),
                new("KJFK","New York / coastal showers","Wind 170° / 14 kt","Showers","Sample: 22°C and visibility 6 statute miles. The dashboard does not ingest live airport reports."),
                new("KSEA","Seattle / overcast","Wind 210° / 6 kt","Overcast","Sample: 17°C, visibility 8 statute miles and overcast cloud at 3,000 ft."),
                new("KDEN","Denver / clear","Wind 140° / 9 kt","VFR","Sample: 27°C with clear skies and visibility above 10 statute miles."),
                new("KMIA","Miami / scattered cloud","Wind 090° / 11 kt","VFR","Sample: 30°C, visibility 10 statute miles and scattered cloud at 3,000 ft.")
            ],"weather"),
        "Dispatch" => new("DISPATCH DESK", "Flight preparation and company coordination", "LOCAL DESK • NO DISPATCH CHAT OR VOICE SERVICE CONNECTED",
            [new("ACTIVE CHANNEL","LOCAL","No messages are transmitted"),new("BRIEFING SOURCE","SIMBRIEF","Available through Flight tools"),new("REVIEW QUEUE","4","Illustrative coordination tasks")],
            [
                new("BRIEFING","Import and review the active flight","Before connecting","Available","Use Flight tools → Set active flight / Import SimBrief. Review route, registration and planned UTC times. The latest successful SimBrief response is cached for offline reuse."),
                new("PREFLIGHT","Complete personal preparation checks","Before block-out","Available","Use Start preflight on the dashboard. Checks are saved for that flight and are a personal reminder; they do not release or dispatch an aircraft."),
                new("SCN-ATL","Atlanta arrival-bank coordination","Example scenario","Review","Sample coordination task associated with thunderstorms in the demonstration network. No external dispatch message is sent."),
                new("DEBRIEF","Review actual milestones and delays","After flight","Available","The live and replay timelines use the same phase detector. Use the appropriate timeline or debrief action in Flight tools.")
            ],"occ"),
        "OCC" => new("OPERATIONS CONTROL CENTER", "A cross-desk view of the demonstration network", "DEMONSTRATION SCENARIO • LOCAL PREVIEW",
            [new("DESKS","6","Operations, weather, crew & more"),new("OPEN ALERTS","7","Sample review queue"),new("PRIMARY HUB","ATL","Illustrative network center")],
            Alerts.Select(a=>new OpsRow(a.Id.ToUpperInvariant(),a.Title,a.Age,"Review",a.Detail)).ToArray(),"occ"),
        _ => new("NETWORK", "Illustrative station network and route connections", "SCHEMATIC ROUTES • NOT LIVE AIRCRAFT POSITIONS",
            [new("STATIONS","10","Illustrative US network"),new("PRIMARY HUB","ATL","Scenario hub"),new("TRACKING","NOT POSITIONAL","SimConnect position is not supplied")],
            new[]{"SEA","LAX","DEN","MSP","ORD","MKE","DTW","ATL","JFK","MIA"}.Select(s=>new OpsRow(s,City(s),"Scenario station","Reference","Illustrative station location. Route curves and aircraft symbols are schematic; they do not show actual traffic or navigational guidance.")).ToArray(),"operations")
    };
}

