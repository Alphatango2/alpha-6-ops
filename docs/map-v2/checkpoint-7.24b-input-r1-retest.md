# 7.24B INPUT R1 — installed physical retest

**Keep MSFS closed throughout. Status: NOT STARTED.** The original installed left-drag result remains FAIL. This sheet records only the new R1 build.

## Install and identify

1. Exit Sierra 4 from its tray menu if it is running. The correction handoff preflight found it closed. Keep MSFS closed.
2. Run `Alpha6OPS-Setup-0.22.0-7.24B-INPUT-R1.exe` from the uniquely named R1 archive folder. The installer must say **0.22.0 • 7.24B INPUT R1**. Use its normal **UPGRADE ALPHA 6 OPS** action. Preserve the existing install location and profile.
3. If upgrade fails, stop and capture the error; do not uninstall, delete files or switch to the portable package to bypass the gate.
4. Tell Codex when upgrade completes, before launching the app, so the installed files can be checked against the R1 `payload-manifest.json` and the saved-data snapshot. The included `verify-installed-build.ps1` performs a read-only payload check. Expected result: **514/514**, validation ID **S4-7.24B-INPUT-R1-20260925-62b8c871dd05**. Version 0.22.0 alone is insufficient.
5. Launch normally. Check existing history, gates, settings and assignment remain readable. Operations must begin in classic mode; select **Use Map V2** manually.

A hash-verified saved-data backup exists in `work/checkpoint-7.24b-input-correction/pre-reinstall-profile-backup/`. It excludes only regenerable MapCache. Do not restore it unless a specific problem is diagnosed.

## Physical checks

Record PASS / FAIL / UNAVAILABLE for each row, with local time and a screenshot or short description for any issue. Use the actual mouse and keyboard, not automated browser commands.

| Check | Required observation | Result |
|---|---|---|
| Left-drag pan | Drag horizontally, vertically and diagonally in both directions. Repeat several times. Map follows the drag and stops after release; normal left pan does not unexpectedly rotate. | Pending |
| Wheel and buttons | Wheel zoom works over the map; ordinary left-click on + and − still works. Pan again immediately afterwards. | Pending |
| Selection | Click available airport, route and aircraft objects; correct details/highlights appear. A drag must not become an unintended click. Record absent object types as unavailable, not passed; do not start MSFS to populate them. | Pending |
| Right-drag | Right-drag still rotates/changes bearing according to existing MapLibre behavior. Release works, and subsequent left-drag still pans. | Pending |
| Surrounding page | At a window size with page overflow, wheel/scrollbar and clicks outside the map still scroll/focus the WPF page normally. Re-enter the map and pan again. | Pending |
| Focus escape/return | Press Escape from the map and Tab/Shift+Tab to reach surrounding controls. Activate an outside button, then return and drag. No trapped focus or lost first press. | Pending |
| Resize/restore | Resize, maximize/restore and minimize/restore; repeat pan, zoom and selection. No frozen surface, shifted hit targets or capture stuck after release. | Pending |
| WPF/WebView integration | Switch workspaces and return several times; switch classic → Map V2. Open shell search/menu over the map, use and dismiss it. Focus, keyboard input and map dragging still work; no new error dialog or unexpected reconnect. | Pending |

Record Windows resolution/scaling, monitor(s), pointer device, WebView2 runtime and test time. If moving between monitors with different scaling, repeat input checks and record the actual combinations tested. Automated scaling screenshots do not establish physical DPI success.

## Return the result

Send the eight row results, installation screenshot, any failed/unavailable item and its exact sequence/time. Keep MSFS closed until the installed payload, physical results and new report/log inventory have been reviewed. Do not proceed merely because left-drag alone works. Native connection/reconnect and the pilot flight remain separate pending gates.
