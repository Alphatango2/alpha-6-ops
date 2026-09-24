CHECKPOINT 7.22 — SIERRA 4 MAP V2 PRODUCTION INTEGRATION

Checkpoint 7.21 is complete and committed as 259dc17.

7.21 selected MapLibre GL JS hosted in WebView2 as the recommended Sierra 4 map renderer, behind a renderer-neutral interface.

Checkpoint 7.21 successfully demonstrated in an isolated prototype:

- Sierra 4 dark geographic styling
- MapLibre GL JS 6.11.2
- WebView2 integration with WPF
- geographic route rendering
- aircraft symbols and heading
- dynamic aircraft movement
- selection
- camera controls
- day/night shading
- view disposal/recreation
- synthetic rendering through 5,000 aircraft

The prototype did NOT replace the production Operations map.

Verified existing production baseline remains:

- 1,871 / 1,871 existing production checks passed
- 0 failed
- 2 native simulator checks unavailable
- 9 / 9 recorded-flight replays passed
- 91,366 / 91,366 telemetry samples processed

OBJECTIVE

Implement Sierra 4 Map V2 in the production application behind OperationsMapHost.

This checkpoint must convert the validated 7.21 architecture into a production-quality map implementation using REAL CURRENT SIERRA 4 MAP DATA.

The map remains PRESENTATION ONLY.

FlightSessionCoordinator and existing operational systems remain authoritative.

Map V2 must never calculate or modify:

- flight phase
- flight progress
- ETA
- flight completion
- operational events
- history
- PIREP state
- simulator state

The new map visualizes authoritative state. It does not create operational truth.


CRITICAL ROLLOUT RULE

DO NOT immediately delete the existing FlightRouteMap.

Implement Map V2 behind the renderer boundary with a safe fallback/rollback path.

The existing renderer must remain available until all 7.22 acceptance gates pass.

If Map V2 initialization or runtime fails, Sierra 4 Operations must remain usable and must not affect the flight session.


PART 1 — PROMOTE MAP CONTRACTS INTO PRODUCTION

Review the renderer-neutral contracts created in the isolated 7.21 prototype.

Promote/refine the appropriate contracts into production presentation code.

Examples include:

- MapSnapshot
- MapAircraft
- MapAirport
- MapRoute
- MapWaypoint
- MapSelection
- MapCameraState
- MapPresentationState
- IMapRenderer

Use names appropriate to the existing production architecture.

Contracts must remain renderer-neutral.

No production Operations/ViewModel code should require knowledge of:

- JavaScript
- WebView2 messages
- MapLibre source/layer names
- browser objects
- tile providers

Validate:

- stable IDs
- coordinates
- finite numeric values
- source generation
- timestamps
- payload size/budget
- duplicate IDs
- invalid geographic state

Aircraft/map source must remain explicitly typed:

- Observed
- Inferred
- Simulated
- Replay
- Unknown

Never infer source from display color or wording.


PART 2 — PRODUCTION MAP PROJECTION SERVICE

Implement a read-only projection layer that converts current Sierra 4 authoritative state into MapSnapshot presentation state.

Current production scope should include only data that exists today:

- current observed aircraft
- current aircraft heading
- current route
- origin airport
- destination airport
- route waypoints where authoritative
- observed/flown track
- completed/planned route distinction
- inferred position/segments only where already authoritative
- current selection/focus
- relevant timestamps/reliability

Do not fabricate:

- airline-world traffic
- simulated network aircraft
- airport congestion
- passenger connections
- crew state
- maintenance state
- network delays
- OCC decisions

The projection architecture should be ready to accept future world-state aircraft without requiring the renderer to be redesigned.


PART 3 — OBSERVED VS INFERRED TRUTH

Preserve the existing semantic distinction between observed and inferred geography.

Observed track must remain observed.

Inferred position/track must remain typed as inferred.

Never visually join gaps in a way that implies continuous observation when Sierra 4 did not observe the aircraft.

Handle:

- stale telemetry
- paused simulator
- slew
- simulator menu
- reconnect
- source generation change
- telemetry time jump
- aircraft identity change
- hidden map interval
- route unavailable

A new/reconnected session must not inherit an old visual trail.


PART 4 — SNAPSHOT MAILBOX / MAP BRIDGE

Implement the bounded presentation pipeline designed in 7.21:

Authoritative state
        ↓
Map projection
        ↓
