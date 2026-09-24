# Checkpoint 7.22 — production Operations Map V2 implementation plan

> Use focused subagent implementation for the independent presentation model and browser layer; root implements host/bridge/integration and verification. Preserve this ledger across interruptions.

**Goal:** Integrate a truthful, isolated MapLibre map behind OperationsMapHost, retaining safe rollback and the complete flight regression baseline.

**Architecture:** Existing immutable FlightSessionSnapshot feeds a read-only map projection on a bounded worker. A capacity-one mailbox serializes validated snapshots; a short STA post forwards them to a local WebView renderer with generation/sequence acknowledgments. Map state never feeds operational state. Host controls initialization, visibility, failure/restart and the old renderer fallback.

**Stack:** .NET 10 WPF, pinned WebView2 1.0.4191.47, local MapLibre GL JS 6.11.2, bundled Natural Earth 50m, existing Sierra tokens.

**Spec:** `docs/superpowers/specs/2026-09-24-checkpoint-7.22.md` supplied/authorized by the user; 7.21 architecture remains the design basis.

## Global constraints and rulings

- Operations only. Flight Tracking's FlightRouteMap, Core algorithms, persistence paths/schema, simulator behavior and product version remain unchanged.
- Real currently available data only; synthetic load data lives in explicit diagnostics, never normal presentation.
- Keep legacy renderer available. Default enablement follows evidence, not implementation completion.
- Full production suite, 9 journals and 91,366 samples required. Two native checks can remain unavailable, never passes.
- No external runtime install, API key, provider account, push or rebranding.
- Ruling: existing detailed user-authored specification and 7.21 design authorize execution; no redundant design approval. Work on a new local branch in the existing checkout to preserve the prepared replay/dependency environment. The unrelated 7.11 bundle remains untouched.

## Tasks and acceptance

### Task 1 — neutral model, validation and projection

Create `src/Alpha6Ops.Desktop/MapV2/MapContracts.cs`, `MapProjectionService.cs`, `MapGeography.cs`, `MapSolar.cs` and model diagnostics. Promote immutable prototype types with typed sources; validate duplicate/empty IDs, UTC timestamps, finite numbers, position bounds, schema/generation/sequence, array/geometry/payload budgets. `MapProjectionService.Project(FlightSessionSnapshot state, bool demo, long generation, long sequence, DateTimeOffset now)` returns a complete `MapSnapshot`; `Reset()` clears visible trail. It consumes existing snapshot fields plus additive read-only `ConnectionGeneration`. No service calls or operational mutation.

- [x] Implement model/geometry/solar and regression assertions. Deviation: some initial implementation was test-after; explicit corrective red/green evidence retained below.
- [x] Test observed/free-flight/planned-only/unknown states; stale/pause/slew/menu, reconnect/time reversal/teleport/identity/hidden gaps; route completed/planned distinction; dateline/high latitude; duplicate/NaN/budget rejection; immutable ownership and projection purity.
- [x] Verify solar cases using independent approximate reference calculations and source-labeled expected values, including seasons, leap year and poles.

### Task 2 — bounded host/bridge and offline packaging

Create `MapSnapshotBridge.cs`, `WebMapRenderer.cs`; add local assets to Desktop output/publish and a pinned WebView package/lock. `IMapRenderer` exposes a native view, async initialization/disposal, nonblocking Offer, presentation state and typed events. Root integrates additive snapshot input into OperationsOverview and OperationsMapHost. Standard WebView is the first measured host; use composition if real shell clipping/overlays fail.

- [x] Test mailbox supersession, stale ACK/renderer generation, malformed payloads, slow ACK timeout, disposal and queue bounds before final adapter wiring.
- [x] Worker validates/projects/serializes; bounded Dispatcher posts; one in flight and one latest pending; generation reset on recreation. Record counts/timings only.
- [x] Runtime detection, 10-second initialization timeout, one bounded restart then old-map fallback; opt-in selector/environment rollback. Hidden views stop updates and clear observed continuity.
- [x] Package pinned local assets, licenses, offline geography, generated palette; prohibit network map requests. No silent runtime installation.

### Task 3 — browser rendering and interactions

Create `src/Alpha6Ops.Desktop/MapV2/Web/{index.html,map.css,map.mjs}`. Consume camelCase neutral contracts supplied by host. Validate version/generation/sequence and ACK only applied snapshots. No synthetic generation in production scripts.

- [x] Test typed feature conversion, gaps and interpolation resets before rendering implementation.
- [x] Implement Sierra geographic layers, origin/destination, waypoints, planned/completed/observed/inferred routes, source-aware batched aircraft and accessible textual selection.
- [x] Spherical position/shortest heading interpolation, bounded delayed sample pairs, stale/freeze/reset/reduced motion; no output of interpolated position.
- [x] UTC sun-vector day/night, custom Sierra zoom/home/focus/follow controls, selection/highlight, keyboard bypass/focus and hidden rendering suspension.

