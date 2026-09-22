# Checkpoint 7.13 — local version 0.18.0

Built from the existing local 7.12 working tree; no GitHub push or merge.

## Corrections

- Progress uses a shared core service with cumulative route distance and spherical segment projection. It no longer depends on opening the map or its tessellation points. Duplicate/short legs cannot lock the forward search. A distance-based continuity corridor limits jumps across intersecting legs; reconnect can reacquire position, and large off-route excursions retain the last reliable fraction.
- The hero, tracking footer, map colouring and remaining distance consume the same progress value. The hero and footer use the same published gate-IN estimator. Estimates use simulator UTC, round to minutes, change when the new estimate differs by at least two minutes, and include a five-minute taxi-in allowance. Actual IN supersedes estimates. No live ETA is invented from scheduled duration before usable airborne data.
- Connecting after departure leaves actual OUT unknown; it does not turn connection time into a departure milestone. Confirmed IN can still be displayed for a late-joined flight.
- Approach/final no longer require a fixed progress percentage. Prior descent or destination proximity with evidence of arrival supplies context. The phase display uses the event monitor's phase interpretation.
- Without a planned cruise altitude, sustained level flight requires 120 samples and is explicitly labeled as observed level flight, not a confirmed planned cruise level. The QFA124 departure level-off no longer fires the false cruise event in replay. Newer logs with planned cruise and pressure altitude retain those stronger inputs.
- Gear retraction follows movement through intermediate values to a stable up indication; extension follows a stable down indication. Milestone event timestamps use milestone onset instead of later confirmation time. Airborne trip-fuel comparison starts at takeoff, rather than including taxi-out fuel.
- Scheduled UTC and airport-local values occupy separate lines. Four columns over two rows give each footer field room. Planned route is yellow, completed planned route grey, actual trail cyan; interpolation retains short-leg endpoints and the colour boundary follows route distance.
- Aircraft identification events and new history records use canonical aircraft display names; raw simulator titles remain in telemetry for diagnostics.
- Journals now include computed tracking-state snapshots to diagnose future progress and ETA reports.

## Validation

- Core console suite: 99 checks passed, including new geographic progress, duplicate legs, date-line crossing, intersection, reconnect, pause/stale telemetry, UTC midnight and arrival-estimate cases.
- Recorded component replay: all five supplied flight journals, 47,142 telemetry samples, passed assertions for advancing progress, bounded jumps, approach/final and block-in. JST721 additionally checks gear events and matching milestone timestamps; QFA124 checks the departure level-off regression.
- Production WPF orchestration: native-style readings with no supplied progress advance the hero before opening the tracking page. On navigation both screens agree on progress and gate-IN estimate. A late connection leaves actual OUT blank.
- WPF smoke suite exercises startup, navigation, recovery, identity, replay, history, PIREP persistence and existing window flows. Time fields were checked for non-overlap at 1920, 1366, 1280 and 1100 logical-pixel window widths. Existing responsive tests also cover simulated display scaling; these are not claims of testing every physical monitor configuration.

Replays test the current progress/ETA/phase/event components against captured inputs, not a new live SimConnect session or every add-on capability. Five-minute taxi-in is an estimate, not airport-specific ground routing. Missing historical cruise/pressure data cannot be reconstructed from geometric altitude. A new live test flight remains the next validation step.

## Local update

Exit OPS from the app or tray, run `outputs/Alpha6OPS-Setup-0.18.0.exe`, and confirm v0.18.0 in the footer. A portable alternative is `outputs/Alpha6OPS-Desktop-0.18.0-win-x64.zip`. The installer preserves existing pilot data; validation extracts into an isolated workspace rather than updating the user's installation automatically.

Replay entry point: `Alpha6OPS.exe --replay-journals <manifest.json> <output-directory>`. The manifest is a JSON array of local JSONL paths. Results include a summary and per-flight event/snapshot reports. Any failed replay assertion exits unsuccessfully.
