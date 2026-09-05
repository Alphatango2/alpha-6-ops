# Alpha 6 OPS

MSFS 2024 virtual-airline operations platform. Flight telemetry comes in (replay
now, SimConnect later), a domain engine derives flight phases and aircraft
rotations, and three clients present it: a WPF desktop pilot app, a local
read-only API, and a React dashboard.

Windows-only. WPF and SimConnect do not build under WSL or Linux.

## Commands

Run from the repository root.

- Build everything: `dotnet build Alpha6Ops.slnx`
- Run tests: `dotnet run --project tests/Alpha6Ops.Tests --no-build`
- Run the console pilot against a fixture: `dotnet run --project src/Alpha6Ops.Pilot --no-build -- samples/delayed-flight.jsonl`
- Run the API: `dotnet run --project src/Alpha6Ops.Api --no-build`
- Dashboard: `cd apps/ops-web && pnpm install --frozen-lockfile && pnpm dev`

The dashboard serves on `http://127.0.0.1:5173` and proxies `/api` to the API on
`http://127.0.0.1:5080`. Both must be running.

## Critical: tests are not `dotnet test`

**`dotnet test` does not work in this repository and never will.** The test
project is a dependency-free console executable that exits nonzero on failure.
Run it with `dotnet run --project tests/Alpha6Ops.Tests --no-build`.

If you find yourself typing `dotnet test`, stop. There is no test framework here.

## Toolchain versions are pinned

- .NET 10 SDK. Not the runtime alone. `global.json` will fail the build on a
  mismatch rather than falling back.
- Node 22.12+ or Node 24.
- pnpm 11.19.0. Use `--frozen-lockfile`; do not regenerate the lockfile.

The backend has no external NuGet dependencies. Do not add one without asking.

## Machine-specific configuration

`simconnect-sdk-path.txt` is gitignored and points at the MSFS SDK install on a
specific PC. Without it, SimConnect-dependent builds will not work on a fresh
clone. `work/` holds a local .NET SDK on the original workspace only and is not
a portable prerequisite.

Consequence: `Alpha6Ops.Core`, `Alpha6Ops.Api`, `tests` and `apps/ops-web` build
anywhere. `Alpha6Ops.Desktop` and the SimConnect adapter need a configured SDK
path first.

## Layout

| Path | Owns |
|---|---|
| `src/Alpha6Ops.Core` | Domain records, phase detection, rotation engine, integration contracts |
| `src/Alpha6Ops.Pilot` | Console replay host, Windows adapter boundary |
| `src/Alpha6Ops.Desktop` | WPF pilot UI, embedded replay, tray lifecycle |
| `src/Alpha6Ops.Api` | Local read-only ASP.NET Core demo API |
| `apps/ops-web` | React / TypeScript dashboard (deprioritized — see below) |
| `tests/Alpha6Ops.Tests` | Executable regression checks |
| `packaging` | Offline desktop packaging and installer source |
| `samples` | SDK-free telemetry fixtures |
| `datasets` | Aircraft catalog source data |
| `docs` | Architecture, data model, roadmap, SimConnect boundary, validation |

## Not in source control

`work/`, `**/bin/`, `**/obj/`, `**/node_modules/`, `**/dist/`, `.env`, `*.user`,
`*.log`, `*.dmp`, `data/`, `simconnect-sdk-path.txt`, and everything under
`outputs/` matching the installer and desktop zip patterns.

The README links to `outputs/Alpha6OPS-Setup-*.exe` and
`outputs/Alpha6OPS-Desktop-*-win-x64.zip` as downloads. Those paths are
gitignored, so they do not exist in a fresh clone. Do not assume a build
artifact is present because the README references it.

## Gotchas

- `FlightSession` applies one replay to the first leg only. Each request creates
  a fresh session; refresh and reset do not preserve actuals. Exercising a second
  leg requires constructing a new scoped session, which does not exist yet.
- Block-in detection defaults to parking brake plus engines off plus near-zero
  groundspeed. It is configurable per aircraft via `AircraftGroundProfile`/
  `AircraftGroundProfiles.ForFamily` in `Alpha6Ops.Core`, but every family
  currently resolves to that same default — no real per-type ground procedures
  have been supplied yet.
- Views (Simple / Advanced / OCC) are presentation modes only. They are not
  authorization roles and enforce nothing.
- The API refuses to start outside the Development environment and exposes only
  the synthetic `alpha6` tenant. This is deliberate. Never expose it publicly.
- All sample dates are fixed to 2 September 2026 and all times are UTC. Replay
  runs instantly rather than in simulation time.
- `--simconnect` on the console pilot returns an unsupported message and exit
  code 2. That is current expected behavior, not a bug to fix incidentally.

## Desktop-first direction

Dan (repo owner, pilot-side stakeholder) wants Alpha 6 OPS to be a desktop app,
not a web app. As of 2026-09-04, active development targets
`src/Alpha6Ops.Desktop` only. `apps/ops-web` stays in the tree (still builds,
still useful as a quick reference for what the API returns) but should not
receive new features going forward.

## Boundaries

- Do not add NuGet or npm dependencies without asking.
- Do not regenerate `pnpm-lock.yaml`.
- `src/Alpha6Ops.Desktop` is open for active work: Dan wants the desktop app to
  be the primary client going forward, not the web dashboard. `packaging` and
  the SimConnect adapter still need coordination before changes.
- Do not commit anything under `outputs/`.
- Do not weaken the API's environment or tenant restrictions to make something
  work locally.

## TBD, confirm before relying on

<!-- Fill these in after talking to the repo owner, then delete this section. -->

- Which tests currently fail on `main`, if any.
- Whether `main` history gets rewritten or force-pushed.
- Provenance and refresh process for the `datasets` aircraft catalog.
- Where release binaries actually live, since they are not in the repo.
