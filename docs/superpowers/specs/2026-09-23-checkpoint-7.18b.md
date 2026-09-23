CHECKPOINT 7.18B — FLIGHT-SESSION ISOLATION

Checkpoint 7.18A established the verified pre-migration baseline.

The baseline completed:

- production build/package: PASS
- 1,779 checks executed
- 1,772 passed
- 7 migration-specific failures
- 2 live-simulator checks unavailable
- 9/9 recorded-flight replays passed
- 91,366/91,366 telemetry samples processed

The seven migration failures are now REQUIRED completion gates for this checkpoint.

OBJECTIVE

Create a stable application/runtime boundary so that the existing UI no longer owns or influences operational flight truth.

This is an architectural isolation checkpoint, NOT a Sierra 4 visual redesign.

Do not redesign the application.
Do not replace existing workspaces.
Do not replace the map.
Do not change database schemas.
Do not introduce the future persistent airline-world engine.
Do not migrate to React.
Do not introduce a production HTTP API simply for UI communication.

Preserve existing operational behavior unless a behavior is specifically identified by the 7.18A migration tests as presentation coupling that must be removed.


ARCHITECTURAL TARGET

Existing WPF UI
        ↓
View / presentation state
        ↓
Application Coordinator
Commands + typed immutable snapshots
        ↓
Existing Core rules + infrastructure
        ↓
SimConnect / SQLite / JSON / SimBrief / weather

There must be ONE application-lifetime owner for the active flight session and simulator connection.

Views must not own the active flight runtime.


PRIMARY TASKS

1. Extract active flight-session ownership from MainWindow.

Move responsibility for the live processing pipeline into an application-lifetime coordinator/service while preserving:

- telemetry processing order
- flight identity
- phase detection
- route progress
- arrival estimation
- operational event generation
- actual departure/arrival handling
- history integration
- recovery behavior
- connection/reconnect behavior

Do not rewrite existing Core algorithms unnecessarily.


2. Introduce typed immutable UI snapshots.

The UI should consume snapshots representing operational state rather than invoke stateful calculations during rendering.

A snapshot should provide the authoritative values needed by presentation, including where applicable:

- connection state
- assignment state
- aircraft identity
- telemetry state
- flight phase
- route progress
- ETA/arrival estimate
- actual times
- completion state
- PIREP eligibility
- operational events

Rendering a snapshot must not mutate operational state.


3. Introduce explicit application commands.

Presentation controls should issue commands rather than perform operational workflows directly.

Examples include:

- AcceptAssignment
- ConnectSimulator
- DisconnectSimulator
- ApplyGate
- CorrectSchedule
- SubmitPirep
- FinalizeFlight

Use naming appropriate to the existing architecture.

Do not create commands merely for cosmetic UI behavior.


4. Eliminate animation-dependent operational behavior.

Resolve the 7.18A failures where:

- assignment acceptance waits for animation
- PIREP persistence waits for animation
- PIREP closeout waits for presentation delay

Animations may continue visually, but operational commands and persistence must not depend on animation completion.

Animation is presentation.
Operational state is application state.


5. Eliminate presentation-text-dependent business behavior.

Resolve the failures where:

- PIREP eligibility depends on displayed phase wording
- flight-history creation depends on event-title wording

Introduce typed phase/event/state identifiers where necessary.

Human-readable labels must be presentation output only.

Changing a displayed phrase must not change application behavior.


6. Fix render-induced estimator mutation.

Rendering, refreshing, reopening or switching views must not advance or mutate the arrival estimator.

Stateful estimator advancement must occur only as part of the authoritative ordered operational processing pipeline.

All UI surfaces must read the resulting estimate from the shared snapshot.


7. Isolate connection generations.

Address the migration failure where an old queued callback can reach a replacement simulator connection.

Introduce a safe connection/session-generation mechanism appropriate to the existing architecture so callbacks from obsolete connections cannot mutate the active flight session.

Preserve existing reconnect behavior.


REGRESSION REQUIREMENTS

After implementation:

- Run the complete 7.18A regression suite.
- All seven migration-specific failures must PASS.
- All previously passing checks must remain passing.
- All 9 recorded-flight replays must remain passing.
- All 91,366 recorded telemetry samples must remain valid.
- Existing assignment, recovery, history and gate data must remain readable.
- Workspace switching and view recreation must not affect the active runtime.
- Hidden/minimized windows must not stop tracking.
- No operational behavior may depend on visible text or animation timing.

The two checks requiring a live simulator may remain unavailable if MSFS is not available. Do not fake their results. Clearly report them as unavailable.


COMPATIBILITY REQUIREMENTS

Do not:

- change existing SQLite schemas
- change existing storage locations
- rename application data directories as part of Sierra 4 branding
- invalidate existing JSON/JSONL data
- change PIREP semantics
- change flight-history semantics
- change phase thresholds
- change telemetry continuity rules
- change route-progress algorithms
- change arrival-estimation algorithms unless strictly necessary to prevent rendering from mutating them

If any existing behavior must change beyond removing presentation coupling, STOP and explain why before making that change.


COMPLETION REPORT

At completion provide:

1. Architecture before/after summary
2. New application/runtime boundary
3. Responsibilities moved out of MainWindow
4. Commands introduced
5. Snapshot/state contracts introduced
6. Resolution of each of the seven 7.18A failures individually
7. Files created/modified
8. Build result
9. Total regression checks
10. Passed/failed/skipped counts
11. Replay result
12. Telemetry sample result
13. Compatibility verification
14. Remaining migration risks
15. Recommendation: GO or NO-GO for Sierra 4 design-system/shell work

STOP after Checkpoint 7.18B.

Do not begin Sierra 4 visual implementation.