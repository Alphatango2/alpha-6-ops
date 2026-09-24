CHECKPOINT 7.20 — SIERRA 4 OPERATIONS DASHBOARD

Checkpoint 7.19 is complete and committed as 507f657.

The Sierra 4 application shell and design system are now established.

Verified 7.19 baseline:

- Production build/package: PASS
- 1,834 / 1,834 executed checks passed
- 0 failed
- 2 native simulator checks unavailable
- 9 / 9 recorded-flight replays passed
- 91,366 / 91,366 telemetry samples processed
- FlightSessionCoordinator remains the application-lifetime owner
- Sierra 4 shell/navigation/design resources are in place
- Existing workspaces remain operational

OBJECTIVE

Replace the legacy Operations dashboard with the first fully migrated Sierra 4 production workspace.

THIS IS A VISUAL REPLACEMENT OF THE OPERATIONS WORKSPACE.

Do not merely reorganize the existing Operations dashboard.
Do not preserve the legacy card/tile layout simply because it already exists.
Do not wrap the existing dashboard in new styling and call the migration complete.

Preserve useful existing functionality and real data sources, but redesign the Operations presentation from first principles using the Sierra 4 design system created in 7.19.

The supplied Sierra 4 Operations concept image is the PRIMARY VISUAL TARGET for this checkpoint.

The finished Operations workspace should clearly look like the same product family and design direction as that concept.

It does not need to reproduce illustrative/fake data from the concept.

VISUAL GOAL

The Operations workspace should feel like a modern airline Operations Control Center:

- dark, sophisticated, information-first
- premium professional desktop software
- strong typography and information hierarchy
- restrained Sierra yellow
- minimal borders
- generous but efficient spacing
- large geographic/network centerpiece
- operational information surrounding the map
- clear status hierarchy
- subtle depth
- very limited glow
- no generic SaaS dashboard appearance
- no portal/home-page appearance
- no grid of large photographic navigation tiles

The page should answer:

"WHAT IS HAPPENING IN MY AIRLINE RIGHT NOW?"

rather than:

"WHICH PART OF THE APPLICATION DO I WANT TO OPEN?"


PART 1 — REMOVE THE LEGACY OPERATIONS PRESENTATION

Replace the current Operations workspace presentation.

The following legacy presentation concepts should NOT define the new layout:

- large "Your Next Flight" photographic hero
- photographic navigation tile grid
- Pilot Logbook / Operations / Aircraft / Maintenance / Crews /
  Passengers / Weather / OCC launcher tiles
- portal-style dashboard composition
- legacy Fleet Reference card composition
- legacy Company Messages card composition

Do not delete useful underlying functionality merely because its old visual entry point disappears.

Navigation to major application areas already exists in the Sierra 4 shell.

Operations should now be an operational overview, not a second navigation menu.


PART 2 — OPERATIONS PAGE STRUCTURE

Build a responsive Sierra 4 Operations workspace around this general hierarchy:

------------------------------------------------------------

OPERATIONS
Global Operations Overview
[operational status / data freshness / simulation context]

KPI / STATUS STRIP

[ ACTIVE ] [ ON TIME ] [ DELAYED ] [ AT RISK ]
[ AIRCRAFT ] [ CONNECTIONS ]

------------------------------------------------------------

LARGE NETWORK / OPERATIONS MAP
                                   OPERATIONAL ALERTS
                                   AIRPORT / WEATHER STATUS

------------------------------------------------------------

ACTIVE / UPCOMING FLIGHTS          ACTIVITY / DECISIONS

------------------------------------------------------------

This is a structural direction, not a rigid pixel layout.

Use the supplied concept image to make appropriate visual decisions.


PART 3 — DATA HONESTY

CRITICAL:

Do not fabricate airline-world data to make the dashboard look complete.

The persistent Sierra 4 airline-world simulation does not exist yet.

Every displayed operational value must be one of:

1. real existing application data
2. derived deterministically from real existing application data
3. existing explicitly identified demo/scenario data
4. clearly labeled unavailable / not yet available

