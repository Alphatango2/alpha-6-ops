# Checkpoint 7.15 — flight-tracking reliability update

Status: implemented locally as v0.20.0. The sections below retain the agreed
requirements. See [release-0.20.0.md](release-0.20.0.md) for delivered behavior,
validation and live-simulator limitations. No GitHub publication.

## 1. One flight state for dashboard, tracking and history

Separate observed flight facts from assignment verification and schedule trust.
Both pages consume the same session, route progress and actual milestones.
An assignment needing review must not create a contradictory FREE FLIGHT hero
while the tracking page presents that assignment as accepted. Preserve the
candidate assignment visibly with a review state; do not silently attach an
unrelated flight. Keep a stable observed origin instead of replacing it with
each nearby airport.

Acceptance: ANG2 and QLK1579 rejected-assignment cases show consistent identity
and observed actuals; registration mismatch and late join remain reviewable.

## 2. Date/time preflight check and in-flight correction

Compare full planned UTC dates/times against simulator UTC before monitoring.
Show a compact mismatch explanation with both dates and a user-confirmed
correction flow. Local time, if offered in planning, includes airport timezone
and date; tracking remains clean UTC. Never silently change simulator time or
rewrite captured events. Record original/corrected schedules and correction source.
Allow correction without resetting milestones, progress, trail or session.

Acceptance: wrong day, twelve-hour mismatch, midnight crossing, different
departure/arrival timezones and valid long-haul schedules. While unresolved,
suppress confident early/late claims and retain observed tracking.

## 3. Preserve and display OUT/OFF/ON/IN independently of schedule trust

Use recorded session milestones as the source of actuals. Missing observations
remain explicitly unknown, particularly for late joins. Show actual IN once
completion is confirmed; never show "waiting for airborne data" after landing
or completion. Carry the same actuals into history and PIREP, with provenance.

Acceptance: ANG2 displays recorded OUT and IN despite its invalid schedule;
late-connected QFA124 does not invent a departure observation; reconnect and
schedule correction preserve already-recorded values.

## 4. Aircraft-independent engine capability and detection

Capture supported per-engine combustion, fuel-flow, spool-speed and starter
signals with units, validity and source metadata. Introduce unknown/running/off
states and starting/stopping transitions where evidence supports them. Apply
debounce/hysteresis and corroborate signals; do not label unavailable zero
values as engines off. Keep aircraft/add-on adapters behind a common contract.
Support repeated starts and shutdowns for every detected engine, including four.

Acceptance: existing A320/737 behavior and JST786 restart are retained; missing,
stale and contradictory data stay truthful. New A220 capture must demonstrate
usable alternate signals before claiming corrected live engine detection.
Synthetic four-engine coverage is required; A380 live compatibility remains
unverified until tested.

## 5. Reliable block-in with unavailable engine data

Make phase detection consume engine validity, not just a boolean. Use observed
landing/taxi-in, sustained stop, parking brake and destination context together.
When automatic completion lacks sufficient evidence, provide an explicit pilot
confirmation instead of assuming shutdown. Resume taxi after a temporary stop
without creating duplicate completion events or prematurely enabling closeout.

Acceptance: taxi hold, brake cycle, missing engine sensors, real gate stop,
go-around, diversion and duplicate PIREP submission scenarios.

## 6. ETA and schedule variance from trustworthy inputs

Feed observed OUT into the estimator independently of assignment acceptance.
Only use schedule-derived baselines and early/late comparisons when the schedule
is trusted. Keep an observed-motion ETA available when possible, with uncertainty
or an unavailable reason where needed. Preserve minute-level display and stable
updates. Switch to actual IN at confirmed completion.

Acceptance: replay early climb, cruise, descent, extended taxi and late join
across the seven flights; measure prediction error by phase. Do not force ANG2
to match its final arrival by hindsight or hide legitimate ETA changes.

## 7. Persistent, visible map follow mode

Turn the aircraft-centre control into an explicit follow toggle with highlighted
active state. Follow recent live position while preserving chosen zoom. Manual
drag exits follow visibly; route-fit exits follow; switching pages preserves the
chosen mode. Stale/disconnected data retains the last position without implying
live movement. Keep route-fit separately accessible.

Acceptance: aircraft remains visible during an extended flight, at close zoom
and across the date line; manual navigation behaves predictably.

