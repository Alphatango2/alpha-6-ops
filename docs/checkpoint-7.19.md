# Checkpoint 7.19 — Sierra 4 design system and application shell

**Complete. Recommendation: GO for Checkpoint 7.20, when separately authorized.** Local branch `codex/checkpoint-7.19`, based on `ccad431`. Verified 24 September 2026. No 7.20 work was started. The application was not installed over the pilot's copy or pushed to GitHub. Package identity/version remains 0.22.0; the visible shell is Sierra 4.

Full-run evidence: [`20260924-085644-1405e8a0/summary.json`](../work/checkpoint-7.18a/20260924-085644-1405e8a0/summary.json). The complete production solution, package, test host, all recorded flights and remaining compatibility checks passed in this one final run. Earlier failed/development runs remain available and are excluded from the final counts.

## 1. Before/after shell architecture

**Before:** MainWindow's fixed 1920×1080 reference surface and outer Viewbox scaled the frame and feature pages together. Button tags and several visibility handlers controlled navigation.

**After:** MainWindow composes a native WPF `SierraShell` with a flexible header, navigation rail, content presenter and footer. The shell responds to its actual viewport. Existing page instances occupy a scrolling compatibility host; their internal layouts remain intact. A one-time composition adapter preserves their WPF namescope and existing bindings.

```text
Sierra 4 shell / design resources / presentation navigation
    ↓ existing workspace instances and retained dialogs
    ↓ explicit commands; detached snapshots flow back up
FlightSessionCoordinator — existing application lifetime
    ↓ existing Core, SimConnect, storage and other infrastructure
```

There is no new coordinator, engine, web framework or API. No Core algorithms, flight/aircraft state rules, database schema, storage implementation, map renderer or native simulator integration were changed. The runtime still uses the application dispatcher ordering established by 7.18B; this checkpoint does not move it to a background thread.

## 2. Design tokens/resources

`SierraDesignTokens.xaml` provides explicit `S4.*` keys for application background, primary/secondary/elevated surfaces, three text levels, yellow accent, selection, dividers, success/warning/critical/information and overlay scrim.

The Segoe UI hierarchy includes application title (19 DIP), page title (22), section title (16), metric (28), body/table (14/13), secondary (13), label/status (12), metadata/caption (11). Shared resources cover spacing, padding, control height, rail widths, content/dialog dimensions, separators and 4/6 DIP corner radii. Explicit keys avoid replacing the legacy pages' implicit theme.

## 3. Reusable components/styles

`SierraComponents.xaml` supplies primary, secondary and icon buttons; normal/selected navigation; search/input; status indicator; section header and metric; data panel and operational alert; DataGrid, tabs, dropdown, tooltip and dialog styles; and a shared keyboard-focus visual. `SierraStatePanel` provides loading, empty, unavailable, stale and error states with optional action content.

A diagnostic-only component gallery verifies resource resolution and actual rendering. It is not a production workspace or fabricated airline data. The shell uses the supplied S4 mark, neutral charcoal surfaces, quiet separators and restrained yellow selection/focus. Scoped text resources keep Sierra controls separate from the legacy type system.

## 4. Navigation architecture

`ShellSection`, `ShellDestination` and `ShellNavigation` centralize presentation destinations and selected content. A single MainWindow adapter routes requests to existing content or dialogs. Navigation has no simulator, flight-session or persistence ownership.

Nine primary entries are present: Operations, Flights, Dispatch, Crew, Maintenance, Fleet, Airports, Network and Reports. Pilot logbook, Settings and Help are secondary. Header actions expose workspace search, actual snapshot connection status, real UTC clock/status panel, local product messages and pilot/flight tools.

Search intentionally searches available workspaces. Ctrl+K opens it, Enter activates and Escape closes; repeated search preserves both query and original focus. No global flight/crew search or new online notification feed is implied. Separately opened legacy dialogs leave the underlying workspace selected. Airports clearly states that its workspace is unavailable.

## 5. Existing workspaces retained

- Embedded: original Operations dashboard, Flight Tracking, Dispatch and Pilot Logbook, kept as the same instances.
- Existing dialogs: Fleet/catalog, Network, Reports, Settings, local messages, operations/crew/maintenance/passenger/weather/OCC demonstrations, OFP, diagnostics, timeline and debrief.
- Existing header weather and dated UTC/local clocks remain in the status panel. Existing simulator/profile/replay tools remain available from the profile/tools entry.

Their business logic, data sources and demonstration labels remain authoritative. The concept's world traffic, airline totals and new dashboard panels were not fabricated.

## 6. Files created/modified

Production files below are under `src/Alpha6Ops.Desktop/`.

