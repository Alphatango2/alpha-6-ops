# Checkpoint 7.22 — Operations Map V2 production integration

**Implemented and evaluated locally. Rollout: OPT-IN. NO-GO for default activation.** The classic Operations renderer remains the default and fallback. Flight Tracking's map is unchanged. No airline-world simulation, other workspace migration, installation, GitHub push or distribution rebranding was performed.

The production solution/package builds, all **1,871 existing assertions** pass, and all **133 new map assertions** pass. Nine recorded flights process all **91,366 samples**. However, the final map verification command **exits 1** because its additional unexpected-report guard detects an intermittent existing `flight_recovery_save` access-denied error. This report does not call that run clean. The same error also appears with the classic renderer in the production suite and in a separate file-replacement probe with no application or map loaded. The storage code was not changed. Manual input/DPI and sustained resource gates also remain incomplete.

Base: `259dc17`; local branch: `codex/checkpoint-7.22`. Product/package version remains **0.22.0** during migration. Primary raw runs: `work/checkpoint-7.18a/20260924-161917-12245ffd` and `work/checkpoint-7.22/packaged-map-final`. Compact evidence is retained in [map-v2/evidence/7.22](map-v2/evidence/7.22).

## 1. Production architecture

```text
FlightSessionCoordinator (existing authority)
  → detached FlightSessionSnapshot / OperationsOverview
  → OperationsMapHost (visibility, presentation state, fallback)
  → MapProjectionPump: latest input + latched discontinuities, worker at ≤5 Hz
  → read-only MapProjectionService → validated immutable MapSnapshot
  → capacity-one renderer mailbox → worker validation/serialization
  → short Dispatcher post → local WebView2 / MapLibre workers and WebGL
```

Only camera, selection, layer preferences and renderer status return from the map. No returned geographic interpolation enters phase, progress, ETA, events, history, PIREP or simulator logic. The coordinator's sole production change publishes its existing connection-generation counter in the immutable snapshot. Core, simulator implementations, flight algorithms and persistence schemas are unchanged.

## 2. Contracts promoted/refined

Production records include `GeoPoint`, `MapSnapshot`, `MapAircraft`, `MapAirport`, `MapRoute`, `MapWaypoint`, `MapSelection`, `MapCameraState`, `MapPresentationState`, renderer status, weather/region placeholders and `IMapRenderer`. WPF is confined to the view boundary; Operations queries never handle browser messages or MapLibre layer IDs.

Source remains an enum: **Observed / Inferred / Simulated / Replay / Unknown**. Added continuity/source generation, interpolation permission, airport role, sun vector and explicit camera restoration. Validation covers UTC timestamps, schema/epoch/sequence, enum values, finite WGS84/numeric values, identity uniqueness, text, geometry and serialized-envelope budgets. Limits: 5,000 aircraft, 2,048 airports/routes/waypoints per applicable collection, 100,000 geometry points and a 4 MiB envelope. Unsupported weather/regions must remain empty.

## 3. Projection and provenance

The projection reads the existing current aircraft, telemetry heading/position, assignment, authoritative route points, reliability, phase and progress. Origin/destination coordinates and names can use the existing offline airport catalog. It retains at most 2,048 points of **visible observed presentation history**, not a second flight recorder.

Known endpoints without supplied route geometry do not invent a direct route. Candidate geography is distinguishable from verified flight association. Demo/replay-preview mode does not relabel a live aircraft as synthetic network traffic; it emits no live traffic. Future collections fit the contracts, but production currently supplies one observed aircraft and current-flight geography only.

## 4. WebView host choice

Selected **WebView2CompositionControl**. The diagnostic instantiated both standard and composition hosts inside the actual Operations shell. The standard control is an `HwndHost` and its map is absent from the WPF render capture; the composition map appears beneath the real shell search overlay and inside scrolling/clipping. These captures are evidence of the composition path, not a claim of exhaustive manual native-input testing.

Microsoft documents the standard WPF control's airspace limitation and the composition control intended to address it. [Microsoft WPF hosting guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/platforms/wpf).

