# Live flight identification and MSFS launch (0.12.2)

The connection card offers **Launch & Connect** when MSFS 2024 is closed. Windows activates the installed Microsoft Store package (`Microsoft.Limitless_8wekyb3d8bbwe!App`) through `IApplicationActivationManager`, keeping the Store/Xbox launch path intact. If `FlightSimulator2024` is already running, OPS skips launch. One connection session owns startup and capped reconnect retries; another click opens controls. Disconnect cancels connection/retry work without closing MSFS. Sign-in, updates and flight selection remain in the simulator. Launch failure explains how to open MSFS manually; it does not report a successful connection.

## Evidence and conflict handling

- MSFS via SimConnect supplies aircraft title, ATC registration, latitude/longitude, simulation state and existing flight telemetry. The optional context definition cannot take down core telemetry when unsupported.
- The active MSFS flight-plan path is requested every five seconds. Only that reported local PLN/XML file is read, with a 2 MB XML character limit, no DTDs/external entities, and no UNC network paths. Empty, inaccessible, malformed or incomplete plans leave the route unknown. Custom avionics/EFBs that do not publish a readable plan are not inferred from old files or their next waypoint.
- Nearby MSFS scenery airports are refreshed every 30 seconds. An embedded OurAirports public-domain reference provides offline coverage when facilities are unavailable, and names for recognized simulator airports. Proximity is labeled **Near**; it never proves the actual departure or intended destination. Reference provenance is in `assets/airports/provenance.json`.
- A SimBrief/pilot assignment is attached only with corroborating live evidence: a session start within 18 hours of scheduled departure, no known registration or simulator-route conflict, and either a matching simulator route or a ground start within 5 nm of the assigned airport. Missing requested registration or initial position leaves a new assignment unverified. These are conservative matching heuristics, not universal aircraft/airport validation.
- An unmatched session shows **Free Flight**, or **MSFS Flight** when a simulator route is known, with actual aircraft identification. Saved airline identity, gates, scheduled delays and invented progress are excluded. Flight Details shows each source and conflict; pilots can correct the assignment during observation.
- Joining airborne/taxiing starts observation at that phase. Departure/takeoff timestamps are not fabricated. Landing can still be recorded; assignment block-in requires an observed block-out and a destination proximity check. A different/unknown arrival airport is marked for review and does not complete the scheduled route.
- Aircraft/registration swaps, simulator clock reversals/jumps, implausible position jumps and returning from menus reset the observed flight segment and its actuals. Earlier samples remain in the diagnostic journal. Pause and slew suppress phase transitions. Data older than five seconds is marked last observed; missing core telemetry still invokes the existing 30-second timeout/reconnect path.

## Scope and verification

Free-flight timeline and telemetry are available through Flight Tools and local diagnostic journals. The separate flight-history database still primarily contains replay runs. The network map, dispatch alerts and hero photograph remain illustrative dashboard content; the weather header still describes the computer's approximate location, not aircraft weather.

Tests cover Sydney versus a stale LAX–JFK report, wrong registration/route/date, unavailable context, cross-date-line distances, round trips, airborne joins, teleports, aircraft swaps, destination mismatch, broken/missing PLN files, XML entity rejection, source fallback and launch button states. Native live context and Store activation are additionally checked using `--simconnect-probe` and `--simulator-launch-probe`; a probe result is required before claiming a live runtime path passed. No finite test set guarantees every MSFS add-on, mission or failure condition.

References:
- [MSFS system state / active flight-plan path](https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/api-reference/general/simconnect_requestsystemstate/)
- [Microsoft Store application activation](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-iapplicationactivationmanager-activateapplication)
- [OurAirports public-domain data](https://ourairports.com/data/)
