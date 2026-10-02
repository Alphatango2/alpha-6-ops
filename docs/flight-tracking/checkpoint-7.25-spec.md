# Checkpoint 7.25 — Tracking Correctness and Diagnostic Hardening

Status: approved design specification  
Approved baseline: `S4-7.24C-FINAL-L1-20260930-318659425b88`  
Application version: `0.22.0`  
Baseline source-content SHA-256: `318659425B88D7529FA7A8C014366EDCB06C37DC894849BF915F2173A65EB478`

## Purpose and boundaries

Checkpoint 7.25 corrects four bounded defects before packaging cleanup and premium UI work:

1. observed cruise and Top-of-Descent recognition when the filed and flown cruise levels differ;
2. predicted Gate IN continuity across application recovery;
3. intermittent replacement failure of the diagnostic program-health marker; and
4. misleading completion wording when terminal shutdown is authoritative while generic mechanical classification is still settling.

This checkpoint does not implement SimBrief revision handling, redesign the UI or maps, add aircraft adapters, change lifecycle semantics, change engine thresholds, change terminal-shutdown authority, clean the repository, optimize package footprint, begin 7.26, or begin premium UI work.

The following 7.24C behavior is protected: authoritative exactly-once OOOI; active-flight recovery; persistent flight/session identity; exactly-one PIREP; same-flight-number isolation; terminal T2 policy and Fenix A320-family support; Map V2 scheduling and restoration; direct responsiveness acceptance; Logbook operational-time resolution; and route-progress endpoint correction.

## Cross-cutting authority rules

- Simulator telemetry and recorded lifecycle milestones remain authoritative for actual flight history.
- Predictive, presentation, diagnostic and tracking-derived state cannot authorize or rewrite OOOI, completion, PIREP, flight identity or terminal shutdown.
- Simulator UTC remains the time basis for operational observations and predictions. Computer UTC remains receipt/diagnostic time.
- Recovery additions are optional and backward compatible. Invalid optional derived state is discarded without rejecting otherwise valid authoritative flight recovery.
- Restart downtime never counts as an observed stable interval.
- No accepted dispatch field is mutated by an observation-derived result.

## A. Observed cruise and Top of Descent

### Provenance model

Sierra 4 must keep three distinct concepts:

1. **Filed/planned cruise altitude** is immutable dispatch/OFP plan data held by `ActiveFlightPlan.CruiseAltitudeFeet`.
2. **Current observed cruise altitude** is tracking-derived operational state established from telemetry. It may change after a legitimately observed step climb.
3. **Actual flown altitude/profile** is the authoritative telemetry/history stream. It is never replaced by either plan or current observed state.

Observed cruise qualification never changes, normalizes, widens, or silently replaces the filed value. Operational event history preserves each established observed level and each transition. For example, filed FL350, observed FL330, step climb, then observed FL350 retains both FL330 establishment and the later FL350 establishment. Future SimBrief revision architecture may consume these distinct records, but 7.25 does not implement revision detection or acceptance.

### Observation state

Add a small flight-scoped observed-cruise state machine owned by the tracking event monitor. Its optional recovery state records:

- current operational airborne stage (`Climb`, `Cruise`, `Descent`, or go-around climb context);
- current established observed level, its establishment time and prior established level where needed for a transition;
- pending stable-level band, accumulated valid observed duration and last counted sample time;
- whether qualifying climb evidence has been seen;
- pending step-climb and descent evidence with accumulated valid observed duration; and
- enough transition identity to prevent duplicate events after recovery.

Candidate duration is accumulated only between valid, consecutive simulator samples. A sample interval greater than three seconds contributes zero duration. Pause, slew, invalid altitude/vertical speed, ground state and time reversal contribute zero and reset the applicable pending evidence. A restart may retain already accumulated valid duration, but the gap between the last saved sample and the first recovered sample contributes zero.

Malformed or incoherent optional observed-cruise state resets only this tracker. It cannot invalidate the flight recovery record.

### Qualification rules

- Use pressure altitude when finite; otherwise use indicated altitude.
- Cruise observation is available only while airborne at or above 18,000 ft.
- A stable-level candidate requires finite altitude and vertical speed, absolute vertical speed below 300 ft/min, and altitude remaining within a 400-ft band around the candidate level.
- The displayed/recorded observed level is the nearest 1,000-ft level after stability is established; raw telemetry remains unchanged in history.
- A candidate within 600 ft of the filed level requires 30 seconds of valid observed stability. This preserves the established matching-plan behavior.
- A candidate that differs from the filed level requires 180 seconds of valid observed stability and prior qualifying climb evidence. The plan difference itself is not a tolerance and does not qualify the level.
- A recovered established level remains established when the optional state is coherent. A recovered pending level resumes only with its previously accumulated valid observed duration; restart downtime is excluded.
- A step climb begins only after cruise was established, altitude has moved at least 750 ft above the established level, and vertical speed of at least +500 ft/min is sustained for 30 valid observed seconds.
- A new post-step observed level requires 60 seconds of stable-level evidence. The previous observed level and step-climb event remain in event history.
- Top of Descent requires an established observed level, altitude at least 500 ft below it, and vertical speed at or below -500 ft/min for 30 valid observed seconds.
- Turbulence and brief altitude/vertical-speed excursions reset pending evidence but do not delete an already established observed level.
- A level segment after TOD remains descent context and does not create a new cruise event by itself.
- A go-around resets current arrival presentation to climb without deleting historical cruise/TOD events and cannot establish a new cruise below the normal altitude/context guards.

