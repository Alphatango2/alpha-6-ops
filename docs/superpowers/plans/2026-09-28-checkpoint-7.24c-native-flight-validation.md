# 7.24C TERMINAL T1 — native flight validation plan

**Purpose:** Pilot-run validation of destination shutdown → IN/Complete → PIREP eligibility/persistence and active-flight recovery/reconnect. Preparation only; no production changes, simulator actions or new flight have been performed by the agent.

**Baseline:** Successful [installed T1 parked validation](../../flight-tracking/7.24c-terminal-t1-native-20260928.md), plus the [implementation and automated verification](../../flight-tracking/7.24c-terminal-shutdown-implementation.md). Original C1, T1 and earlier physical evidence remain separate. This plan does not close 7.24C or begin 7.25/premium UI work.

## Decision: one flight, distinct test stages

Use **one normal simulator flight**, preferably **YMML → YPAD** to reuse the JST776 airport pair, in the exact supported **FenixA321 IAE WF SC / Fenix package 2.4.0.4720**. Allow at least 15 minutes of steady cruise before descent for two short, separate exercises:

1. A single in-process Disconnect → Connect, validating the connection-close/resume path.
2. A single normal Sierra 4 Exit → reopen → Connect while still airborne, validating disk recovery and a new application process.

Keep both engines operating during these exercises and leave MSFS running normally. The adapter qualifies master-ON/combustion-ON evidence independently on every new connection. Finish both exercises well before descent, then maintain one continuous connection through touchdown, terminal shutdown and PIREP submission. This allows the same recovered operational flight to prove subsequent ON/IN and final persistence.

The two interruptions cover different code paths; repeatedly cycling either adds little evidence. No separate flight is required if both recover cleanly. If recovery fails, stop the validation branch and retain the evidence; a later uninterrupted terminal flight may isolate shutdown behavior, but it would not close the failed recovery gate.

**Excluded from the minimum native test:** forced process kill, Windows/MSFS restart, induced SimConnect transport fault, automated reconnect backoff, engine failure, pause/slew, off-destination completion, or reconnecting during shutdown. Automated interruption/negative tests remain separate evidence. A clean native restart must not be labelled native crash recovery. Reconnecting with both engines already OFF is a known conservative limitation, not part of this arrival test.

## Fixed build and setup

- Installed frozen build: **S4-7.24C-T1-20260928-00e3f628b90e**, version **0.22.0 / 7.24C TERMINAL T1**. All **514 installed files** were read-only verified against its frozen manifest on **29 September 2026 at 01:08:47 UTC**; zero mismatches. Version 0.22.0 alone is not sufficient identity.
- Application: `C:\Users\DanniBaLs\AppData\Local\Alpha6Designs\Alpha6OPSPreview\Alpha6OPS.exe`.
- Data root: `C:\Users\DanniBaLs\AppData\Local\Alpha6Designs\Alpha6OPS` — **different from the installation directory**.
- Start normally from the installed shortcut/executable. Do **not** use the two-minute `Start-Parked-Retest.ps1` trace launcher for this flight. Ordinary flight journals run throughout each connection. F9 does not create a diagnostic marker in this normal run.
- Keep the exact Fenix title/livery/version used for T1, with the planned registration matching the simulator. A different Fenix variant/version is not an automatic-shutdown validation substitute.
- Use a fresh SimBrief/Dispatch release for this flight and correct **dated simulator UTC**; note the computer UTC separately. Use an identifiable flight number and write it in the notes. Do not change the schedule, assignment, aircraft, registration or date after departure.
- Run at normal 1× simulator time with pause, active pause and slew OFF. Set any pause-on-focus-loss option appropriately **before** the test. Use an offline/local MSFS flight so the cruise checks need no live ATC interaction.
- Record resolution/scaling, maximized/windowed state, MSFS and Fenix versions. Keep the display configuration unchanged during the run.
- Classic remains the default. Select **Map V2 as the session opt-in** once connected; reselect it after the application restart if the default Classic view appears. That reset is expected and does not reset the flight.

## Evidence folders and cold snapshots

Create a new folder on the Desktop named `S4-7.24C-T1-FLIGHT-<date>-<flight-number>`. Use these subfolders; never reuse an old evidence directory:

```text
00-before/
01-airborne-restart/
02-after-restart-audit/
logs/
screenshots/
pilot-notes.txt
```

At each named cold-snapshot point below, **Sierra 4 must be fully exited**. Copy, do not move, edit, rename or delete the source files:

- `Alpha6OPS-flights.sqlite` from the data root, plus matching `Alpha6OPS-flights.sqlite-wal` and `Alpha6OPS-flights.sqlite-shm` if present. Keep each snapshot's files together. Never substitute a standalone live database copy for a closed snapshot.
- `active-flight.json` and `active-flight-state.json` if present. Record absence in the notes; do not create placeholder source files.

