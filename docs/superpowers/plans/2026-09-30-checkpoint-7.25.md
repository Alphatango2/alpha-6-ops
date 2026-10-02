# Checkpoint 7.25 Tracking Correctness and Diagnostic Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correct observed cruise/TOD tracking, ETA restart continuity, bounded program-health persistence and terminal-completion wording while preserving every authoritative 7.24C lifecycle and terminal behavior.

**Architecture:** Add one telemetry-derived observed-cruise state machine beside immutable dispatch data, checkpoint the minimum predictive ETA continuation state, isolate diagnostic status persistence behind a bounded atomic store, and make completion wording consume existing terminal-decision context without changing authority. Each workstream has its own behavioral RED, minimal GREEN and targeted gate before the next production workstream begins.

**Tech Stack:** .NET 10, C#, WPF, System.Text.Json, Windows file replacement semantics, existing console/WPF smoke harnesses, PowerShell verification and Python historical-evidence assertions.

**Spec:** `docs/flight-tracking/checkpoint-7.25-spec.md`

## Global Constraints

- Immutable starting baseline: `S4-7.24C-FINAL-L1-20260930-318659425b88`, version `0.22.0`, source-content SHA-256 `318659425B88D7529FA7A8C014366EDCB06C37DC894849BF915F2173A65EB478`.
- Preserve authoritative exactly-once OOOI, identity, recovery, exactly-one PIREP, same-flight-number isolation, terminal T2, Fenix A320-family qualification, maps, responsiveness, Logbook OOOI and route endpoint behavior.
- Filed cruise, current observed cruise and actual flown telemetry/history remain separate provenance domains.
- Restart downtime contributes zero stable-cruise observation time.
- ETA state remains derived/predictive and cannot authorize lifecycle, completion or PIREP.
- Invalid optional ETA or observed-cruise recovery state is discarded without rejecting authoritative flight recovery.
- Health-marker retries are limited to 10/20/40/80 ms for demonstrated transient Windows replacement conditions; permanent failures stay visible.
- Engine wording is presentation-only. Do not change mechanical thresholds, adapters, T2 policy or completion authority.
- Do not add dependencies, implement SimBrief revisions, redesign UI/maps, clean storage/repository state, begin 7.26 or begin premium UI.
- The working tree intentionally contains the verified 7.24 baseline. Do not reset, stage or commit inherited work. Preserve per-task manifests, focused diffs and test output under a new `work/checkpoint-7.25/<run-id>` evidence root.
- If an additional production file becomes necessary, stop and report the reason before changing it.

## Review Focus

- Telemetry gaps, pause/slew and application downtime must contribute zero stable-cruise duration; Task 2 exercises all four.
- Corrupt optional tracking/ETA state must preserve the authoritative flight and fall back safely; Tasks 2 and 3 exercise this.
- A route estimate that is unavailable at restart must not revive a stale published ETA; Task 3 exercises withdrawal and recovery.
- A permanent status-file lock or read-only target must fail within the bounded budget and remain diagnostically visible; Task 4 exercises both.
- Enhanced, manual and unsupported terminal cases must render truthful completion wording with byte-for-byte identical authority inputs; Task 5 exercises all three.

---

### Task 1: Freeze the 7.25 entry ledger and baseline behavior

**Files:**
- Create: `work/checkpoint-7.25/<run-id>/entry/*` evidence only
- Read: `docs/flight-tracking/7.24c-logbook-correction-closeout.md`
- Read: `docs/flight-tracking/7.24c-final-t2-destination-validation.md`
- Read: `docs/flight-tracking/7.24c-jst771-simbrief-revision.md`

**Interfaces:**
- Consumes: frozen 7.24C manifests/packages and the current inherited working tree.
- Produces: immutable entry status, diff, file inventory, source hashes, package hashes and copied controlling specification for every later task.

- [x] **Step 1: Create a fresh evidence root and copy the controlling specification**

