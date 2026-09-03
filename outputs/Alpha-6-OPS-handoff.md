# Alpha 6 OPS foundation

UPDATE: Windows desktop preview 0.2 is now available as `Alpha6OPS-Setup-0.2.exe` and `Alpha6OPS-Desktop-0.2-win-x64.zip` in this outputs folder. It runs natively with a bundled runtime, Simple/Advanced views and close-to-tray behavior. See `Desktop-Quick-Start.txt` and root `docs/desktop.md`. Setup payload extraction and the extracted WPF application were verified; interactive installation/uninstallation and fresh-PC testing remain. Earlier foundation notes below predate this addition.

Created directly in `C:\Users\DanniBaLs\Documents\Codex\2026-09-02\alpha-6-ops` as a new local Git repository on `main`. Source is in the project root, not this outputs folder. No remote or commit has been created.

Includes a .NET 10 deterministic operations engine, console pilot replay, local ASP.NET Core API, React/TypeScript dashboard with Simple / Advanced / OCC views, sample telemetry, 22 regression checks, and the product/architecture/data-model/UI/SimConnect/two-developer roadmap documents.

The sample captures block-out at 10:25Z, takeoff at 10:40Z, landing at 11:30Z and block-in at 11:45Z. The next departure inherits 30 minutes of delay; later schedule slack reduces the following departure delay to 5 minutes.

Validation: .NET build clean, all 22 checks passed, pilot replay and API smoke checks passed, frontend type-check/build passed, browser replay/reset/view switching verified. Desktop layout visually inspected. Detailed evidence is in `docs/validation.md`.

Local preview: http://127.0.0.1:5173/ while the development servers remain running. The API listens on loopback port 5080. Stop the two development servers when finished. See root `README.md` for portable setup commands and `docs/product-roadmap.md` for the two-developer plan.

This is an initial foundation: live SimConnect, persistent assignments, authentication and role enforcement, Windows tray UI/installer, telemetry upload, and voice dispatch are not implemented. Demo state is in memory and replays only the first assigned leg. Full MVP acceptance remains future work. The SDK adapter is explicitly unsupported rather than simulated as a live connection.

Current machine: local SDK at `work/dotnet/dotnet.exe`; Node and pnpm were accessed from the Codex bundled runtime. For normal development install .NET 10 SDK, Node 24 and pnpm 11.19.0. No proprietary SDK binaries or secrets are included. Nothing was deployed or published.
