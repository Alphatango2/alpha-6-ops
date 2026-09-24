CHECKPOINT 7.21 — SIERRA 4 MAP V2 ARCHITECTURE, RENDERER SELECTION & PROTOTYPE

Checkpoint 7.20 is complete and committed as 3b51b5c.

The Sierra 4 Operations workspace is now migrated and provides a stable replaceable map boundary through OperationsMapHost.

Verified 7.20 baseline:

- Production build/package: PASS
- 1,871 / 1,871 executed checks passed
- 0 failed
- 2 native simulator checks unavailable
- 9 / 9 recorded-flight replays passed
- 91,366 / 91,366 telemetry samples processed
- FlightSessionCoordinator remains authoritative
- OperationsOverviewQuery remains read-only
- OperationsMapHost provides a replaceable map presentation boundary
- Existing FlightRouteMap remains the current renderer

OBJECTIVE

Design, evaluate and prototype the architecture for Sierra 4 Map V2.

This checkpoint is primarily an ARCHITECTURE / RENDERER-SELECTION checkpoint.

Do not replace the production map yet unless a minimal isolated prototype requires temporary diagnostic integration.

The goal is to determine exactly how Sierra 4 should implement its long-term geographic operations renderer before committing the production application to a mapping technology.

PRIMARY VISUAL TARGET

Map V2 should eventually become the visual centerpiece of Sierra 4 Operations.

It should resemble a premium global airline Operations Control Center rather than a consumer road map.

The supplied Sierra 4 concept image remains the visual direction.

Desired appearance:

- near-black / very dark ocean
- charcoal land
- subtle geographic borders
- restrained labels
- minimal road detail at global/airline scales
- subdued city lights at night where technically appropriate
- accurate airport geography
- Sierra-yellow active/selected operational emphasis
- subdued inactive routes
- clear observed vs inferred vs simulated semantics
- elegant aircraft symbols
- great-circle route geometry
- smooth pan and zoom
- professional OCC appearance
- no bright consumer-map styling

The map must visually integrate with the existing Sierra 4 design tokens.

RESEARCH REQUIREMENT

Before choosing an implementation, investigate CURRENT viable options for a WPF/.NET 10 Windows desktop application.

At minimum evaluate:

1. MapLibre GL JS hosted through WebView2
2. MapLibre Native / native Windows interop feasibility
3. Mapbox or another commercially supported vector-map renderer if technically relevant
4. A WPF-native/custom GPU renderer if justified
5. Any other serious option discovered during investigation

Do not assume a renderer merely because it was previously suggested.

Use current official documentation and current package/project status where possible.

For each serious candidate evaluate:

- WPF/.NET 10 compatibility
- Windows desktop support
- GPU acceleration
- vector-tile support
- custom style support
- custom operational layers
- dynamic aircraft updates
- large numbers of aircraft
- route rendering
- great-circle geometry
- symbol rotation
- smooth interpolation
- hit testing / selection
- labels
- airport layers
- weather/raster overlays
- day/night visualization
- offline/cache possibilities
- licensing
- attribution requirements
- API/token requirements
- tile-provider requirements
- distribution implications
- memory usage
- startup cost
- rendering performance
- thread/process isolation
- accessibility implications
- packaging complexity
- long-term maintainability

Do not choose based solely on easiest prototype.


MAP DATA AND RENDERER MUST BE SEPARATE

Design Map V2 around a provider-neutral Sierra 4 map contract.

The rest of Sierra 4 must NOT know whether the renderer is MapLibre, Mapbox, WebView2, native C++, or something else.

Conceptually:

FlightSessionCoordinator / future World State
                    ↓
             Map Presentation Model
                    ↓
              IMapRenderer
                    ↓
        Selected rendering technology

The renderer must consume map state.

It must not own airline operational truth.


DEFINE MAP PRESENTATION CONTRACTS

Design typed immutable contracts appropriate for future Map V2.

Consider records such as:

MapSnapshot
MapAircraft
MapAirport
MapRoute
MapWaypoint
MapWeatherLayer
MapOperationalRegion
MapSelection
MapCameraState