Use the footer **Exit** or tray **Exit Sierra 4**. The window's X may only minimize to the tray. If the process does not exit normally or a copy reports a sharing error, stop and report it; do not force-kill or retry through an installer. These file copies preserve evidence only and are never restored into the profile during the test.

## Pilot procedure

### A. Before departure

- [ ] With Sierra 4 closed, take the `00-before` cold snapshot. Preserve an unrelated unfinished flight rather than replacing it; report that situation before beginning. The earlier parked T1 session is not an arrival and must not be submitted as one.
- [ ] Load the exact validated Fenix at a normal departure gate, stationary and braked. Start Sierra 4 normally, review the new release, enter the gates if desired and accept it while disconnected. Verify flight number, YMML → YPAD, aircraft/registration, and dated UTC. Screenshot the accepted assignment.
- [ ] Connect once **before pushback/movement**. Both engines may be started normally after connecting. Confirm current telemetry, the correct assigned flight and successful log recording. If it shows FREE FLIGHT, assignment/airport/aircraft mismatch or logging unavailable, stop before departure; do not override or manufacture identity.
- [ ] Opt into Map V2. Once both engines are running, leave at least 10 seconds of steady telemetry before any later interruption. Fly the departure normally. Confirm one OUT and one OFF event; screenshot the tracking event log/actuals in established climb or cruise. No IN, Complete or Submit PIREP should be available.

### B. One airborne disconnect/reconnect

- [ ] In steady, autopilot-controlled cruise with both engines operating and at least 15 minutes before descent, note simulator UTC and take a Flight Tracking screenshot showing the flight, OUT/OFF and airborne phase.
- [ ] Click **Disconnect** once. Wait until Disconnected is shown, then remain disconnected for **10 seconds**. Leave MSFS running at 1×; do not pause, change aircraft or move its position artificially.
- [ ] Click **Connect** once. Allow up to **30 seconds** for connection and advancing telemetry. This is an observation limit, not a change to application retry timing. If it fails, do not repeat cycles; record the error and stop validation.
- [ ] Observe for **30 seconds** after telemetry resumes. Confirm the same assignment/route, unchanged OUT/OFF, retained earlier events and an appropriate current airborne phase. No new flight, joined-in-progress warning, new OUT/OFF, IN or PIREP should appear. A temporary unavailable ETA during disconnection is expected; values should resume with current telemetry.
- [ ] Screenshot the recovered live state. Once stable, pan the map briefly and perform one Operations → Flight Tracking → Operations switch. Note any hitch, flicker, wrong solar first frame or unusable return; do not initiate repeated stress cycles.

### C. One airborne application restart

- [ ] While still in steady cruise, take a second pre-exit screenshot and note simulator UTC. **Exit Sierra 4 normally while connected**; do not separately disconnect first. This exercises application-exit persistence separately from stage B. Leave MSFS, the autopilot and both engines running.
- [ ] Once Sierra 4 has fully exited, take the `01-airborne-restart` cold snapshot. It must contain the current accepted assignment and recovery state. These preserve the exact original `FlightHistoryId`, OUT/OFF, event sequence and recovery checkpoint for later audit. If files are unexpectedly missing, stop the validation and retain what exists.
- [ ] Reopen the **same installed T1 application**, preferably within **60–90 seconds** of exit. Do not accept/import the flight again or clear/reset the assignment. Expect a recovered flight and a prompt/status to reconnect. Screenshot the restored state **before** connecting.
- [ ] Connect once. Allow up to 30 seconds for current telemetry; then observe for 30 seconds with both engines still operating. Verify the original assignment, OUT/OFF and earlier events, no joined-in-progress fallback or duplicate milestones, and a correct current airborne phase/position. Requalifying current engine signals is expected; transient initial unavailable sensor samples must not create a new flight or completion.
- [ ] Reselect Map V2 if necessary; inspect the current position and one return from Flight Tracking. A new diagnostic journal/connection identity is expected. Pre-restart in-memory telemetry/trail caches need not be recreated in full; no straight line should fabricate flight through the disconnected interval. The **operational history identity and recorded milestones must survive**.
- [ ] If recovered state is lost, changed or marked invalid, **stop validation here**. Do not reaccept the assignment to make the display look right. Preserve the evidence and report; flying the simulator to a safe stopping point is separate from claiming a successful validation.

### D. Landing and controlled terminal shutdown

