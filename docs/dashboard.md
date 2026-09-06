# Desktop operations dashboard

The Windows WPF application now follows Dan's dashboard reference: a black/navy shell with yellow accents, supplied Alpha 6 branding, next-flight photography, left navigation, connection status, alerts, eight module tiles, a route map, operations table, fleet ring and company notes.

The September 6 integration uses the shared v0.11.4 base (`0723f17`): the themed window controls, updated navigation icons/hover behavior and seven-section Settings workspace are retained alongside the local weather, clocks and adaptive dashboard. Settings opens its own workspace; Flight tools remains available separately.

The supplied visual target is preserved in [assets/design/dashboard-reference.png](../assets/design/dashboard-reference.png). Local screenshots from each verified build are available in timestamped folders under `work/dashboard-review/`, including `dashboard-default.png` and previews of the flight tools, preflight checklist and module workspaces.

## Open the application

From the repository root:

```powershell
dotnet build Alpha6Ops.slnx
dotnet run --project src/Alpha6Ops.Desktop --no-build
```

The executable is `src/Alpha6Ops.Desktop/bin/Debug/net10.0-windows/Alpha6OPS.exe`. This is a development build; release packaging was not changed. Close/minimize keeps the process in the tray. Use **Exit OPS** to quit.

The default window targets 1536 × 1024 physical pixels, bounded by the current monitor's usable area. Saved sizes are restored in WPF device-independent units; a maximized exit preserves the normal restore size. The body and navigation scroll independently. The normal minimum is 640 × 480 logical pixels, reduced if the monitor's usable area is smaller.

Layout adapts to logical window width (after Windows display scaling), including during resize, maximize/restore and monitor changes:

- Below 1500: the network map moves below the module tiles; operations occupies a full row above fleet/messages.
- Below 1250: navigation becomes an icon rail with hover labels and accessible names; alerts move below the hero.
- Below 1150: connection, dispatch, weather and clocks form a two-row group alongside the logo; local/Zulu clocks stack inside their card.
- Below 900: operations, fleet and messages stack into separate rows.
- Module cards automatically use two to four columns while maintaining readable widths. The rest of the dashboard stays at its normal text size instead of shrinking the entire UI.

The body fills the available height on larger windows. After measuring each section's minimum content height, spare space is shared between the hero/alerts (40%), module tiles/map (35%), and operations/fleet/messages (25%). At smaller sizes the sections keep their natural minimums and the body scrolls. This updates continuously within a breakpoint, including height-only resizing. Module photographs and the map stretch with their cards, while text retains its readable size. Section and tile gaps are 12 logical pixels. Status-card contents are centered horizontally and vertically; long weather labels, module captions, alerts and message text wrap instead of being cut off.

The existing PerMonitorV2 manifest is retained. Monitor changes, DPI changes and display reconfiguration fit oversized windows to the destination work area, keeping the window accessible. Screen placement uses physical pixel coordinates so secondary displays can have negative origins. Manual resizing on the same monitor is not forcibly restricted to one screen, allowing windows to span monitors.

