# Checkpoint 7.18A — Pre-migration regression baseline

**Status: baseline established; seven migration contracts fail.** No application code, UI, database schema, simulator integration, version or production behavior changed. Checkpoint 7.18B has not begun.

Baseline: v0.22.0 / checkpoint 7.17, commit `6c60a8e2435058d7cae5e5e15b266a9c08a557b4`, branch `codex/checkpoint-7.18a`. Verification date: 23 September 2026. Environment: Windows, repository .NET SDK 10.0.400 and packaged runtime 10.0.11.

## Build and regression results

The complete five-project production solution built in Release with **zero warnings and zero errors**. The normal `packaging/build-desktop.ps1` process also produced the portable distribution, private runtime, installer and ZIP successfully. The packaged application passed the existing Desktop and replay checks. The installer was not installed over the pilot's application. The separate test-only migration executable compiled and ran.

| Check group | Executed | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| Core regressions | 122 | 122 | 0 | 0 |
| Desktop smoke | 18 | 18 | 0 | 0 |
| Dashboard, header, responsive and monitor checks | 447 | 447 | 0 | 0 |
| Refresh regression assertions | 1,010 | 1,010 | 0 | 0 |
| Live identity | 24 | 24 | 0 | 0 |
| Default diagnostic index | 19 | 19 | 0 | 0 |
| Monitor lifecycle | 22 | 22 | 0 | 0 |
| Tracking integration | 43 | 43 | 0 | 0 |
| Single-instance activation | 1 | 1 | 0 | 0 |
| Recorded-flight validations | 9 | 9 | 0 | 0 |
| New migration contracts and compatibility checks | 23 | 16 | 7 | 2 |
| Explicit large diagnostic-index suite | 35 | 35 | 0 | 0 |
| Existing Python fleet-import tests | 6 | 6 | 0 | 0 |
| **Total** | **1,779** | **1,772** | **7** | **2** |

Counts are **reported assertion executions**, not 1,779 independent test functions. Refresh reports include repeated assertions. The explicit large-index suite repeats the 20-file workload before exercising 1,000 files. Each recorded flight counts as one validation. Builds, preliminary development attempts and superseded reruns are excluded. Skipped checks are additional to the executed total.

No existing Core/Desktop/fleet/replay checks failed. No flaky existing checks were observed in the completed runs; this was not a repeated stress campaign establishing absence of flakiness. The 1,000-file index workload is disabled by default through `includeLargeWorkload=false`; it was explicitly enabled and passed here. No other disabled test cases were identified in those suites. The separate web prototype has no automated test script and is not the packaged Desktop UI.

## Replay and telemetry coverage

All nine original journals referenced by checkpoint 7.17 passed production replay. Independent record counting matched each replay report:

| Flight | Telemetry processed |
| --- | ---: |
| JST221 | 11,965 |
| QFA124 | 11,535 |
| JST786 | 6,952 |
| QLK1579 | 9,040 |
| JST721 | 7,650 |
| JST724 | 7,459 |
| JST760 | 10,552 |
| QLK1274 | 10,519 |
| ANG2 | 15,694 |
| **Total** | **91,366** |

Expected = raw telemetry records = replay-eligible records = processed samples = **91,366**. There were **zero omitted pre-assignment telemetry samples**. The original journals and tested package files retained their recorded SHA-256 hashes.

## New migration coverage

The external WPF harness references production assemblies and exercises their real methods using isolated diagnostic directories. It does not start a simulator or write to the pilot's data. Its 23 executed assertions cover:

- Nine populated view creations/unloads across Flight Tracking, Dispatch and Pilot Logbook without resetting the active flight.
- Twelve workspace switches preserving the connection owner, assignment, actuals, events, route tracker, estimator, history and recovery.
- Twenty settled-sample refresh calls without state changes, plus a separate first-render purity contract.
- Recorder, route-progress and recovery advancement in every workspace; hidden-window landing updates phase, milestone history and persisted recovery.
- Queued stale and current callbacks through the actual production received delegate, below the native SimConnect boundary.
- Immediate versus eventual assignment persistence, PIREP submission and closeout through their real handlers. Existing Desktop smoke also verifies durable, idempotent PIREP submission.
- Phase-label and event-title substitutions without changing the underlying flight facts.
- Four frozen assignment/recovery/gate/history compatibility fixtures, three copied-profile checks, and independent replay coverage reconciliation.

All four frozen formats were readable, including missing newer assignment fields, recovery telemetry containing named `NaN`, legacy gate defaults and historical SQLite events. A copy of the existing profile returned **497 flights, 960 events and 10 saved gates**. Original profile hashes were unchanged. No current assignment/recovery files existed in that profile; those formats were covered by frozen fixtures, not claimed as current-profile reads.

## Seven baseline issues

