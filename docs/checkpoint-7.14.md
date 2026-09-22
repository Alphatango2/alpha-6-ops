# Checkpoint 7.14 — v0.19.0 (local)

Based on JST724 YMHB–YSSY on v0.18.2: 7,459 continuous telemetry samples;
OUT 09:31:53Z, OFF 09:41:00Z, ON 11:12:20Z, IN 11:16:56Z.

## Delivered

- UTC-only scheduled times in tracking, with existing separated columns.
- Timestamp ordering in the event list, including backdated milestones.
- Individual engine shutdown events retained; aggregate shutdown only used
  when individual engine telemetry is unavailable.
- Neutral “Off planned route” wording accommodates possible vectors without
  claiming A6OPS knows ATC clearance.
- Early ETA blends the actual-OUT-adjusted planned block time into live speed
  over the first 20 observed airborne minutes. Late joins retain live estimates.
- Taxi allowance anchored to first observed taxi-in, with conservative updates
  if taxi takes longer. Actual IN remains the confirmed block-in time.
- Assign gates from Dispatch or Flight Tracking during flight, preserving the
  tracker, route, SimBrief plan and recovery state. Compact values, blank unknowns.
- Removed arbitrary exact-gate catalog suggestions. Explicit SimBrief notes
  and dispatch entry remain available.
- Retained v0.18.2 SimConnect discovery and packaged local SDK configuration.

## Deferred dependency

Live real-world gate lookup requires a selected provider, credentials and
verified Australian/NZ coverage. No live lookup or terminal inference is
implemented in this release; manual dispatch entry is available now.

## Validation

- 103 core checks, including five-hour ETA, UTC midnight and extended taxi.
- Six real-flight replay assertions passed, 54,601 samples: JST221, QFA124,
  JST786, QLK1579 (A220), JST721 and JST724.
- Desktop smoke and tracking integration passed, including gate persistence
  without progress reset and timestamp sorting.
- Tracking layout checked at 1100, 1280, 1366 and 1920 DIP.
- Installer extraction and packaged desktop smoke passed. Packaged SimConnect
  probe loaded the native SDK and reached the connection call; no loaded MSFS
  flight was available (0x80004005), so live telemetry was not verified today.
- Local installer: outputs/Alpha6OPS-Setup-0.19.0.exe. Portable ZIP and SHA256
  file are alongside it. Nothing pushed to GitHub.

Next validation is the user's five-hour A220 flight. Recorded A220 replay and
synthetic long-flight checks do not substitute for that live flight.
