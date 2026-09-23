# Checkpoint 7.16 — JST760 corrections and Dispatch cleanup

Status: implemented locally as v0.21.0. No GitHub publication.

## Flight tracking

- A32x flap handles map to `UP`, `1`, `1+F`, `2`, `3` and `FULL`, including
  add-ons that report indexes 0–5 with an inconsistent position count.
- Agreeing left and right flap angles confirm physical surface movement in the
  event detail. Slats remain unconfirmed when no usable slat movement exists.
- A previously confirmed running engine enters a debounced shutdown state when
  combustion stops and spool/oil signals decay, even while residual fuel-flow
  telemetry remains above the final-off threshold.
- Submit PIREP requires both Complete phase and recorded actual IN. Successful
  closeout clears the active Dispatch release immediately.
- Initial preflight events omit fuel while loading can still be in progress.
  Parking-brake events say `Parking brake set at stand` until block-in confirms
  flight completion.

## Dispatch

- Draft and active releases show the catalog airport name beneath each ICAO
  code, with the same uppercase unavailable fallback as the dashboard hero.
- Gate entry is a compact responsive panel with explicit saved-gate dropdowns,
  `SAVE ASSIGNMENTS` and `MANAGE SAVED GATES` actions.
- Confirmed assignments accumulate a local use count. After at least two uses
  for the same airport, airline and operation, A6OPS can recommend the best
  matching gate, prioritizing aircraft type, frequency and recency.
- Recommendations are labeled `LOCAL HISTORY`, never fill the field silently,
  and require the pilot to choose `USE`. They do not claim live availability.

## Validation

- Core suite expanded from 116 to 117 checks with residual-fuel shutdown decay.
- Desktop smoke and integration coverage verify airport names, PIREP readiness,
  atomic closeout, event wording, learned-gate confidence and pilot confirmation.
- Untouched JST760 journal replay checks 10,552 samples, actual IN, A32x Flaps 2
  surface confirmation, shutdown transitions, stable progress and event wording.