- [ ] Continue normally with **no further disconnect, restart, pause/slew or schedule change** until PIREP submission. Land at the assigned destination, confirm one ON / touchdown and Taxi In, then taxi to a normal terminal stand within the airport. Never use teleport/slew to simulate arrival.
- [ ] At the final stand, stop completely, set the parking brake and keep both engines running. Start a stopwatch and, preferably, a short screen recording showing Flight Tracking/Submit PIREP area, the cockpit engine masters/display and simulator UTC. Use screenshots plus UTC notes if recording is unavailable; eligibility timing precision will then be limited.
- [ ] Hold **both engines running for 15 seconds**. No IN/Complete/PIREP eligibility is expected despite stationary/braked destination parking.
- [ ] Command **engine 1 master OFF** and record the time. Leave engine 2 running for **20 seconds**. No IN/Complete/PIREP is allowed during this single-engine interval.
- [ ] Command **engine 2 master OFF** and record the time. Keep the brake SET, remain stationary/unpaused and do not interact with connection/assignment controls. Note the first Complete indication, actual IN and appearance of **SUBMIT PIREP**. Capture engine N1/N2 at that point if visible.
- [ ] Expected behavior is approximately **3–4 simulator seconds after the first complete fresh all-engine cutoff/no-combustion evidence**, normally several seconds after the final master-OFF action. Recognition must not wait for N1/N2 or fuel flow to decay to zero. The displayed IN is backdated to the beginning of the successful confirmation interval; it is **not** the time the button first becomes visible.
- [ ] If no Complete/Submit PIREP appears within **10 seconds of the last master-OFF action**, mark a timing exception and keep recording, parked and connected, for up to 60 seconds. Do not use manual Confirm Block In, toggle engines, reconnect or reset anything to obtain a pass. Return the evidence for review before closeout if this occurs. The 10-second observation trigger is not a relaxed acceptance threshold: log analysis must identify the actual first eligible sample and any blocked condition.
- [ ] On the normal path, leave the aircraft untouched for **60 seconds after the last master-OFF**. Check that IN stays unchanged, Complete/PIREP eligibility remains available and no second completion appears. This also captures subsequent mechanical spool-down.

### E. One PIREP and a cold persistence check

- [ ] Screenshot Complete, actual IN and the available Submit PIREP action **before clicking it**. Click **SUBMIT PIREP once** while still connected. Let the normal UI finish; note the click time and result. Expect completed closeout and the active assignment to clear. Do not click repeatedly or interrupt the transmission animation to test a different scenario.
- [ ] If submission fails, screenshot the exact message and stop; do not repeat submissions during this validation. Keep the application/data for diagnosis.
- [ ] On success, disconnect normally and exit Sierra 4. Reopen the same installed build **without connecting to MSFS**. Open **Pilot logbook**, select this flight and screenshot its completed status, route and events. Confirm one new completed entry for this run and no resurrected active assignment/Submit PIREP action. Older flights with the same flight number are not duplicates of this run.
- [ ] Exit again and take the `02-after-restart-audit` cold snapshot. Successful closeout should have removed `active-flight.json` and `active-flight-state.json` for this assignment. Record their absence rather than recreating or deleting them. Do not reconnect with engines OFF to perform this audit.
- [ ] Copy all test-session JSONL files **and their matching JSON exports** from `...\Alpha6OPS\TestLogs` into `logs`. Expect **three connection journals** on the planned path: departure through stage B disconnect; stage B reconnect through airborne Exit; post-restart connection through PIREP closeout. Preserve any extra/error journal too. The program-monitor files for every application start during this test and any crash/error reports from the test window must also be included. Do not return only the last Export test log result.
- [ ] Complete the [pilot notes template](../../flight-tracking/7.24c-terminal-t1-native-flight-notes.txt), add screenshots/video, ZIP the new evidence folder and return it. Keep the originals. A hash/SQLite audit will be performed on review; a screenshot alone does not establish durable persistence.

## Pass/fail criteria