Use a timestamp/GUID directory beneath `work/checkpoint-7.25`. Copy `docs/flight-tracking/checkpoint-7.25-spec.md` into `entry/` and record its SHA-256.

- [x] **Step 2: Record repository and source identity without modifying the tree**

Record `git status --short`, `git diff --binary`, `git rev-parse HEAD`, the 7.24C source/support manifest comparison, and current hashes of every expected production/test file. Expected: 7.24C manifest remains 239/239 with zero mismatch and frozen installer/ZIP hashes remain unchanged.

- [x] **Step 3: Confirm the inherited acceptance reports**

Record the closing 7.24C results: 2,492 automated passes, zero failures, two explicit native skips; 11/11 replays and 100,632 recorded samples; final JST500 6,197 native samples kept separate.

- [x] **Step 4: Build the unmodified production solution**

Run: `work/dotnet/dotnet.exe build Alpha6Ops.slnx -c Release --configfile NuGet.Config -nodeReuse:false`

Expected: exit 0 with no compiler errors. Store the full log and output hashes. Do not package a 7.25 candidate.

- [x] **Step 5: Record the four old-behavior reproductions before test edits**

Preserve the JST771 FL350-filed/FL330-flown missing cruise/TOD evidence; documented 22:50 to 23:10 ETA jump; denied `program-status.json` replacement stack/HResult; and final JST500 Complete wording with Unknown/Stopping mechanics plus confirmed enhanced shutdown.

### Task 2: Observed cruise provenance and TOD state machine

**Files:**
- Create: `src/Alpha6Ops.Desktop/ObservedCruiseTracker.cs`
- Modify: `src/Alpha6Ops.Desktop/FlightTrackingEvents.cs:11-122,173-190`
- Modify: `src/Alpha6Ops.Desktop/DashboardSmokeTest.cs:78-145`

**Interfaces:**
- Consumes: `Telemetry`, `FlightPhase`, optional filed cruise and go-around milestone context.
- Produces: `ObservedCruiseTrackerState CaptureState()`, `ObservedCruiseObservation Observe(Telemetry sample, double? filedCruiseFeet, bool goAround)`, current observed level/stage and zero-or-one transition per sample. `FlightTrackingMonitorState` gains optional `ObservedCruiseTrackerState? ObservedCruise=null` as its final backward-compatible member.

- [x] **Step 1: Add behavioral RED tests against the existing monitor API**

In `DashboardSmokeTest`, add named checks that feed one-second telemetry without referencing the proposed tracker type:

- filed FL350 / flown stable FL330 for 240 seconds must create one observed-cruise event, preserve plan FL350 and later create one TOD after 30 seconds below the established level;
- restart after 120 valid FL330 seconds, restore `FlightTrackingMonitorState`, skip a 90-second downtime, then supply 60 valid seconds; downtime must not count and exactly one cruise event must appear after 180 observed seconds;
- filed FL330 / flown FL330 qualifies after 30 seconds;
- filed FL330 / flown FL350 qualifies after 180 seconds;
- 179-second mismatched climb level-off followed by climb creates no cruise event;
- turbulence, pause, slew and greater-than-three-second gaps contribute no qualification time;
- step climb emits start and later level-change events while preserving the original established level event;
- temporary descent level-off does not return presentation phase to Cruise;
- go-around resets current phase to Climb without deleting historical TOD.

- [x] **Step 2: Run the Desktop smoke target and verify RED**

Build the current Desktop project, run its Release executable with `--smoke-test <red-output>`, and preserve `dashboard-smoke.json`/error output.

Expected: the FL350/FL330 and recovery assertions fail because existing cruise qualification requires filed-level proximity; no other prior assertion fails. The filed plan remains FL350 in the test fixture.

- [x] **Step 3: Implement the tracker types and exact thresholds**

Create `ObservedCruiseTracker.cs` with:

- `internal enum ObservedAirborneStage { Climb, Cruise, Descent }`;
- `internal enum ObservedCruiseTransitionKind { None, CruiseEstablished, StepClimbStarted, CruiseLevelChanged, TopOfDescent }`;
- immutable `ObservedCruiseTrackerState`, `ObservedCruiseTransition` and `ObservedCruiseObservation` records;
- `internal sealed class ObservedCruiseTracker` with constructor accepting optional state, `CaptureState()`, `Observe(...)`, and `ResetForGoAround()`.

Implement the spec's 18,000-ft floor, 300-ft/min stable limit, 400-ft band, 30/180/60/30-second durations, 750-ft step displacement, +500/-500-ft/min climb/descent limits, 500-ft TOD displacement and three-second maximum counted interval. Accumulate only observed valid deltas.

- [x] **Step 4: Replace only cruise/TOD presentation logic in the event monitor**

Delegate current cruise, level-change, step-climb, TOD and airborne presentation-phase decisions to `ObservedCruiseTracker`. Keep filed cruise unchanged and include filed-versus-observed provenance in mismatched event text. Map transitions to the existing event list without changing lifecycle milestones.

- [x] **Step 5: Capture and restore optional observed-cruise state**

Append the optional state to `FlightTrackingMonitorState`; construct the tracker from it and include it in `CaptureState()`. Invalid state resets the tracker only. Preserve all older serialized monitor records.

- [x] **Step 6: Run targeted GREEN verification**

Run the same Desktop smoke target twice in fresh directories.

Expected: every new cruise/TOD assertion passes on both runs; existing dashboard, tracking integration and recovery-schema checks remain green. Preserve event transcripts showing filed FL350, observed FL330, subsequent TOD and the step-climb history.

- [x] **Step 7: Replay JST771 and inspect provenance**

Run `Alpha6OPS.exe --replay-journals work/checkpoint-7.24c/jst771-review-20260929/jst771-replay-manifest.json <output>` and inspect the existing replay result without changing the replay engine.

**Approved amendment — 2026-09-30:** Expected: 1/1 and 3,121 samples; lifecycle, telemetry, route/tracking, existing-event/state and manual-completion compatibility remain intact; no observed FL330 cruise or TOD is expected from this isolated segment. The supplied journal begins post-restart already level at FL330 and contains neither the preceding climb evidence required for mismatched-cruise qualification nor serialized 7.25 observed-cruise state. Its lack of observed-cruise/TOD is therefore an affirmative false-positive protection result, not a waived failure.

The persisted-state recovery smoke scenario is the authoritative 7.25 proof that valid prior climb and accumulated pre-restart observation survive restart, restart downtime receives zero stability credit, fresh telemetry completes qualification, filed FL350 remains immutable, observed FL330 remains distinct and that observed level can subsequently support TOD.

Provenance boundary: the historical JST771 post-restart replay cannot retroactively establish tracking evidence that was never serialized by the 7.24C implementation. Do not weaken altitude/prior-climb safeguards, qualify mismatched cruise merely from level-at-connect, count disconnected time, modify the replay engine, add the earlier 2,800-sample journal to the approved manifest or expand Stage 2 production scope to manufacture that context.

- [x] **Step 8: Save a focused task diff and hash manifest**

Do not stage inherited files. Save the Task 2 test-first patch, production patch, RED/GREEN logs and resulting hashes under the evidence root.

### Task 3: ETA estimator recovery continuity

**Files:**
- Modify: `src/Alpha6Ops.Core/ArrivalEstimator.cs:1-70`
- Modify: `src/Alpha6Ops.Desktop/FlightRecovery.cs:12-25`
- Modify: `src/Alpha6Ops.Desktop/FlightSessionCoordinator.Tracking.cs:14-78,155-215`
- Modify: `tests/Alpha6Ops.Tests/TrackingTests.cs:20-130`
- Modify: `tests/Alpha6Ops.RecoveryChecks/ActiveFlightRecoveryChecks.cs`

