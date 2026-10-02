# Checkpoint 7.24C Implementation Plan

> **For agentic workers:** Use systematic debugging and test-driven development task by task. The user's supplied checkpoint specification authorizes proceeding with scoped implementation; do not repeat permission requests for that work.

**Goal:** Correct measured Map V2 interaction/restoration defects, geographic progress and gate-ETA correctness, and active-flight recovery invariants without changing flight-session ownership or weakening persistence.

**Architecture:** Preserve the existing WPF shell, application-owned FlightSessionCoordinator, Core algorithms and WebView2/MapLibre renderer boundaries. Extend diagnostics before selecting a hitch correction. Keep map visibility/resource lifetime separate from authoritative flight lifetime; correct route and ETA at their calculation boundary; recover durable flight identity through the existing recovery store.

**Tech stack:** .NET 10, WPF, WebView2 1.0.4191.47, bundled MapLibre, SQLite; existing console/desktop diagnostic harnesses and PowerShell guards.

**Spec:** `work/checkpoint-7.24c/spec.txt`, preserved verbatim from the user's attachment. Evidence: `docs/flight-tracking/jst776-post-migration-validation-20260928.md` and `historical-reference-20260923-24.md`.

## Global constraints

- Classic remains default; Map V2 remains opt-in.
- No premium UI, new shell, globe, animations, RYNEL, route refresh or unrelated refactoring.
- Preserve source data, installed R3, original physical failures and all previous frozen packages.
- Preserve coordinator ownership, telemetry cadence/meaning, native reconnect protection, immutable snapshots, milestones, PIREP and recovery durability.
- Distinguish current 6,145-sample JST776, baseline 91,366 samples, synthetic performance input and recovery input.
- Native/manual gates require explicit physical evidence; automation alone cannot close them.
- Work in the existing checkout on `codex/checkpoint-7.24c`: its current uncommitted source is the required starting state. Entry patch/status and a 215-file source archive are preserved in `work/checkpoint-7.24c/`. No stash/reset or move to an incomplete clean checkout.

## Review focus

- A hidden renderer receiving late ACK/failure after rapid navigation must not revive stale state or reconnect the simulator — task 2 epoch/lifecycle checks.
- A reversed arrival, overshoot or go-around must not make zero remaining distance look credible far from destination — task 3 geographic lower-bound and replay checks.
- An expired estimate at minute rounding and phase boundaries must become future/unavailable while actual IN stays exact — task 3 freshness checks.
- Restart after OUT/OFF, after IN before PIREP, or after a failed durable save must not split one flight into duplicate history/PIREP rows — task 4 real-process fixtures.
- Diagnostic input/frame probes can change scheduling themselves; instrumentation-enabled and passive controls must remain labelled — task 1 counter/observer controls and physical handoff.

## Affected paths and existing coverage (before edits)

- Native readings: `FlightSessionCoordinator.Connection.cs` posts to WPF dispatcher; `Tracking.cs:ObserveLive` logs, detects phases, calculates route/ETA, saves recovery, publishes; `MainWindow.FlightSession.cs` refreshes presentation. The shared dispatcher means a hitch is not automatically rendering-only.
- Map: `OperationsMapHost` owns visibility/pump/renderer; hidden Operations calls `EndVisibleTrail`, disposing WebView2. `WebMapRenderer` bridges snapshots, validates epochs, and gates visible readiness on the current first frame. `map.mjs` changes GeoJSON sources, interpolates markers, applies solar/layers and reports camera. `MapProjectionPump` coalesces visual work only.
- Route: Core `RouteProgressTracker` can select the clamped final endpoint within 25 NM and latch along distance. Desktop currently recalculates remaining from fraction. Existing `TrackingTests`, replay and map projection tests exercise this boundary but miss premature zero distance.
- ETA: Core `ArrivalEstimator` publishes rounded gate-IN predictions through a two-minute hysteresis threshold; expired predictions can remain. Existing Core checks cover midnight, pause and invalid route distance.
- Recovery: `FlightRecoveryStore` durably replaces one JSON checkpoint using fsync, same-directory move, bounded Windows sharing retries and path locks. Coordinator restores milestones/events; database identity continuity must be tested through actual process restart. Existing `RecoveryChecks` cover 5,000 replacements, contention, crash-before-replace, coordinator writes and guard reports; migration tests cover detached sessions and legacy data.
- Existing validation: `verify-desktop.ps1`, `verify-migration-baseline.ps1`, `verify-map-v2.ps1`, `verify-flight-recovery.ps1`, map scheduling/responsiveness policy guards, Core and fleet-import tests. Prior R3: 2,097 pass, 0 fail, 2 automated native skips; do not present these as new results.

