# JST721 flight review and proposed checkpoint 7.13

Reviewed 17 September 2026. Source: `JST721 - 9-17 FT - A320.zip`, JSON/JSONL session `8243977147324ed8b7b638d8b02e4d00`, and 16 screenshots. Build reviewed: 0.17.0. Route: YSSY–YMHB, Airbus A320. This document preserves the investigation; see [checkpoint 7.13](checkpoint-7.13.md) for subsequent implementation and validation status.

## Recorded flight

The exports contain 7,650 telemetry samples, 38 tracking events and four flight milestones. JSON and JSONL events agree. The largest telemetry interval is approximately 1.21 seconds; no simulator-clock reversal or paused sample was found. PIREP closeout was recorded. Connection loss does not explain these failures.

| Time | Simulator UTC, 17 September |
|---|---|
| Scheduled OUT | 02:05:00 |
| Actual OUT milestone | 02:05:45 |
| Airborne milestone | 02:15:40 |
| Touchdown milestone | 03:54:37 |
| Actual IN milestone | 03:57:30 |
| Scheduled IN | 04:13:27 |

Block time was 1:51:45; airborne time was 1:38:57. Arrival was approximately 16 minutes early. Scheduled departure 02:05 UTC equals 12:05 AEST. The screenshot's overlapping text is a layout problem, not evidence of an incorrect UTC conversion on this flight.

## Confirmed causes

1. **Progress locks at the departure.** `FlightRouteMap.GreatCircle` returns the start point for every sample of a leg below 0.0001 radians. YSSY–TESAT is approximately 0.303 NM, producing five identical initial points. `NearestRouteProgress` then searches only four points forward. A numerical reproduction of these methods over all 7,650 recorded positions produced a 48-point path and 0% maximum progress. The search cannot escape the repeated initial points. Separately, fraction-of-point-index is not fraction-of-route-distance: short and long legs receive unequal distance weighting.
2. **Distance and ETA inherit the broken progress.** Remaining distance stayed around 564 NM while the aircraft approached Hobart. ETA continued adding travel time for the full route to the advancing simulator clock. The completion display jumps to 100% because TaxiIn/Complete force that value; it does not demonstrate successful route tracking.
3. **Approach milestones depend on this faulty value.** Approach/final require progress thresholds of 65%/80%. Both were absent despite an actual arrival. Instantaneous display-phase logic is also separate from event-phase logic.
4. **Hero time and lateness use different calculations.** Hero arrival can retain a planned-duration projection while its badge uses live ETA. Screenshots show an arrival around 04:14 alongside a much later predicted-arrival badge. Scheduled fallback values can also be presented as estimates before a meaningful live estimate exists.
5. **Time fields overflow adjacent columns.** Eight equal-width columns contain long, unwrapped UTC/local strings. Local scheduled time collides with actual time. UTC/local values need separate labeled lines and responsive layout.
6. **Gear events miss gradual movement.** Retraction requires the previous sample above 50% and current sample below 10%. Normal animation crosses these thresholds in separate samples. Extension also depends on the missed retraction event. Detect settled states across the transition instead.
7. **Milestone and event timestamps differ.** Milestones preserve candidate onset; event messages use confirmation time, typically several seconds later. Use the milestone time consistently, keeping observation/confirmation time separately for diagnostics.

The map screenshot also demonstrates poor distinction between planned route, completed route and actual trail. Actual and planned paths can legitimately differ; do not erase that difference by snapping the aircraft trail to the plan. Verify route ordering against the OFP before claiming a route-import defect.

## Additional review items

- Canonical aircraft naming currently covers UI fields incompletely; raw add-on titles remain in events/history. Store canonical type/subtype separately from simulator/add-on metadata. Do not invent a subtype absent reliable evidence.
- Audit fuel comparison boundaries: block-out fuel consumption through touchdown is not directly equivalent to planned airborne trip fuel.
- Preserve the working assignment behavior in this run: a registration mismatch was advisory and the accepted assignment remained attached.
- Unknown engine/flap data must remain unknown, with capability handling shared across aircraft. Do not solve this review by special-casing the Fenix A320.

## Corrective implementation order

1. Build a headless replay harness for all five supplied flight packages. Capture baseline results before modifying production behavior. Earlier builds require schema-aware parsing; keep original logs unchanged.
2. Move progress into a shared core service independent of map rendering/navigation. Project onto route segments using cumulative geographic distance. Handle zero/short legs, loops, late connection, reconnect, off-route flight and reacquisition explicitly. Avoid both a permanently bounded search and unrestricted nearest-point jumps across crossing legs. Preserve correct short-leg endpoints in map interpolation.
3. Make hero, tracking footer, remaining distance, ETA and route drawing consume one timestamped flight-state snapshot. Separate airborne arrival estimates from gate-IN estimates. Publish rounded, stable estimates with explicit unavailable/stale states and freeze actual IN once confirmed.
4. Unify phase/event state. Treat route progress as supporting evidence rather than a mandatory prerequisite for recognizing approach. Include destination proximity, altitude trend and prior phase; test level-offs, go-arounds and late connection. Fix settled gear transitions and event timestamp consistency.
5. Repair time layout and planned/actual map styling. Put UTC and local values on separate lines with clear dates/labels; allow fields to reflow at narrow widths and increased Windows scaling.
6. Run all recorded sessions plus targeted edge cases before packaging. Keep changes local unless publication is requested.

## Release evidence required

- JST721 advances during flight rather than staying at 0%; remaining distance trends toward Hobart, and landing does not hide a broken calculation by jumping from 0 to 100.
- Hero and tracking views show the same progress, arrival estimate and lateness even when either view was never opened.
- JST721 approach/final and gear transitions are recorded from appropriate telemetry; departure level-offs do not create false cruise/approach events.
- Confirmed OUT/IN retain the recorded simulator UTC date and time. Overnight/date rollover, UTC offset, simulator pause/time changes and late connection have separate regression cases.
- All previous A320, 737-800 and A220 sessions replay without losing assignment identity or fabricating unavailable events. Supported aircraft details remain optional inputs to shared tracking behavior.
- Visual verification at representative window widths and 100%, 125% and 150% scaling shows no overlapping time fields; route layers remain distinguishable.

## Prior validation limitation

The earlier 7.12 summary overstated completion. Production progress remains inside the map control; the independent shared progress engine was not implemented. Existing smoke tests supply synthetic progress (including a hardcoded 58%) and do not reproduce this native-telemetry failure. Passing those tests was insufficient evidence of recorded-flight correctness. The next release should report exactly which recorded sessions and edge cases passed, rather than treating a successful build as flight validation.