A real initialization attempt exposed missing WinRT bindings. Desktop and the migration harness now target `net10.0-windows10.0.17763.0`. Composition-template graphics failures are caught at the map boundary. Remaining tradeoffs include browser/composition memory, possible capture/scaling softness and unverified physical monitor-DPI transitions.

## 5. Security model

Pinned local files are served at `https://sierra-map.invalid`. A resource allowlist and restrictive CSP allow only the local renderer, its worker, geography/palette and generated image data. No CDN, provider key, arbitrary host objects or external navigation. Popups, downloads and unnecessary permissions are blocked; browser developer/context UI is disabled in production.

Inbound messages have origin/schema/type/size/depth validation, epoch checks and selection membership checks against the applicable snapshot sequence. Invalid/stale messages cannot operate the flight session. Outbound snapshots and the final envelope are budgeted. Failure records contain bounded reasons, not telemetry payloads or credentials. Automated malformed-origin/JSON/stale-message checks passed; this is not a full security certification.

## 6. Sierra basemap

Bundled **Natural Earth 1:50m land**, near-black ocean, charcoal land, subdued coastlines and Sierra tokens generated into the browser palette. MapLibre GL JS **6.11.2** is local and integrity-checked. The old blue-globe appearance is absent when Map V2 is selected.

This is a flat geographic map with restrained airline-scale overlays. There are no political borders, roads, buildings, terrain, city lights or commercial tiles. Fine airport/terminal zoom detail and hosted vector geography are deferred. The package retains Natural Earth attribution, MapLibre license and WebView SDK notices.

## 7. Airports

Current origin/destination receive catalog/route coordinates, ICAO labels, role text, markers, selection highlights and non-canvas detail. IATA is supported in the contract but is not fabricated when the existing source lacks it. Operational status remains **Unknown**; no gate availability, congestion or weather is inferred.

## 8. Routes and tracks

Planned route: Sierra yellow. Completed planned portion: muted gray. Observed trail: restrained information blue. Inferred track: dashed, with source text. Supplied route waypoints are displayed selectively. Great-circle segments and dateline splitting are presentation geometry; the completed split uses the existing authoritative progress only when reliable.

Stale telemetry, pause, slew, menu, reconnect, source/identity change, time reversal/jump, teleport and hidden-view intervals prevent false continuous observation. A brief pause is latched **before** queue coalescing, so it cannot disappear between displayed samples. Recreating the renderer starts a fresh visible trail. Unknown position is never synthesized. No inferred live trail is produced unless an authoritative input exists.

## 9. Aircraft

Aircraft are batched GPU/map features, not individual WPF controls. The current aircraft has a heading-oriented symbol, typed source/reliability, selection and follow mode. Missing heading uses a nondirectional marker. Stale observations are dimmed and frozen; unavailable/menu states do not claim current observation. Fleet traffic appears only in explicit diagnostic load fixtures.

## 10. Interpolation

Accepted sample pairs use spherical position and shortest-angle heading interpolation with a bounded delay of up to 1.5 seconds. Epoch, identity, source, continuity, timestamp and capture-gap checks freeze/reset movement. Reduced motion disables easing/interpolation. Simulator dates are not compared directly with computer wall-clock dates when deciding continuity. Interpolated positions have no operational write-back path.

## 11. Day/night

`MapSolar` uses NOAA's fractional-year solar approximation; the browser draws a geographic sun-vector mask with a restrained twilight blend. This is not a fixed screen gradient. In current production, lighting follows **computer UTC at snapshot capture**, independently of simulator date. It is a visualization, not navigational solar precision.