## Task 1 — Isolate input/processing stalls

**Files:** diagnostic partial in `src/Alpha6Ops.Desktop/MapV2/`, existing `MapPerformanceChecks.cs` diagnostic mode dispatch, isolated script under `work/checkpoint-7.24c/map/`; renderer/JS diagnostic hooks only if necessary.

**Interfaces:** consumes existing `FlightSessionCoordinator`, independent synthetic `Source`, `Timings`, `DispatcherSchedulingTrace`, `WebMapRenderer.Core` and measurements. Produces bounded timestamped input/queue/frame/source/ingestion observations; never modifies operational sample cadence.

- [ ] Run a labelled baseline with sustained browser drag, independent telemetry and normal Operations work; correlate UI queue, browser input/render, source updates, received/processed timestamps, GC and durable writes.
- [ ] Verify collector sensitivity using an explicit diagnostic injected stall, including pending ages and data-retention checks. Expected: the injected delay is visible; no invented physical pass.
- [ ] Form one causal hypothesis from the measurements; run a minimal controlled comparison. Do not repeat the old broad matrix without a concrete question.
- [ ] Add a failing regression for a proven defect, then implement the smallest correction. If attribution remains unresolved, retain instrumentation and report the exact missing physical/native evidence; do not claim a fix.
- [ ] Repeat the same workload and report before/after plus latency/retention separately. Physical installed dragging remains a handoff gate.

## Task 2 — Map restoration with bounded background cost

**Files:** `OperationsMapHost.cs`, `OperationsWorkspace.xaml.cs`, `MapV2/WebMapRenderer.cs`, `MapV2/Web/map.mjs`, relevant `MapV2Checks`/performance lifecycle checks; isolated diagnostic runner.

**Interfaces:** host continues accepting `Present(OperationsOverview)` and retaining latest snapshot. Coordinator references remain prohibited. Visibility changes may suspend visual work; renderer disposal remains explicit for failure, Classic selection, shutdown/unload, or bounded dormant expiry.

- [ ] Add lifecycle checks that fail on current destroy-on-navigation behavior: stable renderer/epoch across return; latest aircraft/route, camera/selection/layers; no duplicated listeners; unchanged connection count.
- [ ] Measure current create→ready and correct-visible-frame latency and hidden resource baseline.
- [ ] Trial retained hidden WebView with visual projection/animation suspended; keep current snapshot/solar first-paint correctness. Compare hidden CPU/process/memory and other-view input with disposal.
- [ ] Select only the measured safe lifetime policy. Stress Operations→Dispatch/Flights/other→Operations and rapid transitions, minimize/restore, Classic/V2, fault/recreation and final disposal.
- [ ] Verify retained visual state never fabricates an observed segment through an unobserved gap; no source/session ownership changes.

## Task 3 — Route and ETA correctness

**Files:** Core `RouteProgressTracker.cs`, `ArrivalEstimator.cs`; `tests/Alpha6Ops.Tests/TrackingTests.cs`; Desktop numeric consumers `FlightSessionCoordinator.cs`, `FlightTrackingEvents.cs`, `FlightTrackingWorkspace.xaml.cs` as necessary; `RecordedFlightReplay.cs`.

**Interfaces:** preserve public Telemetry and milestone contracts; document fraction versus remaining distance semantics before code. Numerical distance must not be smaller than actual distance to destination while airborne and reliable. An unavailable estimate remains nullable. Actual IN is authoritative and bypasses prediction.

- [ ] Document chosen progress semantics after tracing every numeric consumer; no 100→99 cosmetic cap.
- [ ] Add red tests for normal route, 50/20 NM remaining, short final, touchdown/taxi/Complete, cross-track/reversed arrival, overshoot and unavailable route.
- [ ] Correct the calculation and consumer coupling narrowly; retain route geometry as original plan/provenance.
- [ ] Add red ETA tests for expired publication before/after taxi allowance, minute rounding, phase transitions and actual IN; fix publication freshness without increasing authoritative update cadence.
- [ ] Strengthen replay checks using geometry/phase/ETA invariants rather than JST776 event text. Run nine baseline journals plus JST776, keeping sample pools separate.

## Task 4 — Active-flight restart and durable identity