immutable visual snapshot
        ↓
capacity-one latest-snapshot mailbox
        ↓
serialization / bridge
        ↓
MapLibre renderer

The flight producer must never wait for map rendering.

Operational telemetry/events must continue to process in authoritative order.

Visual map updates MAY be coalesced.

Operational events/history MUST NOT be coalesced or dropped.

Use generation and sequence identifiers so stale browser messages or obsolete renderer instances cannot affect the current map.

Keep WebView Dispatcher work short.

Instrument:

- snapshot production
- serialization time
- bridge/post latency
- acknowledgement latency
- dropped/coalesced visual snapshots
- renderer-ready time
- restart/recreation count

Do not log sensitive configuration or excessive telemetry payloads.


PART 5 — WEBVIEW2 PRODUCTION HOST

Implement the production WebView2 MapLibre adapter behind IMapRenderer.

Use local pinned MapLibre assets prepared from the reviewed 7.21 prototype.

Do not depend on a CDN for renderer JavaScript.

Apply the security decisions from 7.21:

- trusted local/virtual origin
- restrictive CSP
- no arbitrary host objects
- block unexpected navigation
- block popups
- block downloads
- block unnecessary permissions
- validate message origin
- validate message type/schema
- validate payload size
- reject stale generation/sequence messages
- resource allowlist

Do not commit API secrets.

Map V2 should require no provider key for the initial production implementation.


PART 6 — WPF HOSTING / AIRSPACE

Explicitly evaluate the production shell with:

A. standard WPF WebView2 hosting

and, if necessary,

B. WebView2 CompositionControl or another documented supported host option.

Test real Sierra 4 shell behavior including:

- header
- navigation
- scrolling
- overlays
- tooltips
- dialogs
- dropdowns
- map controls
- keyboard focus
- mouse input
- DPI/scaling
- window resize
- minimize/restore

Choose the host based on measured behavior, not theoretical preference.

Document the choice and remaining compromises.


PART 7 — SIERRA 4 BASEMAP

Implement the Sierra 4 production basemap style.

The map should visually belong to Sierra 4.

Target:

- near-black ocean
- charcoal land
- subtle coastline
- muted political/geographic boundaries where data supports them
- subdued labels
- minimal road/building clutter
- strong route readability
- restrained yellow selection
- neutral normal traffic
- existing Sierra warning/critical semantics

Derive visual values from Sierra 4 design tokens where practical.

Do not visually reproduce a generic consumer road map.

For 7.22, use the approved bundled/offline geography strategy from 7.21.

Do not introduce an unapproved commercial tile dependency merely to make the map prettier.

If detailed hosted vector geography is deferred, state that clearly.


PART 8 — CURRENT PRODUCTION AIRPORTS

Render authoritative current origin/destination airports.

Support:

- airport marker
- ICAO/IATA label where available
- selected/highlight state
- origin/destination role

Do not claim live airport operational status unless Sierra 4 has an authoritative source.

Design airport rendering so additional airports can be introduced later from the persistent airline-world model.


PART 9 — ROUTE RENDERING

Render the current authoritative flight route geographically.

Support:

- planned route
- completed planned portion
- observed track
- inferred track where applicable
- route waypoints where useful
- origin
- destination
- dateline crossing
- high latitude
- great-circle geometry

Use visual semantics consistent with Sierra 4.

Example intent:

Selected/current planned route:
Sierra yellow

Completed/planned:
muted neutral

Observed track:
distinct but restrained

Inferred:
visually distinct from observed and identifiable without relying solely on color

Do not imply accuracy that the underlying source does not provide.


PART 10 — AIRCRAFT RENDERING

Render the current aircraft as a GPU/map feature rather than a WPF control.

Support:

- aircraft symbol
- geographic position
- heading
- selection
- follow mode
- source semantics
- stale/unavailable handling

Do not create one WPF visual element per aircraft.

Architecture must support future batched aircraft collections.


PART 11 — SMOOTH AIRCRAFT INTERPOLATION

Implement presentation-only interpolation based on the 7.21 design.

Authoritative samples remain untouched.

Renderer interpolation should provide smooth visual movement between accepted observations.

Reset/freeze interpolation on:

- stale gap
- reconnect
- generation change
- teleport
- time jump
- pause
- slew
- simulator menu
- aircraft identity/source change

Use shortest heading interpolation.

Use geographic/spherical interpolation appropriate to the implementation.