**Created:** `SierraDesignTokens.xaml`, `SierraComponents.xaml`, `SierraStatePanel.cs`, `SierraShell.xaml`, `SierraShell.xaml.cs`, `ShellNavigation.cs`, `MainWindow.Shell.cs`, `Assets/Sierra4/s4-mark.png`.

**Modified composition/presentation:** `App.xaml`, `Alpha6Ops.Desktop.csproj`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `MainWindow.Dashboard.cs`, `MainWindow.FlightSession.cs`, `MainWindow.Header.cs`, `MainWindow.Responsive.cs`. FlightSession changes are snapshot-to-header rendering and tray wording only.

**Verification:** new `SierraShellSmokeTest.cs`; adjusted `DashboardSmokeTest.cs`, `HeaderSmokeTest.cs`, `ResponsiveSmokeTest.cs`, `tests/Alpha6Ops.MigrationChecks/Program.cs` and `scripts/verify-migration-baseline.ps1`.

**Documentation:** this report; updated `docs/sierra4-migration-regression-checklist.md`; specification and execution/resume ledger at `docs/superpowers/specs/2026-09-24-checkpoint-7.19.md` and `docs/superpowers/plans/2026-09-24-checkpoint-7.19.md`.

The preexisting untracked checkpoint 7.11 bundle is untouched and excluded.

## 7. Visual review

Actual WPF-rendered captures were inspected; no generated mockup was substituted for implementation evidence.

- Reviewed 1920×1080 and 2560×1440; the rail/header/footer use native sizes while the workspace fills the available area.
- Reviewed 1366×768, 640×480, narrow search, Dispatch within the new shell, unavailable state and component gallery. Automated bounds/text/scroll checks also cover 720×480, 900×700, 1024×768, 1149/1150×768, 2560×1080 and 900×1200.
- Reviewed 150% and 200% render captures and retained the existing work-area/DPI fitting and monitor checks. These are controlled rendering/geometry scenarios; a manual OS display-setting change and screen-reader audit were not performed.
- The compact rail now retains complete icons and named targets. Search fits the small viewport. A duplicate tooltip resource, compact icon clipping, external namescope binding issue and repeated-search focus issue were corrected during development.

Representative final captures: [1920×1080 shell](../work/checkpoint-7.18a/20260924-085644-1405e8a0/desktop/responsive-1920x1080.png), [2560×1440 shell](../work/checkpoint-7.18a/20260924-085644-1405e8a0/desktop/responsive-2560x1440.png), [compact shell](../work/checkpoint-7.18a/20260924-085644-1405e8a0/desktop/responsive-640x480.png), [search](../work/checkpoint-7.18a/20260924-085644-1405e8a0/desktop/sierra-search-640.png), [components](../work/checkpoint-7.18a/20260924-085644-1405e8a0/desktop/sierra-components.png), [retained Dispatch](../work/checkpoint-7.18a/20260924-085644-1405e8a0/desktop/dispatch-review.png).

**Intentionally legacy:** everything inside the Operations dashboard (hero photograph, tiles, charts, tables, globe), Dispatch/Tracking/Logbook interiors, standalone dialog interiors, flight tools/status cards, legacy scrollbars and executable/tray icon metadata. The dark Sierra frame, navigation, header, typography hierarchy, inputs, selection and outer spacing are new.

**Compromises/accessibility:** compatibility pages retain a 1220×900 DIP minimum and can scroll horizontally/vertically instead of shrinking. This is usable but less compact than a migrated page. The rail itself scrolls on short windows; compact icons retain names/tooltips and keyboard access. Color is supplemented by status text, selection edges and focus indicators. Calculated theme text contrast is 16.77:1 for primary text and 6.37:1 for muted text on SurfacePrimary, and 12.88:1 for primary-action text; this is not a complete accessibility certification. Small legacy captions await their individual page migrations.

The supplied concept guides the frame and design system, not the dashboard contents. No second top-level navigation duplicates the rail; header space is reserved for current workspace and existing controls. The photorealistic world map and network-wide dashboard belong to later authorized work.

## 8. Build result

- Complete production solution Release: **PASS**, zero warnings/errors.
- Normal `packaging/build-desktop.ps1` production publish/package: **PASS**. Packaged executable was exercised.
- External migration harness: **PASS**, zero warnings/errors.
- Local package artifacts: `outputs/Alpha6OPS-Desktop-0.22.0/`, `outputs/Alpha6OPS-Setup-0.22.0.exe`, `outputs/Alpha6OPS-Desktop-0.22.0-win-x64.zip`. No installation or upload was performed. Their SHA-256 values are in the final run's `build-hashes.json`.