**Files:** existing `FlightRecovery.cs`, `FlightHistoryDatabase.cs`, `FlightSessionCoordinator.Tracking.cs`/`.Commands.cs` only for demonstrated defects; isolated recovery-process tests under `tests/Alpha6Ops.RecoveryChecks` or a dedicated harness.

**Interfaces:** legacy recovery JSON remains readable; existing fields/milestones stay unchanged. If history identity is missing, introduce backward-compatible identity metadata validated against the assignment and existing row; never attach unrelated flights by number alone.

- [ ] Establish red real-process restart scenario: simulate gate→OUT→OFF, stop the process, reopen checkpoint/database, reconnect once, continue ON→IN→PIREP.
- [ ] Assert one logical flight row, one PIREP, unique milestones/event history, correct actuals and identity, readable immutable snapshots and no premature eligibility.
- [ ] Exercise Complete-before-submit restart, legacy checkpoint compatibility and persistence failure/retry boundaries.
- [ ] Implement only a reproduced recovery defect; preserve fsync/atomic replace/retry guards.
- [ ] Run the full existing recovery stress/guard suite. Write an exact native MSFS test handoff if still needed; never turn synthetic source passes into native passes.

## Task 5 — Integrated verification and report

**Files:** new verification orchestration under `work/checkpoint-7.24c/`, `docs/flight-tracking/7.24c-map-route-recovery-validation.md`, separate evidence directory. Production installers remain untouched until a new candidate is verified and archived separately if needed for physical handoff.

- [ ] Build complete production solution by normal process; run Core/Desktop/migration/map/browser/input/recovery/fleet suites and repository guards against the final candidate, with isolated profiles and expected-report allowlists unchanged.
- [ ] Run all nine baseline replays (91,366 samples) and JST776 (6,145) with strengthened checks.
- [ ] Inspect every failure/skip; preserve initial failures and corrected runs. Hash all candidate/source/evidence artifacts without overwriting frozen R3.
- [ ] Obtain one focused independent review of final changes and address substantive findings with red/green tests.
- [ ] Populate the user's 23-section report, production/test file inventory, counts and native/physical limitations; give GO only when every exit criterion has evidence. Stop at 7.24C.

## Execution ledger

- Baseline entry: repository source preserved before production changes; authorized spec archived; branch created without altering existing work.
- Shared interfaces: task 3 owns coordinator numeric getters; task 4 owns recovery/PIREP methods. Map instrumentation reads coordinator boundaries and does not alter these methods without coordination. Desktop build/test processes are serialized by the primary agent to avoid locked outputs; independent Core tests may use an isolated output directory.
- Plan uses the supplied spec as design authority. Instrumentation is diagnostic, not a chosen performance workaround; lifecycle policy selection requires measurements.

## Final execution state — 2026-09-28

- Task 1: instrumentation, sensitivity checks and controlled priority comparison complete. Selective WPF input delay measured; physical/native cause and corrective action remain open. No production priority/cadence workaround selected. Parked physical trace is the next required evidence.
- Task 2: retention/suspension/expiry and state restoration implemented. Final published 18-cycle stress 157/157; final in-flight-publication/trail fixes included in integrated map tests. Responsiveness remains a separate failing gate, including a long hidden-host heartbeat observation; quiet browser CPU is not universal host responsiveness.
- Task 3: route/ETA correction and consumer tests complete. Nine historical replays (91,366 samples) and JST776 (6,145 samples) pass strengthened checks. Old-Core hybrid negative replay detects both original calculation defects.
- Task 4: scoped identity/restart/completion/retry fixes complete; final 69/69 real-process synthetic checks and 21/21 persistence guard pass. Native active-flight restart pending. Arbitrary cross-store failure atomicity is not claimed.
- Task 5: complete production build/package, selected final regression and guards executed. **2,434 executed: 2,433 passed / 1 failed; 2 additional native checks skipped.** Remaining failure is unchanged map contention heartbeat threshold. Earlier failed/partial runs preserved, not replaced by passing counts. Independent review's five findings resolved. Full 23-section report and evidence ledger saved.
- Candidate frozen locally: `S4-7.24C-C1-20260928-64e864462ace`; 514 payload files verified against ZIP and embedded installer payload. Original R3 unchanged. No installation, native connection, source commit/push, premium UI or next checkpoint performed.
- **NO-GO for closure.** Continue from the exact candidate's parked physical trace; after a justified correction passes performance/physical gates, execute the documented native active-flight recovery procedure. Do not restart this checkpoint or discard evidence.
