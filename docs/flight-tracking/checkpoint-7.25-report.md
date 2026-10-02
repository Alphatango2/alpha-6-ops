# Checkpoint 7.25 — Tracking Correctness & Diagnostic Hardening

Status: **PASS / GO**  
Completed: 2026-09-30  
Application version: `0.22.0`

## Baseline identity

Checkpoint 7.25 started from the frozen `S4-7.24C-FINAL-L1-20260930-318659425b88` baseline. Its source-content SHA-256 was `318659425B88D7529FA7A8C014366EDCB06C37DC894849BF915F2173A65EB478`; its installer SHA-256 was `E2106A3A3A9C30A738F2FF75BF00232CDAFADD7054023F3A53E2146986232824`; and its portable ZIP SHA-256 was `204E3CB16C723F5C2A7A073E82AB54A400A99922830780597A41F6C1F314CFFD`.

The Stage 1 comparison matched all 239 frozen source files. The inherited 7.24C acceptance result was 2,492 passed, zero failed and two native-only skips, with 11/11 replays and 100,632 historical telemetry samples. The final JST500 native flight contained 6,197 samples and remained separate from the replay total. The untouched production solution built with zero errors and zero warnings before 7.25 behavior changed.

Evidence: `work/checkpoint-7.25/20260930-132035-d723e7ff/entry/`.

## Workstream results

### 1. Observed-cruise provenance and TOD

**Before:** with filed FL350 and flown FL330, the 7.24C tracker could not establish cruise or TOD. A second RED showed that a pending mismatched-cruise observation did not survive application restart.

**After:** the flight-scoped tracker retains filed, observed and actual-flown provenance as separate domains. Matching filed cruise qualifies after 30 valid observed seconds. A mismatched level requires 180 valid observed seconds plus preceding climb evidence. Restart restores valid accumulated observation but grants zero credit to restart/disconnected time. Step climbs retain the earlier observed level and add a later level-change history entry. TOD can follow a valid observed level. Pause, slew, invalid values, time reversal and gaps greater than three seconds cannot manufacture stability. Go-around restores climb presentation without deleting prior cruise/TOD history.

The RED FL350/FL330 and restart-continuity defects were reproduced. GREEN passed twice, including filed FL350 remaining unchanged, observed FL330 represented separately, TOD from FL330, step-climb history, go-around, invalid telemetry and corrupt optional-state protection.

The exact JST771 post-restart replay passed 1/1 and processed 3,121 samples. It correctly produced no mismatched observed-cruise or TOD event: that segment begins already level at FL330 and contains neither the preceding climb evidence nor serialized 7.25 tracker state. The persisted-state recovery smoke test is the authoritative proof for qualification and restart continuity. Historical 7.24C data cannot retroactively provide tracking evidence it never serialized.

RED evidence:

- `work/checkpoint-7.25/20260930-132035-d723e7ff/task-2/red-smoke/desktop-smoke-error.txt`
- `work/checkpoint-7.25/20260930-132035-d723e7ff/task-2/red-recovery-smoke/desktop-smoke-error.txt`

GREEN and replay evidence:

- `work/checkpoint-7.25/20260930-132035-d723e7ff/task-2/stage-2-closeout.json`
- `work/checkpoint-7.25/20260930-132035-d723e7ff/task-2/green-smoke-2/cruise-provenance.json`
- `work/checkpoint-7.25/20260930-132035-d723e7ff/task-2/green-smoke-3/cruise-provenance.json`
- `work/checkpoint-7.25/20260930-132035-d723e7ff/task-2/jst771-replay/summary.json`

### 2. ETA recovery continuity

**Before:** the coordinator-level RED reproduced the documented restart discontinuity exactly: the pre-restart prediction was 22:50Z and the first recovered prediction nine simulator seconds later was 23:10Z. Lifecycle authority and event/history counts remained unchanged.

**After:** optional `ArrivalEstimatorState` preserves the last processed simulator sample, phase, publication state, anchors and finite smoothed speed. A coherent same-flight recovery retains the 22:50Z estimate and normal hysteresis. Invalid, corrupt, phase-incoherent or time-incoherent predictive state is discarded and deterministically reconstructed without rejecting authoritative flight recovery. Legacy recovery files remain readable. ETA state cannot authorize OOOI, phase, completion, PIREP, identity, route progress or history.

Core passed twice at 194/194. Active-flight recovery passed twice at 74/74. The corrected reproduction is 22:50Z to 22:50Z; invalid-state fallback reconstructs 23:10Z safely and does not duplicate lifecycle events.

Evidence:

- `work/checkpoint-7.25/20260930-132035-d723e7ff/task-3/stage-3-closeout.json`
- `work/checkpoint-7.25/20260930-132035-d723e7ff/task-3/red-active-restart-final-4/summary.json`
- `work/checkpoint-7.25/20260930-132035-d723e7ff/task-3/green-active-restart-2/summary.json`
- `work/checkpoint-7.25/20260930-132035-d723e7ff/task-3/green-active-restart-3/summary.json`

### 3. Program health-marker replacement

**Before:** deterministic RED reproduced the one-attempt Windows replacement failure. A temporary destination lock left the old `running=true` marker, and a permanent replacement failure had no required diagnostic record.

**After:** a complete same-directory staged file is flushed before atomic replacement. Only demonstrated transient Windows sharing/lock/access-denied replacement failures use the fixed 10/20/40/80 ms schedule: five attempts and at most 150 ms of planned sleep. Read-only or directory targets fail immediately. Exhaustion preserves the last complete published file and produces visible stage, attempt, path and HResult diagnostics.

Five fresh GREEN smoke runs plus the final timing run passed. The transient case published stopped state in 58 ms. The continuously held target failed visibly after five attempts and 199 ms elapsed while preserving the old complete file. Read-only failed after one attempt. The final concurrent-reader stress made 544,394 reads across 500 replacements with zero malformed JSON and introduced no unexpected `previous_unclean_exit` report.

Evidence:

- `work/checkpoint-7.25/20260930-132035-d723e7ff/task-4/behavioral-red.json`
- `work/checkpoint-7.25/20260930-132035-d723e7ff/task-4/stage-4-closeout.json`

### 4. Engine completion presentation

**Before:** enhanced terminal completion could say engine data was unavailable and shutdown was still in progress even though terminal shutdown had been confirmed. Manual completion did not identify pilot-confirmed Block In.

**After:** Complete-only wording describes enhanced terminal shutdown authority, continuing mechanical spool-down/partial mechanical evidence when applicable, or pilot-confirmed Block In for limited support. Generic non-completion mechanical wording is unchanged.

The same telemetry, mechanical engine states, terminal eligibility and Complete milestone were used before and after. Desktop passed 18/18 top-level and 552/552 dashboard checks; terminal/Fenix capability passed 203/203. Engine thresholds, Fenix/T2 adapters, completion policy and PIREP ownership did not change.

Evidence:

- `work/checkpoint-7.25/20260930-132035-d723e7ff/task-5/red/completion-wording-before.json`
- `work/checkpoint-7.25/20260930-132035-d723e7ff/task-5/green/completion-wording-after.json`
- `work/checkpoint-7.25/20260930-132035-d723e7ff/task-5/authority-equality-and-wording.json`
- `work/checkpoint-7.25/20260930-132035-d723e7ff/task-5/stage-5-closeout.json`

## Exact implementation inventory

Production files:

- created `src/Alpha6Ops.Desktop/ObservedCruiseTracker.cs`
- modified `src/Alpha6Ops.Desktop/FlightTrackingEvents.cs`
- modified `src/Alpha6Ops.Core/ArrivalEstimator.cs`
- modified `src/Alpha6Ops.Desktop/FlightRecovery.cs`
- modified `src/Alpha6Ops.Desktop/FlightSessionCoordinator.Tracking.cs`
- created `src/Alpha6Ops.Desktop/ProgramStatusStore.cs`
- modified `src/Alpha6Ops.Desktop/ProgramMonitor.cs`

Test and verification files:

- modified `src/Alpha6Ops.Desktop/DashboardSmokeTest.cs`
- modified `src/Alpha6Ops.Desktop/ProgramMonitorSmokeTest.cs`
- modified `tests/Alpha6Ops.Tests/TrackingTests.cs`
- modified `tests/Alpha6Ops.RecoveryChecks/ActiveFlightRecoveryChecks.cs`
- created `scripts/verify-checkpoint-7.25.ps1`

Conditional candidate metadata, changed only after the combined gate passed:

- modified `packaging/PreviewSetup.cs`
- modified `packaging/Desktop-Quick-Start.txt`

Specification and record:

- created `docs/flight-tracking/checkpoint-7.25-spec.md`
- created `docs/superpowers/plans/2026-09-30-checkpoint-7.25.md`
- created `docs/flight-tracking/checkpoint-7.25-report.md`

The pre-package source audit compared 242 files with the Stage 1 manifest. It found exactly 12 approved production/test/script changes and zero unexpected changes. The two packaging metadata changes were then applied within the conditional scope. No database-authority, map, SimConnect, SimBrief revision, terminal adapter, engine threshold, PhaseDetector authority, installer-mechanics or replay-input file changed.