**Interfaces:**
- Consumes: recovered phase, last simulator time/telemetry, phase events, active assignment identity and route estimate.
- Produces: public Core record `ArrivalEstimatorState`; `ArrivalEstimatorState CaptureState()`; `bool TryRestore(ArrivalEstimatorState? state)` with intrinsic validation; optional final `ArrivalEstimatorState? ArrivalEstimatorState=null` member on `FlightRecoveryState`.

- [x] **Step 1: Add a coordinator-level JST771 ETA RED**

In `ActiveFlightRecoveryChecks`, create a real coordinator/recovery scenario whose pre-restart published ETA is 22:50Z, whose accepted block-time baseline is 23:10Z, and whose first recovered telemetry occurs about nine simulator seconds later with unchanged flight identity/phase/position continuity.

Assert that ETA remains 22:50Z under normal two-minute anti-churn and that no lifecycle/event/history row is duplicated. Do not use reflection to assign the result under test.

- [x] **Step 2: Run the active-restart suite and verify RED**

Run: `work/dotnet/dotnet.exe run --project tests/Alpha6Ops.RecoveryChecks -c Release -- --active-restart <red-output>`

Expected: the new continuity assertion reports actual 23:10Z versus expected 22:50Z; established recovery/identity assertions pass.

- [x] **Step 3: Add Core RED tests for the wished-for recovery contract**

Add tests for capture/restore in climb, cruise/descent-equivalent Airborne, Taxi In, UTC midnight, unavailable publication, invalid future/past anchors, nonfinite/out-of-range speed and phase-incoherent state. These may initially fail to compile only after the behavioral RED in Step 2 is preserved.

- [x] **Step 4: Implement `ArrivalEstimatorState`, capture and validated restore**

The record contains last sample, last phase, explicit last publication, airborne anchor, taxi anchor and smoothed speed. `TryRestore` accepts null as no state, rejects intrinsic incoherence without mutation and never receives or returns lifecycle authority.

- [x] **Step 5: Persist and restore the optional predictive checkpoint**

Append the optional member to `FlightRecoveryState`. Save `arrivalEstimator.CaptureState()`. During normal same-assignment restore, verify coherence with `LastSimulatorUtc`, `LastTelemetry`, recovered phase and phase-event chronology before calling `TryRestore`; otherwise reset and reconstruct from the authoritative milestones/current sample path. Legacy files remain valid.

- [x] **Step 6: Preserve availability and discontinuity semantics**

Ensure a persisted unavailable estimate stays unavailable until a valid current route estimate returns. Discard predictive state on time reversal, wrong phase, invalid speed or identity/session discontinuity. Actual IN continues to override all predictions.

- [x] **Step 7: Run targeted GREEN verification**

Run Core tests, then active-restart/recovery twice in fresh directories.

Expected: the synthetic JST771 result remains 22:50Z across restart; climb/cruise/near-TOD/descent/Taxi In/midnight cases pass; corrupt and legacy states recover safely; no duplicate events; all existing persistence tests pass.

- [x] **Step 8: Save focused before/after evidence**

Store the old 22:50→23:10 transcript, corrected 22:50→22:50 transcript, serialized optional state, invalid-state fallback and focused hashes/diff.

### Task 4: Bounded program-status replacement

**Files:**
- Create: `src/Alpha6Ops.Desktop/ProgramStatusStore.cs`
- Modify: `src/Alpha6Ops.Desktop/ProgramMonitor.cs:20-160`
- Modify: `src/Alpha6Ops.Desktop/ProgramMonitorSmokeTest.cs:10-105`

**Interfaces:**
- Consumes: status path and serializable heartbeat/stopped value.
- Produces: `internal sealed class ProgramStatusStore`; `JsonDocument? Read()`; `void Write(object value)`; failures decorated with `StatusStage`, `StatusAttempts`, `StatusPath` and original HResult.