Do NOT invent:

- hundreds of active flights
- simulated aircraft positions
- passenger connection counts
- network-wide delay statistics
- crew legality risks
- maintenance events
- airport congestion
- fleet availability
- OCC decisions

unless those values already exist in an authoritative existing source.

If a future Sierra 4 capability has no real source yet, design the UI state for it without pretending the capability exists.

Prefer elegant empty/unavailable/demo states over fake production information.


PART 4 — OPERATIONS KPIs

Create the visual framework for the Operations KPI strip.

Use real data where currently authoritative.

Possible categories include:

- Active
- On Time
- Delayed
- At Risk
- Aircraft
- Connections

However, only display a numerical value when the current application can support it honestly.

If a metric is not yet authoritative, use the Sierra unavailable/demo-state system rather than inventing a value.

The KPI architecture should be ready for the future persistent airline-world engine without depending on it today.


PART 5 — NETWORK MAP AREA

Create the new Sierra 4 map AREA and surrounding composition.

IMPORTANT:

DO NOT implement Map V2 in this checkpoint.

DO NOT rewrite the geographic renderer.
DO NOT introduce MapLibre, Mapbox or another new mapping dependency.
DO NOT move map processing to another thread as part of 7.20.
DO NOT create thousands of fake aircraft.
DO NOT fabricate a living network.

Reuse/adapt the existing map capability where practical so the new Operations workspace has a functional geographic centerpiece.

The map should visually fit the new dashboard composition.

Preserve existing map truth semantics.

Clearly distinguish existing real/observed/inferred/demo information as currently supported.

Map rendering architecture/performance isolation and the true living-network map belong to the next dedicated map checkpoints.

Design this area so Map V2 can later replace the renderer without requiring another Operations dashboard redesign.


PART 6 — ACTIVE / UPCOMING FLIGHTS

Replace the legacy Today's Operations presentation with a Sierra 4 operational flight list/table.

Use authoritative existing flight/assignment/rotation data.

Prioritize useful fields such as:

- flight
- origin
- destination
- scheduled departure
- scheduled arrival
- aircraft
- status
- delay / operational state where authoritative

Use typed operational state where available.

Do not derive business meaning from display strings.

The table should visually match the Sierra 4 concept and design system.


PART 7 — OPERATIONAL ALERTS

Create an Operations alerts area.

Use existing authoritative operational events/state where available.

Do not fabricate network alerts merely to fill the panel.

Support meaningful visual severity:

- information
- warning
- critical

Empty state should look intentional and premium.

The architecture should allow future airline-world events to populate this panel later.


PART 8 — AIRPORT / WEATHER STATUS

Create a compact airport/weather operational area.

Reuse existing authoritative weather/airport information where available.

Do not implement a new weather engine.

Do not imply network-wide airport monitoring if the current application does not provide it.

Design the panel so future airport-capacity/weather/network data can be introduced without redesigning the page.


PART 9 — ACTIVITY / OPERATIONAL TIMELINE

Create an activity area using existing authoritative events where practical.

This should eventually become the visible pulse of Sierra 4:

- flight milestones
- operational events
- disruptions
- decisions
- recoveries

For now, only display events supported by current state/history.

Do not fabricate AI/OCC decisions.

Use typed event identities where available rather than parsing presentation wording.


PART 10 — CURRENT PILOT FLIGHT

Do not lose the useful "next/current flight" information from the old dashboard.

However, it should no longer dominate the page as a giant photographic hero.

Integrate the user's current/next flight elegantly into the Operations experience.

It may appear as:

- an active-flight summary
- highlighted row
- compact operational strip
- map selection
- contextual card/panel

Choose the solution that best fits the supplied Sierra 4 concept.

Preserve access to Dispatch / Flight Tracking where appropriate.


PART 11 — RESPONSIVE BEHAVIOR

The new Operations workspace must use the responsive Sierra 4 shell established in 7.19.

Do not recreate a fixed 1920x1080 reference surface.

Review at minimum:

