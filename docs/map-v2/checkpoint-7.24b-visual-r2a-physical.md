# 7.24B VISUAL R2A — physical retest result

**Eight input checks PASS; first-paint terminator retest PASS. GO for parked native connection/reconnect validation. Neither native gate is closed yet.**

The user reported all eight installed physical checks passed: left pan, wheel/buttons, available-object selection, right rotation, outside scrolling, Escape/Tab and return, resize/minimize/restore, and workspace/menu interaction followed by dragging. No input/focus regression was observed. The original terminator response retained both checklist alternatives, so it was not treated as an unambiguous pass. The user's subsequent clarification explicitly states: **“PASS — no incorrect first frame or snap.”** This closes the first-paint physical correction gate for the reported required transitions.

Actual user-reported display: **3440×1440 at 100% Windows scaling**. Monitor count and cross-monitor/mixed-scaling behavior were not supplied and remain unestablished. Selection coverage is for available objects; absent live aircraft or route object types are not silently marked passed. Follow/Unfollow and live aircraft behavior will be observed with native telemetry.

Read-only audit at approximately **27 September 2026, 00:49:51 UTC** confirmed **514/514** installed files still match `S4-7.24B-VISUAL-R2A-20260927-87fa792a7c29`. No OPS/MSFS processes were running at the audit, and no Map V2 startup override was set. All **26 reporter files** are unchanged, with no new, changed or missing reports.

Saved-data comparison: **217/219** files byte-identical, with zero files added or missing. The two changed files were investigated without editing the profile:

- `Alpha6OPS-logs.sqlite`: schema and SQLite integrity checks pass. Of the existing index entries, 169 changed only their `indexed_utc` timestamps. Two entries were added for JSON/JSONL logs created on 25 September at 22:36 UTC. Both underlying files existed in the pre-test manifest and remain byte-identical; these are newly indexed prior logs, not new failures from this retest.
- `program-status.json`: process ID, last-exit time and connection status changed. It records normal `application_exit` at `2026-09-27T00:48:58.8283129Z`, disconnected, zero samples.

Flight history/recovery, assignments, settings, gates and remaining saved files are unchanged. No repair, restore, rebuild or installation was performed during this audit. The original left-drag failure, INPUT R1 visual failure and R2 candidate test-fixture failure remain preserved separately.

Proceed to [the parked native gate](checkpoint-7.24b-native-connection-r2a.md) using this exact installed build. This authorizes evidence collection for `NATIVE_CONNECTION_LIFECYCLE` and `NATIVE_RECONNECT`, not a declaration that they pass. Classic stays default; Map V2 stays opt-in. Real-flight, recovery, PIREP/history, longer responsiveness observation and final classification remain pending. 7.25 has not begun.

Compact evidence: `evidence/7.24b-visual-r2a-physical/`. Raw snapshots and database copies: `work/checkpoint-7.24b-first-paint-r2a/physical-20260927-004950/`.
