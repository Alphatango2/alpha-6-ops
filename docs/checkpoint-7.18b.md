# Checkpoint 7.18B — Flight-session isolation

Verification date: 23 September 2026. Local branch: `codex/checkpoint-7.18b`. Production version remains **0.22.0**. Comparison baseline: completed 7.18A at `971ed63` (production baseline `6c60a8e`). Runtime implementation: `9ef87e5`; reviewed attachment corrections culminate in `00ff637`. Final tested source: `00ff637`.

**Status: complete — all seven required migration failures resolved; 1,785/1,785 executed checks passed.** Two native simulator checks remain unavailable. No Sierra 4 visual implementation, map replacement, schema migration, new web/API stack or airline-world engine is included. Nothing has been pushed or installed over the pilot's application.

## 1–3. Architecture, boundary and moved responsibilities

Previously, `MainWindow` owned the live recorder, identity reconciliation, estimator, route tracker, event monitor, connection/retry loop, recovery, journal and history integration. Rendering could advance the estimator. Some operational actions completed only after UI animation delays, and two decisions inspected human-readable text.

Now `App.GetFlightSession` creates **one production application-lifetime `FlightSessionCoordinator`**. App shutdown disposes it. A window subscribes to its snapshots and issues commands; closing the presentation unsubscribes and disposes its own timers/tray resources without cancelling the flight session. Diagnostic hosts explicitly own separate isolated instances.

```text
Existing WPF controls / presentation
       commands ↓       ↑ detached typed snapshots
       FlightSessionCoordinator (owned by App)
       Existing Core rules and storage/integration adapters
       SimConnect / SQLite / JSON / JSONL
```

Moved out of MainWindow:

- The ordered live telemetry pipeline, recorder, phase observation and telemetry continuity orchestration.
- Assignment/aircraft association, observed-session identity and recovery restoration.
- Route-progress tracking, live rotation actuals and arrival-estimator advancement.
- Operational events, actual OUT/IN, block-in confirmation and PIREP eligibility.
- Live history creation/events/closeout, recovery writes and diagnostic journals.
- Simulator connection ownership, cancellation, launch/retry orchestration and callback generations.
- Program-monitor ownership and session pilot identity.

The service has no Window or Control dependency. An injected `IFlightRuntimeDispatcher` preserves the existing application-dispatcher ordering. It is **lifetime isolation**, not a move to a background processing thread. Existing SimBrief/weather UI flows, read-only logbook/diagnostic access and the separate fixture-preview UI remain in place. No algorithm extraction into a new Core framework was needed.

## 4. Explicit commands

| Command | Responsibility |
| --- | --- |
| `AcceptAssignment` / `ClearAssignment` | Persist/remove the active plan and reset its runtime/recovery through the existing rules; accepted gate history is recorded before animation. |
| `ConnectSimulatorAsync` / `DisconnectSimulator` | Own the connection task, cancellation and generation boundary. |
| `ApplyGates` | Save dispatch gates, remember gate history, record the change and refresh recovery. |
| `CorrectSchedule` | Save explicit UTC corrections, preserve actuals and recalculate the authoritative estimate. |
| `ConfirmBlockIn` | Record the existing pilot confirmation used by the next ordered telemetry update. |
| `SubmitPirep` | Validate the existing completion requirements and durably submit through the existing database method. |
| `FinalizeFlight` | Private application operation called immediately after successful PIREP submission; clears assignment/recovery and the completed live session. |
| `SetPilotName` / `ExportFlightLog` | Explicit session identity update and existing diagnostic export. |

Existing UI event handlers delegate to these methods. Cosmetic animations can continue after successful commands; their completion handlers no longer finalize operational state.

## 5. Snapshot and state contracts

`FlightSessionSnapshot` provides connection state/activity, assignment, aircraft identity, telemetry, simulator UTC, typed phase plus its label, route progress/reliability/distance, the cached ETA, actual OUT/IN, completion and PIREP eligibility, assignment association, rotation, operational events and milestones.

Nested route points, engine readings/states, association issues, rotation legs and event collections are detached immutable arrays. Incoming telemetry collections are copied before entering the runtime. A retained snapshot cannot change when the next sample arrives, and mutation of a simulator-owned input list cannot change accepted state. Persistence occurs before snapshot notification; exceptions from one subscriber are contained so other subscribers and the runtime continue.

`SimulatorConnectionState` replaces presentation-derived connection meaning. `TrackingEventCode` supplies typed block-out, liftoff and connection-loss identities. Existing JSON events without `Code` deserialize as `None`; their titles and historical payloads are retained. Recovered display labels do not trigger new history decisions.

Diagnostics status has its own notification. A heartbeat does not republish flight state or refresh unrelated connection controls. Reattaching a window applies the current snapshot even if there is no assignment or telemetry yet.

## 6. Seven required 7.18A failures

All seven pass in the final complete-suite run. Previously passing baseline checks remain passing.

