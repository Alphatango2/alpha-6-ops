# 7.24B — LAYOUT R3 installed retest

**Follow-up:** installation and the [connected/disconnected physical stability comparison](checkpoint-7.24b-layout-r3-physical.md) have passed. Do not repeat the upgrade. The next step is the [controlled parked reconnect gate](checkpoint-7.24b-native-connection-r3.md); full flight remains HOLD. The original staged procedure is retained below.

Use **S4-7.24B-LAYOUT-R3-20260927-666f495dce7e**, version 0.22.0. This revision addresses the repeating Operations table/scrollbar layout reset. It preserves INPUT R1 and VISUAL R2A fixes. Classic remains default and Map V2 stays opt-in.

**2026-09-27 installed follow-up:** [Identity and saved-data gate passed](checkpoint-7.24b-layout-r3-installed.md). Continue with the conditional physical observations below; the upgrade steps are retained as history and need not be repeated.

## First gate: upgrade and identity only

1. Keep Sierra 4 fully exited and disconnected. Do not start a flight or run reconnect cycles. MSFS may remain parked, without a Sierra 4 connection.
2. Run the archived **Alpha6OPS-Setup-0.22.0-7.24B-LAYOUT-R3.exe** and use its normal upgrade action. Its label must say **7.24B LAYOUT R3**. Do not uninstall, delete files, reset the profile or launch a different portable build.
3. Report upgrade completion before opening Sierra 4. Codex will verify all installed payload hashes against this package and compare profile/history/settings with the 224-file pre-upgrade backup. The retained R2A native failure remains FAIL.

Installer SHA-256: `19FA4133006BFE69A6975F03467E8608630D1E140D9AFDB087FE8CAF55FB4490`.

## Subsequent physical gates — after installed identity is confirmed

With Sierra 4 disconnected, verify the saved profile, history, gates and assignment are readable. Confirm stable Operations layout, normal surrounding scrolling, map pan/zoom/selection, focus escape, navigation and resize. Opt into V2 and confirm the first-frame lighting and input fixes remain present. These observations must be recorded as R3 results, separately from earlier R2A passes.

Only after that gate passes, perform the single parked layout comparison explicitly agreed with Codex: Classic while connected for 10–15 seconds, then Map V2 in that same connection for 10–15 seconds, without resizing. Observe the entire workspace, flight table, scrollbar transitions and map host. Disconnect afterward and confirm stability. Stop immediately if the repeating flicker returns; do not cycle connections to try to obtain a pass.

This is a correction-specific parked observation, not completion of native lifecycle/reconnect or real-flight validation. Those broader gates remain paused until this result is reviewed. No production default change is authorized.
