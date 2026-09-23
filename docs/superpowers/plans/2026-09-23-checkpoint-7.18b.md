# Checkpoint 7.18B implementation plan

> Execute inline using the executing-plans workflow; the user supplied and authorized the architectural specification. Preserve the existing checkout and baseline evidence. Review the completed change with one independent reviewer.

**Goal:** Remove live flight and connection ownership from presentation while preserving the verified operational baseline.

**Architecture:** App owns one production `FlightSessionCoordinator`. It serializes commands and telemetry on an injected application dispatcher, reuses existing Core/infrastructure classes, and publishes detached immutable `FlightSessionSnapshot` records. MainWindow subscribes and renders; child workspaces issue commands through existing UI events. Diagnostic hosts explicitly supply isolated coordinators.

**Tech Stack:** Existing .NET 10/WPF/SQLite/JSON/SimConnect. No new packages.

**Spec:** `docs/superpowers/specs/2026-09-23-checkpoint-7.18b.md` (user-provided).

## Global constraints

- No visual redesign, map replacement, web migration, HTTP bridge or airline-world engine.
- No database schema/storage-directory, phase threshold, telemetry continuity, route algorithm, PIREP/history semantic or existing-data changes.
- The only operational changes remove the seven documented presentation dependencies.
- All 7.18A checks, nine original journals and 91,366 samples remain gates. Native MSFS checks stay unavailable unless actually exercised.
- Stop after 7.18B; no Sierra 4 visual implementation or remote publication.

## Review focus

- Old connection callbacks and old finally blocks must not change a replacement session, including retry attempts and disconnect during launch/backoff.
- Removing or replacing every view must preserve phase, actuals, recovery, history and the connection; UI observer failures cannot stop telemetry persistence.
- Snapshot collections and nested telemetry must be detached from mutable runtime/input lists.
- Assignment/PIREP commands must persist before any presentation wait and must preserve existing error and duplicate-submission behavior.
- Existing recovered events lack typed identifiers; they must remain readable without using their display labels to trigger new operational decisions.

## Tasks

### 1. Extract coordinator, connection boundary and immutable state

- [x] Retain seven failing baseline contracts as red tests. Add a coordinator-without-window lifetime test, stale callback tests using an injected source, and detached-snapshot assertions.
- [x] Create `FlightSessionCoordinator.cs` (owner, state publication, disposal), `.Tracking.cs` (ordered existing pipeline/recovery/logging), `.Identity.cs` (existing identity rules), `.Commands.cs` (assignment/gates/schedule/PIREP), `.Connection.cs` (source/retry generations), `FlightSessionSnapshot.cs`, and `SimulatorConnection.cs` (injected source/dispatcher adapters).
- [x] Move the existing processing statements in their current order. Only the ordered processing/explicit schedule command may invoke `ArrivalEstimator.Observe`; rendering reads cached ETA.
- [x] Use `App.GetFlightSession` as the production composition root; diagnostic hosts inject explicitly owned isolated instances. MainWindow must neither construct the production service nor dispose it when a view closes.
- [x] Replace mutable live fields in MainWindow with read-only snapshot projections, render on snapshot change, unsubscribe on closure. Keep unrelated fixture-preview presentation and existing controls intact.
- [x] Build Release and run Core to detect extraction errors before UI command work.

### 2. Commands and typed operational meaning

- [x] Move acceptance, gate/schedule updates, block-in confirmation, submission and finalization into coordinator methods. Existing view event handlers synchronously issue commands before animation awaits.
- [x] Add typed `TrackingEventCode` alongside the existing human-readable event data; mark block-out/liftoff/connection-loss at production event creation. Default old serialized events to `None`.
- [x] Pass explicit completion/PIREP eligibility into Flight Tracking rendering. Changing phase text or event titles must not change the coordinator's decisions.
- [x] Adapt existing diagnostic reflection to the new owner, retaining all behavioral assertions. Replace the compiler-generated callback test seam with an injected fake source that drives the production connection loop.
- [x] Run the migration harness; require all seven prior failures to pass and report any unexpected behavioral differences before making changes outside scope.

### 3. Full verification and report

- [x] Run `scripts/verify-migration-baseline.ps1 -ReplayManifest .work/replay-7.17-manifest.json`; inspect every report, including nine journals, compatibility copies and explicit large-index workload.
- [x] Independently review the complete diff and address concrete issues; repeat only the gates affected by fixes.
- [x] Add `docs/checkpoint-7.18b.md`, update the reusable checklist and record actual counts, seven individual resolutions, compatibility and native limitations.
- [x] Stop. Do not implement Sierra 4 UI or push/install anything.

## Decisions and evidence

- The user's written specification and instruction to continue authorize implementation; no repeated design permission is required. Work stays on a new local branch in the prepared checkout so private replay paths/runtime assets remain intact.
- 7.18A was saved in a separate local baseline commit before extraction. Its verified seven failing tests supply the initial red evidence; completed baseline work is not repeated for setup.
- Serialization remains on the application dispatcher to preserve current ordering/thread assumptions. The service does not depend on any Window or Control; a worker-thread redesign is out of scope.
- The review found an empty replacement window could miss a pending connection before its first sample. A new failing check reproduced this. The final presentation attachment applies connection state independently, preserving the fixture dashboard and the initial disconnected palette. The reviewer confirmed the correction.
- Six additional migration assertions now cover view-free processing, reattachment (active and pending sessions), detached snapshots, simulator input ownership and observer-failure isolation. No existing check was weakened or deleted.
- Final complete verification against `00ff637`: 1,785 executed, 1,785 passed, 0 failed, 2 native unavailable; nine replays and 91,366 raw/processed samples. Evidence: `work/checkpoint-7.18a/20260923-182525-4f3b5440/summary.json`. Production build/package and test harness succeeded; solution and harness have zero warnings/errors. No native simulator claim is inferred from injected-source checks.
