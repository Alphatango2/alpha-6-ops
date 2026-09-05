# Desktop operations dashboard

The Windows WPF application now follows Dan's dashboard reference: a black/navy shell with yellow accents, supplied Alpha 6 branding, next-flight photography, left navigation, connection status, alerts, eight module tiles, a route map, operations table, fleet ring and company notes.

The supplied visual target is preserved in [assets/design/dashboard-reference.png](../assets/design/dashboard-reference.png). Local screenshots from the verified build are available under `work/dashboard-review/latest/`, including `dashboard-default.png` and previews of the flight tools, preflight checklist and module workspaces.

## Open the application

From the repository root:

```powershell
dotnet build Alpha6Ops.slnx
dotnet run --project src/Alpha6Ops.Desktop --no-build
```

The executable is `src/Alpha6Ops.Desktop/bin/Debug/net10.0-windows/Alpha6OPS.exe`. This is a development build; release packaging was not changed. Close/minimize keeps the process in the tray. Use **Exit OPS** to quit.

The default window targets 1536 × 1024 physical pixels, bounded by the available work area. Saved sizes are restored in WPF device-independent units. The body and navigation scroll independently on smaller displays; 1366 × 768 and 1100-pixel widths are covered by visual checks.

## Walkthrough

1. The initial replay scenario shows A601 ORD → DTW and its three-leg aircraft rotation. The hero and operations table consume the shared `RotationPlanner`; the background photograph illustrates the visual design and is not aircraft identity evidence.
2. **View flight details** opens planned/projected times, block duration, delays and a personal preflight checklist. **Start preflight** opens the same preparation view. Checks are saved per aircraft, flight number and scheduled date.
3. Select a row and choose **Watch selected**. The Watchlist tab filters to saved flights; Assigned shows incomplete legs. Double-click a flight to open its details.
4. **Flight tools** (footer, hero ellipsis, or Settings) opens the drawer containing pilot name, active-flight/SimBrief entry, simulator connection, replay fixtures, timeline/debrief, history, diagnostics and tray controls.
5. Run the delayed fixture. A601 completes; the hero advances to A602. The operations table shows A602 +30 minutes and A603 +5 minutes. Original replay/timeline/debrief tools remain available.
6. Import or enter an active flight. The dashboard shows the active assignment and, when telemetry is available, the live rotation's actuals and projections. Assignment edits are blocked during a running replay or open simulator session.
7. Select any map station to focus connections. Use +/−, reset, wheel zoom, or drag to pan. Network in the sidebar opens a larger map.
8. Open a demo alert to inspect and acknowledge it locally. The panel refills from the remaining open alerts. View all includes acknowledged items.
9. Open Operations, Maintenance, Crews, Passengers, Weather, Dispatch or OCC to inspect the corresponding desk. Each provides summary metrics, search, status filtering, selectable detail and JSON export of the visible records.
10. Aircraft opens the existing searchable reference catalog. Reports reads local flight history. Company messages are bundled product briefings with locally saved reading status.

## Data sources and limits

| Surface | Source / behavior |
| --- | --- |
| Hero, operations table, flight details | Current replay rotation or entered live assignment, using the Core scheduling engine. All times carry UTC/date context. |
| Simulator badge | Actual connection state; no simulated connected indicator. The adapter itself was not changed by the dashboard work. |
| Header clock | Current real-world UTC; separate from dated fixture and simulator clocks. |
| Fleet ring | Actual bundled SQLite reference catalog: 1,006 aircraft, 999 Active, seven Parked, ten families. Reference status does not indicate virtual fleet availability or airworthiness. |
| Map | Hand-drawn schematic US network. Curves and aircraft symbols are illustrative. The current adapter does not supply position. |
| Weather and broader operations desks | Explicitly labeled demonstration scenarios. No live weather, traffic, crew legality, passenger bookings or maintenance release system is connected. |
| Alerts | Fixed demonstration scenario; local acknowledgment does not resolve an external incident. Relative ages are part of the fixed scenario. |
| Dispatch | Local preparation desk; no chat, voice, dispatch messaging or flight-release service. |
| Company messages | Bundled product notes; no company messaging server. |
| Reports | Existing SQLite flight history, currently populated by replay runs. Live diagnostics remain in the journal/export path. |
| Preflight checklist | Personal preparation aid, not performance validation or authorization to depart. |

The sample fleet, weather and disruption records remain separate from live assignments and the read-only aircraft reference catalog. The dashboard does not claim the scenario entries describe real aircraft condition, flights or passengers.

## Local state and implementation

`%LOCALAPPDATA%/Alpha6Designs/Alpha6OPS/dashboard-state.json` stores watchlist keys, acknowledged demo alert IDs, read message IDs and per-flight checklist items. Writes use a temporary file followed by replacement. Existing pilot/window preferences, assignment, SimBrief cache, journals and databases keep their existing locations.

- `MainWindow.xaml`: dashboard layout and flight-tools drawer.
- `DashboardStyles.xaml`: shared dashboard controls, typography, table and scrollbar styles.
- `MainWindow.Dashboard.cs`: presentation updates and dashboard actions.
- `DashboardData.cs`: typed rows, explicit demo desk content and local annotation state.
- `NetworkMap.cs`: offline, selectable, pannable/zoomable network illustration.
- `OperationsWorkspaceWindow.cs`: searchable desks, export, flight details/preflight and enlarged network view.
- `Assets/Dashboard/`: supplied branding and generated aviation photographs; see [asset provenance and prompts](dashboard-assets.md).

No NuGet/npm dependencies, web dashboard changes, packaging changes or server deployment were introduced.

## Verification

```powershell
dotnet run --project tests/Alpha6Ops.Tests --no-build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/verify-desktop.ps1
```

The verification script launches the built WPF app with its explicit diagnostic flag, captures actual rendered windows, and writes a unique run under `work/dashboard-review/`. It leaves release outputs untouched. Diagnostic mode uses isolated dashboard state, flight history and program-monitor files, and does not load the user's saved assignment or preferences.

Current verification: full solution builds with zero warnings/errors; 54 Core checks, 16 existing desktop checks and 51 dashboard checks pass. Dashboard checks cover projection integration, local persistence/reload, empty states, module search/filtering, readable table columns, map selection/zoom/reset and simulator control guards. Visual captures cover the full dashboard, compact layouts, flight drawer, preflight, desks and expanded map.

Real MSFS telemetry, live third-party data, clean-PC installation and end-to-end airline operations are not validated by these checks.