Use names appropriate to the repository.

The contracts should support future states such as:

AIRCRAFT

- stable ID
- callsign/flight
- registration
- latitude
- longitude
- altitude
- heading
- groundspeed
- source
- observation timestamp
- reliability
- operational status
- selected/highlighted state

AIRCRAFT SOURCE MUST BE TYPED

Examples:

Observed
Inferred
Simulated
Replay
Unknown

Never infer source from color or display wording.


ROUTES

Support:

- origin/destination
- route geometry
- great-circle segments
- planned route
- completed portion
- flown/observed track
- inferred segment
- diversion route
- selected route
- future multi-leg rotations

Observed track and inferred track must remain semantically distinct.


AIRPORTS

Support:

- ICAO/IATA where known
- geographic coordinates
- operational status
- selected state
- future delay/capacity/weather indicators

Do not invent operational airport status when no source exists.


MAP LAYER MODEL

Define a clear conceptual layer stack.

For example:

1. Sierra basemap
2. day/night or atmospheric layer
3. weather layer
4. airport layer
5. route/network layer
6. aircraft layer
7. operational disruption layer
8. selection/highlight layer
9. labels / interaction

The exact implementation may vary by renderer.

Operational layers must be independently enabled/disabled where practical.


SIERRA 4 MAP THEME

Determine how the selected renderer can be styled to Sierra 4.

Prototype a Sierra 4 map style if the candidate technology supports it.

Target palette should derive from existing SierraDesignTokens where practical.

Do not hard-code a separate unrelated visual identity.

Explore:

- dark ocean
- charcoal land
- muted political borders
- low-emphasis roads
- airport emphasis
- muted geographic labels
- Sierra-yellow selected route
- distinct but restrained observed-flight treatment
- subdued simulated traffic
- critical/warning overlays consistent with Sierra status colors

The final production map should not visually look like an embedded third-party website.


DAY / NIGHT

Investigate a physically meaningful day/night terminator based on UTC and solar position.

Do not implement a decorative fixed gradient and call it day/night.

Determine how the selected renderer could support:

- solar terminator
- nighttime dimming
- optional city-light appearance

Separate physically meaningful state from visual enhancement.


PERFORMANCE / THREADING ARCHITECTURE

This is critical.

Current flight processing remains serialized through the WPF application dispatcher.

Map V2 must not make flight telemetry processing dependent on expensive map rendering.

Design how map updates cross from application state to renderer state.

Consider:

- immutable/coalesced map snapshots
- update throttling
- interpolation performed by renderer
- independent render cadence
- background preparation
- process/thread isolation where appropriate
- avoiding WPF creation of one UI control per aircraft
- batching geographic layers
- GPU-rendered aircraft/routes
- capped/simplified trails
- route geometry caching

The operational pipeline must preserve every required operational event.

The map presentation is allowed to coalesce visual position updates.

Clearly document that distinction.


AIRCRAFT INTERPOLATION

Design smooth visual aircraft motion without altering authoritative telemetry.

For example:

authoritative samples
        ↓
MapSnapshot
        ↓
renderer interpolation
        ↓
smooth visual movement

Interpolation is presentation only.

It must never feed interpolated positions back into FlightSessionCoordinator or flight history.


SCALE TARGETS

Design for progressive scale.

Evaluate expected behavior around:

- 1 aircraft
- 25 aircraft
- 100 aircraft
- 500 aircraft
- 1,000 aircraft
- several thousand aircraft if technically practical

Do not claim performance that has not been measured.

If useful, create an isolated synthetic benchmark/prototype using generated NON-PRODUCTION map entities.

Synthetic entities must never appear as production airline state.

Report measured results where possible.


ZOOM-DEPENDENT DETAIL

Design map detail based on zoom.

Example intent:

GLOBAL
- major airports
- selected routes
- summarized traffic
- major disruptions

REGIONAL
- more airports
- aircraft
- route structure
- weather

LOCAL
- detailed airport/aircraft information
- labels
- route fixes where appropriate

Avoid rendering every label/entity at every zoom.