| Original failing check | Resolution |
| --- | --- |
| `FIRST_RENDER_PURITY` | The coordinator advances the estimator in ordered telemetry processing or an explicit schedule correction. Rendering reads `EstimatedArrival`; both first-render and settled-refresh purity checks pass. |
| `RECONNECT_REJECT_OLD_CALLBACK` | Each connection attempt captures its cancellation owner and monotonic generation. Posted telemetry/open callbacks validate both before processing. Disconnect, retry, completion and disposal invalidate obsolete generations. Old queued and delayed callbacks are rejected; current callbacks still work. |
| `ASSIGNMENT_NO_ANIMATION_WAIT` | Acceptance and persistence run before the first animation await. |
| `PIREP_SUBMIT_NO_ANIMATION_WAIT` | The existing durable PIREP write completes before presentation delays. |
| `PIREP_CLOSEOUT_NO_ANIMATION_WAIT` | Successful submission immediately invokes application finalization. Assignment/recovery cleanup no longer waits for the closing animation. |
| `TEXT_COMPLETION_INDEPENDENCE` | Flight Tracking receives explicit completion and PIREP-eligibility values. Renaming the displayed phase preserves submission eligibility. |
| `TEXT_HISTORY_INDEPENDENCE` | History creation uses typed block-out/liftoff event codes rather than event-title matching. Renamed departure wording produces the same history behavior. |

## 7. Files created and modified

All production files below are in `src/Alpha6Ops.Desktop/`.

**Created:** `FlightSessionCoordinator.cs`, `FlightSessionCoordinator.Tracking.cs`, `FlightSessionCoordinator.Identity.cs`, `FlightSessionCoordinator.Connection.cs`, `FlightSessionCoordinator.Commands.cs`, `FlightSessionSnapshot.cs`, `SimulatorConnection.cs`, `MainWindow.FlightSession.cs`, `FlightSessionDiagnosticAccess.cs`.

**Modified production/composition files:** `App.xaml.cs`, `MainWindow.xaml.cs`, `MainWindow.Dashboard.cs`, `MainWindow.LiveIdentity.cs`, `MainWindow.Gates.cs`, `MainWindow.Schedule.cs`, `DispatchWorkspace.xaml.cs`, `FlightTrackingWorkspace.xaml.cs`, `FlightTrackingEvents.cs`, `Alpha6Ops.Desktop.csproj`.

**Modified existing diagnostic/test files:** `DashboardSmokeTest.cs`, `LiveIdentitySmokeTest.cs`, `RefreshSmokeTest.cs`, `TrackingIntegrationSmokeTest.cs`, and `tests/Alpha6Ops.MigrationChecks/Program.cs`. Existing assertions were retained; reflection access was adapted to the extracted owner. The project grants internal access only to the external test assembly; the diagnostic reflection adapter is not used by the production flow.