| Failing check | Observed behavior | Required isolation outcome |
| --- | --- | --- |
| `FIRST_RENDER_PURITY` | Rendering with an unconsumed estimator changes its state through `LiveEstimatedArrival`. Repeated rendering of an already consumed sample passes. | Telemetry processing owns estimator advancement; rendering reads a snapshot. |
| `RECONNECT_REJECT_OLD_CALLBACK` | The real received delegate queues a sample; after cancellation and replacement of the connection owner, the queued old sample is still accepted. | Reject callbacks from obsolete connection generations before any operational mutation. |
| `ASSIGNMENT_NO_ANIMATION_WAIT` | Assignment persistence occurs after approximately 1.62 seconds of presentation delays. | Accept and persist the assignment independently of animation completion. |
| `PIREP_SUBMIT_NO_ANIMATION_WAIT` | Durable PIREP submission occurs after approximately 1.62 seconds of presentation delays. | Persist submission independently of animation completion. |
| `PIREP_CLOSEOUT_NO_ANIMATION_WAIT` | Assignment/recovery cleanup occurs around 2.72 seconds, after a further 1.1-second UI delay. | Complete operational closeout independently of presentation delays. |
| `TEXT_COMPLETION_INDEPENDENCE` | Replacing `BLOCK-IN / COMPLETE` with `Arrived at gate` hides PIREP submission despite identical actual IN and 100% progress. | Use typed completion state for eligibility. |
| `TEXT_HISTORY_INDEPENDENCE` | Replacing `Block-out / taxi out` with `Gate departure` prevents history creation for the otherwise identical event. | Use event meaning, not displayed wording, to create history. |

Relevant existing boundaries: [MainWindow callback](../src/Alpha6Ops.Desktop/MainWindow.xaml.cs), `LiveEstimatedArrival` and `AddTrackingEvent` in that same file; [Dispatch acceptance](../src/Alpha6Ops.Desktop/DispatchWorkspace.xaml.cs); [Flight Tracking rendering and PIREP handler](../src/Alpha6Ops.Desktop/FlightTrackingWorkspace.xaml.cs).

These failures establish existing migration risks, not regressions introduced by this checkpoint. The first-render test deliberately resets only the test estimator to expose ownership; it is an architectural contract test, not evidence of a newly observed in-flight ETA fault. The eventual assignment and PIREP persistence checks pass. The failures remain failures and return exit code 1; no production repairs were attempted.

## Unavailable checks and limits

Two explicit skips remain: **native connection lifecycle** and **native rapid reconnect**. Synthetic navigation and the real queued delegate were exercised, but no actual SimConnect open/close, native handle disposal or simulator worker shutdown was tested. A live simulator session is required to close those gates.

WPF UserControls have no `IDisposable` contract. View tests verify creation, loading and unloading, not garbage-collection leak freedom or disposal of the entire MainWindow. Hidden-window tracking still runs inside the existing window owner; passing that test does not establish an independent flight-session service. Copied history reading is capped at 1,000 flights (all 497 current records fit). Replay does not establish aircraft sensor calibration or replace a new live flight.

Test-host startup and an inherited WPF `Render` reflection ambiguity were encountered while developing the harness and corrected only in test code. The final run has no harness errors. Its reflection seams intentionally fail loudly if future production method/capture shapes change; adapt the harness to the future session interface without weakening the behavioral expectations.

## Evidence and resumption

The interrupted run had already completed production build/package, Core, Desktop, fleet and all nine replays. On resumption, Git showed no tracked production changes; package and input hashes still matched. Those completed results were reused. Only the strengthened migration harness, its large-index workload and coverage audit were rerun.

- [Consolidated final summary](../work/checkpoint-7.18a/final-summary.json).
- [Completed production run](../work/checkpoint-7.18a/20260923-125255-d7471e26/summary.json), with adjacent build/package logs, individual Desktop reports, fleet log and replay reports. Its earlier six-failure migration result is superseded by the final harness below.
- [Final migration checks](../work/checkpoint-7.18a/migration-resumed/migration-checks.json), [coverage reconciliation](../work/checkpoint-7.18a/migration-resumed/replay-coverage.json), and [large-index report](../work/checkpoint-7.18a/migration-resumed/large-index/diagnostic-index-smoke.json).

Evidence paths are local and ignored by Git; they contain private flight/profile data. The reusable [Sierra 4 migration checklist](sierra4-migration-regression-checklist.md) documents the full rerun command, counts, failure handling and native validation gates.

## Files added and decision

Ten files were added: this report; the migration checklist; `docs/superpowers/plans/2026-09-23-checkpoint-7.18a.md`; `scripts/verify-migration-baseline.ps1`; the migration-check project and `Program.cs`; and four fixtures (`active-flight.json`, `active-flight-state.json`, `gate-history.json`, `history.sql`). No existing production files were edited. Private evidence remains local, and the pre-existing checkpoint 7.11 bundle was left untouched. Nothing was committed, pushed or installed.

**Recommendation: conditional GO for a separately authorized, narrowly scoped flight-session isolation checkpoint.** The seven failing contracts must become explicit completion gates while all currently passing checks remain green. **NO-GO for Sierra 4 presentation replacement or declaring migration readiness** until those dependencies are resolved and native lifecycle/reconnect validation is completed. The baseline is now established; stop here. Checkpoint 7.18B has not started.
