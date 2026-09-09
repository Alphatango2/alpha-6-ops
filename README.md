# Alpha 6 OPS

## Desktop dashboard update

The Windows application now follows Dan's operations-dashboard mockup, with supplied branding, aviation photography, a next-flight hero, module desks, alerts, network map, operations table and fleet chart. Flight details, personal preflight checks, watchlists, replay/live controls and local history are connected. Weather, crew, maintenance and passenger desks are clearly labeled demonstration scenarios. See the [dashboard walkthrough, data sources and verification](docs/dashboard.md). Historical preview notes below describe earlier versions and may no longer reflect the current desktop UI.

**You fly the airplane. We'll run the airline.**

Alpha 6 Designs' MSFS 2024 virtual-airline operations platform. This repository is the initial foundation, not a completed live simulator client. Black/yellow textual branding is intentional; no original logo has been supplied.

## Windows desktop preview

Published release **0.12.1 Window controls** added direct, verified minimize, maximize/restore, and close actions to the shared secondary-window title bar. Current source removes the short-lived carrier badge experiment: SimBrief's combined airline ICAO and flight number, such as `DAL742`, now appears as one larger identifier. The hero and Fly tile use separate unbranded aircraft-at-gate photographs. Install the published build with `outputs/Alpha6OPS-Setup-0.12.1.exe`, or fully extract `outputs/Alpha6OPS-Desktop-0.12.1-win-x64.zip` for portable use.

Current local builds also include **Alpha 6 Flight Lab**, a separate desktop companion that sends virtual flight phases and failure scenarios through the same Alpha 6 OPS live-telemetry boundary. See [Flight Lab usage and limits](docs/flight-lab.md).

The local v0.13.0 work now includes **Flight Tracking Checkpoints 1 and 2**: Flights is renamed in the navigation, the full tracker opens inside the main application, dashboard actions link to it, View Flight Deck replaces Start Preflight, and assignments have a dedicated tracking layout plus a defined No Active Flight state. Fresh SimBrief imports retain airport and navlog coordinates and plot the complete planned route on a detailed orthographic globe with Natural Earth coastlines, great-circle paths, automatic framing, drag rotation, zoom, and date-line handling. Live SimConnect aircraft movement follows in Checkpoint 3.

Latest: **0.12.0 Flight Deck foundation** simplifies Flight Tools around the active assignment, SimConnect session, live timeline/debrief, and rotation detail, leaving room for the expanded flight-tracking experience. Recorded-flight replay controls are no longer pilot-facing. Flight History, Log Database, test-log export, health monitoring, local data, and crash-report access now live together in a redesigned **Settings → Logs & Diagnostics** workspace. SimBrief import assigns departure and arrival gates: explicit gates in dispatch remarks take priority, followed by stable airport/airline catalog suggestions, with an unassigned state when no trustworthy match exists. Pilots can review and correct both gates before saving. Install with `outputs/Alpha6OPS-Setup-0.12.0.exe`, or fully extract `outputs/Alpha6OPS-Desktop-0.12.0-win-x64.zip` for portable use.

The 0.12.0 display work centers the final saved window dimensions in the active monitor's usable work area on every normal launch, including monitors with taskbar offsets and non-primary screen coordinates.

General settings include persisted minimize-to-tray behavior, taskbar notification flash, notification sound, weight/altitude/landing-distance units, advanced Flight Deck controls, and Save/Reset Defaults actions. The Alpha 6 operations theme remains the supported appearance; no inactive dark-mode, Discord, remote analytics, or remote crash-upload toggles are shown.

All internal secondary windows and application notices share reusable Alpha 6 navy/yellow chrome, borders, logo/title treatment, caption controls, typography, inputs, panels, and tables. Native Windows open/save pickers retain the operating-system appearance.

Latest: **0.11.6 Monitor-optimized display** uses a 1920×1080 reference surface that scales proportionally to the active monitor, including 2560×1440 displays. The dashboard and navigation remain stationary, every panel stays visible, and scrollbar, mouse-wheel, and touch-panning movement are disabled. It also detects the monitor work area and DPI, expands undersized saved windows, preserves useful custom sizes, and includes the refreshed A6-themed installer with clearer release information and a centered install/upgrade action. Install with `outputs/Alpha6OPS-Setup-0.11.6.exe`, or fully extract `outputs/Alpha6OPS-Desktop-0.11.6-win-x64.zip` for portable use.

