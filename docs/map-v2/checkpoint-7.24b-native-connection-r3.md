# 7.24B — LAYOUT R3 controlled parked reconnect gate

**Completed follow-up:** the user reports both reconnects and connected workspace switching PASS. The [separate native result and audit](checkpoint-7.24b-native-r3-results.md) records 68 new samples across three orderly connection journals, unchanged 514-file installed identity and no new reports. GO for the [controlled real-flight stage](checkpoint-7.24b-flight-r3.md) after reviewing a current assignment. The procedure and then-pending statements below are preserved as the original handoff; do not repeat it unnecessarily.

Use installed **S4-7.24B-LAYOUT-R3-20260927-666f495dce7e**, version 0.22.0. The [connected Classic/V2 and disconnected physical stability comparison passed](checkpoint-7.24b-layout-r3-physical.md). The first native open/telemetry/normal close is already recorded; do not redo the installer or that comparison. The earlier R2A native failure and paused procedure remain separate historical evidence.

Keep the aircraft parked with the parking brake set. No flight, engine test, profile reset, reinstall or build change is needed. Use Map V2 for this gate; Classic remains the default for ordinary startup.

1. Starting disconnected, connect once and allow 10–15 seconds. Confirm the correct aircraft, plausible parked position and stable Operations UI.
2. **Prompt reconnect 1:** disconnect, then reconnect promptly once Connect is available. Click once per action; do not force disabled controls. Observe for 10–15 seconds. Confirm no error, repeated connection attempt, duplicate aircraft/events, stale position reversion or layout flicker.
3. **Prompt reconnect 2:** repeat that disconnect/reconnect once, then observe for another 10–15 seconds.
4. With the final connection stable, switch Operations → Flight Tracking → Operations. Confirm that telemetry remains live and the connection stays up, with no layout flicker. Disconnect normally at the end so the session logs finalize.

Stop and disconnect if flicker, a hang, error, map fallback, duplicate telemetry or position rollback appears. Preserve the result and do not repeat attempts to obtain a pass.

Report **Reconnect 1: PASS/FAIL; Reconnect 2: PASS/FAIL; workspace switching: PASS/FAIL**, plus any visible hitch or error. Note approximate time and any aircraft or display change; logs save automatically. The existing stale dispatch schedule can remain as-is for this parked test and will be reviewed before flight.

Codex will audit the resulting native session journals and reports before clearing the flight gate. Native open/close, observed telemetry continuity and physical stability must be distinguished from internal worker/generation guarantees that the installed build does not directly log. The two native automated skips are not silently converted to passing assertions. **Full-flight validation remains HOLD until this result is reviewed.**
