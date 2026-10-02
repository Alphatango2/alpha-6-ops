# 7.24B VISUAL R2A — installed physical retest

Keep MSFS closed. VISUAL R2A installation and physical validation are pending. INPUT R1's eight user-reported input passes and separate first-paint visual failure remain preserved; this sheet records a new build only.

## Install and identify

1. Exit Sierra 4 completely, including its tray icon. Run `Alpha6OPS-Setup-0.22.0-7.24B-VISUAL-R2A.exe` from `Alpha6OPS-Desktop-S4-7.24B-VISUAL-R2A-20260927-87fa792a7c29`. Check the setup caption says **0.22.0 • 7.24B VISUAL R2A**.
2. Use the normal **UPGRADE ALPHA 6 OPS** action, keeping the existing location. If installation fails, stop and capture the error. Do not delete files, uninstall, or substitute a portable launch.
3. Tell Codex when the upgrade completes, before opening the application. Verify **514/514** installed payload files against the included manifest and compare saved data with the pre-reinstall backup. Expected ID: **S4-7.24B-VISUAL-R2A-20260927-87fa792a7c29**. The version number alone cannot identify this revision.
4. Start normally; confirm existing profile, assignment, gates, settings and history remain readable. Classic must still be the default. Select **Use Map V2** manually.

The final source snapshot and package were frozen on 27 September UTC. The earlier VISUAL R2 candidate was not installed: it exposed a timing assumption in its new test fixture. That candidate and its failed results remain archived separately; R2A changes only that fixture and package identification relative to R2. The runtime first-paint correction is identical.

## First-paint observations

An opening indication while the correct first frame is prepared is expected. Once the map becomes visible, there must be no wrong terminator that snaps to a different position. Ordinary changes caused by pan, rotation, clock progression or a deliberate layer choice are separate observations.

Record PASS / FAIL / UNAVAILABLE and the time for each path. A short screen recording is useful for a fast flash but is not required.

| Path | Physical observation | Result |
|---|---|---|
| Initial application session | Start normally in classic, opt into V2, observe its first visible lighting. Repeat after one complete clean exit/start. | Pending |
| Tracking → Operations | Navigate away/back at least five times, watching the first visible map each time. Try both immediate and slower returns. | Pending |
| Other workspace → Operations | Visit Dispatch and another available workspace, then return. | Pending |
| Fresh renderer | Switch classic → V2 repeatedly, including one quick V2 → classic → V2 sequence. | Pending |
| Minimize/restore | Minimize and restore several times; watch the first visible map. | Pending |
| Resize/maximize | Resize and maximize/restore. Lighting and hit targets remain correct. | Pending |
| Day/night visibility | Disable DayNight, navigate away/back, then enable it. Preference is retained and no placeholder terminator appears. | Pending |
| Cached session | Repeat navigation after several minutes of normal map use. | Pending |

Do not deliberately crash the browser or use diagnostic failure injection in the installed app. Automatic recreation/restart/fallback paths have automated coverage; record any naturally occurring restart or fallback separately.

## Retain INPUT R1 behavior

Repeat all eight checks on this exact R2 installation:

1. Left-drag pans horizontally, vertically and diagonally, stops on release.
2. Wheel zoom and +/− work; pan still works afterward.
3. Available objects can be selected. Record absent aircraft/route types as unavailable; do not launch MSFS to populate them.
4. Right-drag retains rotation/bearing behavior; subsequent left-drag pans.
5. Page scrolling and clicks outside the map work at a window size with overflow.
6. Escape/Tab/Shift+Tab can leave and return to the map without trapped focus.
7. Resize and minimize/restore preserve input and hit targets.
8. Workspace switches and shell menus/search preserve dragging and keyboard focus, with no new focus regression or error.

Record Windows resolution/scaling, monitor(s), pointer device and approximate test time. If tested across monitors with different scaling, list the combinations actually used.

## Stop and report

Return the visual path results, the eight input results, and any error or unexpected classic fallback. Keep MSFS closed until installed identity, saved-data preservation, physical results and report/log audit are reviewed. This retest does not close native connection/reconnect, flight or performance gates, and does not enable Map V2 by default.
