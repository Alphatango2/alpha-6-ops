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

- [ ] Retain seven failing baseline contracts as red tests. Add a coordinator-without-window lifetime test, stale callback tests using an injected source, and detached-snapshot assertions.
- [ ] Create `FlightSessionCoordinator.cs` (owner, state publication, disposal), `.Tracking.cs` (ordered existing pipeline/recovery/logging), `.Identity.cs` (existing identity rules), `.Commands.cs` (assignment/gates/schedule/PIREP), `.Connection.cs` (source/retry generations), `FlightSessionSnapshot.cs`, and `SimulatorConnection.cs` (injected source/dispatcher adapters).
- [ ] Move the existing processing statements in their current order. Only the ordered processing/explicit schedule command may invoke `ArrivalEstimator.Observe`; rendering reads cached ETA.
- [ ] Use `App.GetFlightSession` as the production composition root; diagnostic hosts inject explicitly owned isolated instances. MainWindow must neither construct the production service nor dispose it when a view closes.
- [ ] Replace mutable live fields in MainWindow with read-only snapshot projections, render on snapshot change, unsubscribe on closure. Keep unrelated fixture-preview presentation and existing controls intact.
- [ ] Build Release and run Core to detect extraction errors before UI command work.

### 2. Commands and typed operational meaning

- [ ] Move acceptance, gate/schedule updates, block-in confirmation, submission and finalization into coordinator methods. Existing view event handlers synchronously issue commands before animation awaits.
- [ ] Add typed `TrackingEventCode` alongside the existing human-readable event data; mark block-out/liftoff/connection-loss at production event creation. Default old serialized events to `None`.
- [ ] Pass explicit completion/PIREP eligibility into Flight Tracking rendering. Changing phase text or event titles must not change the coordinator's decisions.
- [ ] Adapt existing diagnostic reflection to the new owner, retaining all behavioral assertions. Replace the compiler-generated callback test seam with an injected fake source that drives the production connection loop.
- [ ] Run the migration harness; require all seven prior failures to pass and report any unexpected behavioral differences before making changes outside scope.

### 3. Full verification and report

- [ ] Run `scripts/verify-migration-baseline.ps1 -ReplayManifest .work/replay-7.17-manifest.json`; inspect every report, including nine journals, compatibility copies and explicit large-index workload.
- [ ] Independently review the complete diff and address concrete issues; repeat only the gates affected by fixes.
- [ ] Add `docs/checkpoint-7.18b.md`, update the reusable checklist and record actual counts, seven individual resolutions, compatibility and native limitations.
- [ ] Stop. Do not implement Sierra 4 UI or push/install anything.

## Decisions and evidence

- The user's written specification and instruction to continue authorize implementation; no repeated design permission is required. Work stays on a new local branch in the prepared checkout so private replay paths/runtime assets remain intact.
- 7.18A was saved in a separate local baseline commit before extraction. Its verified seven failing tests supply the initial red evidence; completed baseline work is not repeated for setup.
- Serialization remains on the application dispatcher to preserve current ordering/thread assumptions. The service does not depend on any Window or Control; a worker-thread redesign is out of scope.
