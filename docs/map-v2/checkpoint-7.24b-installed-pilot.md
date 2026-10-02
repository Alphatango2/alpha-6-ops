# Checkpoint 7.24B — installed pilot checklist

Status: **normal upgrade and 514-file installed identity passed; physical left-drag pan FAILED after manual Map V2 opt-in**. Native testing is on hold. See `checkpoint-7.24b-physical-input.md`; do not advance to section 4 until this gate is resolved. The earlier installation failure is preserved in `checkpoint-7.24b-installation-gate.md`; no installer rebuild was needed. Historical A-series NOT READY decisions remain unchanged.

Validation build: **S4-7.24B-20260925-46a1b304c3d6**, application version **0.22.0**. Use the frozen installer named `Alpha6OPS-Setup-0.22.0-7.24B.exe` in the matching build directory. Package/storage identities remain Alpha6OPS-compatible. No globe or default-enable work is included.

## 1. Installation stop — do only this first

1. Exit the current application using **Exit Sierra 4** in the tray menu (or the in-app exit control). Closing the window may only hide it. Finish any active flight before updating.
2. Run the frozen installer. Its caption should say **0.22.0 · PILOT 7.24B**. Install normally.
3. Launch **Alpha 6 OPS** from the Start menu. The shell should be Sierra 4; the version remains **0.22.0**. The version alone cannot distinguish this pilot from older builds; the archived file hashes provide exact identity.
4. Reply with whether Operations opens, classic map loads, and your profile, history, settings and saved gates remain present. Mention any startup error. Do not reset or edit profile files to make these checks pass.

Stop here for installed-file hash verification and the startup/log baseline. Do not infer success from the automated package extraction check.

## 2. Installed baseline — before MSFS

Record startup and Map V2 initialization time where practical, WebView2 runtime version, browser process count, resource baseline, screen resolution, actual Windows scaling, monitor configuration and window size. Record the exact launch time to delimit new reports.

Confirm classic is the default. Use **Use Map V2** in Operations to opt in for this session. Do not set a persistent override. Hash verification uses the companion `verify-installed-build.ps1`, `payload-manifest.json` and `validation-build.json`; it reads application files without changing profile data. Codex can run this after installation.

## 3. Physical controls and displays

Physically try pan, wheel zoom, aircraft/origin/destination selection, route selection where available, Follow/Unfollow, World/Home, Focus, Layers, keyboard navigation, Escape focus bypass and switching focus between shell and map. Mark unavailable controls explicitly. Record smooth / minor hitch / obvious freeze / input ignored / incorrect focus / other.

Resize, maximize/restore, minimize/restore, and move between monitors where available. Record each monitor resolution and scaling. Check search, dropdowns, tooltips and dialogs over the map; verify readable text and clickable controls. Previous browser automation and LayoutTransform images do not satisfy this physical gate.

## 4. Native connection checks — parked, before the flight

Launch MSFS normally and load the aircraft parked. Confirm Sierra 4 starts disconnected, connect, confirm real telemetry/aircraft and connection state, disconnect intentionally, then reconnect. Capture timestamps and diagnostics for native open/close, worker disposal, session identity and connection generation.

Perform a controlled rapid disconnect/reconnect while parked. Verify the new generation alone is authoritative, obsolete callbacks cannot overwrite current aircraft state, and there is no duplicate stream/session, hung worker, orphan connection or crash. Do not use rapid reconnect during the primary flight merely to satisfy a test. Synthetic callbacks do not close either native gate.

## 5. Real flight

Use normal import/review/accept/Dispatch workflow. Check flight, airports, aircraft, complete UTC dates, schedule and gates. Record the flight identifier, aircraft subtype, simulator/add-on versions and start/end UTC. Enable Map V2 manually.

Observe position, heading, movement, route, airports, observed track, planned/completed route semantics, selection, Follow, World/Home and day/night. Interpolation must stay presentation-only. Record any stale handling after a safe interruption.

Track preflight, taxi out, OUT, takeoff/OFF, climb, cruise, descent, approach, landing/ON, taxi in, IN, completion and PIREP eligibility. Submit the PIREP normally and verify history persistence. Keep Map V2 visible for a meaningful portion including cruise where practical; record visible duration, memory trend, browser behavior and any fallback/restart/degradation.

Periodically pan/zoom/select, toggle Follow, switch Operations → another workspace → Operations, and resize or minimize/restore where safe. A visible freeze matters even if flight telemetry continues; a dispatcher measurement alone does not prove a visible freeze.

## 6. Observation record

Use A = no noticeable hitch; B = minor visible hitch, not disruptive; C = major pause roughly 0.5–1 second; D = severe roughly 1+ second or disruptive. Mark unknown facts as unknown.

| UTC time/date | Phase | Action | A/B/C/D | Map visible/moving? | Surrounding UI frozen? | Telemetry continued? | Auto-recovered? | Screenshot/log |
|---|---|---|---|---|---|---|---|---|
| Pending | | | | | | | | |

## 7. Recovery and classic fallback

Prefer a separate short native session for recovery, or choose a safe point deliberately. Confirm real samples and saved recovery state, exit normally, relaunch, recover, reconnect and verify one continuous session with continued tracking. Do not risk an important flight for this test.

Where safe, use **Use classic map**. Confirm the flight continues, connection generation does not change and Operations remains usable. Do not inject a browser crash in the primary flight; automated failure coverage is retained.

## 8. Post-flight audit and decision

Verify one persisted flight/PIREP, expected events, appropriate recovery cleanup, no unexpected stale assignment and readable profile data. Audit crash, unobserved-task, recovery, renderer, browser restart/fallback and SimConnect reports; inspect generations and stale-callback/duplicate evidence. Keep unexpected errors; do not whitelist them afterward or modify data to obtain a pass.

Both native gates, all physical checks and real-flight/recovery/PIREP/history checks remain open until evidenced. Final classification follows the specification: candidate for default only with all required gates and no serious visible freeze; remain opt-in if visible freezes occur while flight truth remains intact; disable for normal use if Map V2 destabilizes the workflow. No classification automatically changes the default.

Record globe-relevant preferences (interaction, zoom, Follow, World/Home, route visibility, day/night and map proportions). Do not begin 7.25 or another workspace migration here.
