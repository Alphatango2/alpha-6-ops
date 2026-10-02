# 7.24B — verified installation and initial installed startup

Status at 25 September 2026, 18:55 UTC: **normal upgrade and installed identity PASS; startup inspection partly verified; physical and native checks pending.**

Later update: after the user enabled Map V2, physical left-drag failed. The isolated A/B investigation identified a parent ScrollViewer focus conflict. Native testing is now **on hold**; see `checkpoint-7.24b-physical-input.md`. The startup observations below are retained as the earlier baseline.

## Installation gate closed

The user retried the original frozen installer after Windows restart and supplied its **Upgrade complete** confirmation. Read-only verification of `%LOCALAPPDATA%\Alpha6Designs\Alpha6OPSPreview` matched **514/514** expected payload files, with no missing or mismatched files, to build `S4-7.24B-20260925-46a1b304c3d6`, version **0.22.0**, source commit `46a1b304c3d6e97b78098379332d7b197edf7aee`.

All **216 profile/history/settings/log files** matched the pre-failure incident manifest immediately after the upgrade, with no additions, removals or changed hashes. The profile was not edited or restored to obtain that result. The original failed attempt and process-query diagnosis remain recorded separately. No replacement package or installer was generated.

## Installed startup observed

The verified installed application was launched through the computer-use app launcher at **18:53:24.543 UTC**. Its process path is the real installation path, PID **9668**, and its window title is **Sierra 4 v0.22.0**. The launcher returned after approximately **1.862 seconds**; this is launch-call duration, **not an exact rendered-ready time**.

Text accessibility inspection confirms:

- Operations workspace is present, with the existing pilot profile loaded.
- Simulator connection is **Disconnected**.
- **Classic map · Map V2 available** and the **Use Map V2** control are present at first startup.
- No active flight/verified route is currently shown. No assignment was created, accepted or modified during these checks.
- Flight logbook, Dispatch and Settings navigation remain present. Their detailed visible contents still require user confirmation.

No Map V2 environment override was present before launch. Map V2 has not been enabled during this turn.

## Initial baseline

| Observation | Result |
|---|---|
| Application working set at 18:54:42 UTC | 262,086,656 bytes, about 250 MiB |
| Private memory | 232,579,072 bytes, about 222 MiB |
| Handles / threads | 860 / 26 |
| Direct child processes under classic startup | 0 |
| Registered WebView2 runtime | 153.0.4234.48; actual Map V2 process/version still to be observed after opt-in |
| Report inventory | 26 existing reporter files before and after startup; **0 new reports** through 18:55:42 UTC |
| Saved gate/settings JSON | Readable; unchanged at post-upgrade check |
| Display controller mode | 3440 × 1440, 239 Hz reported for both adapters; does not establish physical monitor count, per-monitor scaling or window size |
| Map V2 initialization timing/browser baseline | Pending manual opt-in |

## Automation limitation — not a product responsiveness result

Computer-use screenshot capture failed with `SetIsBorderRequired failed: No such interface supported (0x80004002)`. Text-only accessibility inspection succeeded. The attempted logbook click failed with `coordinate input geometry is unavailable`; a subsequent Ctrl+K input produced no observable search transition. No claim of successful physical input, visual map rendering, overlay behavior or logbook navigation is made from those attempts. No alternate UI automation or application code changes were used to bypass this limitation.

The application remains open on Operations, disconnected, with classic selected. The user must verify the visual startup and physically enable Map V2. The two native gates remain open and no MSFS session has started.

## Next user observations

1. Confirm Operations and the classic map are visibly displayed; check that Pilot logbook, saved gates and settings still look correct.
2. Return to Operations and click **Use Map V2**. Report whether the map loads and send a screenshot, including any error or fallback message.
3. While MSFS remains closed, try mouse pan, wheel zoom, window resize/maximize/restore and shell navigation. Report smooth / minor hitch / major pause / severe freeze, and the actual Windows scaling percentage and monitor arrangement.

Aircraft/route selection and Follow behavior need a real assignment/telemetry later; do not invent a flight to make the empty startup view appear populated. After the initial map observations, capture its resource/runtime baseline and proceed through the existing physical and parked-native checklist. Do not skip directly to a flight or claim 7.24B complete.

Evidence: `docs/map-v2/evidence/7.24b-installed-baseline/`. Full local accessibility captures are in `work/checkpoint-7.24b-installed-baseline/`.
