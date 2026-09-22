# Checkpoint 7.17 — QLK1274 tracking corrections and Dispatch polish

Status: proposed scope; not implemented or packaged. Evidence is documented in
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
- Package only when implementation is requested and the checks pass.