### Task 4 — production diagnostics and release decision

Create explicit `--map-v2-checks` diagnostics and `scripts/verify-map-v2.ps1`. Use real production adapter/bridge, test-only fixture input, and existing flight session harness patterns. No diagnostic traffic in normal startup.

- [x] Measure 1/25/100/500/1000/2000/5000 aircraft with route/trail/label workload; record worker/Dispatcher/post/ACK/CPU/memory/startup/cadence/coalescing.
- [x] Deliberately stall ACK/renderer; run authoritative flight samples and compare events/history/phase/PIREP plus recorder counts and Dispatcher heartbeat latency.
- [x] Exercise missing assets/runtime, malformed/stale messages, crash/context loss, restart/fallback, repeated show/hide/recreate/resize/follow cycles and process cleanup; bounded soak, honest leak limits.
- [x] Capture actual Operations at 1920×1080, 2560×1440, 1366×768, 150% and 200%; compare/polish. Evaluate clipping/overlays/focus/minimize/restore and disclose untested tooltip/dialog/manual input/assistive technology cases.
- [x] Full build/package and existing suite, all nine replays/91,366 samples, review and correct findings. Decide default versus opt-in against measurements.
- [x] Write full 30-item report, local commit and stop. No 7.23 work.

## Review focus / interface scan

| Boundary | Risk | Resolution / test |
| --- | --- | --- |
| Tasks 1→2→3 | mismatched record/wire names | preserve prototype names, camelCase and string enums; coordinate refinements before agent writes |
| Task 1→OperationsOverview | display text becoming authority | carry detached session snapshot and explicit demo flag, never parse MapContext |
| Task 2→coordinator | expensive map callback | publish reference only; all projection/serialization on worker; contention test |
| Task 2→3 | obsolete ACK/selection applied to new view | renderer epoch plus snapshot generation/sequence, strict schema/type validation |
| Task 2→old tests | legacy renderer assertions bypass V2 | keep old-mode checks, add separate real V2 diagnostics and fallback checks |
| Task 3→solar/geometry | visually plausible false continuity | typed observed/inferred routes, gaps and independent solar cases |
| Task 4→enablement | benchmark without realistic layers | use production bridge and bounded routes/trails/labels; fail closed to opt-in if a major gate is unproven |

## Execution ledger

- Base `259dc17`; no preexisting tracked changes. Existing 7.21 full verification retained as start baseline; no unnecessary repeat before implementation.

### Implementation and review evidence (24 September)

- Production contracts/projection, capacity-one bridge, local browser assets, opt-in Operations host and diagnostics implemented. Flight Tracking/Core algorithms unchanged; additive snapshot connection generation only.
- Root review corrected demo/live provenance and prevented airport endpoints fabricating a route. Model and pump suite: 54 passes (`work/checkpoint-7.22/pump-green/model.json`).
- Independent review caught loss of a brief pause before coalescing. New pump check reproduced it (`pump-red`), then a latched discontinuity token fixed it. Projection and renderer queue counters both reported. Constructor recovery and selection-filter restoration corrected.
- First integration found blocked local solar PNG fetch (CSP corrected); composition revealed a missing WinRT assembly (Windows 10.0.17763 target/reference pack added, normal dependencies retained). Composition template failures now remain contained at the map boundary.
- Real lifecycle/failure/load checks executed through 5,000 aircraft. 12 repeated navigation/recreation cycles release owned browser processes. Missing runtime/assets, context loss, process crash, invalid payload and readiness timeout exercise bounded restart/fallback. Early failed runs retained under work/checkpoint-7.22/integration-*.
- Focused contention: both real 395-sample coordinator flights produced identical 27 operational events, complete phases, history and durable PIREPs. All 395 stressed samples processed inside the 1-second actual JavaScript stall. Production projection/bridge coalesced 393 visual updates; no authoritative samples lost (`contention-focused/contention.json`).
- Browser math checks 7 passes; mailbox checks 7 passes. Pure-model initial implementation was partly test-after rather than fully test-first; pump/mailbox corrective checks have explicit red/green evidence.
- Visual polish: enlarged only V2 map panel to avoid clipping the destination; added compact/scaled scrolled captures. Actual OS monitor-DPI/input/screen-reader certification remains unproven; default rollout remains gated.
- Final packaged map assertions: 119/119; mailbox 7/7; browser 7/7. Full production: 1,871/1,871; 9/9 replays and 91,366/91,366 samples; two native checks unavailable. Final map runner exits 1 because the unexpected-report guard found an existing recovery-file replacement failure. The classic-renderer production suite and independent no-map file probe reproduce that error; storage behavior was not modified. Results and remaining gates are detailed in docs/checkpoint-7.22.md.