**Documentation:** this report, the updated [rerunnable checklist](sierra4-migration-regression-checklist.md), the [implementation plan](superpowers/plans/2026-09-23-checkpoint-7.18b.md) and the [user's specification](superpowers/specs/2026-09-23-checkpoint-7.18b.md).

No Core, storage schema/adapter, native SimConnect source, map, XAML layout, style, version, package dependency or packaging-script changes. The pre-existing checkpoint 7.11 bundle remains untouched.

## 8–10. Build and regression results

The complete production solution and the separate migration harness built in Release with **zero warnings and zero errors**. The normal `packaging/build-desktop.ps1` process produced the portable distribution, private runtime, installer and ZIP. The packaged application passed the Desktop and replay suites. The installer was not installed. Final runner exit code: **0** against source `00ff637`.

| Check group | Executed | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| Core regressions | 122 | 122 | 0 | 0 |
| Desktop smoke | 18 | 18 | 0 | 0 |
| Dashboard, header, responsive and monitor checks | 447 | 447 | 0 | 0 |
| Refresh assertions | 1,010 | 1,010 | 0 | 0 |
| Live identity | 24 | 24 | 0 | 0 |
| Default diagnostic index | 19 | 19 | 0 | 0 |
| Monitor lifecycle | 22 | 22 | 0 | 0 |
| Tracking integration | 43 | 43 | 0 | 0 |
| Single-instance activation | 1 | 1 | 0 | 0 |
| Recorded-flight validations | 9 | 9 | 0 | 0 |
| Migration contracts and compatibility | 29 | 29 | 0 | 2 |
| Explicit large diagnostic-index suite | 35 | 35 | 0 | 0 |
| Python fleet import | 6 | 6 | 0 | 0 |
| **Total** | **1,785** | **1,785** | **0** | **2** |

Counts are reported assertion executions, not independent test functions. Repeated refresh/index assertions are included as in 7.18A, and each recorded flight counts once. Skipped checks are additional to the executed total. Build steps, development failures and superseded reruns are not added to the final count. The increase from 1,779 to 1,785 is six new assertions, not dropped or renamed failures. No simulator was launched.

No flaky checks were observed in the completed runs; this was not a stress campaign proving their absence. The normally disabled 1,000-file diagnostic workload was explicitly enabled and passed. No other disabled cases were identified in these suites. The separate web prototype remains outside the packaged Desktop UI and has no automated test script.

Six additional migration assertions protect:

1. Continued recorder/recovery advancement with every flight view closed.
2. Reattachment preserving pilot identity, recorder and phase.
3. Reattachment before any sample arrives, preserving pending connection status and access to Disconnect.
4. Earlier snapshots remaining unchanged after later samples.
5. Ownership of mutable simulator input collections.
6. Continued processing/persistence when a presentation subscriber throws.

The original stale-callback tests now exercise the production connection loop through an injected fake source, instead of depending on compiler-generated MainWindow callback internals.

Development gates caught diagnostic heartbeats refreshing unrelated controls, a new window overwriting session pilot identity during initialization, and an empty replacement view missing an already pending connection. Correcting attachment also required preserving the independent fixture dashboard and initial disconnected-card palette. Connection presentation is now applied independently of the full live-flight refresh. Existing or added checks cover these issues. Independent review found the pending-connection omission and confirmed the final correction. No required repair to Core algorithms or storage behavior was discovered.

## 11–12. Replays and telemetry

**All nine original flights passed.** Expected = raw telemetry = replay-eligible = processed samples = **91,366**. No pre-assignment samples were omitted.

| Flight | Actual telemetry samples processed |
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

Independent raw-record counting matched every replay report. All original journal SHA-256 values remained unchanged. Private journals and output evidence remain ignored local files.

## 13. Compatibility

Frozen assignment, recovery, gate and history fixtures remain readable, including missing newer fields, named NaN telemetry and legacy gate defaults. Copied pilot data reads **497 flights, 960 events and 10 saved gates**; source-profile hashes are unchanged. The profile has no current assignment/recovery files, so those formats are validated through frozen fixtures rather than claimed as current-profile reads.

SQLite schema, storage locations, Alpha6Designs/Alpha6OPS directory names, JSON/JSONL formats, PIREP payload/duplicate handling and history semantics are retained. Typed event codes are additive with a legacy default. Phase thresholds, telemetry continuity rules, route-progress calculations and arrival-estimator algorithms are unchanged; only their ownership and render-time invocation changed.

## 14. Remaining risks and limits

- **Two native gates are unavailable:** real SimConnect open/close/worker disposal and actual rapid simulator reconnect. They are skipped, not passed. Synthetic callback tests exercise the coordinator and dispatcher below the native boundary.
- Processing remains serialized on the WPF application dispatcher. A blocked dispatcher can still delay tracking; this checkpoint does not promise worker-thread isolation or improve map rendering cost.
- Retry/backoff and disconnect-during-launch paths received static review. Automated connection tests exercise explicit disconnect/reconnect; they do not prove native launch timing or handle cleanup.
- Closing/recreating views and preserving runtime state are tested; garbage-collection leak freedom is not established. Application shutdown still intentionally ends the session.
- Existing fixture-preview controls and visual animation continuations remain presentation-owned. Review also noted a pre-existing Submit PIREP button reset limitation across successive flights; it was not introduced by this extraction or changed without authorization. A later UI checkpoint should include a two-flight, single-window interaction check.
- Existing diagnostics/logbook retain their read/query paths to services. Future views must keep live mutations behind coordinator commands and consume authoritative snapshot flags rather than labels.
- Replay does not establish real-aircraft sensor calibration or replace a live flight. No new sensor mapping was introduced.

## 15. Decision and evidence

**GO for a separately authorized Sierra 4 design-system/shell checkpoint using the coordinator's commands and immutable snapshots.** The seven isolation gates and all previous automated checks pass. This is not approval to rewrite Core rules or replace simulator/storage behavior. Native lifecycle/reconnect validation remains necessary before claiming a fully validated live simulator release. No Sierra 4 visual implementation has begun.

Final evidence:

- [Consolidated result](../work/checkpoint-7.18a/20260923-182525-4f3b5440/summary.json).
- [Migration contracts and compatibility](../work/checkpoint-7.18a/20260923-182525-4f3b5440/migration/migration-checks.json).
- [Nine-flight replay result](../work/checkpoint-7.18a/20260923-182525-4f3b5440/replays/summary.json) and [independent sample coverage](../work/checkpoint-7.18a/20260923-182525-4f3b5440/migration/replay-coverage.json).
- [Release build](../work/checkpoint-7.18a/20260923-182525-4f3b5440/build.log), [normal packaging](../work/checkpoint-7.18a/20260923-182525-4f3b5440/package-build.log) and [built artifact hashes](../work/checkpoint-7.18a/20260923-182525-4f3b5440/build-hashes.json). Individual Desktop, large-index and fleet-import reports are alongside these files.

Rerun with the [migration checklist](sierra4-migration-regression-checklist.md). Evidence directory for the final run: `work/checkpoint-7.18a/20260923-182525-4f3b5440/` (the reused baseline runner intentionally retains its original output-root name). Focused review correction evidence: `work/checkpoint-7.18b/pending-red/` and `work/checkpoint-7.18b/palette-green/`. The incomplete runs at `20260923-181841-ae8035d0` and `20260923-182237-cd3f46fa` preserve evidence of the fixture-preview and palette regressions; neither is counted as a successful run.

Stop after 7.18B. No remote publication or installation is part of this checkpoint.
