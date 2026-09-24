CHECKPOINT 7.19 — SIERRA 4 DESIGN SYSTEM + APPLICATION SHELL

Checkpoint 7.18B successfully established the application/runtime boundary required for the Sierra 4 UI migration.

Verified baseline:

- Production build/package: PASS
- 1,785 / 1,785 executed checks passed
- 9 / 9 recorded-flight replays passed
- 91,366 / 91,366 telemetry samples processed
- FlightSessionCoordinator now owns the application-lifetime flight session
- Views consume immutable FlightSessionSnapshot state and issue explicit commands
- Existing Core algorithms, storage behavior and simulator integration remain authoritative

We are now authorized to begin the Sierra 4 visual implementation.

OBJECTIVE

Create the Sierra 4 visual foundation and new WPF application shell while preserving all existing functionality.

Use the supplied Sierra 4 Operations UI concept image as the primary visual reference.

This checkpoint is NOT the Operations dashboard migration.

Do not rebuild feature pages yet.
Do not replace the map.
Do not change Core flight logic.
Do not change database schemas.
Do not change SimConnect behavior.
Do not introduce React or a web frontend.
Do not implement the future persistent airline-world engine.


DESIGN INTENT

Sierra 4 should feel like premium professional airline operations software.

The desired character is:

- modern airline OCC / operations-center software
- calm rather than flashy
- dark near-black / charcoal environment
- restrained Sierra yellow accent
- crisp white and neutral-gray typography
- strong information hierarchy
- generous negative space
- subtle depth
- minimal borders
- very limited glow
- restrained gradients
- high information density only where operationally useful
- polished desktop-software feel
- sophisticated rather than game-like
- futuristic because of information behavior, not decoration

Avoid:

- generic AI-generated SaaS appearance
- excessive cards
- every region being boxed
- excessive rounded rectangles
- excessive status pills
- neon/cyberpunk styling
- unnecessary yellow everywhere
- decorative gradients
- oversized dashboard typography
- excessive shadows/glow

The supplied concept is a visual direction, not a requirement for pixel-for-pixel reproduction.


PART 1 — SIERRA 4 DESIGN TOKENS

Create centralized reusable WPF resources for:

COLORS

Define semantic resources rather than hard-coding colors throughout views.

Examples:

- AppBackground
- SurfacePrimary
- SurfaceSecondary
- SurfaceElevated
- TextPrimary
- TextSecondary
- TextMuted
- SierraYellow
- Divider
- Success
- Warning
- Critical
- Information

Use Sierra yellow selectively for brand identity, selected navigation, primary operational emphasis and important interaction states.

Do not use yellow as the default decoration for every component.


TYPOGRAPHY

Establish a clear hierarchy for:

- application title
- page title
- section title
- operational metric
- body
- secondary body
- table
- metadata
- labels
- status
- captions

Favor readability and professional desktop density.


SPACING / LAYOUT

Create consistent resources for:

- spacing
- margins
- control heights
- navigation dimensions
- content widths
- corner radii
- separators
- panel spacing

The new shell must respond to actual viewport dimensions.

Do not reproduce the existing fixed 1920x1080 Viewbox architecture.


PART 2 — REUSABLE SIERRA 4 COMPONENTS

Create reusable WPF styles/templates or controls where appropriate for:

- primary button
- secondary button
- icon button
- navigation item
- selected navigation item
- search/input
- status indicator
- section header
- metric presentation
- data panel
- operational alert
- table/grid
- tabs
- dropdown
- tooltip
- dialog
- loading state
- empty state
- unavailable state
- stale-data state
- error state

Do not create components simply because the concept image contains a rectangle.

Prefer composition and typography over excessive containers.


PART 3 — APPLICATION SHELL

Replace the existing primary application frame with the Sierra 4 shell.

Create:

SIERRA 4 BRAND AREA

Use the existing Sierra 4 branding assets supplied with this task where practical.

Provide a clear location for:

- S4 mark
- Sierra 4 identity

Do not introduce an engine name or "Powered by" branding during this checkpoint.


PRIMARY NAVIGATION

Prepare navigation for:

- Operations
- Flights
- Dispatch
- Crew
- Maintenance
- Fleet
- Airports
- Network
- Reports

Secondary area:

- Settings
- Help/About as appropriate

Do not invent operational functionality for sections that are currently demonstrations or placeholders.


TOP APPLICATION AREA

Create locations for:

- current workspace/page identity
- search
- simulator/connection status
- notifications/messages
- user/profile access

Use actual existing state where already available.

Do not fabricate online/network/aircraft statistics merely to match the concept.


CONTENT WORKSPACE

Create a flexible content host where existing WPF workspaces can continue operating during migration.

Unmigrated pages must remain reachable and functional inside or through the new shell.

The Sierra 4 shell must not require feature pages to be visually migrated all at once.


PART 4 — NAVIGATION ARCHITECTURE

Centralize presentation navigation state rather than continuing to expand ad-hoc button/tag/visibility logic.

Navigation state is PRESENTATION state only.

It must not own or alter:

- active flight session
- simulator connection
- assignment
- telemetry
- operational events
- PIREP state

Switching between Sierra 4 sections must not affect FlightSessionCoordinator lifetime.

Do not introduce an unnecessarily large navigation framework if a clean lightweight WPF solution fits the application.


PART 5 — RESPONSIVE BEHAVIOR

Remove dependence on a fixed 1920x1080 reference surface for the new shell.

Verify reasonable behavior at:

- 1920x1080
- 2560x1440
- narrower desktop windows
- Windows display scaling scenarios already represented by existing tests

Navigation and primary content must remain usable without stretching the entire application as one bitmap-like surface.


PART 6 — LEGACY WORKSPACE COMPATIBILITY

Existing functional workspaces may retain their current internal visual design during this checkpoint.

That is expected.

The objective is:

NEW SIERRA 4 SHELL
        ↓
EXISTING WORKSPACES
        ↓
FlightSessionCoordinator
        ↓
Existing Core / infrastructure

Do not visually rewrite Dispatch, Flight Tracking, Logbook, Fleet, Maintenance or other feature workspaces merely to make the application appear completely migrated.

Their individual migrations will occur in later checkpoints.


PART 7 — REGRESSION REQUIREMENTS

Run the complete Sierra 4 migration regression suite after implementation.

Required:

- all previously passing automated checks remain passing
- 9/9 recorded-flight replays remain passing
- 91,366/91,366 telemetry samples remain valid
- closing/recreating views preserves the active flight
- workspace switching does not reconnect the simulator
- presentation refresh does not mutate operational state
- assignment/PIREP behavior remains independent of animations
- typed operational state remains independent of display wording
- existing persisted data remains readable

The two native simulator checks may remain unavailable if MSFS is unavailable.
Do not report them as passed.


VISUAL REVIEW

In addition to automated regression verification, inspect the implemented shell visually.

Report:

- target resolutions reviewed
- layout compromises
- components that still use legacy styling
- areas intentionally deferred
- accessibility concerns
- any deviations from the supplied Sierra 4 concept and why


COMPLETION REPORT

At completion provide:

1. Before/after shell architecture
2. Design tokens/resources created
3. Reusable components/styles created
4. Navigation architecture
5. Existing workspaces retained
6. Files created/modified
7. Visual review results
8. Build result
9. Regression counts
10. Replay result
11. Telemetry result
12. Known visual/technical debt
13. Recommendation: GO or NO-GO for Checkpoint 7.20 — Sierra 4 Operations Dashboard

STOP after Checkpoint 7.19.

Do not begin the Operations dashboard migration.
Do not replace the map.