Interpolated positions must NEVER feed back into:

- FlightSessionCoordinator
- telemetry history
- route progress
- ETA
- phase
- operational events

Support reduced-motion behavior.


PART 12 — DAY / NIGHT

Promote the reviewed solar architecture into production presentation code.

Day/night must be based on UTC/geographic solar state.

Do not implement a fixed decorative gradient.

Support:

- physical terminator
- restrained twilight transition
- correct movement with UTC

Validate against independent reference cases for:

- equinox
- solstice
- leap year
- dateline
- northern/southern high latitude
- near-polar conditions

City lights are NOT required in 7.22.

Do not add unlicensed imagery.


PART 13 — MAP CONTROLS

Provide Sierra 4 controls for:

- zoom in
- zoom out
- world/home view
- follow current aircraft
- selected flight focus

Controls should use Sierra 4 styling and integrate with the existing Operations page.

Avoid copying generic MapLibre controls if they visually conflict with Sierra 4.


PART 14 — SELECTION / INTERACTION

Support:

- aircraft selection
- airport selection
- route selection where appropriate
- selected entity highlighting
- follow aircraft
- return to world/network view

Interaction state is presentation state.

Selecting something on the map must not change flight operational truth.

If tooltips/popovers are used, ensure they cooperate with the WPF/WebView hosting model.


PART 15 — MAP FAILURE / FALLBACK

Production Map V2 must fail safely.

Handle:

- WebView2 runtime unavailable
- initialization timeout
- local asset failure
- JavaScript initialization failure
- renderer crash
- WebView process failure
- WebGL/context loss
- invalid map payload
- map disposal/recreation

On failure:

1. Operations remains functional.
2. FlightSessionCoordinator continues uninterrupted.
3. SimConnect must NOT reconnect merely because the map failed.
4. Attempt a bounded renderer restart where appropriate.
5. Fall back to the existing FlightRouteMap or truthful map-unavailable state according to the architecture.
6. Record diagnostics without flooding logs.

Test failure paths where practical.


PART 16 — HIDDEN / DISPOSED MAP BEHAVIOR

When Operations is not visible:

- stop unnecessary map rendering/animation
- do not accumulate an unbounded visual queue
- do not stop the flight runtime
- do not lose authoritative telemetry/history
- do not visually connect an unobserved interval as though it were continuously rendered

On return:

- consume the newest complete visual snapshot
- preserve truthful observed/inferred gap semantics
- restore appropriate camera/selection state where safe


PART 17 — PERFORMANCE GATES

The 7.21 prototype benchmark was encouraging but not sufficient for production acceptance.

Benchmark the REAL production bridge and Map V2 integration.

Measure at minimum:

- initialization time
- recreated-view initialization
- steady-state memory
- CPU
- bridge serialization time
- WPF Dispatcher post duration
- acknowledgement latency
- map update cadence
- dropped/coalesced visual snapshot count
- resize behavior
- hidden/visible transition
- disposal behavior

Perform synthetic presentation-only load tests at practical stages such as:

- 1 aircraft
- 25 aircraft
- 100 aircraft
- 500 aircraft
- 1,000 aircraft
- 2,000 aircraft
- 5,000 aircraft if the production path remains stable

Synthetic traffic must remain test-only and never appear as production airline state.

Add realistic combinations where practical:

- aircraft
- labels
- routes
- trails

Do not claim capacity based solely on icon-only tests.


PART 18 — DISPATCHER CONTENTION TEST

This is a critical 7.22 gate.

Deliberately slow/stress the map renderer/bridge.

Verify that:

- FlightSessionCoordinator continues processing authoritative telemetry
- operational events remain complete
- history remains complete
- flight phase remains correct
- PIREP state remains correct
- presentation snapshots may coalesce without affecting operational truth

Measure WPF Dispatcher impact.

If map integration materially delays authoritative flight processing, DO NOT enable Map V2 by default.

Report the bottleneck and recommend remediation.


PART 19 — LONGER-RUN RESOURCE TEST

Perform a bounded soak/lifecycle test appropriate to available time.

Include repeated:

- show/hide
- navigate away/back
- resize
- follow/unfollow
- renderer recreation
- map failure/restart if feasible

Look for:

- browser processes left after disposal
- steadily increasing memory
- stale bridge callbacks
- duplicate map instances
- duplicate message handlers
- flight-session interference