Maximized windows now use the current monitor's work area, excluding the taskbar. A native `WM_GETMINMAXINFO` hook is installed before saved maximized state is applied; display/work-area changes also refit maximized windows. This addresses the custom-frame taskbar overlap described in [Microsoft's WindowChrome documentation](https://learn.microsoft.com/en-us/dotnet/api/system.windows.shell.windowchrome). The footer has a 32-pixel exit click target and bottom clearance; normal resizing and restore dimensions are preserved.

The header is ordered logo → simulator connection → dispatch → local weather → local/Zulu clocks. Flexible card widths fill the available space after the logo, with consistent gaps and larger status/date labels. On wide screens the clocks are side by side, local first and Zulu accented yellow. The flight-simulation tagline and duplicate assignment/simulator-UTC subtitle have been removed; the hero retains its live/replay context. Weather unit cycling, condition icons, tooltips, dispatch navigation, and both second-resolution clocks remain functional.

The simulator status card is also a keyboard-accessible quick-connect button. **Click to connect** calls the same connection flow as Flight tools, including its existing retries and session guards. During connection, reconnection, an active session or replay it offers **View controls** and opens Flight tools; it never disconnects a flight or starts a second connection from another click. Failure restores quick retry. Compact status labels preserve the full status/error context in tooltips.

Startup keeps the bottom rotation-detail/flight-milestone panel closed, even when legacy saved preferences contain `Advanced: true`. Other pilot, fixture and window preferences are retained. Quick connect explicitly closes any already-open detail panel, including when opening controls for an existing connection. Both connectors use `ConnectSimulatorAsync`; the header passes `showRotationDetails: false`, while the Flight tools connector retains `true`. Rotation details remain available through **Flight tools → Show rotation detail**.

Shared dashboard buttons no longer use a persistent white border for focus after a mouse click. Hover and pressed feedback remain. Keyboard navigation uses a separate yellow/dark focus overlay via WPF's [keyboard-only FocusVisualStyle](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/styling-for-focus-in-controls-and-focusvisualstyle), without changing layout or clearing keyboard focus. Header checks verify focusability, unchanged border thickness/color, and the shared focus style on connection, dispatch and weather buttons.

A terminal connection failure flashes the card red once, fading back to its normal navy background over 650 ms. The dot stays red, the label remains **FAILED**, and **Click to connect** is available as soon as the existing connection cleanup completes. Retrying immediately replaces the flash with the new state; an older animation cannot change the new status. Temporary simulator/I/O failures still follow the existing amber automatic-reconnect path (3, 5, 10, 20, then 30-second waits); this styling change does not alter that behavior. Smoke checks verify the neutral resting background, persistent red dot, repeated failure flash and an immediate retry during the flash.

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
| Header clocks | Real-world Zulu and Windows local time, both with seconds and their own dates; separate from fixture and simulator clocks. Local time follows Windows time-zone and daylight-saving rules. |
| Header weather | Current model weather from Open-Meteo for the approximate city detected from the user's internet connection. Units alternate between Fahrenheit and Celsius every five seconds. |
| Fleet ring | Actual bundled SQLite reference catalog: 1,006 aircraft, 999 Active, seven Parked, ten families. Reference status does not indicate virtual fleet availability or airworthiness. |
| Map | Hand-drawn schematic US network. Curves and aircraft symbols are illustrative. The current adapter does not supply position. |
| Weather and broader operations desks | Explicitly labeled demonstration scenarios, separate from the header's local weather. No live airport weather, traffic, crew legality, passenger bookings or maintenance release system is connected. |
| Alerts | Fixed demonstration scenario; local acknowledgment does not resolve an external incident. Relative ages are part of the fixed scenario. |
| Dispatch | Local preparation desk; no chat, voice, dispatch messaging or flight-release service. |
| Company messages | Bundled product notes; no company messaging server. |
| Reports | Existing SQLite flight history, currently populated by replay runs. Live diagnostics remain in the journal/export path. |
| Preflight checklist | Personal preparation aid, not performance validation or authorization to depart. |

The sample fleet, weather and disruption records remain separate from live assignments and the read-only aircraft reference catalog. The dashboard does not claim the scenario entries describe real aircraft condition, flights or passengers.

## Local state and implementation

The header shows the complete supplied logo, including its bottom aviation strip, in a larger area with high-quality bitmap scaling. Only blank background above/below the artwork is excluded.

Local weather starts asynchronously on normal launch. [ipwho.is](https://ipwhois.io/documentation) resolves the connection's approximate city; [Open-Meteo](https://open-meteo.com/en/docs) receives its rounded coordinates and returns current temperature and WMO conditions. These HTTPS requests share the connection IP with the providers, but do not send pilot names, assignments, simulator telemetry, or saved flight records. No GPS/device location is requested and the location/weather response is held only in memory. VPNs and ISP routing can affect the detected city. Local time uses Windows settings independently of IP location, so it also works offline.

Weather refreshes every 15 minutes, rechecking location each time. Failed requests retry after five minutes; a previous reading remains visibly marked **STALE**, as do observations older than 45 minutes. Before any successful lookup the header shows a dash, never a sample temperature. Click the header weather tile for the full city, both temperature units, model timestamp and provider attribution. The separate Weather desk remains a demo. Open-Meteo's free endpoint is for non-commercial use; review the provider's commercial plan before commercial distribution.

A small vector icon beside the temperature represents clear, partly cloudy, overcast, fog, rain, snow or thunderstorms using the same WMO code as the condition text. Unknown/unavailable conditions use a neutral symbol. Stale icons are dimmed while the text remains explicitly marked stale.

Weather-source recommendation (not implemented): default to the assigned departure airport for preparation, then offer destination weather while airborne; retain local weather when there is no assignment. Airport observations can come from the [Aviation Weather Center's METAR service](https://aviationweather.gov/). Label real-world observations separately from simulator weather, since a custom simulator scenario may differ from current real-world conditions.

`%LOCALAPPDATA%/Alpha6Designs/Alpha6OPS/dashboard-state.json` stores watchlist keys, acknowledged demo alert IDs, read message IDs and per-flight checklist items. Writes use a temporary file followed by replacement. Existing pilot/window preferences, assignment, SimBrief cache, journals and databases keep their existing locations.

- `MainWindow.xaml`: dashboard layout and flight-tools drawer.
- `DashboardStyles.xaml`: shared dashboard controls, typography, table and scrollbar styles.
- `MainWindow.Dashboard.cs`: presentation updates and dashboard actions.
- `MainWindow.Header.cs` and `LocalWeatherService.cs`: second-resolution clocks, unit cycling, asynchronous location/weather requests and stale states.
- `WeatherConditionIcon.cs`: DPI-independent condition artwork.
- `MainWindow.Responsive.cs`, `DashboardTilePanel.cs` and `MainWindow.Display.cs`: adaptive panel layout, tile sizing, monitor fitting and display-change handling.
- `DashboardBodyPanel.cs`: minimum-size measurement and distribution of spare viewport height across the main sections.
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

The verification script launches the built WPF app with its explicit diagnostic flag, captures actual rendered windows, and writes a unique run under `work/dashboard-review/`. Use `-Configuration Release` to verify a Release build while an older Debug app is open. Diagnostic mode uses isolated dashboard state, flight history and program-monitor files, and does not load the user's saved assignment or preferences. Automatic weather requests are disabled in diagnostic mode; header tests use controlled HTTP responses and a Milwaukee weather fixture for screenshots.

Current verification: Release solution builds with zero warnings/errors; 54 Core checks, 16 existing desktop checks and 352 dashboard checks pass (422 total). Quick-connect checks cover keyboard focusability, its accessible name, action labels, guarded clicks opening controls in simulated connection states, and retry availability after failure; they do not initiate a live MSFS connection. Header checks cover condition icons, Fahrenheit/Celsius cycling, negative temperatures, separate local/UTC dates, daylight-saving transitions, stale/missing readings, HTTP response parsing, rate limits and culture-independent coordinate formatting. Dashboard checks also cover the Settings workspace and plugin status, full-height viewport fill, minimum card sizes, shared tile/map edges, centered status contents, wrapped captions, projection integration, local persistence/reload, empty states, module search/filtering, readable table columns, map selection/zoom/reset and simulator control guards. New checks cover header text fit at both sides of the 1150-pixel breakpoint, full-width card placement, clock reflow, footer click targets, maximized work-area bounds, Exit OPS screen coordinates/hit testing on both connected monitors, restore dimensions, and calculated taskbar offsets at negative monitor origins. Live HTTP checks previously resolved this development connection's city and retrieved its current weather successfully.

Responsive captures cover 640×480, 720×480, 900×700, 1024×768, 1366×768, 1920×1080, 2560×1080, 2560×1392 and 900×1200 logical window sizes, plus 150%/200% bitmap renders. Size-fitting checks cover scaled work areas, including a 1366-pixel laptop at 150%. The diagnostic window was also moved across both attached 2560×1440 monitors and fit within each 2560×1392 usable area. `monitor-smoke.json` records the display configuration. Both physical displays were at 100%; physical mixed-DPI switching, monitor unplugging, and negative-origin arrangements still need hardware validation.

Real MSFS telemetry, clean-PC installation and end-to-end airline operations are not validated by these checks. The deterministic smoke suite does not depend on live third-party availability.
