# Checkpoint 7.25A implementation plan

Spec: `docs/flight-tracking/checkpoint-7.25a-spec.txt`

## Stage 1 — baseline and boundaries

- Preserve the frozen 7.25 candidate and DAL40 evidence unchanged.
- Record entry status and relevant source hashes.
- Confirm the session-owned presentation service is downstream of snapshots and has no
  command, persistence or lifecycle authority.

## Stage 2 — flight-scoped flown track (RED/GREEN)

- Add failing model checks for a 15-hour one-second stream, endpoint retention, bounded
  payload, stationary preflight, hidden accumulation, renderer recreation, dateline and
  interruption segmentation.
- Implement a presentation-lifetime history owned by the application presentation,
  sampled at a bounded interval with live endpoint replacement and bounded compaction.
- Pass immutable history into projection; remove renderer-owned history accumulation.
- Run map model, pump, browser/lifecycle, restoration and scheduling gates.

## Stage 3 — native step-climb intent (RED/GREEN)

- Add the three DAL40-derived profiles and negative cases before production changes.
- Demonstrate FL370 and FL410 fail under the frozen predicate.
- Implement a bounded two-stage intent/displacement/stability model.
- Verify sequential FL350, FL370, FL390, FL410 provenance and TOD from FL410.
- Verify invalid data, gap, pause, slew, return, descent, go-around and recovery behavior.

## Stage 4 — targeted integration

- Run observed-cruise/TOD, active recovery, Map V2 scheduling/restoration/direct
  responsiveness, terminal/generic Block In and Logbook gates.
- Add read-only DAL40 assertions for evidence identity and expected native observations.

## Stage 5 — combined acceptance

- Build Release and run the complete regression suite.
- Run 11/11 historical replays with telemetry counts kept separate from synthetic and
  DAL40 evidence assertions.
- Review the final diff for scope and operational-authority isolation.

## Stage 6 — freeze and handoff

- Only after every automated gate passes, freeze a separately identified 7.25A package,
  archive hashes and prepare the minimum native procedure.
- Native procedure requires one realistic shallow A350 step transition unless evidence
  shows a second is essential; it also exceeds the former trail window, hides Operations
  for >5 minutes, recreates the renderer, verifies the complete sampled trail and checks
  TOD from the newly established level.
- Do not install the candidate.
