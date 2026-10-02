# 7.24B — installed LAYOUT R3 native reconnect results

Recorded **2026-09-27 02:28 UTC**, installed **S4-7.24B-LAYOUT-R3-20260927-666f495dce7e**, version 0.22.0, source `666f495dce7efb3ccd70b99939501144d91ab606`.

**Physical reconnect 1: PASS. Physical reconnect 2: PASS. Connected workspace switching: PASS.** These are the user's explicit results for the R3 procedure. They supplement the earlier R3 parked Classic/V2/disconnected stability pass; no original, INPUT R1 or VISUAL R2A failure is overwritten.

## Captured native session evidence

All three journals are from the same installed process (PID 16280, started 02:14:04 UTC), while MSFS 2024 PID 26184 remained running. Installed payload verification again matches **514/514** files.

| Connection | Journal time, UTC on 27 September | Telemetry samples | Logged native open | Final record |
|---|---|---:|---|---|
| Initial connection | 02:24:49–02:25:02 | 12 | Once, attempt 1 | `session_ended`, `connection_closed` |
| Reconnect 1 | 02:25:09–02:25:39 | 30 | Once, attempt 1 | `session_ended`, `connection_closed` |
| Reconnect 2 | 02:25:50–02:26:16 | 26 | Once, attempt 1 | `session_ended`, `connection_closed` |

There are **151 journal records and 68 new real telemetry samples**. Each journal has a contiguous record sequence, one start, one open, one end and no logged connection error. Journals do not overlap. Close-to-next-journal-start gaps are **7.508250 s** and **10.253174 s**; this was the instructed controlled reconnect through available controls, not an injected overlapping-callback stress test.

All samples remain on ground with parking brake set, `AtGate`, aircraft title `FenixA320 IAE SL`, registration `VH-YXV`, near YBCS. Recorded and simulator sample times strictly increase, including across connection boundaries; positions remain stable. Median cadence within the three connections is 1.000302 / 1.000300 / 1.000294 seconds. No telemetry follows a session-end record. Captured status is **Disconnected**, **108 cumulative samples**, matching the earlier 40 plus these 68.

All **26 existing CrashReports files** are unchanged, with no new, missing or changed report. No application, simulator, connection or profile data was modified by the audit. Raw JSON/JSONL pairs, status, recovery snapshot, process identity, hashes and report inventory were captured separately under `work/checkpoint-7.24b-layout-oscillation/native-reconnect-20260927-022726/`. Compact evidence is in `docs/map-v2/evidence/7.24b-native-r3/`.

## What this establishes and what remains unobserved

- **Physical/native behavior passes for the tested sequence:** native opens, real parked telemetry, intentional close, two prompt reconnects and user-observed stable workspace switching. No logged automatic retry, overlapping diagnostic session or duplicated telemetry timestamp was found.
- Three journals are expected for three intentional connections; they are not three completed flights. No flight/PIREP was completed here, so this is not evidence of post-flight history uniqueness or active-flight continuity through recovery.
- Exact frozen source corroborates lifecycle ordering: `FlightSessionCoordinator.Connection.cs` prevents another connect while its cancellation source exists, awaits `simulator.RunAsync`, rejects callbacks unless cancellation identity and generation are current, and invalidates the generation on disconnect. `SimConnectSource.cs` calls native `Close(handle)` in `finally` before its task completes. The logged session close occurs after that awaited task returns. These are source-backed inferences, distinct from direct native instrumentation.
- The build does **not** log native close return values, worker/handle identities, generation numbers or actual stale-callback rejection. We cannot certify zero orphan native handles or demonstrate an obsolete callback being rejected from these journals. These remain explicitly limited evidence, not invented passes. Parked stationary data also cannot exercise every stale-position rollback scenario.
- Workspace navigation timestamps are not logged. The user's physical pass is authoritative; no additional open/retry is present in the final journal. No new per-control, DPI, resize or long-duration result is inferred.
- The pre-install suite's two native skips remain historical skips; they are not changed to automated passes or added to its assertion totals. Manual native results now have their own evidence.

## Decision

**GO for one controlled real validation flight on this exact R3 build, after the normal dispatch checks in the [flight handoff](checkpoint-7.24b-flight-r3.md).** The saved JST941 assignment is still outside the accepted 18-hour departure window; use a current reviewed release before flight. No data was corrected to force acceptance.

Full-flight milestones, real recovery, PIREP/history persistence, longer Map V2 observation and final checkpoint classification remain pending. Native internal evidence limits above remain open in the final report. Classic stays default, V2 opt-in. This does not complete 7.24B or authorize default enablement/7.25.