## 8. Truthful event text and telemetry diagnostics

Use "engine data unavailable" when appropriate. Rename speed-only "runway
vacated" to a rollout description unless runway-position evidence is available.
Record the evidence/source behind phase and engine decisions. Mark flap labels
as mapped/inferred in diagnostics when only percentage data supports them;
do not claim verified aircraft detents without supporting signals.

## 9. Regression gates through the production application path

Retain all seven flight fixtures and exercise reconciliation, recording, shared
state, hero, tracking footer and closeout together. Add assertions for invalid
schedule + valid observations, missing engine capabilities, repeated starts,
late join, reconnect, schedule edits, midnight, paused clock and map follow.
Ensure release metadata and fixture expectations are explicit so improvements
are not confused with original recorded behavior.

Release gate: core and production-path integration checks pass; both pages show
the same flight/progress/actuals; history and PIREP preserve observations; map
follow is visually checked. A short live A220 ground capture should verify new
engine fields before spending another full flight on that integration.

## 10. Flap selection versus actual movement

The A220 comparison found clean selected-handle transitions in both flights.
The current `flapsExtendedRatio` is sourced from `FLAPS HANDLE PERCENT`, not
physical extension. Correct the internal naming/schema with backward-compatible
replay handling and make event wording reflect selection. Capture handle index,
available position count and actual flap/slat positions when supported, keeping
validity and units explicit. Validate aircraft label mappings against supported
telemetry rather than assuming all add-ons share equal percentage intervals.

Acceptance: replay ANG2 and QLK1579 with all sustained selections preserved;
filter ANG2's one-sample departure intermediate selection; do not invent the
unsampled QLK1579 intermediate setting. Test extension/retraction, direct jumps,
brief deliberate selections, unavailable fields and noisy inputs. Report physical
movement completion only when actual-position evidence supports it. Retain A320
and 737 regression coverage through the same common telemetry contract.

## 11. Dispatch gate entry and local gate history

Replace live gate lookup with manual Dispatch entry and a reusable local gate
history database. No external flight-data subscription is required.

Add two compact, equally sized fields to the initial Dispatch release review's
left panel, below the schedule details and above Full Filed Route: Departure
gate (origin ICAO) and Arrival gate (destination ICAO). Match existing navy input
backgrounds, subtle borders, typography and spacing. Stack at narrow widths.
Use short identifiers such as `12` or `B7`, without prominent terminal names.
Both fields are optional; blank values display as `—` in Flight Tracking.

Accept Flight saves the fields with the assignment. The hero and tracking page
read those same persisted values. Remove Assign Gates from the tracking header.
Allow later changes in Dispatch with a compact Save gates action, without
reaccepting the flight or resetting progress, milestones or the flown trail.
Preserve unsaved edits during routine refreshes and manual values during a
same-flight SimBrief refresh. Never transfer a gate silently to another airport.

On explicit acceptance/save, remember airport, gate, airline when known,
canonical aircraft type, departure/arrival use and last-used date. Suggest prior
gates for that airport, prioritizing matching airline and aircraft type, while
allowing free text. Do not silently assign suggestions. Normalize whitespace and
case; avoid duplicate airport/gate entries. Do not store abandoned drafts or
placeholder values such as NA as gates. Provide edit/delete controls in a
secondary history-management view; deleting a suggestion must not rewrite past
flight records. Optional scenery/notes belong in secondary details.

Label suggestions previously used or dispatch assigned. Saved history does not
prove live availability or aircraft compatibility. Persist the database across
restart and upgrades; corrupt history must not block tracking or manual entry.

Acceptance: verify entered gates flow from release acceptance to hero, tracking
and stored assignment; verify restart persistence, in-flight edits without reset,
same-flight refresh preservation, airport changes, optional blanks, deduplication,
history suggestions and edit/delete. Check narrow and wide layouts and confirm
Assign Gates is absent from the tracking header.

## Scope boundaries

Preserve the working ANG2 route-progress algorithm, current UTC layout and
canonical aircraft naming unless evidence requires a change. Broader airport/city
map expansion and live gate-provider integration remain deferred. Item 11 uses
manual Dispatch entry and local gate history. No per-aircraft workaround should replace the shared
tracking-state contract.
