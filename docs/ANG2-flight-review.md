# ANG2 flight review — 19 September 2026

Status: investigation and checkpoint proposal; no application changes or release built.

## Evidence

Compared ANG2 YSSY–AYPY against the extracted journals of JST221, QFA124,
JST786, QLK1579, JST721 and JST724. Reviewed all 14 ANG2 screenshots and the
current identity, milestone, ETA and map rendering paths. The input package is
`ANG2 - 9-18 FT - A220.zip`. Working results are in
`work/ANG2-review-20260919-210735/comparison.json`; the analysis script is
`work/review_ang2.py`.

The seven journals contain 70,295 telemetry samples. ANG2 contributes 15,694
samples over 4:21:33, with no backwards simulator timestamps or gaps greater
than two seconds. Its JSON export exactly matches all 31,430 JSONL entries,
reports no incomplete final line, and ends with `pirep_submitted`. These are
capture-integrity checks, not proof that every sensor value or PIREP field is correct.

## Cross-flight comparison

| Flight | Build | Aircraft family | Samples | Relevant comparison |
| --- | --- | --- | ---: | --- |
| JST221 | 0.13.0 | A320 | 11,965 | Individual engine states available; earlier phase/event baseline. |
| QFA124 | 0.14.0 | 737-800 | 11,535 | Late connection and registration mismatch; assignment remained rejected despite continued observations. |
| JST786 | 0.15.0 | A320 | 6,952 | Twelve-hour schedule mismatch; engine restart cycle exposed one-event-per-flight limitation. |
| QLK1579 | 0.16.0 | A220-300 | 9,040 | All engine flags zero; assignment rejected and actual-time display missing despite milestones. Schedule aligned, unlike ANG2. |
| JST721 | 0.17.0 | A320 | 7,650 | Reported hero/tracking progress and time-layout regressions; engine states available. |
| JST724 | 0.18.2 | A320 | 7,459 | Smooth recorded progress; ETA/taxi behavior informed 7.14. |
| ANG2 | 0.19.0 | A220-300 | 15,694 | Smooth tracking progress, but rejected assignment splits hero/footer from recorded flight; engine flags remain zero; map lacks persistent follow. |

Earlier journals do not all contain `tracking_state`. Original progress behavior
cannot be numerically compared across every release from those journals alone.
Previous screenshots and existing reviews provide additional historical context.

## Schedule mismatch: confirmed user input, poor application handling

The user confirmed the planned date and UTC time were entered incorrectly.
The imported schedule says 18 September 21:45Z to 19 September 01:43:44Z.
Simulator observations begin on 17 September at 21:09Z. The association log
rejects the assignment as outside its 18-hour departure window.

This is not evidence that the simulator clock drifted. A6OPS nevertheless
continues calculating large early-arrival claims from an untrusted schedule.
The wrong date explains the mismatch, but does not justify hiding observations.
Do not silently shift the plan by one day: the intended date/time requires user
confirmation. Preserve imported values and recorded simulator timestamps.

## Recorded actuals exist, but the interface suppresses them

| Milestone | Recorded simulator UTC |
| --- | --- |
| OUT | 17 September 21:37:22 |
| OFF | 17 September 21:51:26 |
| ON | 18 September 01:23:21 |
| IN | 18 September 01:27:53 |

Observed block time is approximately 3:50:30; airborne time is 3:31:54.
These are detected milestones, not independent confirmation of exact gate position.

`MainWindow.LiveIdentity.cs` clears `liveRotation` when association is rejected.
`MainWindow.xaml.cs` gates `ActualFlightIn` on `CanRenderAssignedFlight` and
gets displayed OUT from the rotation. The recorder and route tracker continue
collecting milestones and following the imported route independently.

Consequently the hero shows FREE FLIGHT and changing nearby airports, while
tracking shows ANG2 YSSY–AYPY. The completed footer shows no actual IN and
"waiting for airborne data" despite recorded completion. This is a shared-state
and presentation defect, reproduced in different forms in earlier flights.

ANG2 tracking progress is monotonic from 0 to 100%, with the largest per-sample
increase approximately 0.092 percentage points. Its hero remains blank. Do not
replace the working route-progress calculation just to repair the hero.

## Engine observations are not trustworthy

All 24,734 telemetry samples across the two A220 flights contain false/zero
engine-running fields. A320 and 737 journals contain changing individual masks.
No alternate raw fuel-flow, spool-speed or starter measurements are recorded,
so these packages cannot establish a reliable replacement threshold or prove
exact engine-start/shutdown times.