## 9. Regression counts

**1,834 executed; 1,834 passed; 0 failed; 2 skipped/unavailable.** Counting convention is assertion executions, including repeated viewport assertions and one validation per recorded flight; builds and preliminary attempts are excluded.

| Group | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Core | 122 | 0 | 0 |
| desktop-smoke | 18 | 0 | 0 |
| dashboard-smoke | 494 | 0 | 0 |
| refresh-smoke | 1010 | 0 | 0 |
| live-identity-smoke | 24 | 0 | 0 |
| diagnostic-index-smoke | 19 | 0 | 0 |
| monitor-lifecycle-smoke | 22 | 0 | 0 |
| tracking-integration | 43 | 0 | 0 |
| single-instance activation | 1 | 0 | 0 |
| recorded-flight validations | 9 | 0 | 0 |
| migration contracts and compatibility | 31 | 0 | 2 |
| explicit large diagnostic-index suite | 35 | 0 | 0 |
| fleet import | 6 | 0 | 0 |

The net change from 7.18B is +49 assertions: +47 in dashboard/shell checks and +2 migration contracts. This is not a claim of 49 new independent scenarios. Layout/branding assertions that required the old fixed reference surface were replaced with native-viewport, readable-target, scrolling and shell navigation expectations. Existing operational regression assertions remain intact.

New coverage includes shell presence without a scaling Viewbox, every embedded destination/overlay preserving session/storage/connection owner, existing page instance retention, unique destinations and selected navigation, resource resolution, five state variants, search/no-results/activation, repeated-shortcut focus return, compact targets and readable header text.

All earlier isolation gates pass: view recreation, hidden runtime continuation, pending connection reattachment, stale callback rejection, immutable snapshots, refresh purity, immediate assignment/PIREP persistence and closeout, display-wording independence and existing data compatibility. Copied pilot data yielded 497 historical flights, 960 attached events and 10 saved gates; original profile hashes stayed unchanged.

Only `NATIVE_CONNECTION_LIFECYCLE` and `NATIVE_RECONNECT` remain unavailable. They were not counted as passed. No automated checks are disabled or expected-failure substitutes. Earlier failures were corrected and retained in the execution ledger; no remaining flaky failure was observed in the final run.

Verification-infrastructure corrections: register Sierra dictionaries in the test-only Application; preserve diagnostic namescope bindings; and ignore only a Windows process entry proven terminated by both zero threads and zero handles. Missing/uncertain liveness information still blocks the activation preflight. The latter addresses the crashed test process Windows continued listing; it does not modify production lifecycle behavior.

## 10. Replay result

**9/9 PASS**, original expectations and journals preserved.

| Flight | Processed telemetry samples | Result |
| --- | ---: | --- |
| JST221 | 11,965 | PASS |
| QFA124 | 11,535 | PASS |
| JST786 | 6,952 | PASS |
| QLK1579 | 9,040 | PASS |
| JST721 | 7,650 | PASS |
| JST724 | 7,459 | PASS |
| JST760 | 10,552 | PASS |
| QLK1274 | 10,519 | PASS |
| ANG2 | 15,694 | PASS |

## 11. Telemetry result

**91,366 expected = 91,366 independently counted raw = 91,366 processed.** Zero omitted pre-assignment records in these fixtures. Every journal's SHA-256 remained unchanged. Per-flight coverage is recorded in the final run's `migration/replay-coverage.json`.

## 12. Remaining visual/technical debt

- Legacy page internals and some standalone surfaces still use Alpha 6 styling/names; they are deliberately not restyled globally.
- Legacy minimum sizes/scrollbars remain in the compatibility host. Future page migrations can remove that page's minimum surface while keeping the shell.
- Windows executable/product identity, installer names and existing profile directories remain Alpha6OPS/0.22.0 for compatibility; final distribution rebranding/versioning is separate.
- Workspace search is local navigation search. Airports is unavailable; demonstration modules do not become a persistent airline-world simulation.
- The map and its existing data/rendering limits are unchanged. This is a lifetime boundary plus presentation migration, not background-thread or map performance work.
- Native MSFS lifecycle/reconnect and manual assistive-technology/physical DPI review remain explicit release checks.

Independent read-only review found no remaining blocking issues; its focus-return finding was corrected and covered. Final verification is the evidence cited above.

## 13. Recommendation

**GO for Checkpoint 7.20 — Sierra 4 Operations Dashboard**, subject to its own explicit scope/authorization and the same regression gates. The distinct Sierra shell and reusable visual foundation are ready, with the existing flight runtime and recorded data preserved. Stop here: no Operations dashboard migration or map replacement was begun.