- 1920x1080
- 2560x1440
- 1366x768
- narrower supported desktop window
- 150% rendering/scaling scenario
- 200% rendering/scaling scenario

At 1920x1080 and 2560x1440, the dashboard should feel intentionally composed rather than simply stretched.

At narrower sizes, intelligently reflow, collapse or scroll secondary information rather than uniformly shrinking the entire interface.


PART 12 — VISUAL QUALITY GATE

This checkpoint has a VISUAL acceptance requirement in addition to automated tests.

Before declaring 7.20 complete:

1. Render/capture the actual implemented WPF Operations workspace at:
   - 1920x1080
   - 2560x1440

2. Compare those captures against the supplied Sierra 4 concept.

3. Perform a dedicated visual-polish pass.

Specifically inspect:

- hierarchy
- whitespace
- alignment
- typography
- density
- map prominence
- panel proportions
- border usage
- yellow accent usage
- status colors
- table readability
- empty/unavailable states
- consistency with the 7.19 shell
- whether the page still looks like legacy Alpha 6

Do not declare the visual migration complete merely because controls render and automated tests pass.

If the implemented page still primarily resembles the old Operations dashboard with rearranged controls, continue the visual migration before completing the checkpoint.


PART 13 — ARCHITECTURAL RULES

Operations remains a presentation consumer.

It may consume:

- FlightSessionSnapshot
- existing application/read services
- existing history/query sources
- existing authoritative operational state

It must not become a second operational engine.

Do not move business logic into the view solely to populate dashboard widgets.

Do not mutate operational state during rendering.

Do not create duplicate flight truth.

Do not bypass FlightSessionCoordinator for live-flight mutations.

Where aggregation/query logic is required for the dashboard, keep it separate from WPF rendering and make its data provenance explicit.


PART 14 — DO NOT DO IN 7.20

Do NOT:

- replace the Sierra 4 shell
- migrate every other workspace
- visually rewrite Dispatch
- visually rewrite Flight Tracking
- visually rewrite Logbook
- build the persistent airline-world simulation
- implement CrewEngine
- implement PassengerEngine
- implement MaintenanceEngine
- implement OCC AI
- implement Map V2
- introduce React
- introduce a production web API
- change SQLite schemas unnecessarily
- change existing flight algorithms
- change phase thresholds
- change telemetry continuity rules
- change route-progress algorithms
- change arrival-estimation algorithms
- change existing profile/storage paths
- perform final Alpha6OPS → Sierra 4 distribution rebranding


PART 15 — REGRESSION REQUIREMENTS

Run the complete Sierra 4 migration regression suite after implementation.

At minimum:

- all previously passing checks remain passing
- 9/9 recorded-flight replays pass
- 91,366/91,366 telemetry samples remain valid
- view recreation preserves active flight
- workspace switching does not reconnect simulator
- presentation refresh does not mutate operational state
- stale simulator callbacks remain rejected
- assignment persistence remains independent of animation
- PIREP persistence/closeout remain independent of animation
- operational behavior remains independent of display wording
- existing persisted data remains readable
- Sierra shell navigation remains functional
- existing retained workspaces remain reachable

The two native simulator checks may remain unavailable if MSFS is unavailable.

Do not count unavailable checks as passed.


PART 16 — CHECKPOINT REPORT

At completion provide:

1. Before/after Operations architecture
2. Before/after Operations visual structure
3. Legacy dashboard elements removed
4. New Operations components created
5. Data source/provenance for every dashboard section
6. Which metrics are real, derived, demo or unavailable
7. Map integration approach used
8. Current/next flight integration
9. Responsive behavior
10. Visual comparison against the supplied Sierra 4 concept
11. Visual polish changes made after comparison
12. Files created/modified
13. Build result
14. Regression counts
15. Replay result
16. Telemetry result
17. Known visual/technical debt
18. Remaining demo/unavailable states
19. Recommendation for the next checkpoint

STOP AFTER CHECKPOINT 7.20.

Do not begin Map V2.
Do not begin another workspace migration.
Do not begin the persistent airline-world engine.