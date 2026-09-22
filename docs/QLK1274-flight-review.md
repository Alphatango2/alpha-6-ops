# QLK1274 flight review — 22 September 2026

Status: reviewed; corrections proposed for checkpoint 7.17. No application code,
installer, or installed settings changed by this review.

## Evidence

QLK1274, YPAD–YBBN, Airbus A220-300, recorded with v0.21.0. The supplied
`QLK1274.zip` contains 13 screenshots, the JSON export, original JSONL journal,
and OFP PDF/text. All 13 screenshots and the OFP text were reviewed. The PDF was
not separately rendered; flight-plan comparisons below use the supplied text.

The journal has 21,089 consecutive records, including 10,519 telemetry samples,
10,519 tracking snapshots, 39 tracking events and four flight milestones. The
JSON export's `events` exactly match the JSONL records. There is no incomplete
final line. Simulator timestamps never move backward, no telemetry gap exceeds
two seconds, and no pause, slew or disconnect is recorded. The session ends with
`pirep_closeout_complete` and `pirep_submitted`.

Evidence and diagnostic scripts are retained under
`.work/qlk1274-review-20260922/`. The existing packaged v0.21.0 replay reproduces
the progress failure; its report is `replay/summary.json` in that folder.

## Flight times and improvements

All following times are simulator UTC on **21 September 2026**. The computer
recorded the session on 22 September. The planned flight and simulator dates
align; the defects below are not caused by a one-day or timezone input error.

| Milestone | Observed UTC |
| --- | --- |
| OUT | 21:00:06 |
| OFF | 21:12:44 |
| ON | 23:16:20 |
| IN | 23:22:22 |

Observed block time is 2:22:16; airborne time is 2:03:36. The application compares
IN against its imported 23:37:46 plan and reports 15 minutes early. The OFP also
contains a separate scheduled IN of 23:35, discussed below.

- Engine 2 starting/running is recorded at 21:01:25/21:01:41, followed by
  Engine 1 at 21:03:15/21:03:32. Both remain assessed as running until shutdown.
- Engine 1 shutdown/shut down occurs at 23:21:33/23:22:21; Engine 2 at
  23:21:34/23:22:22. Block-in follows final shutdown confirmation. Unlike the
  earlier ANG2/QLK1579 A220 flights, engine events and actual times are present.
- FL370 cruise is recognized at 21:37:13, with top of descent at 23:02:19.
  Gear retraction/extension, approach, final, touchdown and taxi-in are recorded.
- Departure gate 23 and arrival gate 24 persist in both hero and tracker.
  The completed screenshot shows actual IN and Submit PIREP, and the journal
  confirms submission and closeout. It does not independently prove that every
  post-closeout screen refreshed because those screens were not supplied.

## 1. Blocking defect: progress becomes stuck after departure

Every airborne tracking snapshot has the same fraction:
`0.0010981705016885977` (about **0.11%**, displayed as 0%). Remaining distance
stays near **929.87 NM**. Completion forces displayed progress to 100%, creating
a 99.89-percentage-point jump. The final journal snapshot still contains 929.87
NM remaining, even though the completed screen shows 0 NM.

This is reproduced by the packaged production replay: 10,519 samples, failure
`Airborne progress did not reach 80%.`, maximum airborne fraction 0.00109817.
The replay correctly retains actual IN at 23:22:22.7371215Z. Its generic jump
check alone misses this failure because the replay does not apply the live UI's
completion override; the airborne-progress check catches it.

Root cause is in `RouteProgressTracker.Observe`. Continuous samples are scored
by geographic distance plus an unbounded penalty for distance along the route
from the previously accepted position. The per-sample allowance is at least
5 NM, but it does not accumulate when progress is stuck. A later, geographically
correct segment can consequently lose forever to the original departure
segment. The 25-NM cross-track gate then prevents further advancement.

The OFP departure starts from runway 23 and initially heads southwest. Recorded
takeoff heading is about 051 degrees, and the flown path bypasses the first
departure legs. This difference exposes the defect; vectors, shortcuts and
changed departures must be handled without requiring a reconnect.

At 21:37:13, the aircraft is only **0.004 NM** from TOC–IGIPA and approximately
191.62 NM along the filed route. Yet the continuity score still selects
YPAD–COLPY, whose cross-track distance is about **168.24 NM**. At top of descent,
the aircraft is 0.76 NM from ODUTO–ANSOR but the tracker still favors departure.
Independent geographic calculations in `route_diagnostic.py` reproduce these
selection scores.

Required correction: bounded, evidence-based reacquisition after a changed
departure, direct routing or an excursion, retaining protection at genuine
route intersections. Track matching confidence and age explicitly. Keep both
bars, distance, map route styling, event context and journal snapshots consistent.
Do not treat the completion override as evidence that progress worked.

## 2. ETA inherits the frozen distance

This is principally a consequence of issue 1. `LiveEstimatedArrival` passes
distance calculated from the frozen route fraction into `ArrivalEstimator`.
The estimate keeps adding almost a whole flight to the current time.