INTERACTION MODEL

Define interaction for:

- pan
- zoom
- reset/home
- follow selected aircraft
- select aircraft
- select airport
- select route
- hover/tooltip if appropriate
- map filters/layers
- return to global network
- current-flight focus

Interaction state must remain presentation state.

Selecting an aircraft must not alter operational truth.


OFFLINE / FAILURE STATES

Define graceful behavior for:

- no internet
- tile provider unavailable
- API token unavailable
- renderer initialization failure
- map style unavailable
- partial tile failure

The rest of Sierra 4 Operations must remain usable if the map cannot load.

Do not make application startup dependent on successful remote map initialization.


SECURITY / DISTRIBUTION

If a renderer/provider requires API keys:

- do not commit secrets
- define configuration strategy
- evaluate whether client-visible keys are expected by that provider
- document restrictions and domain/application controls where available

Review licensing and attribution requirements.

Do not introduce a dependency with incompatible redistribution terms.


PROTOTYPE

Build the smallest useful isolated prototype needed to validate the recommended renderer.

The prototype should demonstrate, where technically feasible:

- Sierra 4 dark styling
- geographic map
- pan/zoom
- at least two airport markers
- one great-circle or representative route
- one aircraft symbol with heading
- dynamic position update
- selection/highlight
- integration feasibility with WPF
- clean disposal/recreation

Use explicitly synthetic prototype data.

Do not connect the prototype to production operational state unless necessary to validate the existing map boundary.

Do not replace OperationsMapHost production behavior during this checkpoint.


BENCHMARK

If the prototype supports it, perform a bounded synthetic rendering test.

Test increasing aircraft counts where practical.

Measure rather than guess.

Record:

- renderer
- aircraft count
- update frequency
- approximate CPU behavior
- approximate memory behavior
- visual responsiveness
- notable bottlenecks

This is architectural evidence, not a formal production performance guarantee.


MAP V2 MIGRATION PLAN

Produce the recommended implementation sequence for the next checkpoint.

The migration should ideally allow:

OperationsMapHost
      ↓
OLD FlightRouteMap

to become:

OperationsMapHost
      ↓
Map V2 Renderer

without changing OperationsWorkspace or operational flight truth.


DO NOT DO IN 7.21

Do NOT:

- remove the production FlightRouteMap
- permanently switch Operations to an unvalidated prototype
- build the persistent airline-world engine
- fabricate airline traffic
- build passenger/crew/maintenance engines
- migrate other workspaces
- change flight algorithms
- change telemetry semantics
- change database schemas
- change profile/storage paths
- implement final Map V2 production functionality
- perform final Sierra 4 distribution rebranding


REGRESSION REQUIREMENTS

Any production code changes made for contracts/boundaries must preserve the full Sierra 4 regression baseline.

Required:

- production build/package PASS
- all previously passing checks remain passing
- 9/9 recorded-flight replays pass
- 91,366/91,366 telemetry samples remain valid
- existing Operations map remains functional
- map presentation cannot mutate operational state
- existing persisted data remains readable

The two native simulator checks may remain unavailable if MSFS is unavailable.

Do not count unavailable checks as passed.


COMPLETION REPORT

At completion provide:

1. Renderer candidates investigated
2. Current-source evidence used
3. Comparison matrix
4. Recommended renderer
5. Why it was selected
6. Why alternatives were rejected
7. WPF integration architecture
8. Map presentation contracts
9. Layer architecture
10. Sierra 4 styling strategy
11. Tile/data provider strategy
12. Licensing/attribution requirements
13. API/token requirements
14. Day/night architecture
15. Aircraft interpolation design
16. Threading/performance architecture
17. Scale strategy
18. Offline/failure behavior
19. Security/distribution considerations
20. Prototype description
21. Prototype screenshots
22. Synthetic benchmark results
23. Files created/modified
24. Build/regression results
25. Known risks
26. Exact recommended scope for Checkpoint 7.22 — Map V2 Implementation

STOP AFTER CHECKPOINT 7.21.

Do not begin production Map V2 implementation.