Version **0.11.5** combined the organized Settings and Plugins workspace with the adaptive dashboard layout, local weather presentation, dual clocks, quick SimConnect access, monitor refinements, and expanded header/display/responsive verification from the shared fork.

Version **0.11.4** added a dedicated Settings workspace with General, Simulator, Flight Tracking, SimBrief, Plugins, Logs & Diagnostics, and About tabs. The Plugins page reports the real status of the built-in SimConnect, SimBrief, diagnostics, and flight-history integrations.

Version **0.11.3** added hover highlighting across every navigation tab, removed the permanent Dashboard selection fill, and introduced matching outlined pilot and sun-behind-cloud icons for Crews and Weather.

Version **0.11.2** combined the A6 application icon, SimBrief import, live flight tracking and reliability tools with the desktop operations dashboard, live SimConnect rotation projection, reconnect and telemetry-clock checks, flight history, timeline replay, post-flight debrief, persisted preferences and single-instance protection.

Version **0.10.2** added the polished black-and-yellow A6 mark derived from the supplied Alpha 6 Designs logo. The executable, application windows, taskbar, Start-menu shortcut, system tray, installer and uninstaller share a multi-resolution Windows icon optimized down to 16 pixels.

Latest: **0.10.1 Upgrade installer** automatically replaces a verified earlier preview installation after OPS has exited. Program files are swapped through staging with rollback on activation failure; logs, diagnostics, settings and the SimBrief cache remain in their separate user-data directory.

Latest: **0.10 SimBrief import** adds direct import of the user's latest generated OFP by Navigraph Alias/SimBrief username. The assignment window maps the route, aircraft, registration and operational times, warns when a briefing is old, and retains the latest successful response for offline reuse.

Latest: **0.9 Active flight tracking** replaces the sample card with a live tracking bar. Use **Set active flight** to save a flight number, aircraft registration, route and planned UTC times; SimConnect then updates actual departure, delay-adjusted ETA, elapsed time, phase, aircraft title and progress.

Latest: **0.8 Reliability** adds automatic managed-crash reports, a 15-second program/SimConnect health heartbeat, unclean-exit detection, and a local SQLite database that catalogs flight journals, exports, and crash reports. Use **Log database** to review the indexed files. Install with `outputs/Alpha6OPS-Setup-0.8.exe`; the portable ZIP must be fully extracted before launch.

Latest: **0.7** restores resizing and maximizing. The main window still opens at 1366×768 physical pixels on each launch; that size is applied once, not enforced afterward. The aircraft database window is also resizable. All 0.6 logging features remain included. Download `outputs/Alpha6OPS-Desktop-0.7-win-x64.zip`.

Latest: **0.6 Test-flight logs** adds automatically saved live-session journals and an **Export test log** button. Upload the resulting JSON for diagnosis. See [logging behavior](docs/test-flight-logs.md); the portable download is `outputs/Alpha6OPS-Desktop-0.6-win-x64.zip`.

Latest: **0.5** fixes the main OPS window at 1366×768 physical pixels with DPI-aware sizing and removes the manual Disconnect button. Minimizing/X continues to use the tray; Exit OPS ends the process and connection. Use `outputs/Alpha6OPS-Desktop-0.5-win-x64.zip` or the 0.5 setup. If the aircraft or simulator clock changes during monitoring, exit and reopen OPS at the gate.

Latest: **0.4 Aircraft database** adds a searchable, offline SQLite catalog of Delta's 1,006 current mainline aircraft as observed on Airfleets on 2026-09-02 (999 active, seven parked). Use `outputs/Alpha6OPS-Setup-0.4.exe` or `outputs/Alpha6OPS-Desktop-0.4-win-x64.zip` and click **Aircraft database**. See [database provenance and refresh](docs/aircraft-database.md). Earlier release notes below remain historical.

Latest: **0.3 SimConnect test build** adds a read-only native SDK adapter and a colored/text connection badge. See `outputs/Alpha6OPS-Setup-0.3.exe` or `outputs/Alpha6OPS-Desktop-0.3-win-x64.zip`, and [live connection notes](docs/simconnect-live.md). The package is configured for the installed SDK on this PC. Successful live telemetry is not yet verified; the demo rotation remains separate. The 0.2 notes below describe the original replay release.

The pilot interface is now also a native WPF Windows application. Run `outputs/Alpha6OPS-Setup-0.2.exe` to install the preview, or extract `outputs/Alpha6OPS-Desktop-0.2-win-x64.zip` and open `Alpha6OPS.exe`. Both include a private .NET runtime; neither needs the API, browser, Node, or internet. The generated binaries are ignored by Git. See [desktop build and packaging](docs/desktop.md) for rebuilding and current verification limits.

