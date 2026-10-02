# 7.24B — LAYOUT R3 installed identity and saved-data gate

**Follow-up:** the [parked R3 physical stability comparison and log audit](checkpoint-7.24b-layout-r3-physical.md) now PASS. Continue with the [R3 parked reconnect gate](checkpoint-7.24b-native-connection-r3.md). The installation-time record below is preserved; its then-pending status is historical.

Verified **2026-09-27 02:12:16 UTC** after the user supplied the normal installer “Upgrade complete” screenshot identifying **0.22.0 · 7.24B LAYOUT R3**.

**PASS — exact corrected build installed, saved data unchanged.**

- Build: `S4-7.24B-LAYOUT-R3-20260927-666f495dce7e`.
- Source: `666f495dce7efb3ccd70b99939501144d91ab606`.
- Installed location: `C:\Users\DanniBaLs\AppData\Local\Alpha6Designs\Alpha6OPSPreview`.
- Payload: **514/514** expected files match SHA-256; zero missing or mismatched files.
- Frozen package companions: **14/14** still match their handoff hashes.
- Saved profile/history/settings: **224/224** unchanged from the pre-upgrade backup; zero missing, changed or added files, excluding regenerable MapCache. This also preserves the existing diagnostic reports and recorded native failure logs.
- Sierra 4 was not running. MSFS 2024 remained running (PID 26184), consistent with the parked test workflow. No native connection was made and no application was launched by this verification.
- No Map V2 override at process, user or machine scope. Classic remains default; V2 remains opt-in.

Raw evidence: `work/checkpoint-7.24b-layout-oscillation/installed-20260927-021214/`. Compact identity/summary and the screenshot are retained in `docs/map-v2/evidence/7.24b-layout-r3-installed/`. The screenshot SHA-256 is `D422ABF053DD713E776400698FEE8488F55429CAA316B22039FCE8BFB9F08E1D`.

## Next authorized physical observations

Open Sierra 4 normally and remain disconnected. Confirm the existing data is readable, Operations is stable, and basic map input, focus, navigation/first-frame lighting and resize still work. If any of those fail, stop before connecting.

If those disconnected checks pass, perform **one connection while parked**: select Classic, connect and observe the whole Operations workspace for 10–15 seconds without resizing. Switch to Map V2 within that same connection and observe for another 10–15 seconds. Disconnect afterward and confirm stability. If flicker appears, disconnect immediately and report which renderer/state failed; do not repeat connections to seek a pass.

Report the disconnected checks, Connected + Classic, Connected + V2 and final disconnected stability separately. These results are **PENDING**. The prior R2A connected layout result remains **FAIL**; it is not overwritten by the installed identity pass or automated R3 results. Broader native lifecycle/reconnect cycles and real-flight validation remain HOLD until this correction-specific result is reviewed.