Event text must expose provenance where it matters: a mismatched observation identifies the observed level and preserves the filed level as separate context; it must never imply an amended clearance or accepted dispatch revision.

### Required scenarios

- filed FL330 / flown FL330;
- filed FL350 / flown FL330, including JST771;
- filed FL330 / flown FL350;
- sustained step climb and establishment at a new level;
- temporary climb level-off shorter than the mismatch qualification interval;
- temporary descent level-off;
- turbulence and altitude deviations;
- descent followed by a level segment;
- go-around presentation reset;
- recovery while cruise is established;
- recovery during pending mismatched-level qualification; and
- recovery near TOD without duplicate cruise or TOD events.

## B. ETA recovery continuity

`ArrivalEstimator` predicts Gate IN in simulator UTC. Actual IN remains authoritative after it exists.

The documented defect is caused by loss of estimator memory on application restart: the airborne blend clock, smoothed speed and publication hysteresis reset, producing the JST771 22:50 to 23:10 jump over approximately nine simulator seconds.

### Recovery model

Add an optional `ArrivalEstimatorState` containing only predictive continuation state that cannot be reconstructed exactly from the last authoritative sample:

- last processed simulator sample time;
- last phase;
- last published Gate IN estimate, including an explicit unavailable state;
- airborne and Taxi In anchor times; and
- finite smoothed airborne groundspeed.

The state is derived and predictive. It cannot authorize or alter a milestone, lifecycle phase, completion, PIREP, flight identity, route progress or history row.

Persist the state inside the existing flight recovery checkpoint. Restore it only after the normal flight/assignment recovery identity checks. The estimator validates intrinsic coherence, and the coordinator validates it against recovered phase, last simulator time, last telemetry and phase-event chronology. Invalid state is discarded and the estimator is deterministically initialized from authoritative phase events, actual milestones and the current telemetry sample where possible.

Legacy recovery files with no estimator state remain readable. Temporary route-estimate unavailability remains unavailable after recovery until valid current route evidence returns; it must not revive a stale ETA. Time reversal, implausible gaps, invalid speed and phase-incoherent anchors discard affected predictive state without disturbing flight recovery.

Publication hysteresis remains intact across a valid restart, including simulator UTC midnight. Taxi In remains anchored to ON/current Taxi In context, and Complete returns exact actual IN.

### Required scenarios

- restart in climb, cruise, near TOD and descent;
- restart after ON / during Taxi In;
- JST771 22:50 pre-restart estimate does not jump to the 23:10 schedule baseline;
- simulator UTC midnight;
- unavailable route estimate before and after recovery;
- valid persisted route progress;
- invalid/corrupt optional ETA state safely discarded;
- legacy recovery schema; and
- no duplicate lifecycle or tracking events caused by ETA reconstruction.

## C. Program health/status marker

`program-status.json` is diagnostic state only. It is not an authority for active flight, lifecycle, recovery, history or PIREP.

The established failure is an intermittent Windows `UnauthorizedAccessException`/access-denied result while replacing the status file. Sierra 4 has one normal application instance and serializes its own heartbeat operations. Evidence does not identify the external handle owner; no antivirus, indexer or other process is blamed.

### Persistence correction

- Retain same-directory staged write plus atomic replacement.
- Write and flush the complete staged file before replacement.
- Application readers use read plus delete sharing so their completed-file reads do not block replacement.
- Retry only Windows sharing violation, lock violation and the demonstrated transient replacement access-denied case.
- Use the fixed bounded schedule 10, 20, 40 and 80 ms: five total attempts and a maximum sleep budget of 150 ms.
- Known permanent conditions, including a read-only target or a directory at the target path, fail immediately.
- An exhausted heartbeat write remains a visible `PROGRAM MONITOR ERROR`.
- An exhausted final stopped-status write produces a `program_status_stop` diagnostic containing stage, attempt count, path and HResult. Shutdown may continue; the failure is not silently swallowed.
- Preserve the last complete published status file on failed replacement and clean the bounded staging file where possible.

