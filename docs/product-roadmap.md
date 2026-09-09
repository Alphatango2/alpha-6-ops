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

## Delivered in 0.12.1

- Change the active-flight airline badge from the three-letter SimBrief airline ICAO code, with a neutral labeled fallback when the carrier has no bundled treatment.
- Repair and verify minimize, maximize/restore, and close behavior in every internal secondary window using the shared Alpha 6 title bar.

## Post-0.12.1 working changes

- Remove the carrier badge and airline-name lookup after interface review. Present the imported airline ICAO and flight number as one larger flight identifier.
- Give the next-flight hero and Fly tile separate unbranded aircraft-at-gate photography instead of reusing one airline-livery image.
- Replace the flat schematic network map with a native offline globe-style view, including shaded oceans, curved graticule, refined land, glowing routes, vector aircraft, cleaner station labels, and themed hub selection.
- Add the local Alpha 6 Flight Lab companion with manual and automatic virtual flight phases, named-pipe telemetry through the production live-reading boundary, accelerated simulator UTC, and reconnect/continuity fault injection.

## Planned Flight Tracking update

Checkpoint 1 is implemented locally: **Flights** is now **Flight Tracking**, its native workspace and No Active Flight state live inside the main shell, dashboard details and compact network-map actions navigate into it, and the former preflight hero action opens Your Flight Deck.

Checkpoint 2 is implemented locally: SimBrief imports now retain departure, destination, and navlog waypoint coordinates with the cached active assignment. The Flight Tracking workspace plots that route on a detailed orthographic globe, frames regional and international routes automatically, follows great-circle paths across the date line, labels both airports, and supports drag rotation, close zoom, and route reset. Intermediate SimBrief fix names appear progressively at close zoom with spacing to limit collisions. The aircraft remains at the planned departure point until live position arrives in Checkpoint 3. Older cached assignments show a clear reimport message because they predate saved route geometry. The duplicate dashboard three-dot Flight Tools button is removed, and Actual / Est. In remains blank until live telemetry supplies an estimate or confirmed arrival. Coastlines use the public-domain Natural Earth 1:110m land dataset bundled for offline use.

- Expand the next-flight hero with separate scheduled departure/arrival and actual departure/arrival fields. Actual values remain blank until their corresponding confirmed operational milestones occur.
- Make **View Flight Details** navigate to the full Flight Tracking workspace once that workspace is available.
- Replace the static on-time badge during an active flight with a signed schedule status derived from the actual departure and current ETA. Show whether the projected arrival is early or late, while retaining the planned block time as the baseline.
- Remove **Start Preflight** from the hero. Replace it with **View Flight Deck**, which navigates directly to the existing Flight Deck workspace.
- Turn the hero's bottom route line into a milestone progress track. Show operational event dots and move an aircraft marker between them using confirmed flight phase progress.
- Add an explicit **Clear Active Flight** action. Clearing removes the saved assignment and returns the hero and tracking workspace to a defined **No Active Flight** state without deleting its completed journal or history record.
- Include the reliability work exposed by Flight Lab: rearm or explicitly recover after accepted clock/aircraft discontinuities, publish aircraft-name edits atomically, distinguish expected shutdown from connection failure, preserve monotonic journal ordering with separate effective milestone times, report simulated pause state, and coalesce repeated reconnect timeouts.

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
