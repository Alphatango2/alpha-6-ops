# Checkpoint 7.15 — v0.20.0 (local)

## Delivered

- Observed OUT and IN no longer depend on schedule acceptance. A plausible
  candidate assignment stays visible on both pages with an explicit review
  state. An unrelated route/origin still falls back to observed free flight.
- Schedule-review action compares dated simulator UTC with planned UTC. Dispatch
  can correct OUT/IN dates and times without resetting milestones or progress.
  Corrections retain original values in the journal. Untrusted schedules no
  longer supply ETA baselines or early/late claims in the tracking footer.
- PIREP payload includes observed OUT/OFF/ON/IN and pilot block-in confirmation.
- Optional per-engine fuel flow (lb/h), N1/N2 (%), starter and engine-count
  requests are isolated from mandatory SimConnect telemetry. Optional request
  exceptions cannot terminate the main connection. Raw readings and assessed
  engine states are journaled with source information.
- Generic engine assessment distinguishes unknown, starting, running, stopping
  and off, and supports repeated cycles and four engines. Fuel flow alone does
  not establish running. Never-observed all-zero sensors remain unknown.
- Unknown engine state cannot automatically complete a flight. After landing,
  a sustained stationary/braked aircraft offers Confirm Parked when automatic
  engine/destination verification is incomplete. Moving again clears that
  eligibility. Destination checks prevent automatic completion at another airport.
- Map follow is a highlighted toggle, preserves zoom and survives page changes.
  Dragging or fitting the route exits follow.
- Speed-only runway-vacated text is now Landing rollout slowed.
- Flap events describe handle selection. Capture includes optional handle
  index/count, left/right flap and slat angles. Percentage-derived labels are
  marked inferred in event details; they do not imply physical travel completion.
- Dispatch has inline departure/arrival gate fields above the filed route.
  Acceptance persists them; active-release Save Gates changes them without
  resetting the flight. Tracking's Assign Gates button is removed.
- Local gate history supplies optional suggestions by airport, airline and
  aircraft. Saved Gates supports correction/deletion. Blanks and NA are not
  treated as gate identifiers. No external data subscription is used.
- Duration formatting no longer rounds the hours up before displaying minutes.

## Verification and limits

- 113 core checks passed.
- Packaged desktop smoke and tracking integration passed.
- All seven recorded-flight replay checks passed (70,295 samples).
- Installer diagnostic extraction passed without registry changes.
- Packaged SimConnect probe resolved the SDK and reached the connection call;
  MSFS had no active connection (0x80004005), so zero live samples were received.

Core checks cover four-engine fallback, fuel-only evidence, repeated starts,
unknown sensors and completion guards. Desktop integration checks exercise the
production rejected-assignment path, actual-time display, schedule correction,
gate persistence, map follow, and pilot-confirmed block-in. Dispatch screenshots
were inspected at 1100 and 1366 DIP; captures also cover 1920 DIP. Existing
tracking layout checks cover 1100, 1280, 1366 and 1920 DIP.

The seven historical journals contain 70,295 telemetry samples. Their replay
checks retain old-schema compatibility and verify phase/event/progress behavior;
they cannot validate alternate sensor readings absent from the original logs.
Production UI integration uses targeted scenarios rather than rendering all
70,295 samples. Live A220 alternate engine signals and actual flap/slat values
remain unverified until a simulator capture. Unsupported fields stay unknown.
Four-engine synthetic coverage does not establish live A380 compatibility.

The next flight should start with a short A220 gate session: connect before
starting, start each engine, select/retract flaps, and verify the journal's new
sensor fields before committing to another long test. Dates must still be
reviewed by the pilot; A6OPS does not silently rewrite the simulator clock.

Installer: `outputs/Alpha6OPS-Setup-0.20.0.exe`. Portable package:
`outputs/Alpha6OPS-Desktop-0.20.0-win-x64.zip`. Existing user data is retained by
the normal installer update flow. Nothing is pushed to GitHub.
