# Checkpoint 7.25A native live-telemetry validation

Use only frozen candidate `S4-7.25A-A350-20261001-6020896a402b`.

This is a bounded live-telemetry validation. The preserved native DAL40 journal
is the authoritative A350 evidence and deterministic proof for FL350→FL370,
FL370→FL390 and FL390→FL410. The remaining gate is aircraft-independent live
integration of the corrected state machine and presentation history. One
realistic shallow transition from either the iniBuilds A350 or PMDG 777-200ER
is acceptable when, and only when, the recorded telemetry meets the required
profile below. A second natural step climb is not required unless this test
reveals a specific behavior that the DAL40 profiles and one live transition do
not cover.

## Before flight

1. Preserve the current installed-build identity and profile/history snapshots.
2. Exit Sierra 4 normally. Install the exact frozen 7.25A installer and verify
   its identity against `validation-build.json`.
3. Use the iniBuilds A350 or PMDG 777-200ER and a normal route long enough to
   provide at least 40 minutes of continuous airborne telemetry before descent.
   KLAX→PHNL is suitable. Do not use pause, slew, time acceleration or
   artificial position changes.
4. File an initial cruise level that permits one realistic later step climb.
   The new level must be at least 750 feet above the established observed level.
5. Start Sierra 4 at the departure, load/accept the assignment, select Map V2,
   connect once, and preserve a screenshot showing the departure end of the blue
   flown trail.

## Flown-track gate

1. Accumulate more than 2,048 one-second simulator observations. Use 40 minutes
   as the minimum continuous telemetry duration.
2. While the aircraft continues normally, leave Operations for at least six
   uninterrupted minutes. Use another Sierra workspace; do not disconnect.
3. Return to Operations. The current aircraft and the blue trail must include
   the distance flown while Operations was hidden. The departure end must still
   be present.
4. Select the classic map, then return to Map V2. This explicitly creates a new
   renderer. The complete sampled origin-to-current trail must reappear; it must
   not restart at the switch point.
5. Pan and zoom the recreated map. Confirm the trail remains continuous except
   for a real invalid/reconnect gap, and that normal input and workspace
   switching remain responsive.

## One qualifying shallow live step-climb gate

1. Confirm a `Top of climb / cruise established` event exists for the current
   observed level.
2. Fly one realistic shallow step climb. The recorded telemetry must show at
   least 30 valid observed seconds at or above +500 ft/min while at least 100
   feet above and departing the established level.
3. Before reaching +750 feet displacement, the recorded vertical speed must
   taper below +500 ft/min on valid samples. Continue normally past +750 feet,
   level at the new cleared cruise, and remain stable for at least 60 observed
   seconds. A climb that remains at or above +500 ft/min until after +750 feet
   still exercises normal step-climb handling, but does not reproduce the fixed
   shallow-climb boundary and therefore does not close this gate.
4. Confirm exactly one sequential `Cruise altitude changed` event whose summary
   says the new observed cruise was established. The filed cruise shown in
   dispatch must remain unchanged.
5. During the later descent, confirm `Top of descent` derives from the new
   observed level. No event may rewrite or remove the previous observed level.

## Complete the flight

Complete ON and Taxi In normally. Neither the iniBuilds A350 nor PMDG 777 has an
enhanced terminal-shutdown adapter in this candidate. Use the guarded manual
Block In path: park at the correct destination, remain on ground and stationary
with the parking brake set for the required stable interval, then use Block In
when it becomes available. Submit the PIREP once and exit Sierra 4 normally.

## Evidence to return

- Exported flight JSON and every JSONL journal belonging to the logical flight.
- The resulting flight-history database copy after normal exit.
- `program-status.json`, crash/error reports, and Map V2 diagnostic reports if
  any were produced.
- Screenshots of the trail before hiding Operations, after the six-minute hidden
  interval, and after classic→Map V2 renderer recreation.
- Screenshots of the original and new observed-cruise events, TOD, completion,
  submitted PIREP, and the post-flight Logbook row.
- A short note with simulator aircraft title and add-on/version, filed cruise,
  old observed cruise, new observed cruise, approximate step start/end times,
  whether VS fell below +500 before +750, hidden-workspace duration, and total
  connected duration.

## Pass criteria

- One realistic shallow transition matches the required tapering profile and
  establishes the new observed level in order, with unchanged filed cruise and
  no false or duplicate events.
- TOD uses the latest observed level.
- More than 2,048 source observations do not erase the departure or route shape.
- More than five minutes outside Operations does not stop trail accumulation.
- A fresh Map V2 renderer reconstructs the complete sampled trail.
- No OOOI, PIREP, recovery, terminal, scheduling, responsiveness, or logbook
  regression is observed.

Any missing origin, hidden interval, renderer-recreation history, sequential
cruise event, or latest-level TOD result is a failure. Stop and preserve evidence;
do not begin 7.26.