- [x] **Step 1: Add deterministic replacement RED tests**

Extend `ProgramMonitorSmokeTest` with real filesystem tests:

- publish running status, hold `program-status.json` with `FileShare.Read`, release after 30 ms while `Stop` runs, then require final `running=false`;
- hold the target for the entire bounded interval, require shutdown returns within two seconds, old complete file remains and one `program_status_stop` report records replacement stage/attempt/HResult;
- make the target read-only and require one attempt, not five;
- repeatedly read with read/delete sharing during 500 replacements and require every parsed document is complete JSON.

- [x] **Step 2: Run Desktop smoke and verify RED**

Expected: the temporary-lock stopped-status assertion fails because the old implementation tries replacement once and silently leaves the running record. The permanent-failure observability assertion also fails. Preserve both results separately from the historical intermittent evidence.

- [x] **Step 3: Implement the bounded atomic store**

Stage and `Flush(true)` a complete file, then replace in the same directory. Retry only HResult low words 32, 33 and transient 5 using 10/20/40/80 ms. Detect read-only and directory targets as permanent. Preserve old published content on failed replacement and perform bounded cleanup without masking the primary exception. Readers use `FileShare.Read | FileShare.Delete`.

- [x] **Step 4: Route ProgramMonitor reads/writes through the store**

Keep the existing heartbeat serialization and dispatcher behavior. Heartbeat exceptions continue to publish `PROGRAM MONITOR ERROR`. Final Stop catches only to continue shutdown, but first writes `program_status_stop` with retry metadata; do not create flight authority or change recovery handling.

- [x] **Step 5: Run stress GREEN verification**

Run the Desktop smoke target five times in fresh directories plus the deterministic 500-replacement reader test.

Expected: no stale running marker after the released transient lock, no partial JSON, bounded permanent failures with one diagnostic each, no unexpected `previous_unclean_exit`, and all existing diagnostic-index/monitor checks pass.

- [x] **Step 6: Save focused evidence**

Preserve the one-attempt RED, bounded GREEN attempt/timing data, permanent-failure report, stress summary and focused hashes/diff. Do not claim the historical external handle owner was identified.

### Task 5: Truthful completion wording without authority changes

**Files:**
- Modify: `src/Alpha6Ops.Desktop/FlightSessionCoordinator.Tracking.cs:155-172`
- Modify: `src/Alpha6Ops.Desktop/FlightTrackingEvents.cs:170-285`
- Modify: `src/Alpha6Ops.Desktop/DashboardSmokeTest.cs:95-115`

**Interfaces:**
- Consumes: the already evaluated `Telemetry` returned by `ApplyTerminalPolicy`, Complete milestone, unchanged `EngineStates`, `TerminalShutdown`, `TerminalBlockInEligible` and `PilotConfirmedBlockIn`.
- Produces: presentation-only completion summary/detail. No new authority type.

- [x] **Step 1: Add presentation RED tests with frozen authority inputs**

Use the event monitor directly with:

- enhanced eligible Complete, Engine 1 Unknown and Engine 2 Stopping;
- limited/manual eligible Complete with `PilotConfirmedBlockIn=true`;
- unsupported/noncomplete generic mechanical samples.

Assert enhanced text says terminal shutdown confirmed and identifies continuing spool-down/partial mechanics where applicable; manual text says pilot-confirmed Block In; generic text remains conservative. Assert the input telemetry, mechanical conditions and Complete milestone are unchanged.

- [x] **Step 2: Run Desktop smoke and verify RED**

Expected: old enhanced completion says engine data unavailable/shutdown in progress and old manual completion does not identify pilot confirmation. Existing terminal policy tests remain green.

- [x] **Step 3: Pass the already evaluated terminal sample to presentation**