The desktop app provides Simple/Advanced views, an approximately eight-second embedded replay, rotation timings, milestones, and a system tray icon. Closing/minimizing hides the window while replay continues. Double-click the tray icon to reopen; use Exit OPS to quit. This is an unsigned replay preview, not live SimConnect capture.

## Run the foundation

Prerequisites: .NET 10 SDK (not runtime alone), Node 22.12+ or Node 24, and pnpm 11.19.0. Run commands from this repository root unless stated otherwise. The backend has no external NuGet package dependencies.

```powershell
dotnet build Alpha6Ops.slnx
dotnet run --project tests/Alpha6Ops.Tests --no-build
dotnet run --project src/Alpha6Ops.Pilot --no-build -- samples/delayed-flight.jsonl
dotnet run --project src/Alpha6Ops.Api --no-build
```

In a second terminal:

```powershell
cd apps/ops-web
pnpm install --frozen-lockfile
pnpm dev
```

Open the localhost address printed by Vite (normally http://127.0.0.1:5173). Click **Run delayed-flight replay**, then select Advanced or OCC. The API listens on http://127.0.0.1:5080; Vite proxies `/api`. For a production asset build run `pnpm build`; serving those assets is not configured. No deployment is included.

For this prepared workspace only, a local SDK also exists at `work/dotnet/dotnet.exe`; substitute that path for `dotnet` if it is not installed globally. `work/` is ignored and is not a portable prerequisite. Tests are a dependency-free executable that exits nonzero on failure; use the command above, not `dotnet test`.

## What works

- JSONL simulator replay through the same telemetry interface intended for SimConnect.
- Flight-scoped gate → taxi-out → airborne → taxi-in → complete state machine with three-second confirmation, stale/duplicate suppression, pause/slew suppression, and gap handling.
- Block-out, takeoff, landing and block-in event timestamps; confirmed go-arounds return to airborne.
- Three-leg aircraft rotation with deterministic minimum-turn propagation and schedule-slack recovery.
- Read-only local API and React Simple / Advanced / OCC shell with loading/error states.
- Executable domain regression tests and a committed dashboard dependency lockfile.

Sample: A601 leaves 25 minutes late and blocks in 30 minutes late. A602 inherits 30 minutes; A603 retains 5 minutes after its extra ground time. All dates are fixed on 2 September 2026; all times are UTC. Replay runs instantly without waiting for simulation time. Reset preview reloads the original plan.

## Deliberate limits

No live SimConnect capture, persistence, authentication, role enforcement, assignment selection, pilot-to-API upload, multi-aircraft operations, signed release installer, or voice service yet. The console pilot's `--simconnect` returns an explicit unsupported message and exit code 2. The API refuses non-Development environments and exposes only the synthetic `alpha6` tenant. Views are presentation modes, not authorization roles. Never expose this demo API publicly.

`FlightSession` applies one replay to the first leg only. Each request creates a fresh session; refresh/reset does not preserve actuals. To exercise another leg requires constructing a new scoped session in future work. Block-in uses parking brake + engines off + near-zero groundspeed; this is a demo heuristic, not a universal aircraft procedure. No gate/geofence or assigned-aircraft validation exists yet.

## Repository guide

| Path | Purpose |
| --- | --- |
| `src/Alpha6Ops.Core` | Domain records, phase detection, rotation engine, integration contracts |
| `src/Alpha6Ops.Pilot` | Console pilot replay host, future Windows adapter boundary |
| `src/Alpha6Ops.Desktop` | Native WPF pilot UI, embedded replay and tray lifecycle |
| `packaging` | Reproducible offline desktop packaging and preview installer source |
| `src/Alpha6Ops.Api` | Local read-only ASP.NET Core demo API |
| `apps/ops-web` | React / TypeScript dashboard shell |
| `tests/Alpha6Ops.Tests` | Deterministic executable regression checks |
| `samples` | SDK-free telemetry fixture |
| `docs` | Product, architecture, model, UI, SimConnect and delivery plans |

See [architecture](docs/architecture.md), [product and roadmap](docs/product-roadmap.md), [data model](docs/data-model.md), [UI plan](docs/ui-shell.md), [SimConnect boundary](docs/simconnect.md), and [validation](docs/validation.md).
