# 7.24B — parked native connection/reconnect gate

**STOP / HOLD:** the installed R2A native attempt exposed a repeating connected Operations layout flicker, reproduced with both Classic and Map V2 and stopped by disconnect. [Failure record](checkpoint-7.24b-layout-oscillation.md). The procedure below is retained as the original handoff, not authorization to continue it. No more reconnect cycles or real-flight validation until the defect is understood, corrected and physically retested.

Use installed **S4-7.24B-VISUAL-R2A-20260927-87fa792a7c29**, version 0.22.0. Its input and first-paint physical retests passed. Both native gates are **PENDING**. Keep the aircraft parked throughout this short test; complete this before starting the validation flight. No rebuild, persistent Map V2 override, synthetic telemetry or profile reset is needed.

## Preparation

1. Launch MSFS normally and load the aircraft you intend to use at a gate/stand, parking brake set. Record aircraft subtype/add-on and simulator version where available.
2. Open Sierra 4 and confirm it initially shows **Disconnected**. Verify the existing profile/history/gates/settings are readable. Classic should load by default; manually opt into Map V2.
3. Note the actual UTC or local time and timezone. Test display already reported: 3440×1440, 100% scaling. Record any different display/scaling used now.

## Native lifecycle

1. Click the simulator connection badge or **LAUNCH / CONNECT TO MSFS 2024** in Flight tools.
2. Allow roughly 30 seconds of live telemetry. Confirm Connected, correct aircraft identity and plausible parked position/heading. Capture a screenshot with connection state and aircraft/telemetry.
3. While connected, clicking the connection badge opens Flight tools; select **DISCONNECT**. Observe the disconnected state and that current telemetry stops updating rather than pretending to remain live. Record any error, freeze or repeated connection attempt.
4. Reconnect normally when Connect becomes available. Confirm one stable current aircraft/telemetry stream.

## Controlled rapid reconnect

1. Still parked, disconnect again and reconnect promptly once the Connect control is available. Do not force disabled controls or repeatedly click while an attempt is pending.
2. Repeat once more. Allow about 10–15 seconds after each successful connection to observe state, telemetry and any duplicate events or unexpected aircraft/position reversion.
3. With the final connection stable, switch Operations → Flight Tracking → Operations. Confirm the connection stays up. Try available aircraft/airport/route selection, Follow/Unfollow, World/Home, Focus and Layers; report unavailable objects explicitly. Record any hitch using A (none), B (minor), C (roughly 0.5–1 second) or D (roughly 1+ second/disruptive).
4. Disconnect normally at the end so the journals finalize. Leave the aircraft parked and do not start the primary flight yet.

## Return evidence and stop

Report aircraft, approximate start/end times, normal connection/disconnection result, both prompt reconnect results, and any error or visible hitch. Logs save automatically under `Alpha6OPS/TestLogs`; Settings → Logs & Diagnostics also offers **EXPORT TEST LOG**. Preserve all logs, including failed attempts. Codex will audit the journals and current status before the flight gate.

Native open/close evidence must come from this installed session. User-observed stability does not by itself prove native worker disposal, connection generation correctness or rejected stale callbacks. The audit will distinguish logged evidence, control-flow corroboration and any remaining instrumentation limits. No new pass will be inferred from earlier synthetic tests, and no production code will be changed during the validation session.

If connection fails, a map fallback occurs, telemetry duplicates/reverts or the application hangs, stop and report the sequence/time. Keep that result as evidence; do not reinstall or alter profile data to obtain a pass.
