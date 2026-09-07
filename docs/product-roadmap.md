# Product brief and delivery plan

Alpha 6 OPS is airline operations for MSFS 2024 under Alpha 6 Designs. The product promise is a persistent airline day: the aircraft you fly has a previous leg and a next leg, and your arrival changes what happens next. ACARS-style telemetry is an input to this experience.

Pilot experience: simple to fly, deep if you want it. Simple focuses on assigned aircraft and the next flight; Advanced exposes timings and rotation impact; OCC gives dispatchers a broader control surface. Tenant branding will supply airline names, colors and approved assets, while Alpha 6 remains the platform identity.

## MVP acceptance target

One signed-in pilot, one tenant, one assigned aircraft, one daily rotation. Windows capture connects to MSFS 2024, confirms the assigned aircraft, records operational milestones, persists actuals, recomputes later legs, and recovers safely after restart/disconnection. Pilot sees the next feasible departure and why it changed. Every operational result is reproducible from recorded inputs. A mock/replay path must pass the same acceptance tests.

This commit supplies a runnable first slice; the full MVP acceptance target is not yet achieved.

## Delivered in 0.12.0

- Keep Flight Tools focused on the active assignment, simulator connection, live timeline/debrief, and rotation detail.
- Remove recorded-flight replay controls from the pilot-facing interface while retaining deterministic replay fixtures for automated verification.
- Consolidate Flight History, Log Database, JSON test-log export, program health, data folders, and crash reports in a polished Logs & Diagnostics settings workspace.
- Use the space recovered in Flight Tools for the next flight-tracking controls and status presentation.
- Add gate-aware SimBrief import. Parse explicit departure and arrival gates from dispatch remarks first; otherwise resolve a suggested gate from the maintained airport/airline catalog. Keep the result stable for the same flight/date, record its source and confidence, allow pilot correction, and leave it unassigned when no reliable match exists.
- Add a functional General settings page with persisted tray behavior, notification attention/sound preferences, weight/altitude/landing-distance units, advanced Flight Deck visibility, and explicit Save and Reset Defaults actions. Keep local reliability reporting enabled and avoid controls for integrations or telemetry that the application does not provide.
- Apply one reusable Alpha 6 navy/yellow window system to every internal secondary window and notice, including consistent borders, logo/title treatment, caption controls, typography, panels, inputs, and data tables. Native Windows open/save pickers continue to follow the operating-system theme.

## Planned 0.12.1 fixes

- Change the active-flight airline logo from the three-letter SimBrief airline ICAO code, with a neutral fallback when the carrier has no bundled logo.
- Repair and verify minimize, maximize/restore, and close behavior in every internal secondary window using the shared Alpha 6 title bar.

## Two-developer division

| Milestone | Developer A: simulator/domain | Developer B: service/product | Shared exit gate |
| --- | --- | --- | --- |
| 0: Foundation (this repository) | Phase/rotation core, replay, tests | Demo API, web shell, contracts | Build and reproduce +30/+5 minute downstream delays |
| 1: Durable assignments | Flight-scoped state, restart checkpoints, offline queue | PostgreSQL migrations, tenant membership, auth policies, assignment UI | Restart resumes same flight; cross-tenant access denied |
| 2: Live capture | SDK compatibility spike, x64 Windows bridge, reconnect/clock handling | Idempotent ingestion, revisions, actuals and read models | Replay and live milestones agree; retries do not duplicate events |
| 3: Pilot usability | WPF tray controls, connection health, aircraft-specific block heuristics | Simple/Advanced screens, audit corrections, branding settings | Pilot completes two linked legs without manual database edits |
| 4: MVP hardening | MSFS aircraft matrix, signed packaging, diagnostics | OCC read-only rotation board, monitoring, retention, recovery | End-to-end simulator flight, restart and interruption tests |

Agree on telemetry units, UTC clock source, event IDs, assignment scope and API schemas before milestone 1. A owns the authoritative calculations; B consumes their results and owns tenant enforcement. Pair on ingestion and reconciliation boundaries. Sequence is a delivery plan, not an elapsed-time commitment.

## After MVP

Extend to aircraft network delay propagation and swaps; passenger connections and reaccommodation; crew duty/legality with explicit rulesets; maintenance/MEL restrictions; diversions and IRROPS actions; richer OCC; white-label tenant customization; versioned API/webhooks; and optional voice company/dispatch. Each new subsystem first needs deterministic state/rules and auditable commands, then conversational presentation. Avoid building these engines before the one-aircraft operational loop is reliable.
