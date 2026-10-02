# Checkpoint 7.25A — engineering acceptance and residual-risk disposition

Decision date: 2026-10-01

## Decision

**7.25A is CLOSED BY ENGINEERING ACCEPTANCE.**

This decision does not revise the final native-validation review and does not
represent every native gate as directly proven. The second live A350 session
did not reproduce the exact shallow-climb taper boundary, and its returned
package did not independently prove the hidden-workspace or Classic-to-Map-V2
renderer-recreation actions. Those results remain **NOT PROVEN** for that live
session.

The remaining items are accepted as bounded residual coverage risks because
the underlying production behavior is directly covered by preserved native
telemetry replay and deterministic automation, while a separate live A350
session proved the real-time integration and downstream state consumption. No
functional failure or new production defect was observed.

## Frozen candidate

Candidate: `S4-7.25A-A350-20261001-6020896a402b`

- Installer SHA-256:
  `2920F198578FA7792D8703BAA8817DFE5E367DF53F55DCDFD51929591DDDA709`
- Portable ZIP / embedded payload SHA-256:
  `5EF952567F3571E7AB91BBF8F8DA21C26067DB0FEBDC9AAF63669F0BD97FD5A3`
- Source-content manifest SHA-256:
  `6020896A402B6F985BB06FD3A969F497B896751A24DC324E101FF0E25CEA47B8`
- Source snapshot SHA-256:
  `D8D05B90A26D2492DA64E5396159B97F077CE94D4A3C5B4236003CBB843CBF3A`

The installer and portable ZIP were rehashed during this review and remain
identical to the freeze manifest. No production source or candidate payload was
modified or rebuilt.

## Functional failure versus coverage gap

The second live session produced no observed-cruise, provenance, TOD, generic
A350, lifecycle, terminal, PIREP, or map-history failure. Its two step climbs
were stronger than the DAL40 shallow transitions and therefore did not exercise
the precise condition in which vertical speed falls below 500 ft/min before the
aircraft reaches 750 feet of displacement. This is an **unexercised native
boundary**, not a failed state transition.

Requiring another natural flight to reproduce that shape would be
nondeterministic because the relationship between vertical speed, elapsed time,
and displacement varies with aircraft weight and operating conditions.
Deliberately flying the acceptance predicate would add a live transport sample,
but little decision-logic evidence beyond the exact preserved DAL40 telemetry
and deterministic boundary tests already processed by the frozen production
tracker.

## Tracker-boundary assessment

Live and replay processing use the same production decision path:

1. Live SimConnect values are normalized into `Telemetry` and recorded in the
   journal.
2. `FlightTrackingEventMonitor.Observe` passes that `Telemetry` to
   `ObservedCruiseTracker.Observe`.
3. `RecordedFlightReplay` reconstructs `Telemetry` from the recorded normalized
   fields and invokes the same `FlightTrackingEventMonitor.Observe` and
   `ObservedCruiseTracker.Observe` implementations.

The cruise tracker depends on simulator timestamp, on-ground, paused, slewing,
pressure/geometric altitude, vertical speed, filed cruise, and go-around state.
The journal preserves the normalized values and order used live. Replay runs
without real wall-clock delay, but the tracker derives all observation credit
from the preserved simulator timestamps, rejects duplicates, and gives no
credit to gaps longer than three seconds. There is no replay-only cruise
predicate or aircraft-specific A350 branch.

Residual timing risk is low rather than zero: replay does not reproduce
dispatcher scheduling or a sample that failed before journaling. DAL9929
independently bounds that risk by demonstrating one continuous live SimConnect
connection, repeated sequential cruise transitions, a long pause/resume, and TOD
from the latest committed live cruise.

## Directly proven natively

DAL9929 directly demonstrated:

- Live iniBuilds A350 SimConnect integration.
- Initial observed cruise at FL350.
- Sequential live observed-cruise transitions FL350 to FL370 to FL390.
- Correct provenance of the second transition from FL370.
- Filed FL350 remaining immutable while observed cruise advanced.
- One TOD event derived from the latest observed FL390.
- Pause detection, suspension, and safe resumption after 1:51:33.
- One connection, 14,613 telemetry samples, and 7,920 valid non-paused
  positions.
- More than 2,048 live source observations without loss of the departure,
  outbound/return route shape, or current endpoint.
