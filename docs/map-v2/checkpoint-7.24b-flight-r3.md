# 7.24B — LAYOUT R3 real-flight handoff

The [physical reconnect and log audit](checkpoint-7.24b-native-r3-results.md) permit the next controlled pilot stage. Keep the installed **S4-7.24B-LAYOUT-R3-20260927-666f495dce7e** build unchanged throughout the flight. No reinstall, update or additional parked reconnect cycles are needed.

## Before departing

1. Use the normal Dispatch workflow to import/review/accept your next planned flight. The old JST941 schedule is stale and is currently being rejected for this session; do not use it unchanged as the validation assignment.
2. Check the **complete UTC date and time**, including any arrival-day rollover, against the simulator date/time. Confirm flight number, origin/destination, aircraft type/subtype, registration and departure/arrival gates. Confirm Dispatch shows the intended accepted assignment and saved profile/history/gates/settings remain readable.
3. Connect normally while parked. Confirm the expected aircraft and flight are recognized, current position is plausible, and Operations remains stable. If assignment review or the repeating flicker remains, stop before departure and report it.
4. Opt into Map V2. Note flight number, route, aircraft/add-on, approximate start time and any change from the earlier 3440×1440 / 100% display setup.

## During the flight

- Fly normally from gate to gate. Observe engine/phase events and OUT, OFF, ON and IN without deliberately changing the flight to manufacture events.
- Keep Map V2 visible for a meaningful period, including cruise where practical. Check position, heading, planned route, observed track and day/night against the flight.
- During low-workload periods, try available aircraft/airport/route selection, pan/zoom, Follow/Unfollow, World/Home, Focus and Layers. Switch Operations → Flight Tracking → Operations. Exercise resize/minimize/restore only when convenient; report unavailable map objects instead of assuming a pass.
- Once when convenient, switch to Classic and back to V2. Confirm tracking continues and the simulator connection remains up. Do not deliberately interrupt the connection or crash the renderer during this primary flight.
- Note approximate time, phase and action for any hitch: **A** none; **B** minor; **C** obvious roughly 0.5–1 second pause; **D** roughly 1+ second/disruptive. Note whether the surrounding UI also froze and whether it recovered. Screenshots are helpful but not required for a fast visual defect.

If the repeating layout flicker, a major freeze, connection error or tracking discrepancy returns, report it and preserve the logs. Do not reinstall, alter profile files or repeat connections simply to obtain a pass.

## After arrival

Complete taxi-in/block-in and the normal flight completion/PIREP workflow once. Confirm the flight appears in history/logbook and report any missing or duplicate record. Then disconnect normally to finalize diagnostics. Share the flight package or tell Codex the flight number and that the logs are ready for inspection, together with approximate Map V2 viewing duration and any visible problems.

**Recovery/restart remains a separate pending gate.** Prefer a dedicated short native session after this flight, so the primary validation flight is not interrupted solely to test recovery. Do not consider full 7.24B, default readiness or later checkpoints complete based on the parked passes.
