# 7.24B — installed native pilot validation checklist

Prepared by 7.24A. **Not started or approved by this document.** First check the 7.24A readiness decision. Keep Map V2 opt-in and classic fallback available. Use a backed-up pilot profile and record build/version, aircraft/add-on, Windows display scaling, monitor refresh rates, WebView2 version, local time and simulator UTC/date. Never replace a real native check with a synthetic result.

## 1. Install and startup

- [ ] Use **Exit Sierra 4 from the tray menu** and confirm its application process has ended (closing the window can merely hide it to the tray). Back up its profile, history, settings, assignments, saved gates and active-flight recovery files.
- [ ] Install the identified test package through the normal updater/install procedure. Do not rename storage paths or migrate the profile.
- [ ] Launch Sierra 4 once; confirm existing history, settings, gates and assignments remain readable. Confirm single-instance activation.
- [ ] Open Operations. Classic should open by default. If Map V2 opens automatically, record whether the optional `SIERRA4_MAP_V2=1` startup override is configured; an override is not a changed default. Select **Use Map V2**; record startup time and WebView2 availability/version. If unavailable, confirm the classic map remains usable.
- [ ] Toggle back to classic, then opt into Map V2 again. Confirm no extra flight session or simulator connection is created.

## 2. Native connection gates

- [ ] Start MSFS manually in a safe parked state, choose the test aircraft, and align the simulator UTC date/time with the accepted flight.
- [ ] Connect Sierra 4. Verify aircraft identity, position, time, phase and engine observations against the simulator.
- [ ] Disconnect, then reconnect once. Confirm the same active flight continues, with one connection and no duplicate start/milestone records.
- [ ] Repeat disconnect/reconnect promptly. Confirm old callbacks cannot overwrite fresh position/time or publish a second session. Preserve logs and note the exact action times.
- [ ] Mark **NATIVE_CONNECTION_LIFECYCLE** and **NATIVE_RECONNECT** passed only after these actual simulator checks succeed. Record failures verbatim.

## 3. Physical map interaction

- [ ] Pan with a real pointer; wheel-zoom and use +/−. Confirm movement is responsive and pan exits follow mode.
- [ ] Select the aircraft, origin airport, destination airport and a route. Confirm details and highlights refer to the item clicked.
- [ ] Follow the aircraft, then unfollow. Use World and Focus; confirm the view remains controllable.
- [ ] Open Layers; toggle airports, routes, aircraft, labels and day/night. Enable reduced motion and confirm it applies after returning to Operations.
- [ ] Tab through controls, activate them with the keyboard, inspect visible focus, and press Escape to move focus back into WPF content. If touch is available, test it separately.
- [ ] Switch to another workspace and return repeatedly. Minimize/restore and resize the app. Verify the newest position appears and no straight trail bridges the hidden interval.
- [ ] Confirm none of these presentation actions reconnects the simulator, resets the flight, duplicates an event or alters progress/ETA.

## 4. Physical display behavior

- [ ] Test actual Windows scaling at 100%, 125%, 150%, 175% and 200% where practical. Record unsupported combinations; do not infer passes.
- [ ] Move the app between monitors, including different scaling/refresh rates if available. Check text, map controls, labels, clipping, selection details and scroll access.
- [ ] Open Sierra shell search/menus over the map. Confirm WPF overlays draw above WebView2 and accept input correctly.
- [ ] Repeat pointer selection, wheel zoom, keyboard focus and Escape after monitor moves, resize and restore. Capture any offset hit targets or frozen surface.

## 5. Complete real flight

- [ ] Import/review/accept a flight; check airport names, UTC dates/times, aircraft subtype and gate assignments.
- [ ] Connect at the gate; observe engine start, pushback/taxi and OUT.
- [ ] Take off and verify OFF, climb, heading, route and moving aircraft.
- [ ] During cruise compare phase, ETA, progress, distance and ordered events with the flight. Exercise map navigation and workspace switching without interrupting tracking.
- [ ] Descend and land; verify ON, taxi-in, block-in/IN and completed progress.
- [ ] Submit one PIREP. Reopen history and confirm the completed flight and single PIREP are durable.
- [ ] Throughout, record freezes, unexpected reconnects, duplicate/missing events, save reports or discrepancies. Save the JSONL, diagnostic bundle and relevant screenshots, including simulator UTC/date.

## 6. Recovery and exit

- [ ] In a safe portion of the validation flight, use **Exit Sierra 4 from the tray menu** while leaving MSFS under the pilot's control. Confirm the application process has ended; a window hidden to the tray is not a restart. Relaunch Sierra 4 and verify recovered assignment, actual times, phase/events and position context.
- [ ] Reconnect manually; verify continued tracking without a second flight or invented events. Complete the flight and PIREP checks above.
- [ ] Use **Exit Sierra 4 from the tray menu**; confirm its application process and owned WebView processes exit. Do not terminate unrelated browsers.
- [ ] Review unexpected-report folders and package the evidence. Record each checklist item as pass/fail/unavailable with evidence references.

Passing this checklist is evidence for a separate default-enable decision. It does not itself authorize default activation, globe development, another workspace migration or airline-world features.
