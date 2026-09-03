# UI shell plan

Implemented: black/yellow textual header, platform slogan, fixed demo day label, assigned aircraft, next departure, replay/reset actions, UTC rotation table, and responsive Simple/Advanced/OCC selection. No logo is fabricated. Keyboard buttons and focus indicators are included; presentation modes have no security significance.

Simple next: connection/assignment status, aircraft match, start/resume flight, next flight briefing, milestone confirmation and a single useful delay explanation. Advanced next: planned/actual times, phase history, data quality and effects on later legs. OCC next: aircraft timeline, propagated delay reasons and scoped audit trail. Current OCC displays the same single aircraft, not a fleet control center.

Use tenant theme tokens for approved name, color, logo and support links. Maintain minimum contrast and preserve status meaning independently of brand colors. Do not load arbitrary tenant HTML/CSS. Roles control available actions on the server as well as visibility in the UI. Future voice interaction is optional, with readable text and explicit command confirmation for consequential changes.
