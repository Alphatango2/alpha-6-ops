# Alpha 6 OPS

**You fly the airplane. We'll run the airline.**

Alpha 6 Designs' MSFS 2024 virtual-airline operations platform. This repository is the initial foundation, not a completed live simulator client. Black/yellow textual branding is intentional; no original logo has been supplied.

## Windows desktop preview

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
