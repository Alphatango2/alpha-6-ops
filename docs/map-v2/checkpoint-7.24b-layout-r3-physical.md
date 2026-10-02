# 7.24B — LAYOUT R3 parked physical stability result

Recorded **2026-09-27 02:21 UTC** against installed frozen build **S4-7.24B-LAYOUT-R3-20260927-666f495dce7e**, version 0.22.0.

**PASS — the user reports stable Operations while connected with either renderer, during renderer switching, and while disconnected.** The prior VISUAL R2A connected layout failure remains FAIL in its original evidence. This is a separate corrected-build result.

## Physical evidence

The user's report:

> Classic: PASS  
> Map V2: PASS  
> Disconnected stability: PASS  
> No performance issues with the program connected and disconnected and when switching between classic and Map V2.

This satisfies the correction-specific parked comparison. Absence of visible flicker is the user's physical observation, not inferred from logs. Earlier display context was 3440×1440 at 100% scaling; no new resolution/scaling or monitor-count confirmation was supplied. The broad stability statement is not counted as an individual repeat of every input, first-frame, resize or multi-monitor check.

## Read-only installed/session audit

- **514/514** installed payload hashes still match the frozen R3 manifest, with no missing/mismatched payload files.
- The running process is the installed `Alpha6OPSPreview/Alpha6OPS.exe`, PID 16280. MSFS 2024 PID 26184 remains running; neither application was launched, closed or connected by this audit.
- Captured journal: `Alpha6OPS-20260927-021457-cce4c9b99de64a7b9803fc3e57a61fbd.jsonl`.
- Session: **02:14:57–02:15:37 UTC on 27 September 2026** (21:14:57–21:15:37 CDT on 26 September).
- **85 records**, contiguous sequence; **1 session start, 1 connection opened on attempt 1, 40 telemetry samples, 40 tracking states, 1 assignment, 1 identification, 1 session end**.
- All 40 samples are on ground, parking brake set, phase `AtGate`; raw simulator aircraft title is `FenixA320 IAE SL`. Recorded and simulator sample times both strictly increase; median recorded interval **1.000118 seconds**. No duplicate sample timestamp or logged error event.
- Final record is `session_ended`, reason `connection_closed`. Captured application status is **Disconnected**, 40 samples, no last crash report. No telemetry follows the close in this finalized journal.
- All **26 existing CrashReports files** are unchanged; no new reporter file. Of the 224 pre-session profile files, 220 hash unchanged, none are missing, and two live state/status JSON files changed. The two databases and four new SQLite sidecars are held open and could not be hashed. The remaining new files are the current JSON/JSONL session pair. No full live database integrity claim is made; no locks were bypassed and no profile data was edited.

The assignment is outside its 18-hour departure window and identification correctly records `FREE FLIGHT` / review required. This does not block a parked connection test. A current dispatch release and correct schedule/date must be verified before the later validation flight; the audit has not changed the saved assignment.

Raw captured files, hashes, process identity and live profile inventory: `work/checkpoint-7.24b-layout-oscillation/physical-20260927-022035/`. Compact audit evidence: `docs/map-v2/evidence/7.24b-layout-r3-physical/`.

## Limits and next gate

The journal does not emit map-selection timestamps, WPF dimensions, native-worker disposal, connection generation or stale-callback rejection. The first two are not retroactively fabricated; native internal guarantees are not certified by this visual pass. Controlled rapid reconnect remains **PENDING**, as do the full flight, PIREP/recovery and remaining pilot checks.

**GO for the [controlled parked native reconnect gate](checkpoint-7.24b-native-connection-r3.md) using this exact installed R3 build. Full flight remains HOLD.** No source, installer or frozen archive was modified or rebuilt for this audit. Classic remains default and Map V2 remains opt-in.