| Gate | Pass | Fail / stop-and-review |
|---|---|---|
| Identity and evidence | Exact T1 build and validated Fenix; correct accepted assignment before OUT; logs available | Wrong build/variant, ambiguous assignment or missing recording |
| In-process reconnect | One original flight; prior OUT/OFF and events retained; telemetry resumes; no premature completion | Lost/changed identity, joined-in-progress reset, duplicate milestone, repeated connection failure |
| Cold airborne recovery | Exact original history ID and checkpoint restored, earlier events/actuals preserved, new samples continue that flight | New operational row, unreadable/missing checkpoint, changed original actuals or reset phase/session |
| Native requalification | Fresh ON/combustion-ON observed from both required engines on the final connection before arrival | Enhanced evidence remains unqualified/invalid; unsupported/manual fallback is not equivalent |
| Negative terminal checks | No IN/Complete/PIREP with both engines running or only one shut down | Any premature completion, irrespective of later correct-looking state |
| Terminal recognition | Qualified all-OFF/no-combustion plus all arrival guards; first eligible sample starts existing stability interval; completion on first eligible sample at/after 3 seconds | Physical spool-down still gates intent, valid continuous candidate unnecessarily delayed, bad/missing telemetry accepted, manual override required |
| Eligibility | One actual IN and Complete, verified completed history row, Submit PIREP available; no second confirmation on subsequent samples | Complete without usable eligibility, altered/repeated IN or unverifiable history |
| Submission/persistence | One accepted submission; one original completed flight row, one `pirep_submitted`, correct dated OUT/OFF/ON/IN; old history retained after cold restart | Duplicate/new row for recovered flight, lost or changed events/actuals, lost PIREP, assignment resurrection or storage errors |
| Physical presentation | No recurring visible hitch/layout flicker; usable map return during this flight | Recurrence recorded as a physical failure, not dismissed by passing telemetry checks |

A disconnect intentionally leaves a telemetry gap. No fabricated samples or milestone timestamps may fill it. Multiple transport journal files are expected; "one flight/session" means the same logical operational history ID. A valid recovery may display updated airborne subphase or recomputed ETA/geographic progress; those values need not be frozen at their pre-interruption values.

Mark a gate **NOT PROVEN**, rather than PASS, if required evidence is missing or the scenario deviates (wrong aircraft, pause/time jump, diversion, uncontrolled restart, recovery failure). Do not repeat the whole flight until the failed/inconclusive stage has been reviewed. No native forced-crash or induced transport-fault result is claimed by this protocol.

## Reviewer measurements after return

1. Preserve source hashes and keep this native flight separate from parked T1, JST776, historical replays and synthetic tests. Verify log closure/sequence and JSON/JSONL consistency; count every recorded sample per connection without inventing coverage across disconnects.
2. Match baseline, airborne cold snapshot and final SQLite by exact flight GUID. Compare earlier event payloads/sequence/times; require one original row to reach Complete, one PIREP and no invalid/interrupted duplicate row left for this run. The same row may temporarily say Interrupted during an intentional disconnect.
3. Reconstruct OUT/OFF, both interruptions, restored identity, ON, stationary/brake, each cutoff/combustion transition, per-connection qualification, first all-engine confirmed evidence, generic mechanical Off, IN, first Complete publication, eligibility observation and submission. Check each fresh receipt/generation and all terminal guards.
4. Report separately: final physical master action → visible eligibility (video/notes precision); recorded last cutoff → fresh shutdown evidence; fresh eligible candidate → first Complete tracking sample; effective IN timestamp; physical visibility vs durable PIREP submission/closeout times. `flight_milestone.simulatorUtc` is the effective candidate time, while the first `tracking_state` with phase Complete dates the publishing sample. Keep wall-clock `recordedAtUtc` and simulator time domains distinct.
5. The expected 3–4 second normal-cadence publication delay is measured from the first continuously eligible sample; apply the existing three-second rule and actual sample cadence, not an invented timeout or N1/N2 threshold. Quantify any interruption/reset that justifies a longer interval. The UI/video proves availability; current tracking journals do not explicitly log `PirepEligible` on every snapshot.
6. Audit the final SQLite `flight`/`flight_event` records, `pirep_submitted` actuals and cleared assignment/recovery. `pirep_closeout_complete`/session ending support the result; the animation alone does not. Report all physical observations, reports and unresolved limitations before recommending 7.24C closure. Existing general WPF queue-delay limitations are not erased by a smooth flight.

## Source grounding / review

Read-only basis: `FlightSessionCoordinator.Connection.cs` (in-process reconnection), `FlightSessionCoordinator.Tracking.cs` (disk recovery, exact history identity, ON/IN journal/DB sequence), `FlightSessionCoordinator.Commands.cs` (PIREP closeout), `FlightSessionCoordinator.cs` (eligibility/disposal), `TerminalShutdown.cs` and `ShutdownIntentAdapters.cs` (qualification/guards), `Operations.cs` (three-second phase stability), `FlightRecovery.cs` and `FlightHistoryDatabase.cs` (persistence), and `ActiveFlightRecoveryChecks.cs` (already exercised synthetic clean/kill/reconnect coverage).

Self-review: both native interruption paths, no completion during partial shutdown, no restart after all masters OFF, exact history identity, dated actuals, three expected journals, cold database/WAL copies, no changes to legacy data, and post-submission cold audit are covered. Production changes/build/replay reruns are not part of this preparation. This is a pilot validation plan, not an implementation plan or authorization for agent-driven flight controls.