Audit evidence: `work/checkpoint-7.25/20260930-132035-d723e7ff/task-6/scope-audit-prepackaging.json`.

## Combined automated acceptance

Definitive run: `work/checkpoint-7.25/acceptance-20260930-143546-4f7e3293/summary.json`.

- Release production solution build: PASS
- Executed: **2,849**
- Passed: **2,849**
- Failed: **0**
- Skipped: **2** explicit native-only migration checks retained
- Replays: **11/11**
- Historical telemetry samples: **100,632**
- Baseline nine: **91,366** samples
- JST776: **6,145** samples
- JST771: **3,121** samples
- Final JST500 native evidence: **6,197** samples, explicitly separate and excluded from the replay total
- Source changed during the acceptance run: **NO**
- Replay inputs changed during the acceptance run: **NO**

Protected results include Core 194, desktop 18, dashboard 552, refresh 1,010, identity 24, diagnostic index 19, monitor lifecycle 26, tracking integration 47, activation 1, migration 31, large-index 35, Python 19, terminal/Fenix 203, active recovery 74, recovery persistence 21, Logbook 40, Map V2 browser/input/lifecycle 199, direct contracts 49, map contracts 22, interaction 13, restoration 157, scheduling 15, JST776 replay 1, JST771 replay 1 and JST771 evidence 69.

The frozen portable payload was also run from a new isolated profile: desktop 18/18 and dashboard 552/552 passed. Evidence: `work/checkpoint-7.25/20260930-132035-d723e7ff/task-6/packaged-smoke-summary.json`. An earlier package-smoke attempt reused a directory contaminated by an accidental normal-app launch and correctly failed the Logbook empty-state assertion; it is preserved as invalid setup evidence, not used as an acceptance result.

## Native-validation decision

**Additional native flight required: NO.**

Every changed behavior is deterministic tracking, predictive recovery, diagnostic persistence or presentation behavior and is covered by synthetic telemetry, restart recovery, filesystem contention, authoritative terminal tests and preserved recorded flights. Checkpoint 7.25 did not change SimConnect acquisition, OOOI authority, terminal shutdown adapters/policy, engine thresholds, Map V2, route acquisition, database authority or installer mechanics. The successful 7.24C T2 parked and JST500 destination/persistence evidence remains the native baseline.

## Frozen local candidate

Candidate ID: `S4-7.25-TRACKING-20260930-e7ee5c18d8ae`  
State: **local, frozen, uninstalled**

- source-content manifest SHA-256: `E7EE5C18D8AE4BA60F18307EFCAC9797D14C4355BAE0540006A3CF14DBAEB2C0`
- source snapshot SHA-256: `06BD7B457802A887398FCEF3287EAAABED109D1660BD303EDE1FBB1C4E656758`
- installer SHA-256: `1EA6A4ED4B8C397850AF60815B0C7A3616CA09311E49C6DCAF8697106337EF65`
- portable ZIP SHA-256: `24C6639621E7A5CFD4CD5BFF43D456D6D741E24F1A21078048BCE55CB6634F72`
- embedded installer payload SHA-256: `24C6639621E7A5CFD4CD5BFF43D456D6D741E24F1A21078048BCE55CB6634F72`
- payload files verified against ZIP: **514/514**
- default map: Classic; Map V2 remains session opt-in

Candidate record: `outputs/Alpha6OPS-Desktop-S4-7.25-TRACKING-20260930-e7ee5c18d8ae/validation-build.json`.

## Remaining limitations and boundaries

- The isolated JST771 replay cannot prove pre-restart observed-cruise qualification because 7.24C never serialized that tracking context. It does prove that insufficient post-restart evidence does not falsely qualify cruise.
- Discarding invalid optional ETA state can temporarily reconstruct a baseline prediction until valid fresh route evidence returns. It cannot affect actual IN or any lifecycle authority.
- The external process responsible for the historical intermittent health-marker handle denial was not identified. The demonstrated transient condition is bounded and exhausted failure is visible.
- SimBrief revision comparison/acceptance remains future work. Filed cruise is preserved and is not treated as an ATC clearance change.
- The application and storage identity remain version `0.22.0`; the frozen build ID and hashes identify this candidate.
- Repository/package cleanup, premium UI work and Checkpoint 7.26 are outside this checkpoint.

## Recommendation

**GO — Checkpoint 7.25 is complete.** The four confirmed defects have RED/GREEN evidence, all targeted and protected gates pass, the exact replay set passes without weakened safeguards, scope remained bounded, and the separately frozen local candidate is verified and uninstalled. Stop after 7.25 pending user direction.