- Top of climb: ETA 23:34Z, four minutes early.
- Top of descent: ETA 00:56Z the next day, 78 minutes late.
- Final approach: **930 NM to go**, ETA 04:21Z, 283 minutes late.
- Actual IN: 23:22Z, 15 minutes early against the imported estimated IN.

Repair route progress first. Also suppress or identify unreliable ETA and
early/late claims when route matching becomes stale. Add assertions for useful
ETA convergence and distance near destination, rather than only minute-stable
formatting and final actual IN.

## 3. Missing registration leaves a permanent assignment warning

The plan contains registration `C-SYNC`; the live registration is empty. The
aircraft starts 0.42 NM from YPAD with an active simulator and valid position.
The journal's second identification event rejects association with:
`Assignment unverified: waiting for matching route/location and aircraft identification.`
It never records an accepted association.

`FlightIdentity.Reconcile` explicitly rejects an unaccepted assignment whenever
the plan has a registration and the simulator does not, even with a matching
origin. `CanPresentCandidate` preserves the flight and observations, explaining
why tracking works while the hero remains on ASSIGNMENT REVIEW.

Handle missing registration as unavailable evidence, separately from a
contradictory identity. Use sufficient independent context, or a clear pilot
confirmation path when context is insufficient, without inventing a simulator
registration. The warning should explain the actual missing evidence. Give it
its own visual state: currently the hero computes schedule colors, then replaces
only the text with ASSIGNMENT REVIEW, producing grey, red and green versions of
the same warning (screenshots 9.3, 9.6 and 9.12).

This is independent of the progress failure: standalone route replay also
freezes without running assignment association.

## 4. Flap mapping and confirmation text

The A220 reports handle indexes **0–5** with `flapHandlePositions = 5` throughout.
The A32x-specific position-count accommodation in 7.16 does not cover this
A220 signal shape. Indexes 0–4 fall back to `Flap handle N`; index 5 falls back
to the percentage path and happens to display `Flaps 5`. This causes inconsistent
labels for a complete observed selection sequence.

The normal title/summary should clearly identify the selected configuration,
with UP and supported detent labels, preserving a generic fallback where a
mapping cannot be established. The supplied annotated screenshot filename also
requests configuration in the title and removal of repeated surface-confirmation
text; this is recorded as feedback accompanying the evidence.

The surface claim is misleading as well as noisy. `FlapSurfaceConfirmation`
only checks that left/right angles agree; it does not establish that the selected
configuration has been reached. For example, at 23:18:07 the handle selects UP
while surfaces are still near 36.9 degrees during retraction, yet the event says
`FLAP SURFACES CONFIRMED 36.9°`. Preserve raw handle and surface measurements in
diagnostics, and distinguish selection, movement and settled position internally.
Remove routine diagnostic confirmation chatter from the normal event display.

## 5. Scheduled IN is imported from the estimated-IN field

The OFP's TIMES table lists **SKED IN 23:35Z** and **ESTIMATED IN 23:37Z**.
The assignment stores 23:37:46 as `PlannedArrivalUtc`, and the interface labels
it Scheduled In. `SimBriefImporter.Parse` explicitly prefers `times.est_in`
over `times.sched_in`, confirming the source of the discrepancy.

Keep scheduled IN and dispatch-estimated IN distinct. Schedule variance must
use the schedule; the initial prediction may use dispatch's estimate. For this
flight, actual arrival is approximately 13 minutes ahead of the OFP schedule,
versus 15 minutes ahead of its dispatch estimate. This small difference does
not explain the hours-late predictions in issue 2.

## 6. Smaller diagnostic cleanup

Shutdown is detected, but both events attribute it to `combustion loss / spool
decay` even though recorded combustion remains zero throughout. The condition
can come from low fuel-flow/spool evidence; source wording is chosen separately
and selects the combustion-loss phrase whenever stopping coincides with decay.
Report only the evidence actually used. Do not change successful start/shutdown
ordering to address this wording issue.

The recorded fuel-flow channel has extremely small values and is not independently
validated by this package. Preserve capability/fallback handling; do not calibrate
a new engine-start rule from these values alone. Zero pre-start readings remain
unknown until supported by evidence, explaining ENGINE DATA UNAVAILABLE on the
initial pushback events.

## 7.17 priorities and validation

First repair progress reacquisition and its ETA consequences. Then address
missing-registration association, A220 flap presentation, scheduled/estimated
arrival separation and diagnostic wording. Retain the two already requested
Dispatch changes: airport-name alignment and a more prominent Gate Assignments
heading. See [checkpoint-7.17.md](checkpoint-7.17.md).

Replay this untouched journal plus the previous A220, A32x and Boeing journals.
Add focused checks for continuous samples that skip initial route legs and
rejoin later, true route intersections, unknown versus contradictory identity,
handle indexes 0–5 with a count of 5, and scheduled IN differing from estimated
IN. Exercise the complete live presentation path so a forced 100% completion
cannot conceal frozen airborne progress or inconsistent journal distance.

This flight supports the engine-event improvements, but it does not support
closing Flight Tracking yet. Resolve the shared progress defect before asking
for another full validation flight solely to repeat this scenario.