Call `FlightTrackingEventMonitor.Observe` with `terminalSample` rather than the pre-policy copy after `PhaseDetector` has made the unchanged milestone decision. Do not feed presentation output back to policy or lifecycle.

- [x] **Step 4: Add completion-specific status formatting**

For Complete only, format enhanced verified shutdown, manual confirmation, and optional continuing mechanical spool-down/partial-data qualifiers. Leave individual engine transition titles and generic noncompletion `EngineStatus` behavior unchanged.

- [x] **Step 5: Run targeted GREEN verification**

Run Desktop smoke, terminal suite and Fenix-family capability suite.

Expected: wording assertions pass; terminal decisions, effective IN, three-second completion stability, mechanical states and all exactly-once assertions match pre-change results.

- [x] **Step 6: Preserve before/after examples**

Store old and new completion records alongside a machine-readable equality comparison of the underlying terminal/mechanical inputs and lifecycle result.

### Task 6: Combined acceptance, report and conditional candidate

**Files:**
- Create: `scripts/verify-checkpoint-7.25.ps1`
- Create: `docs/flight-tracking/checkpoint-7.25-report.md`
- Modify only after every automated gate passes: `packaging/PreviewSetup.cs`
- Modify only after every automated gate passes: `packaging/Desktop-Quick-Start.txt`

**Interfaces:**
- Consumes: all Task 1–5 evidence, existing verification scripts/manifests and unchanged frozen 7.24C references.
- Produces: one fail-closed acceptance summary, requested report and, only when green, one local uninstalled 7.25 candidate with manifests/hashes.

- [x] **Step 1: Write the fail-closed verification wrapper**

The script creates a fresh evidence directory, refuses a live Alpha6OPS process, records input hashes, invokes existing suites without weakening thresholds/retries, and writes counts using the established assertion-count convention. It must keep synthetic assertions, historical replay samples and native evidence separate.

- [x] **Step 2: Run the targeted suites one final time**

Run cruise/TOD, Core ETA, active restart/recovery, ProgramMonitor stress and engine presentation/terminal suites. Expected: all pass in fresh directories with no unexpected crash report.

- [x] **Step 3: Run protected regression groups**

Run Release solution build; Core; complete Desktop smoke; active-flight recovery; terminal/Fenix; Logbook; Map V2 scheduling; restoration/resources/browser/input/lifecycle; direct responsiveness acceptance; and the full migration/regression set. Preserve the two explicit native skips rather than relabeling them.

- [x] **Step 4: Run recorded-flight gates**

Run the nine baseline journals (91,366 samples), JST776 (6,145), and JST771 (3,121): expected 11/11 and 100,632 samples. Run JST776, JST771 and final JST500 evidence assertions. Verify every replay input hash remains unchanged.

- [x] **Step 5: Review scope and invariants**

Compare entry/final diffs against the expected production inventory. Confirm no change to engine thresholds, terminal adapters/policy, PhaseDetector authority, maps, SimBrief revision behavior, database authority or installer mechanics. If unexpected production scope exists, stop with NO-GO.

- [x] **Step 6: Write the formal completion report**

Populate the exact user-specified 7.25 return format with RED/GREEN evidence, before/after behavior, counts, replay/sample totals, files, limitations, native-validation decision and GO/NO-GO.

- [x] **Step 7: Decide whether native validation is necessary**

Default to `NO` because all changes are deterministic tracking/prediction/diagnostic/presentation behavior over preserved telemetry. Request only the smallest native test if an automated changed behavior remains unprovable, and explain the exact missing claim.

- [x] **Step 8: Package only after the gate is green**

Update only release labels/guidance, run `packaging/build-desktop.ps1`, freeze a separately named local 7.25 candidate, verify every payload/hash/embedded-installer manifest, and do not install it. If any gate fails, do not change package metadata and do not create a candidate.

- [x] **Step 9: Stop after 7.25**

Do not install, publish, begin 7.26, clean the repository or begin premium UI work.