Current event code eventually recognizes unsupported engine signals after
airborne observations; before that it presents zero as ENGINES OFF. Core phase
detection also treats false as off, making unknown engine data relevant to
completion decisions. The final ANG2 event acknowledges unavailable engine
data, but the core still needs explicit validity handling.

Add per-engine capability/validity, raw diagnostics and corroborated detection.
Fuel flow alone must not be treated as proof of a completed start. Support
repeated starts, failed-start/restart sequences, and two/four-engine layouts.
Existing logs cannot retrospectively validate signals they did not capture.

## ETA and map findings

The estimator's early-flight planned-duration blend needs actual OUT. Rejected
association removes that input even though OUT was recorded. At about 22:39Z,
ANG2 estimated IN at 00:58Z; detected IN was 01:27:53Z. This roughly 30-minute
optimism is separate from the erroneous one-day early/late comparison. Improve
ETA inputs and uncertainty first; do not tune against this one route alone.

The map's `CenterAircraft` method is a one-shot centre operation. It has no
persistent follow state, matching the screenshot note about losing the aircraft
and needing a highlighted button. Add an explicit follow toggle and defined
drag/zoom/navigation behavior.

"Runway vacated" currently means speed fell below a threshold after touchdown;
there is no runway-position evidence. Prefer a truthful rollout-speed event
until runway geometry is available. ANG2 does not prove premature block-in,
but a taxi stop with unavailable engines must be included in regression tests.

## What improved / what remains unproven

UTC-only columns, canonical Airbus A220-300 naming, chronological events,
gear events and route-progress continuity look improved in this package.
Numbered flap labels are displayed; their correspondence to actual detents is
not established by percentage-only telemetry. Keep that distinction in diagnostics.

Existing component/replay passes did not exercise the rejected-assignment UI
path that failed here. No new replay suite was run for this investigation.
Next validation must exercise the production reconciliation and rendering path,
not only feed samples into isolated phase/progress calculators.

See [checkpoint-7.15.md](checkpoint-7.15.md) for the proposed work and exit gates.

## Follow-up: A220 flap movement

Compared every flap sample in ANG2 and QLK1579. `SimConnectSource.cs` requests
`FLAPS HANDLE PERCENT` in `Percent Over 100`, but stores it as
`FlapsExtendedRatio` / `flapsExtendedRatio`. This is selected handle position,
not measured surface extension. Current A220 labels multiply the ratio by five
and round; this produces the displayed UP/1/2/3/4/5 labels. Discrete telemetry
supports the mapping pattern, but does not independently verify cockpit labels
or physical surface positions for every add-on.

ANG2 first-observed handle changes (event publication is normally one sample later):

| Simulator UTC | Handle ratio | Current mapped label | Context |
| --- | ---: | --- | --- |
| 17 Sep 21:41:38 | 0.2 | 1 | One sample while selecting departure setting; intentionally not published by debounce. |
| 17 Sep 21:41:39 | 0.4 | 2 | Held through takeoff. |
| 17 Sep 21:51:38 | 0.2 | 1 | Retraction after liftoff. |
| 17 Sep 21:53:48 | 0 | UP | Held throughout cruise. |
| 18 Sep 01:17:39 | 0.2 | 1 | Approach selection. |
| 18 Sep 01:18:55 | 0.4 | 2 | Approach selection. |
| 18 Sep 01:19:44 | 0.6 | 3 | Five consecutive samples. |
| 18 Sep 01:19:49 | 0.8 | 4 | Four consecutive samples. |
| 18 Sep 01:19:53 | 1 | 5 | Held through touchdown. |
| 18 Sep 01:24:41 | 0 | UP | Taxi-in retraction selection. |

QLK1579 raw sequence was 0 → 0.4 → 0.2 → 0 → 0.2 → 0.4 → 0.6 → 1 → 0.
There was no sampled 0.8 setting, so its absent intermediate event is not an
observed logging loss. One-second capture cannot rule out a sub-second selection.
The older build displayed percentages; ANG2 displays mapped setting numbers.

Every sustained raw transition in both packages has a corresponding flap event.
ANG2's one-sample departure 0.2 transition was filtered by the two-sample rule.
No sampled cruise oscillation or unexpected flap-state chatter was found.
This does not establish flap travel duration, asymmetry, actual deflection or
aircraft speed-limit compliance: none can be determined from handle ratio alone.

7.15 should distinguish selected configuration from actual surface movement,
capture handle index/count and physical flap/slat positions where supported,
retain raw readings, validate the label map, and test debounce without dropping
brief but deliberate stable selections. Do not fabricate missing detent events.