- A bounded 216-point, two-segment presentation trail.
- OUT, OFF, ON, and IN once each and in order.
- Generic two-engine A350 telemetry and lifecycle compatibility.
- Expected unsupported-enhanced-shutdown classification.
- Guarded pilot-confirmed Block In without a terminal hang.
- One completed PIREP and normal session closeout.
- No malformed journal record, sequence gap, crash, or recorded error.

## Proven through preserved native replay and automation

- Exact 54,061-sample DAL40 native replay through the production tracker.
- DAL40 provenance FL350 to FL370 to FL390 to FL410.
- Exact naturally occurring shallow-climb taper boundary that exposed the
  original defect.
- TOD from the corrected latest observed FL410.
- Nineteen native-style A350 cruise-profile and negative-protection checks.
- Return, descent, invalid telemetry, pause, slew, gap, go-around, expiry,
  duplicate-time, and incompatible-evidence protections.
- Fifteen-hour bounded flight-track behavior with origin, shape, and endpoint
  preservation.
- More-than-five-minute hidden-workspace accumulation in application-owned
  presentation history.
- Equivalent complete-track projection into newly created renderer epochs.
- Repeated workspace restoration, dormant expiry, Classic selection, fresh
  renderer creation, resource cleanup, and latest-hidden-state delivery.
- Overall automated acceptance: 2,878/2,878 passed, zero failures, two
  documented native-only skips.
- Historical replay set: 11/11 and 100,632 samples.
- Total recorded telemetry exercised: 154,693 samples.
- Map V2 browser/input/lifecycle: 208/208.
- Map restoration/resources: 157/157.
- Direct responsiveness: 50/50.
- Active-flight recovery: 74/74.
- Terminal/Block In: 203/203.
- Core: 194/194.

## Not repeated or independently proven in DAL9929

The individual native-review results remain unchanged:

- Vertical speed tapering below 500 ft/min before 750 feet of displacement
  after valid climb intent: **NOT REPEATED NATIVELY**.
- Accumulation while Operations was hidden for more than six minutes:
  **NOT INDEPENDENTLY PROVEN BY THE RETURNED PACKAGE**.
- Classic to Map V2 renderer recreation with complete-track restoration:
  **NOT INDEPENDENTLY PROVEN BY THE RETURNED PACKAGE**.

## Accepted residual risk

| Item | Residual risk | Basis for acceptance |
| --- | --- | --- |
| Exact shallow live boundary | Low | Exact natural DAL40 telemetry passes the same production tracker; deterministic boundary and negative tests pass; separate live A350 operation proves real-time integration, sequential commits, pause handling, and TOD consumption. |
| Hidden-workspace accumulation | Low | Application-owned history is independent of renderer visibility; dedicated hidden-history and workspace restoration checks pass; the long live session proves the new bounded flight-scoped history itself. |
| Renderer recreation | Low | A renderer consumes an immutable captured history rather than owning it; fresh-epoch equivalence, Classic selection, dormant disposal, recreation, and resource checks pass; no live evidence suggests failure. |

The accepted items are coverage gaps in the second native evidence package.
They are not evidence of failing production behavior and do not justify another
purpose-built A350 flight.

## Opportunistic future observations

These are non-blocking observational targets and require no dedicated flight:

1. During the next naturally shallow step climb, confirm intent survives a
   reduction below 500 ft/min before reaching 750 feet of displacement and that
   the new stable cruise is committed once.
2. During normal operations, leave Operations for more than six minutes and
   confirm that the returned blue trail includes the hidden interval.
3. During a normal flight, switch Map V2 to Classic and back to Map V2 and
   confirm that the complete sampled trail reconstructs.

Any contrary future observation reopens the specific affected behavior; it does
not retroactively convert the preserved passing evidence into failure.

## UX carry-forward

Retain the non-blocking terminal presentation improvement for unsupported
aircraft. Make the guarded fallback explicit with wording such as:

- `AUTOMATIC SHUTDOWN CONFIRMATION NOT AVAILABLE FOR THIS AIRCRAFT`
- `READY FOR PILOT BLOCK-IN CONFIRMATION`

This is presentation work only. It does not authorize an A350 enhanced shutdown
adapter or any change to terminal authority.

## Disposition

- Functional production defect found in DAL9929: **NO**
- Additional purpose-built A350 flight required for 7.25A: **NO**
- Production correction required: **NO**
- Frozen candidate remains unchanged: **YES**
- 7.25A: **CLOSED BY ENGINEERING ACCEPTANCE**
- 7.26: **CLEARED FOR A SEPARATE, EXPLICITLY STARTED CHECKPOINT**
- Premium UI work: **NOT STARTED BY THIS REVIEW**