Do not claim leak freedom from a short test.
Report exactly what was measured.


PART 20 — ACCESSIBILITY / INPUT

Verify:

- keyboard access to map controls
- visible focus
- reduced-motion behavior
- map can be bypassed to reach surrounding Operations content
- selected aircraft/airport information has a non-canvas textual representation where appropriate
- high-DPI rendering remains usable

Do not claim full screen-reader certification unless actually tested.


PART 21 — WEBVIEW2 DISTRIBUTION

Implement/document runtime handling appropriate to Sierra 4.

Determine:

- Evergreen runtime detection
- behavior when runtime is missing
- whether bootstrap/install is deferred to packaging
- any installer requirements

Do not silently install external runtimes during tests.

Do not unnecessarily bundle a Fixed Version runtime unless explicitly justified.


PART 22 — OPERATIONS INTEGRATION

Once the production gates pass, connect Map V2 through OperationsMapHost.

OperationsWorkspace should not require redesign.

The new map should replace the visual blue globe while retaining the same authoritative current-flight semantics.

Preserve the old renderer as rollback/fallback until the checkpoint is fully verified.

DO NOT replace Flight Tracking's existing map during 7.22.

This checkpoint applies only to the Operations map.


PART 23 — VISUAL QUALITY GATE

Capture actual production Sierra 4 Operations with Map V2 at:

- 1920x1080
- 2560x1440
- 1366x768
- 150% rendering
- 200% rendering

Compare against the supplied Sierra 4 concept.

Perform a dedicated visual polish pass after comparison.

Inspect:

- map prominence
- coastline/geography quality
- route readability
- aircraft visibility
- label hierarchy
- day/night appearance
- map controls
- Sierra yellow usage
- panel/map balance
- whether Map V2 feels native to Sierra 4
- whether the old blue-globe visual language is gone

Do not declare visual completion merely because MapLibre renders.


PART 24 — DEFAULT ENABLEMENT GATE

Map V2 may become the default Operations renderer ONLY if:

- production initialization is reliable
- fallback works
- bridge validation passes
- Dispatcher contention is acceptable
- lifecycle/disposal behavior is acceptable
- no operational regression occurs
- performance is acceptable for the current product scope
- visual review passes
- all required automated regressions pass

If any major gate fails:

KEEP THE OLD MAP AS DEFAULT.

It is acceptable for 7.22 to finish with Map V2 implemented but opt-in/disabled by default if evidence does not support default activation.

Do not force a GO.


PART 25 — REGRESSION REQUIREMENTS

Run the complete Sierra 4 regression suite after implementation.

Required:

- all previously passing production checks remain passing
- all relevant Map V2 tests pass
- 9/9 recorded-flight replays pass
- 91,366/91,366 telemetry samples remain valid
- existing Operations query/data-honesty checks pass
- map rendering cannot mutate operational state
- workspace switching does not reconnect simulator
- hidden runtime continues
- stale simulator callbacks remain rejected
- assignment/PIREP persistence remains animation-independent
- display wording remains non-authoritative
- persisted data remains readable

The two native simulator checks may remain unavailable if MSFS is unavailable.

Do not count unavailable checks as passed.


PART 26 — COMPLETION REPORT

At completion provide:

1. Production Map V2 architecture
2. Contracts promoted/changed from prototype
3. Projection/data provenance
4. WebView2 host selected and why
5. Security model
6. Sierra basemap implementation
7. Airport implementation
8. Route/track semantics
9. Aircraft implementation
10. Interpolation behavior
11. Day/night implementation and validation
12. Controls/interactions
13. Failure/fallback behavior
14. Hidden/disposed behavior
15. WebView2 runtime/distribution handling
16. Production bridge measurements
17. Dispatcher contention results
18. Synthetic production-path benchmark
19. Lifecycle/soak results
20. Accessibility/input review
21. Visual review and screenshots
22. Files created/modified
23. Build/package result
24. Regression counts
25. Replay result
26. Telemetry result
27. Known risks/debt
28. Whether Map V2 is DEFAULT, OPT-IN, or NOT ENABLED
29. Exact reason for that enablement decision
30. Recommendation for the next Sierra 4 checkpoint

STOP AFTER CHECKPOINT 7.22.

Do not migrate Flight Tracking's map.
Do not begin persistent airline-world simulation.
Do not migrate another workspace.
Do not begin final Sierra 4 distribution rebranding.