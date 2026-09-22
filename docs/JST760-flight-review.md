# JST760 flight review — 21 September 2026

Status: implemented locally in checkpoint 7.16 as v0.21.0. No GitHub
publication.

## Evidence and identity

The package contains the JSON export, original JSONL journal, OFP text/PDF and
23 screenshots. The journal contains 10,552 telemetry samples and 21,159 total
records. The export reports no incomplete final line, records a normal
`pirep_submitted` ending and matches the journal record count. Production replay
passes with no failures.

This was an Airbus A321-family validation flight. SimBrief identifies `FENIX
A321`; MSFS reports `FenixA321 IAE SL SC`; the interface correctly displays
`Airbus A321`. The supplied registration was G-FBIG while the simulator reported
VH-VWQ. A6OPS retained the assignment with a visible registration advisory.

The simulator date was 20 September while the computer recorded the session on
21 September. The imported schedule matched simulator UTC, so this is not a
clock defect.

| Milestone | Simulator UTC |
| --- | --- |
| OUT | 20 Sep 20:43:37Z |
| OFF | 20 Sep 21:04:02Z |
| ON | 20 Sep 22:59:31Z |
| IN | 20 Sep 23:06:32Z |

## Confirmed improvements

- Engine 2 and Engine 1 start sequences were recorded in the correct order.
  Starter and combustion were available on this aircraft, and per-engine events
  did not contain contradictory unavailable-data text.
- Route progress was monotonic, reached 99.85% while airborne and 100% at
  completion. The largest one-sample increase was about 0.217 percentage points.
- ETA converged from 17 minutes early in cruise, to 7 minutes early on approach,
  to an actual arrival 3 minutes early.
- Scheduled and actual times remained aligned, both gate values flowed through
  Dispatch and tracking, international airport names were present, and PIREP
  closeout completed successfully.
- The production replay reports all required milestones, approach/final events,
  block-in and stable progress with no failures.

## Confirmed issue: A32x flap labels

The Fenix A321 reports flap handle indexes 0 through 5 while reporting five
handle positions. The current mapper expects six before treating an Airbus index
as mapped, so it displays raw values such as `Flap handle 3`. The screenshot and
surface data show that raw index 3 corresponds to the cockpit's Flaps 2 setting.

For this aircraft the observed sequence is consistent with UP, 1, 1+F, 2, 3 and
FULL. Build an A32x flap profile instead of displaying raw indexes. Preserve 1+F
as a configuration where supported and suppress duplicate logical events.

Left and right physical flap angles were available, equal and smoothly moved
through approximately 0, 5, 10, 15, 18 and 22 degrees. The event footer still
said surface position was not confirmed. Use the two agreeing surface channels
to report confirmation and keep the raw readings in diagnostics. Slat angles
remained zero and must not be claimed as verified.

## Confirmed issue: shutdown transition becomes unknown

Both engines correctly reached `shut down`, but the intermediate `Engine N
shutdown` events were absent. At cutoff, combustion changed to zero while fuel
flow, N1, N2 and oil pressure decayed normally. The detector requires fuel flow
at or below 1 pph before choosing its stopping state, so it changed from Running
to Unknown for roughly 51 seconds and only then reached Off.

After an engine has been confirmed running, combustion loss plus sustained spool
and pressure decay should enter Shutdown even while the generic fuel-flow value
is still winding down. The final Off decision should retain its low-spool debounce.

## PIREP readiness and closeout

The journal records Engine 2 shut down and block-in on the same 23:06:32Z sample.
The supplied completed-state screenshots include the block-in event before PIREP
submission. This does not prove that the backend accepted an early PIREP; it
already rejects submission unless the recorder phase is Complete.

The button is rendered from the phase label alone. Gate it on both Complete and
a recorded actual IN/block-in fact to eliminate any one-frame presentation race
and make the requirement explicit in a UI regression check.

After PIREP closeout, the saved assignment and recovery state were deleted, but
the Dispatch workspace retained its previous rendered plan until another action
refreshed it. `CompletePirepCloseout` clears `activePlan` but does not call
`UpdateDispatchState`. Refresh Dispatch atomically with the hero and tracker so
the next visit immediately shows Prepare Your Next Flight.

## Additional event cleanup

The initial preflight event froze fuel at 6,602 lb while the aircraft was still
loading. Fuel stabilized near 22,775 lb before block-out. Either omit fuel from
the immediate monitoring-arm event or wait for a stable loading snapshot before
publishing it.

`Parking brake set` currently says `Aircraft secured at the gate` while both
engines are still running. Use `Parking brake set at stand`; reserve secured or
flight-complete wording for confirmed block-in.

## Proposed checkpoint 7.16

1. Add a capability-based A32x flap configuration profile and physical-surface
   confirmation using agreeing left/right values.
2. Preserve a confirmed engine through a debounced shutdown state while fuel,
   spool and oil-pressure readings decay; retain final Off hysteresis.
3. Require recorded block-in/actual IN before showing Submit PIREP.
4. Refresh Dispatch, hero and tracking atomically after PIREP closeout.
5. Remove unstable loading fuel from preflight and correct parking-brake wording.
6. Add JST760 to production replay coverage plus focused flap, shutdown and
   closeout navigation tests.
7. Show the resolved airport name beneath each ICAO code in the draft and active
   Dispatch route headers. Reuse the dashboard hero's airport catalog lookup,
   uppercase presentation and `AIRPORT NAME UNAVAILABLE` fallback; keep the
   existing `DEPARTURE` and `ARRIVAL` role labels below the names and allow long
   names to wrap without disturbing the route layout.
8. Replace the oversized departure and arrival gate controls with a compact,
   responsive `GATE ASSIGNMENTS` panel. Keep both fields side by side at normal
   widths, stack them on narrow windows, replace the ambiguous ellipsis buttons
   with visible saved-gate dropdowns, rename the actions to `SAVE ASSIGNMENTS`
   and `MANAGE SAVED GATES`, and retain optional unassigned values.
9. Recommend locally learned gates when the history contains a meaningful match.
   Rank candidates by airport, airline ICAO, aircraft family, departure or
   arrival use, frequency and recency. Display the reason and a `LOCAL HISTORY`
   label, require pilot confirmation before assignment, and make no recommendation
   when confidence is weak. Confirmed assignments continue training the local
   history; recommendations never claim live gate availability.

Checkpoint 7.16 was implemented as local v0.21.0. JST760 production replay and
ground checks passed before packaging; the next flight can validate the updated
A32x labels, shutdown transition and Dispatch workflow in live telemetry.
