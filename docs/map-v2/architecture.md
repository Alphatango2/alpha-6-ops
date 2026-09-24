# Sierra 4 Map V2 architecture decision — 7.21

Status: **recommended for staged 7.22 implementation; not integrated into production**. Existing OperationsMapHost and FlightRouteMap remain unchanged. Draft types and all executable experiments live in `prototypes/Sierra4.MapPrototype`, outside the production solution and packaging.

## Decision and alternatives

Recommend **MapLibre GL JS hosted in WebView2**, behind a provider-neutral renderer boundary. This recommendation combines the open-source style/layer model, existing Windows browser runtime, worker/GPU separation, ability to use local data, and the measured isolated WPF prototype. It is not a claim that it outperforms the other candidates; only this candidate was benchmarked.

Current registry versions actually pinned and built: MapLibre GL JS **6.11.2**, WebView2 .NET SDK **1.0.4191.47**. Runtime on this workstation: **153.0.4234.48**. MapLibre 6 uses ESM files and a separate module worker; all necessary files are staged locally. [Official current MapLibre documentation](https://maplibre.org/maplibre-gl-js/docs/), [publisher npm metadata](https://registry.npmjs.org/maplibre-gl/6.11.2), [Microsoft package](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4191.47).

The shortlist included official MapLibre Native FFI, a published community MapLibre WPF wrapper, Mapsui, ArcGIS, Mapbox, custom WPF/Skia and Cesium. See [renderer alternatives and primary-source matrix](renderer-alternatives.md). Native C# is no longer absent: official FFI is experimental/snapshot-only, and the community wrapper exists but introduces per-frame readback. ArcGIS is the strongest supported native WPF alternative, with a Windows 11 support floor and production licensing. Mapsui's standard WPF surface and experimental vector-tile support do not currently match the preferred GPU style pipeline. Commercial Mapbox adds account/content terms without removing the WebView boundary. Custom GPU maps would require owning a map engine; Cesium is better motivated by a stronger 3D requirement.

**Selection is conditional:** production host compositing, keyboard accessibility, GPU failure/fallback and low-end hardware budgets must pass 7.22. If WebView hosting fails those gates, run the same workload on the community native wrapper and/or ArcGIS. Do not choose an alternative solely because it is labeled native.

## Current renderer limitations

FlightRouteMap builds WPF visual geometry and a CPU land raster in its render path, synchronously with presentation on the application Dispatcher. It scales a reference canvas, draws a bounded local trail and lacks a provider/style abstraction. Its existing terminator is decorative screen shading. Those constraints help explain geographic detail, font-size, responsiveness and lighting limitations. They are observations of current code, not a measured attribution of every previous tracking issue to the map. Flight estimation/phase/continuity logic remains independently protected by replay tests.

## Ownership and integration

```text
FlightSessionCoordinator / future World State (authoritative)
   │ publish existing immutable snapshots; no map awaits
   ▼
MapProjectionService (read-only, renderer-neutral)
   │ validated, capped immutable MapSnapshot
   ▼
LatestSnapshotMailbox (capacity one) → background geometry / serialization
   │ bounded payload, generation + sequence
   ▼
IMapRenderer adapter ← MapPresentationState (camera / selection / layers / clock)
   │ bounded UI-thread post; no synchronous round trip
   ▼
WebView2 local page → MapLibre sources/layers/workers/WebGL
```

OperationsMapHost becomes a factory/owner for the renderer view and a status/fallback surface. OperationsWorkspace only hosts that surface; it never imports MapLibre types or browser APIs. Flight Tracking may continue using FlightRouteMap until separately authorized. The old renderer remains selectable during rollout.

The prototype proves WPF initialization, one typed snapshot crossing the message boundary, rendering, interaction and recreation. It **does not implement the production mailbox or the complete IMapRenderer adapter**. Its diagnostic JS generator supplies subsequent synthetic motion. Moving draft contracts into a production presentation assembly, validating the bridge and connecting the real read-only projection are explicit 7.22 tasks.

## Draft immutable contracts

Compiled drafts: `prototypes/Sierra4.MapPrototype/MapContracts.cs`. No Core or production Desktop reference exists in this project.

| Contract | Responsibility |
| --- | --- |
| GeoPoint | Finite WGS84 longitude/latitude, explicit units and bounds; invalid constructor input rejected. |
| MapSnapshot | Schema version, generation, sequence, capture time; immutable aircraft, airports, routes, weather and regions. |
| MapAircraft | Stable ID, callsign/registration, position, altitude feet, heading degrees true, groundspeed knots, typed source, observation timestamp, reliability, typed phase and highlight intent. |
| MapRoute / MapWaypoint | Stable IDs, endpoint IDs, rotation/leg identity, immutable multipart geometry, waypoints, typed planned/completed-plan/observed/inferred/diversion semantics, source and reliability. |
| MapAirport | ICAO/IATA, coordinates/name; operational status Unknown until sourced; nullable delay/capacity/weather references and status time. |
| MapWeatherLayer | Trusted provider resource ID, valid/expiry instants, opacity. It is not a credential or arbitrary URL container. |
| MapOperationalRegion | Geographic boundary, severity, typed source and validity. |
| MapSelection / MapCameraState | Entity selection, center/zoom/bearing/pitch/follow target; presentation state only. |
| MapPresentationState | Camera, selection, enabled layer set, reduced-motion preference and explicit UTC lighting instant. |
| IMapRenderer | Async initialization/disposal, nonblocking Offer(snapshot), Present(view state), typed selection/camera/status notifications. No operational commands. |

Production validation must reject default/uninitialized immutable arrays, duplicate IDs, unsupported schemas, invalid/nonfinite scalar values, over-budget geometry/payloads and older generation/sequence. Normalize UTC with ToUniversalTime and headings modulo 360. An unknown position is absence, never a fabricated 0,0 coordinate. Reliability/source are enums: Observed, Inferred, Simulated, Replay, Unknown cannot be inferred from color or labels. The draft constructors are not a complete production validation layer.

Snapshots are complete visual state, making capacity-one replacement safe. Future deltas need explicit base sequence and full-resync behavior; silently dropping intermediate deltas would be incorrect. Authoritative events, history and flight samples remain in their existing pipeline and are **never** coalesced by this mailbox.

## Layers, theme and data

Draw in this order: Sierra basemap → geographic day/night → weather → airports → routes/network → aircraft → disruptions → selection → labels/interaction. Each operational group has a visibility flag. Layer state changes never alter the underlying operational model.

Generate palette values from `SierraDesignTokens.xaml`: AppBackground ocean, SurfaceElevated land, Divider coastline/borders, TextMuted labels, SierraYellow selected route/aircraft; Information for observed emphasis, muted Simulated entities, Warning/Critical for sourced disruptions. Source remains available as text/shape/legend, not color alone. Keep airport/selected labels readable over both day and night. Roads/buildings are hidden at airline scales. Style/schema and source descriptor must be versioned together; changing tile vendors is not necessarily only changing a URL.

The experiment uses existing **Natural Earth 110m land** for a small reproducible offline workload, accurate airport coordinate examples and local Segoe UI labels. It supplies no political borders, roads, terrain, real weather or city lights. Its close-zoom detail is deliberately limited. A later locally bundled 50m fallback is available in the repository.

Provider strategy, current terms and solar references: [data sources and solar](data-sources-and-solar.md). Start production with a bundled offline geographic fallback; select an authorized hosted vector source or licensed/self-hosted regional PMTiles archives separately. Public OSM tile services are not a bulk/offline entitlement. Remote availability must never gate application startup.

## Physical day/night

Use a single explicit UTC lighting time for each frame, independently of local timezone and camera. Compute approximate solar declination/equation of time, then a geographic sun vector; dot it with each location's unit surface normal. A zero dot product is the geometric terminator. Night shading uses a documented artistic 0° to −6° twilight blend beneath operational overlays.

The prototype implements NOAA's fractional-year approximation, including leap-year day count, and two longitude-hemisphere Mercator image masks. Splitting the mask avoids a degenerate whole-world image quad at ±180°. It is not a fixed camera gradient. Automated checks demonstrate noon/midnight movement; full independent solar-reference/equinox/solstice/pole/date-line testing is still required for production. Do not claim navigation-grade astronomy from this experiment. The shader/mask should update at a slow clock cadence, not per aircraft frame.

Optional city lights are an artistic historical texture blended using the same sun state. They require a separately recorded source/date/license and should remain clearly separate from current weather or operational observations. None is included here. NREL/NLR SPA source was not copied because its published software redistribution terms are restrictive; the local experiment implements NOAA's published equations with attribution.

## Motion and update scheduling

Recommended live bridge: at most **5 visual snapshots/second**, configurable down to 2 for large views, with one latest pending snapshot and no unbounded Dispatcher queue. On the producer path, take a reference and signal; do not enumerate/copy routes or serialize JSON in the flight callback. Read-only projection and immutable geometry construction happen in a worker. Cache route geometry by route ID/revision/projection policy. Post serialized data through WebView2 only on its owning STA Dispatcher and at Background priority. If the renderer is busy, keep only the newest complete payload. Limit one bridge payload in flight; acknowledgments must have generation/sequence and a timeout.

WebView2 browser/renderer/GPU processes and MapLibre workers isolate expensive drawing/preparation, but host API calls still use WPF's thread. Browser hosting alone is **not** proof that flight processing cannot be delayed. Set explicit main-Dispatcher latency and serialization/payload budgets, instrument them, and reject the rollout if they are missed. [Microsoft threading model](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/threading-model), [process model](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/process-model).

Renderer interpolation uses a small delayed pair of accepted samples from the same entity/generation/source. Interpolate position on a spherical path and heading along the shortest angular difference. Use one timebase converted to monotonic elapsed time; simulator/replay jumps reset interpolation. Never blend across reconnects, teleports, invalid positions, stale gaps, source changes or paused/slew/menu states. Freeze/dim at the last accepted position rather than predict indefinitely. Reduced motion uses direct sample positions. Renderer positions never return to the coordinator, recorder, ETA/progress calculator or history.

The prototype uses generated 2Hz endpoints with 500ms delayed interpolation and approximately 30Hz batched GeoJSON updates. It is intentionally simple and resends the whole aircraft source. MapLibre's `updateData` can apply ID-based diffs, but it requires unique feature IDs; test it against full-source replacement and later GPU attributes before claiming a better path. [GeoJSONSource API](https://maplibre.org/maplibre-gl-js/docs/API/classes/GeoJSONSource/), [large-data guidance](https://maplibre.org/maplibre-gl-js/docs/guides/large-data/).

## Progressive scale and detail

| Scale | Proposed strategy (not a measured production guarantee) |
| --- | --- |
| 1–25 aircraft | Batched symbol layer, selected flight/route detail, bounded observed trail. |
| 100 | Shared symbols and immutable static routes, selected-only detailed labels. |
| 500–1,000 | Frustum/viewport culling, zoom-sensitive labels, route/trail budgets, throttled updates and diagnostics. |
| Several thousand | Aggregate global traffic, reduce source churn, benchmark updateData/custom GPU interpolation; keep selected aircraft full fidelity. |

Global zoom shows major/assigned airports and selected routes, with summarized traffic. Regional zoom expands aircraft and relevant routes/weather; local zoom adds route fixes and airport labels only when data supports them. A proposed starting budget is 2,000 visible aircraft symbols, 100 labels, 20,000 total route vertices and 512 vertices per displayed trail; these are configurable engineering guardrails to validate, not observed capacity limits. Selected entity survives culling. Simplification never changes stored telemetry or joins known gaps. Spherical great-circle densification precedes projection and antimeridian splitting; antipodal ambiguity requires a waypoint. Mercator latitude clipping is presentation-only and must not delete source coordinates.

## Interaction and accessibility

Support pan, wheel/keyboard zoom, reset/home, global view, current-flight focus, follow selected aircraft, aircraft/airport/route picking, optional hover, and layer filters. Camera/selection live in MapPresentationState and may persist as user preferences. Panning cancels follow; changing selection cannot accept an assignment, change a route or mutate flight truth.

Use batched GPU feature picking, not a WPF/DOM element per aircraft. Keep equivalent selection/detail actions in native accessible lists and labeled buttons; allow focus to leave the map, Escape to end follow/selection, reduced motion, keyboard-only operation and readable text alternatives. Canvas symbols alone do not expose thousands of accessible flight objects.

The prototype uses normal WPF WebView2 in a separate non-overlapping grid cell; map controls are inside the local page. Standard WebView2 has HWND airspace constraints. Production shell overlays may need **WebView2CompositionControl** or a carefully bounded nonoverlapping host. Microsoft's drop-in composition option is documented, but not benchmarked by this prototype; validate input/focus, overlay clipping, DPI and frame cost before choosing it for rollout. [Microsoft WPF guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/platforms/wpf).

## Failure, security and distribution

| Condition | Required production behavior |
| --- | --- |
| No internet / provider unavailable | Keep operational overlays and camera; switch to bundled geography; show offline data scope. |
| Missing token | Local map remains usable; concise configuration state, no repeated modal errors. |
| Missing/invalid style | Use bundled known-good style; distinguish style failure from no flight. |
| Partial tile failure | Bounded retry/backoff; retain good tiles; visibly mark degraded geographic coverage. |
| Runtime missing / initialization timeout | Render native unavailable/fallback state and leave other Operations controls functional. |
| Renderer crash/context loss | Detach failed presentation, bounded restart or old renderer fallback; generation reset; no simulator reconnect. |
| View hidden/disposed | Stop visual updates/cadence, cancel in-flight projection, detach callbacks and dispose controller; runtime remains alive. |

Bundle audited, pinned renderer/style/worker assets, not a CDN page. The prototype uses a private HTTPS virtual-host origin mapped only to its asset folder, same-origin CSP, no host objects, blocked external navigation/popups/downloads/permissions, and origin/size/type checks on messages. It opens no operational files. Production should additionally schema-validate values/counts, reject stale generation, sanitize displayed text, allowlist provider resource hosts, avoid arbitrary URLs and keep secrets out of logs. Credential configuration should hold references rather than putting tokens in snapshots. Public client tokens can be extracted; use least privilege, quotas and provider-supported restrictions rather than claiming desktop secrecy. [Microsoft security guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security).

MapLibre BSD-3 and bundled third-party notices travel with exact assets. WebView2 SDK/runtime have Microsoft terms. Use Evergreen Runtime detection/bootstrap for production or explicitly budget and patch a Fixed Version runtime; do not assume the SDK package installs the browser. Fixed Version trades reproducibility for size and update ownership. The prototype uses the already installed Evergreen runtime and changes no machine installation. [Microsoft distribution guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution).

Natural Earth needs no mandatory attribution, but the prototype retains credit. Every future basemap, font/glyph, sprite, airport database, weather raster and city-light asset needs independent provenance/rights/attribution. No provider signup, key, purchase or service agreement was made for this checkpoint.

## Exact proposed 7.22 scope

1. Promote reviewed renderer-neutral contracts into a presentation assembly; add strict input/schema/generation and payload-budget validation.
2. Implement read-only MapProjectionService using existing snapshots, route geometry and typed source/reliability; no world-engine data.
3. Implement capacity-one coalescing worker bridge, bounded host posts and measurable Dispatcher latency; add stress tests proving operational event/history completeness under a slow renderer.
4. Implement WebView2 adapter behind OperationsMapHost, with runtime detection, startup timeout, crash disposal, bundled assets/style and existing FlightRouteMap fallback/rollback switch. Decide standard vs composition host through shell-overlay tests.
5. Initially ship bundled offline geography, current observed aircraft, verified planned route, gap-preserving observed trail, assigned airports, selection/focus/follow and truthful unavailable states. No paid provider is required for this first integration.
6. Validate interpolation resets, dateline/polar/antipodal paths, day/night against independent references, 150%/200%/monitor changes, keyboard/focus/reduced motion and device/context failure.
7. Measure 1/25/100/500/1000 synthetic aircraft on representative lower-end hardware, with selected labels/trails and real snapshot serialization. Set accepted CPU/memory/startup/latency budgets before enabling by default.
8. Run full migration suite, nine recorded flights / 91,366 samples, persisted-data and presentation-purity checks; perform native simulator release checks when available. Keep Flight Tracking's map and all flight algorithms unchanged.

Explicit exclusions: persistent airline world, simulated production traffic, live gate provider, detailed airport ground/taxi chart rendering, city lights, terrain/3D, other workspace migration and final branding. Any online vector provider rollout remains separately gated by data rights/configuration/testing.
