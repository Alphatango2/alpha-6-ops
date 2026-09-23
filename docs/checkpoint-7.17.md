# Checkpoint 7.17 — QLK1274 tracking corrections and Dispatch polish

Status: implemented and packaged as v0.22.0 on `codex/checkpoint-7.17`. Evidence is documented in
[QLK1274-flight-review.md](QLK1274-flight-review.md).

## Required corrections

1. **Route reacquisition and progress:** recover from changed departures, direct
   routing and route excursions without becoming permanently tied to an old
   segment. Preserve intersection, pause and reconnect protections. Keep hero,
   tracking, route styling, distance and journal snapshots consistent, including
   at completion. QLK1274 must advance during flight instead of jumping from
   0.11% to 100% at block-in.
2. **ETA reliability:** consume reliable remaining distance; identify or suspend
   estimates and early/late claims when route matching is stale. Assert sensible
   approach distance and ETA convergence using the QLK1274 journal.
3. **Assignment evidence:** distinguish absent simulator registration from
   conflicting identity. Use adequate independent context or an explicit pilot
   association where needed. Explain unresolved evidence clearly and give
   assignment review consistent styling independent of schedule colors.
4. **A220 flap events:** handle the observed indexes 0–5 with reported count 5;
   use clear selected-configuration titles and supported labels. Remove repeated
   surface-confirmation diagnostics from the normal event list. Preserve raw
   measurements and do not confuse matched left/right angles with a completed
   movement to the selected position.
5. **Schedule semantics:** retain scheduled IN separately from SimBrief's
   estimated IN. Compare arrival performance against the schedule and use the
   dispatch estimate as an estimate. Preserve UTC dates and source values.
6. **Engine evidence wording:** label shutdown according to evidence actually
   used; do not claim combustion loss when combustion was never observed.
   Retain the successful per-engine sequence and debounce behavior.
7. **Dispatch airport alignment:** align each airport name with its ICAO code,
   preserving role labels, spacing and long-name wrapping in draft and active
   releases.
8. **Gate Assignments heading:** improve size, weight and contrast so it clearly
   reads as a section heading while retaining compact gate controls.

## Release gate

- Untouched QLK1274 production replay passes, with advancing airborne progress,
  correct actual milestones and credible remaining distance near destination.
- Full presentation checks agree with journal state; terminal overrides cannot
  hide a frozen tracker. Estimated and scheduled arrival remain distinct.
- Relevant earlier A220, A32x and Boeing replays and focused edge-case checks
  pass. Validate continuous rejoin after skipped departure legs, ambiguous route
  intersections, late connection, reconnection and midnight dates.
- Inspect Dispatch at normal and narrow widths with long airport names.
- Package after implementation and verification; publish the branch for review.

## Validation and limits

- 122 core checks cover continuous departure shortcuts, route intersections,
  pause/reconnect, unavailable distance, UTC midnight, identity guards and engine
  evidence. Nine untouched journals (91,366 telemetry samples) pass production
  component replay: JST221, QFA124, JST786, QLK1579, JST721, JST724, JST760,
  QLK1274 and ANG2.
- QLK1274 airborne progress reaches 99.92% instead of remaining at 0.11%. Its
  largest sample advance is 0.54 percentage points. Final approach reports 7 NM
  remaining and ETA 23:20Z; actual IN remains 21 September 23:22:22Z.
- Native presentation checks exercise hero/footer agreement after a bypassed
  departure, route excursion/rejoin, missing registration, completion distance,
  telemetry loss/reconnect and paused disconnect. Dispatch captures at 1920,
  1366 and 1100 DIP preserve airport alignment and compact gate controls.
- The source review identified a stale ETA after disconnect; the final confidence
  gate also checks telemetry availability and preserves confirmed actual IN.
  Short reconnects preserve the established estimate and taxi/airborne clocks.
- The packaged executable passes the desktop/integration checks and all nine
  replays using its bundled .NET 10.0.11 runtime. The portable ZIP's application
  and core assemblies match the tested directory. Installer and ZIP SHA-256
  checksums are recorded alongside the local outputs. The installer was built,
  but was not run against the pilot's active installation.
- Existing saved plans retain their original imported times. Refresh from
  SimBrief to acquire distinct scheduled and estimated IN fields. Historical
  journals are replayed unchanged, so their original schedule baseline remains.
- Replay and native synthetic checks do not replace a new simulator flight or
  establish aircraft-specific sensor calibration. Flap events describe selection;
  raw surface/engine measurements remain in diagnostics. Gate suggestions remain
  local history, not live airport availability.
- This publication also includes previously local checkpoints 7.13–7.16 as a
  separate baseline commit. Private journals and generated installers are not
  included in the Git source branch.