Deterministically reproduce the old defect by holding the published destination without delete sharing, releasing it inside the bounded retry window and asserting that one-attempt replacement leaves the old running record while the corrected store publishes the stopped record. Also prove permanent failure is bounded and observable and concurrent allowed readers never observe partial JSON.

Severity remains **diagnostic-only** unless new evidence demonstrates operational coupling.

## D. Engine-state presentation

Mechanical classification and terminal shutdown intent remain separate.

- Do not change `EngineEvidenceTracker`, `EngineCondition`, classification thresholds, Fenix adapters, capability qualification, `BlockInEligibilityPolicy`, PhaseDetector completion authority or the three-second completion stability interval.
- Individual mechanical events continue to describe `Running`, `Starting`, `Stopping`, `Unknown` and `Off` states.
- For a Complete milestone authorized by enhanced terminal evidence, completion text states that terminal shutdown was confirmed. If mechanical evidence is still settling, text may additionally state that spool-down continues or mechanical data is partial.
- For a manual limited-support Block In, text identifies pilot confirmation instead of claiming mechanical shutdown.
- Without terminal completion context, existing generic mechanical wording remains conservative.

Tests must hold the underlying telemetry, mechanical states and terminal decision constant while comparing old and corrected presentation text. No presentation branch may change completion eligibility.

## Expected file scope

Expected production changes:

- create `src/Alpha6Ops.Desktop/ObservedCruiseTracker.cs`;
- modify `src/Alpha6Ops.Desktop/FlightTrackingEvents.cs`;
- modify `src/Alpha6Ops.Core/ArrivalEstimator.cs`;
- modify `src/Alpha6Ops.Desktop/FlightRecovery.cs`;
- modify `src/Alpha6Ops.Desktop/FlightSessionCoordinator.Tracking.cs`;
- create `src/Alpha6Ops.Desktop/ProgramStatusStore.cs`;
- modify `src/Alpha6Ops.Desktop/ProgramMonitor.cs`.

Expected test/verification changes:

- modify `src/Alpha6Ops.Desktop/DashboardSmokeTest.cs`;
- modify `src/Alpha6Ops.Desktop/ProgramMonitorSmokeTest.cs`;
- modify `tests/Alpha6Ops.Tests/TrackingTests.cs`;
- modify `tests/Alpha6Ops.RecoveryChecks/ActiveFlightRecoveryChecks.cs`;
- modify `scripts/test-jst771-evidence.py` only if required to assert corrected replay output while retaining historical T1/manual evidence;
- create `scripts/verify-checkpoint-7.25.ps1` for the combined automated gate.

Conditional release metadata after every automated gate is green:

- modify `packaging/PreviewSetup.cs` only to identify the separately frozen 7.25 candidate;
- modify `packaging/Desktop-Quick-Start.txt` only to identify its validation status and installation boundary.

If evidence requires any additional production file, stop before changing it and report the reason and proposed boundary change.

## Test-first evidence and acceptance

For each workstream, preserve a RED result produced by tests added before production changes and showing the expected 7.24C defect. Compile errors caused merely by referencing a nonexistent proposed API are not sufficient when the old public behavior can be exercised directly.

- Cruise RED must show filed FL350 / flown FL330 does not establish cruise or TOD while the filed plan remains FL350.
- ETA RED must reproduce the 22:50 to 23:10 restart discontinuity through coordinator/recovery behavior.
- ProgramMonitor RED must deterministically demonstrate one-attempt replacement failure with a temporarily held destination.
- Engine RED must show misleading Complete wording while holding authoritative enhanced terminal and generic mechanical states constant.

After each GREEN, run the owning targeted suite before beginning the next production workstream. After all targeted suites pass, run:

- Release production solution build;
- Core regression;
- complete Desktop smoke/regression;
- active-flight recovery;
- terminal-shutdown and Fenix-family capability;
- Logbook OOOI;
- Map V2 scheduling;
- map restoration/resources and browser/input/lifecycle;
- direct responsiveness acceptance;
- full regression suite;
- 11/11 historical replays, keeping the 100,632 recorded samples separate from synthetic checks;
- JST776 assertions;
- JST771 corrected cruise/TOD and historical terminal evidence assertions; and
- final JST500 persistence/terminal/Logbook evidence assertions where applicable.

No native flight is required unless automated evidence cannot safely establish a changed behavior. A candidate may be packaged only after every automated gate passes. It must remain local and uninstalled pending the completion report and user direction.

## Completion report

The report must follow the user-specified 7.25 return format and include baseline identity, before/after evidence per workstream, exact production/test inventory, RED and GREEN evidence locations, targeted and full counts, replay/sample totals, remaining limitations, native-validation decision, candidate identity if produced, and GO/NO-GO. Stop after 7.25.