Checks cover equinoxes, both solstices, leap day, midnight/dateline, normalized vectors, UTC-offset invariance and north/south polar-season reversal. Six additional fixed subsolar latitude/longitude cases were calculated independently from the USNO orbital-coordinate and sidereal-time equations, using UT1≈UTC, and compared within **1°**. [USNO solar coordinates](https://aa.usno.navy.mil/faq/sun_approx), [USNO sidereal time](https://aa.usno.navy.mil/faq/GAST).

| UTC | Independent latitude | Independent longitude |
| --- | ---: | ---: |
| 2024-03-20 12:00 | 0.147536° | 1.828772° |
| 2024-06-21 12:00 | 23.434408° | 0.480127° |
| 2024-09-22 12:00 | 0.011265° | −1.867981° |
| 2024-12-21 12:00 | −23.435666° | −0.424586° |
| 2024-02-29 12:00 | −7.669732° | 3.102056° |
| 2024-06-21 00:00 | 23.435723° | −179.547054° |

The Mercator viewport clips the exact poles; polar solar math is still tested. Raster mask resolution is 512×256 and illumination is intentionally subtle. No city-light imagery was added.

## 12. Controls and selection

Sierra-styled zoom, World, Focus, Follow and Layers controls; aircraft/airport/route selection; restored highlight filters; source/status text outside the canvas; reduced-motion and layer toggles. Selecting/following changes presentation only. A dedicated control strip prevents airport labels being hidden under controls at compact/scaled sizes. Camera, selection and preferences survive safe view recreation within the session.

## 13. Failure and fallback

Ten-second initialization deadline; one automatic restart per activation, then classic-map fallback. Diagnostics exercised constructor failure, missing runtime detection, missing assets, JavaScript initialization failure, missing handshake/timeout, invalid payload, actual WebGL context loss and actual renderer-process crash. All fallback assertions passed. Failure does not reconnect or publish a new flight snapshot. Runtime absence was injected; the installed runtime was not uninstalled.

## 14. Hidden/disposed behavior

Navigating away, unloading or minimizing cancels the projection/mailbox and disposes the WebView. Returning consumes the latest immutable state with a new renderer epoch and fresh visible trail. The application flight owner continues independently. Twelve navigation/recreation cycles rejected obsolete callbacks, retained the authoritative owner snapshot and left no owned browser processes after final disposal.

## 15. Runtime/distribution

The package includes the pinned WebView2 SDK/loader, WinRT bindings, local map assets and notices. It detects the installed Evergreen Runtime; missing/failed runtime falls back safely. No runtime bootstrapper was run or bundled and no Fixed Version runtime was added. Future installer prerequisite handling remains separate. Evergreen is serviced independently of the pinned SDK. [Microsoft distribution guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution).

See [runtime, opt-in and repeatable verification instructions](map-v2/production-runtime.md). Existing profile/database paths and package names remain intact.

## 16. Production bridge measurements

Both projection and renderer queues are capacity-one with one consumer; the producer never awaits the browser. Counters cover offered/produced/consumed/coalesced snapshots, rejects, payload size, projection, serialization, Dispatcher queue delay, post duration, ACK and readiness. Epoch and sequence identify each renderer/snapshot. ACK means the browser accepted source updates, **not** GPU frame completion.

The final complete-flight stress run measured projection p95 **0.072 ms**, serialization p95 **0.070 ms**, Dispatcher post p95 **0.179 ms** (max **0.238 ms**), queue p95 **61.2 ms**, and ACK p95 **76.9 ms** (max **817.2 ms**, including the deliberate stall). These are local bounded measurements, not simulator-wide latency guarantees.

## 17. Dispatcher contention

Two isolated real `FlightSessionCoordinator` instances received the same **395 samples** through production connection callbacks: one without a map, one using the real production projection pump and WebView bridge. The second browser blocked JavaScript for **1,000 ms**; **50 samples** were processed during that interval. Input cadence was 20 ms per ten simulator seconds. Earlier 2 ms runs also retained all samples, but aggravated the unrelated file-replacement issue.

Both runs produced **27 ordered events**, all four OUT/OFF/ON/IN phase events, Complete, eligible/submitted PIREPs and durable reopened SQLite history. Timeline/event/history/PIREP hashes match, excluding volatile submission metadata. Final recovery state contains the final sample. No simulator reconnect occurred.

Feed duration: **7,983 ms baseline / 8,020 ms stressed**. Maximum 20 ms Dispatcher heartbeat gap: **121.0 / 139.7 ms**. The stress path consumed 395 authoritative samples, produced 40 visual projections and acknowledged 37; **358 visual inputs** coalesced across the two queues. None of the authoritative samples coalesced. [Contention evidence](map-v2/evidence/7.22/contention.json).

This isolation gate passed its assertions. The separate recovery-save error guard did not pass; see sections 24 and 27. Native SimConnect was not used by this diagnostic source.

## 18. Synthetic production-path load benchmark

Actual production adapter, mailbox, serialization and composition host; each stage approximately 4.1–4.4 seconds, 19 offered snapshots. Up to 25 great-circle routes, 25 trails × 64 points and 25 aircraft labels accompany the batched aircraft. The one-aircraft case has one route, one trail and one label. This is not icon-only or a fleet-capacity certification.

| Aircraft | Ready ms | Serialize p95 ms | Post p95 ms | ACK p95 ms | ACK/coalesced | CPU, machine % | Peak summed WS MiB |
| ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: |
| 1 | 641 | 0.12 | 0.20 | 705 | 12 / 7 | 4.11 | 806 |
| 25 | 661 | 0.72 | 2.65 | 892 | 11 / 8 | 4.30 | 818 |
| 100 | 634 | 0.87 | 3.20 | 540 | 16 / 3 | 4.72 | 822 |
| 500 | 640 | 1.63 | 5.79 | 724 | 13 / 6 | 4.93 | 887 |
| 1,000 | 665 | 2.41 | 9.60 | 617 | 15 / 4 | 7.19 | 925 |
| 2,000 | 641 | 2.80 | 15.96 | 1,101 | 11 / 8 | 7.56 | 922 |
| 5,000 | 634 | 8.32 | 42.39 | 700 | 14 / 5 | 12.81 | 1,061 |

All seven stages displayed the final expected aircraft count/sequence without losing the final snapshot. Effective accepted-update cadence was approximately 2.7–3.8 Hz. Startup/initial geography work is included; large ACK values largely include Dispatcher queue delay (p95 up to 1,070 ms). A 43 ms maximum post at 5,000 aircraft is not an acceptable basis for a broad capacity promise.

CPU is normalized across machine logical processors. Working-set totals include the WPF host and owned browser processes and can double-count shared pages. **Sustained steady-state memory was not established** by this short benchmark. [Compact benchmark](map-v2/evidence/7.22/benchmark.json).

## 19. Lifecycle/soak

Twelve repeated show/hide, navigation, resize, follow/unfollow and renderer-recreation cycles over **14.0 seconds**. Recreated readiness: **399–612 ms**. Host working set varied **352.5–384.8 MiB**, ending **375.6 MiB**; each active environment reported five browser processes. No owned browser PID remained 2.5 seconds after final disposal. Failure/restart tests are additional to those cycles.

This demonstrates bounded disposal and no monotonically increasing memory across that short sample. It does **not** establish leak freedom or a multi-hour flight memory plateau. [Lifecycle evidence](map-v2/evidence/7.22/lifecycle.json).

## 20. Accessibility/input

Named HTML controls, visible focus styling, reduced-motion preference, live non-canvas selection text and Escape focus bypass are present. Trusted browser keyboard injection activated Layers; WPF focus moved outside the map on Escape. Selection restoration and reduced-motion preference passed automated checks.

Real pointer/touch use, Tab traversal across every shell/browser boundary, native tooltip/dialog interactions, monitor-to-monitor DPI changes and screen-reader certification remain **manual/unavailable**. DOM activation and screenshots do not prove all of those cases. Scaling captures use WPF `LayoutTransform`, not an actual Windows monitor-DPI change.

## 21. Visual review

Actual packaged Operations view with explicitly labeled diagnostic current-flight data, not a separate webpage mockup. Reviewed against the Sierra concept for dark geography, yellow restraint, route/aircraft visibility, controls and panel balance. The concept's imagined network, imagery and airport weather were not fabricated.

| Capture | Review |
| --- | --- |
| [1920×1080](map-v2/evidence/7.22/operations-1920x1080.png) | Map is prominent; complete route/endpoints, plane and muted waypoint hierarchy visible. |
| [2560×1440](map-v2/evidence/7.22/operations-2560x1440.png) | Wider geography and surrounding Operations panels compose correctly. |
| [1366×768, scrolled](map-v2/evidence/7.22/operations-1366x768-map-scrolled.png) | Normal vertical scroll required; controls and airport labels no longer overlap. |
| [150% rendering, scrolled](map-v2/evidence/7.22/operations-scale-150-map-scrolled.png) | Enlarged text/controls remain readable; more vertical scroll required. |
| [200% rendering, scrolled](map-v2/evidence/7.22/operations-scale-200-map-scrolled.png) | Geography/controls remain usable; composition capture shows softness. Not native-DPI certification. |
| [Search overlay](map-v2/evidence/7.22/shell-search-overlay.png) / [utilities](map-v2/evidence/7.22/shell-utilities.png) | WPF shell draws above composition content. |

Dedicated polish enlarged the V2 panel to avoid clipping the route destination, cached label images independently of motion/timestamps, corrected initial route fitting and reserved a separate control strip after compact review. Remaining limitations: 50m close-zoom coast detail, flat-map appearance, subtle day/night effect, compact scrolling and 200% rendering softness. Other workspaces and Flight Tracking retain their existing UI/map by design.

## 22. Files created/modified

**Created:** `.gitattributes` preserves exact hashed asset/vendor bytes across Windows checkouts. `src/Alpha6Ops.Desktop/MapV2/` contains `IMapRenderer.cs`, `LatestMapMailbox.cs`, `MapBridgeMetrics.cs`, `MapContentionChecks.cs`, `MapContracts.cs`, `MapGeography.cs`, `MapModelChecks.cs`, `MapProjectionPump.cs`, `MapProjectionService.cs`, `MapSolar.cs`, `MapV2Checks.cs`, `WebMapRenderer.cs`; `Web/` contains `index.html`, `map.css`, `map.mjs`, `map-math.mjs`, `theme.json`, `land.geojson`, `assets-manifest.json`, `NOTICES.txt`, three vendor ESM files and three license/notice files. Added Desktop `packages.lock.json`, `tests/Alpha6Ops.MapBridgeChecks/{Alpha6Ops.MapBridgeChecks.csproj,Program.cs}`, `tests/map-v2/browser.test.mjs`, `scripts/prepare-map-v2.ps1`, `scripts/verify-map-v2.ps1`, the 7.22 specification/plan, this report, runtime notes and the compact evidence directory.

**Modified:** `NuGet.Config`; Desktop project and `App.xaml.cs`; `FlightSessionCoordinator.cs`/`FlightSessionSnapshot.cs` (additive generation only); `OperationsOverview.cs`; `OperationsMapHost.cs`; `OperationsWorkspace.xaml.cs`; migration-check project; `scripts/verify-desktop.ps1`; `scripts/verify-migration-baseline.ps1`; `packaging/Desktop-Quick-Start.txt`.

No Core, Flight Tracking map, operational tracking, database/recovery schema or simulator implementation edits. The preexisting untracked 7.11 bundle was left untouched.

## 23. Build/package result

**PASS:** normal Release solution build, normal production publish/portable ZIP/setup generation, migration-harness build, locked map build, pinned-asset verification and execution of the map diagnostics from the packaged executable. Package artifacts remain local under `outputs/Alpha6OPS-Desktop-0.22.0`, `Alpha6OPS-Desktop-0.22.0-win-x64.zip` and `Alpha6OPS-Setup-0.22.0.exe`. They were not installed. Existing distribution names/version remain unchanged.

## 24. Regression counts and failing gate

| Assertion group | Passed | Failed | Unavailable |
| --- | ---: | ---: | ---: |
| Core | 122 | 0 | 0 |
| Desktop smoke | 18 | 0 | 0 |
| Dashboard/data honesty | 531 | 0 | 0 |
| Refresh | 1,010 | 0 | 0 |
| Identity / index / monitor / tracking | 108 | 0 | 0 |
| Single-instance activation | 1 | 0 | 0 |
| Recorded flights | 9 | 0 | 0 |
| Migration contracts/persistence compatibility | 31 | 0 | 2 |
| Large diagnostic index / fleet importer | 41 | 0 | 0 |
| Map model/browser/real-host/bridge/contention assertions | 119 | 0 | 0 |
| Mailbox unit checks | 7 | 0 | 0 |
| Browser math/wire tests | 7 | 0 | 0 |
| **Total assertion executions** | **2,004** | **0** | **2** |

Counts exclude preliminary failed runs and reruns. The two unavailable checks are native connection lifecycle and native rapid reconnect, not counted as passes. Existing migration checks protect hidden runtime, reconnect generation rejection, animation-independent assignment/PIREP persistence, display-wording independence and readable saved data.

**Additional gate: FAIL.** `verify-map-v2.ps1` exits **1** because one unexpected `flight_recovery_save` access-denied report exists in the final packaged run. Thus the entire verification is **not all green**, even though all 2,004 assertions pass. The guard was retained, not suppressed or reclassified as a pass. [Unexpected report](map-v2/evidence/7.22/unexpected-recovery-save.json), [map assertions](map-v2/evidence/7.22/map-checks.json), [production assertions](map-v2/evidence/7.22/production-summary.json).

## 25. Recorded-flight replay result

**9/9 passed:** JST221, QFA124, JST786, QLK1579, JST721, JST724, JST760, QLK1274 and ANG2. Input hashes were unchanged. This preserves existing A32x/A220/Boeing coverage and is separate from synthetic map loads.

## 26. Telemetry result

**91,366 expected / 91,366 raw / 91,366 processed**, with no replay exclusions: JST221 11,965; QFA124 11,535; JST786 6,952; QLK1579 9,040; JST721 7,650; JST724 7,459; JST760 10,552; QLK1274 10,519; ANG2 15,694. [Coverage and journal hashes](map-v2/evidence/7.22/replay-coverage.json).

## 27. Known risks/debt

- **Unresolved file replacement:** existing `FlightRecoveryStore.Save` occasionally receives `UnauthorizedAccessException` from `File.Move(..., overwrite:true)`. It recurred in map contention, classic-renderer production smoke and a standalone no-map probe (**3 failures / 500 replacements**). This isolates a filesystem-sensitive failure outside the map, but does not identify the external lock/OS cause or prove recovery reliability. Final timeline/history/PIREP checks succeeded; an intermediate recovery checkpoint can still fail. No retry/storage rewrite was made in this map-only checkpoint. [Probe evidence](map-v2/evidence/7.22/file-replace-probe.json).
- **Performance:** startup/Dispatcher queue delays and composition/browser memory require sustained profiling; high-load posts reach 43 ms. Short staged tests cannot authorize future fleet capacity.
- **Manual verification:** native simulator reconnect, pointer/touch, complete keyboard traversal, physical DPI and assistive technology remain unverified.
- **Visual/data limits:** coarse close zoom, flat Mercator/polar clipping, capped labels, visible-session-only trail, computer-UTC lighting and no imagery/weather/network traffic.
- **Lifecycle/distribution:** longer-run and older-GPU testing plus Evergreen installer handling remain pending. Browser cache remains in a bounded-purpose profile directory; its disk growth was not measured over long sessions.

Independent static review confirmed the earlier coalesced-pause, constructor-fallback, projection-counter and selection-restoration findings were fixed and found no remaining critical flight-isolation path. Runtime evidence, including the failing report guard, remains authoritative.

## 28. Enablement state

**OPT-IN.** `DefaultV2Enabled` remains false. Use Map V2 activates it for the session; Use classic map returns immediately. The legacy renderer remains both default and automatic fallback. No application-wide preference was silently changed.

## 29. Exact enablement reason

The bounded flight-isolation and renderer-fallback assertions support local opt-in evaluation. They do **not** support making it the default while an unexpected persistence report fails the final guard, sustained resource budgets remain unproven, and manual input/native-DPI gates are incomplete. Large-load post/queue measurements reinforce that restraint. This is a deliberate **NO-GO for default rollout**, not a forced acceptance based on rendering alone.

## 30. Next-checkpoint recommendation

First investigate and harden the existing recovery-file replacement under representative filesystem conditions, with a regression that preserves atomic/readable recovery. Then profile a sustained one-flight Map V2 session, reduce composition/Dispatcher cost, and validate real input/DPI/native reconnect on representative hardware. Re-run both verification commands before reconsidering default enablement. Keep Flight Tracking migration and persistent airline-world work separate until those gates are resolved.

Checkpoint 7.22 stops here. No 7.23 implementation was started.
