# 7.24B INPUT R1 — installed identity and saved-data verification

**Installed identity PASS; saved-data preservation PASS; physical input retest PENDING; native MSFS HOLD.** Verified 25 September 2026 at approximately 22:02 UTC after the user reported “Updated.” No application or simulator was launched by this verification.

The installed directory `C:\Users\DanniBaLs\AppData\Local\Alpha6Designs\Alpha6OPSPreview` matches **514/514** expected payload hashes for `S4-7.24B-INPUT-R1-20260925-62b8c871dd05`, source `62b8c871dd0558dd3deed0baa5024521444b00f6`. No payload files are missing or mismatched. This establishes R1 identity rather than relying on the unchanged version number or executable apphost.

The raw pre-reinstall saved-data comparison returned **215/217 byte-identical**, no missing files and no new files. Its non-identical result is retained without reinterpretation. The two differences were investigated read-only:

- `Alpha6OPS-logs.sqlite`: the same diagnostic-index schema and 171 entries remain present. Only `indexed_utc` timestamps changed. Both before/after copied databases pass SQLite integrity checks; the underlying log/report files are unchanged.
- `program-status.json`: only `processId` and `stoppedAtUtc` changed. The new record reports a clean application exit at `2026-09-25T22:00:41.9976438Z`, disconnected, with zero samples. This is evidence of a brief intervening application session, not a physical-input pass.

Flight history, active-flight recovery, gates, settings and all other saved files match the snapshot exactly. The two changed files contain diagnostic reindex/last-exit bookkeeping; no repair or restore was needed. The original hash-verified backup remains intact.

The pre-physical inventory found **no running OPS/MSFS process**, no process/user/machine `SIERRA4_MAP_V2` startup override, and **26 unchanged reporter files**, with none added. Classic remains default and Map V2 remains opt-in.

Proceed only with the [installed physical retest](checkpoint-7.24b-input-r1-retest.md), keeping MSFS closed. User observation must establish pan, wheel/buttons, selection, right-drag rotation, outside page scrolling, focus escape/return, resize/restore and WPF/WebView integration. Missing selectable objects remain unavailable rather than passed. No native gate is closed by this inspection.

Compact evidence is under `evidence/7.24b-input-r1-installed/`; raw read-only comparison evidence is under `work/checkpoint-7.24b-input-correction/installed-20260925-170151/`. The original build's failed physical result, both frozen packages and their handoff artifacts remain unchanged.
