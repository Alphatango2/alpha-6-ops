# Checkpoint 7.20 — Sierra 4 Operations Dashboard

Completed locally on 24 September 2026, from checkpoint 7.19 commit `507f6572691a5149002f60613db84851b08f306a`, on `codex/checkpoint-7.20`.

**Result: production build/package PASS; 1,871/1,871 executed checks PASS; zero failures; two native simulator checks unavailable; nine replays PASS; 91,366/91,366 telemetry samples processed.**

Authoritative scope: [supplied specification](superpowers/specs/2026-09-24-checkpoint-7.20.md). Execution record: [plan and ledger](superpowers/plans/2026-09-24-checkpoint-7.20.md). Final evidence: `work/checkpoint-7.18a/20260924-135928-2615e172/`. That directory retains the regression runner's original name; these are fresh 7.20 results. Its `sourceCommit` is the pre-commit HEAD, 507f657; `work/checkpoint-7.20/verified-source.json` links the final local commit to the tested tree and artifacts.

## 1. Before/after Operations architecture

Before: the Operations visual tree lived directly in MainWindow's legacy minimum-size viewport, with photographic hero/tile controls and several demonstration cards.

After: the existing Sierra shell hosts a dedicated native `OperationsWorkspace` outside that viewport. `OperationsOverviewQuery` turns immutable `FlightSessionSnapshot` and existing read data into presentation records. `MainWindow.Operations.cs` connects existing actions and temporarily adapts existing flight-summary controls. `OperationsMapHost` wraps the unchanged geographic renderer.

Data flow: **FlightSessionCoordinator → immutable snapshot + existing read sources → OperationsOverviewQuery → OperationsWorkspace**. Explicit pilot actions still use existing handlers. Rendering has no storage writes or simulator commands. FlightSessionCoordinator remains the application-lifetime owner. Core, simulator integration, database schema, flight algorithms, telemetry continuity and profile paths were not changed.

## 2. Before/after visual structure

The photographic portal has become an operational overview: heading and observation context, six compact KPI cards, a geographic centerpiece with current-flight strip, an alerts/airport-weather rail, and a flight table/activity row. It uses the established dark Sierra surfaces, native Segoe typography, restrained yellow actions and text-backed status colors.

## 3. Legacy elements removed

Removed from Operations: the large Your Next Flight photograph, photographic launcher grid, Fleet Reference ring/card, Company Messages card, demonstration alert card and their obsolete templates. Existing functionality remains accessible through shell navigation, workspace search, the message control, or Flight tools. Advanced rotation details moved into Flight tools. The Operations demo desk and alert center retain distinct, explicitly labeled search destinations.

## 4. New components

- `OperationsOverview` and typed metric, alert, airport, activity, severity and provenance records.
- Pure `OperationsOverviewQuery`, including typed flight-table status selection.
- `OperationsWorkspace.xaml/.cs`: native responsive page, scoped panel/button/table styles, empty states, summary and navigation events.
- `OperationsMapHost`: replaceable geographic presentation boundary.
- `MainWindow.Operations.cs`: existing-command and visible-control adapters.
- `OperationsSmokeTest`: query, map, navigation and actual WPF capture checks.

Existing Sierra resource dictionaries supply the design foundation. No additional packages or service dependencies were introduced.

## 5. Data provenance by section

| Section | Source and interpretation |
| --- | --- |
| Context/freshness | Snapshot connection, simulator timestamp, sample freshness and explicit scenario selection. Scope is the user's flight session. |
| KPI strip | Typed snapshot phase, current observation, trusted assignment and dated arrival estimate/actual; see metric rules below. |
| Map | Accepted assignment route points, current positioned telemetry and existing reliable route progress. Planned, flown and completed-route semantics remain those of FlightRouteMap. |
| Current/next flight | Existing assignment/observed-flight identity, actuals, estimate, gates and progress presentation. No new timing or tracking engine. |
| Active/upcoming flights | Existing assignment/rotation projection and watchlist. Dates display in UTC, including date rollover. The active flight uses typed phase/review precedence. Explicit replay mode retains labeled sample rotation data. |
| Operational alerts | Typed connection failure/retry, stale/suspended observation, assignment review, schedule trust and arrival mismatch. These are session advisories, not network monitoring. |
| Airports | Assignment origin/destination, existing AirportCatalog names and assigned gates. No live gate provider or airport congestion claims. |
| Weather | Existing Open-Meteo approximate IP-city weather. City, timestamp, stale/error condition and non-airport scope are visible. No new weather source. |
| Activity | Latest eight existing snapshot flight events, ordered by timestamp, retaining typed event codes. No invented OCC decisions. |

## 6. Metric classification

| Metric | Classification and rule |
| --- | --- |
| Active | Derived: zero or one monitored flight; one during taxi-out, airborne or taxi-in with a current normal observation. |
| On time | Derived: zero or one current flight, using actual IN or ETA compared with scheduled arrival; up to five minutes late is included. Requires trusted dates, accepted association and no arrival mismatch. |
| Delayed | Derived: zero or one current flight more than five minutes late, with the same trust requirements. |
| Aircraft | Observed: one current monitored aircraft; expressly not fleet availability. |
| At risk | Unavailable: no authoritative network risk source. |
| Connections | Unavailable: no passenger connection source. |

Absent, stale, paused, slewing or simulator-menu observations cannot supply current counts. Untrusted dates and wrong-airport arrivals cannot become on-time results. Explicit replay mode supplies no live KPI totals. Dashes are intentional unavailable states, not zero-count claims.

## 7. Map integration

The existing `FlightRouteMap` is reused without renderer, geometry, thread or dependency changes. The host updates only changed route/telemetry/progress inputs and does not redraw while hidden. A stale transition clears the former live marker; a new session cannot inherit its trail. Returning from another workspace starts a new visible trail segment rather than joining positions across an unobserved interval.

A positioned aircraft is displayed only when the retained renderer also has at least two verified route points. Missing position never becomes a fabricated flown track. Free-flight observations without renderable route geometry are described as unavailable on this map. The trail is the visible observation interval, not reconstructed full-flight history. The host boundary allows a later renderer replacement without replacing the Operations layout.

## 8. Current/next flight integration

A compact strip below the map retains flight identity, route, airport names, aircraft, dates/times, gates, status and progress, plus the existing Dispatch/Flight Tracking action. With no active flight, it becomes an intentional Open Dispatch state. The flight table retains filters, watchlist, preflight and flight details. Aircraft display uses canonical type names while watchlist identity retains its existing registration key.

## 9. Responsive behavior

Actual WPF captures were reviewed at 1920×1080, 2560×1440, 1366×768 and 640×480, plus 150% and 200% rendering scenarios. Additional existing viewport/monitor assertions also passed.

- KPI strip reflows through six, three and two columns.
- The side rail moves below the map on narrow content widths; the lower table/activity row stacks separately.
- The map receives deliberate height changes for larger windows rather than stretching the whole page.
- Fonts retain native sizes. Secondary content remains accessible through scrolling; table columns have explicit readable minimum widths and horizontal scrolling where needed.
- The page is outside the legacy minimum-width compatibility viewport and has no outer scaling Viewbox.

150%/200% evidence is controlled WPF rendering plus existing monitor-fit checks. Manual Windows display-setting changes and screen-reader testing were not performed.

## 10. Comparison with the supplied concept

The implemented page follows the concept's information hierarchy: compact status strip, dominant geographic region, right-hand operational rail and lower flight/activity panels. It is visually distinct from the former Alpha 6 photographic portal. Dark near-black surfaces, quiet borders, restrained yellow, native typography and consistent spacing match the 7.19 shell.

The major deliberate difference is the retained blue orthographic globe rather than a flat nighttime world map. The page also has less data density because there is one monitored flight and no persistent airline world. No fleet donut, fabricated network traffic or unsupported statistics were added to imitate the concept's sample data.

Final captures: [1920×1080](../work/checkpoint-7.18a/20260924-135928-2615e172/desktop/operations-observed-1920x1080.png), [2560×1440](../work/checkpoint-7.18a/20260924-135928-2615e172/desktop/operations-observed-2560x1440.png), [1366×768](../work/checkpoint-7.18a/20260924-135928-2615e172/desktop/responsive-1366x768.png), [640×480](../work/checkpoint-7.18a/20260924-135928-2615e172/desktop/responsive-640x480.png), [150%](../work/checkpoint-7.18a/20260924-135928-2615e172/desktop/responsive-150-percent.png), [200%](../work/checkpoint-7.18a/20260924-135928-2615e172/desktop/responsive-200-percent.png), [empty](../work/checkpoint-7.18a/20260924-135928-2615e172/desktop/operations-empty-query.png), [stale](../work/checkpoint-7.18a/20260924-135928-2615e172/desktop/operations-stale.png).

Observed-flight captures use clearly marked injected diagnostic data in the actual production view; they are not a claim that MSFS was connected. Their filter tab can show the underlying three-leg fixture count while the injected visual table contains one row, because resize refreshes the existing fixture filter labels. Production uses the corresponding rotation source; this limitation belongs to the capture setup.

## 11. Visual polish after comparison

Scoped native button/table typography removed legacy condensed-font leakage. Map/summary proportions were tightened at 1920 and expanded at 2560. Empty current-flight and alert states were simplified. Table column minimums were corrected after a compact-layout failure. Current-flight actions reflow below identifiers in compact windows. Time columns now include dated UTC values; unavailable observations and data scope remain explicit.

## 12. Files created/modified

Paths below are under `src/Alpha6Ops.Desktop/` unless noted.

**Created:** `OperationsOverview.cs`, `OperationsMapHost.cs`, `OperationsWorkspace.xaml`, `OperationsWorkspace.xaml.cs`, `MainWindow.Operations.cs`, `OperationsSmokeTest.cs`.

**Modified presentation/integration:** `DashboardData.cs`, `MainWindow.Dashboard.cs`, `MainWindow.FlightSession.cs`, `MainWindow.Shell.cs`, `MainWindow.xaml`, `ShellNavigation.cs`. The FlightSession edit only requests an Operations presentation refresh after the snapshot update.

**Modified checks:** `DashboardSmokeTest.cs`, `ResponsiveSmokeTest.cs`.

**Documentation:** this report, `docs/sierra4-migration-regression-checklist.md`, supplied specification and execution plan under `docs/superpowers/`.

The preexisting untracked checkpoint 7.11 bundle is excluded. Journals, private manifests, copied profile data, build products and diagnostic captures remain local ignored evidence.

## 13. Build result

Complete production solution Release and external migration harness: **PASS**, zero warnings/errors. Normal `packaging/build-desktop.ps1` publish/package: **PASS**; packaged executable exercised by the verification runner.

Local artifacts: `outputs/Alpha6OPS-Setup-0.22.0.exe`, `outputs/Alpha6OPS-Desktop-0.22.0-win-x64.zip`, `outputs/Alpha6OPS-Desktop-0.22.0/`. Product version/name intentionally remain 0.22.0/Alpha6OPS pending separately authorized distribution changes. No installation or GitHub upload was performed. Final artifact SHA-256 values are in the evidence directory's `build-hashes.json`.

## 14. Regression counts and findings

**1,871 executed; 1,871 passed; zero failed; two skipped/unavailable.** These are assertion executions, including repeated viewport assertions and one validation per recorded flight. Builds and preliminary attempts are excluded.

| Group | Passed | Failed | Unavailable |
| --- | ---: | ---: | ---: |
| Core | 122 | 0 | 0 |
| Desktop smoke | 18 | 0 | 0 |
| Dashboard/shell/Operations | 531 | 0 | 0 |
| Refresh | 1010 | 0 | 0 |
| Live identity | 24 | 0 | 0 |
| Diagnostic index | 19 | 0 | 0 |
| Monitor lifecycle | 22 | 0 | 0 |
| Tracking integration | 43 | 0 | 0 |
| Single-instance activation | 1 | 0 | 0 |
| Recorded flights | 9 | 0 | 0 |
| Migration contracts/compatibility | 31 | 0 | 2 |
| Explicit large diagnostic index | 35 | 0 | 0 |
| Fleet import | 6 | 0 | 0 |

The net increase over 7.19 is **37 assertion executions**, not 37 new independent scenarios. Obsolete photograph/fixed-layout expectations were replaced with native Operations contracts; operational checks remain. New coverage checks data honesty, trusted/stale/suspended/mismatched states, weather provenance, typed wording independence, UTC rollover, map update idempotence, stale-marker clearing, session/hidden-interval trail isolation and retained demo navigation.

All isolation gates passed: recreated views, workspace switching, runtime without views, hidden flight continuation, first/repeat-render purity, stale-callback rejection, assignment/PIREP durability independent of animations, typed decision independence and persisted-data compatibility. Copied profile data yielded **498 historical flights, 1,003 attached events and 12/12 saved gates**; source hashes remained unchanged.

Only `NATIVE_CONNECTION_LIFECYCLE` and `NATIVE_RECONNECT` remain unavailable. Injected telemetry does not validate native SimConnect handles/workers. No disabled or expected-failure substitute was introduced; no unresolved automated failure was observed in the final run.

Development failures were corrected: the expected preimplementation workspace contract, compact table width regression and a duplicate XAML attribute. Independent review identified simulator-menu counts, unsupported route-free map claims, wrong-airport arrival totals and a demo destination collision; all were corrected. The hidden-trail check was strengthened to retain the same session key, avoiding a false-positive test. Final verification includes these corrections.

## 15. Replay result

**Nine of nine passed**, preserving original journal expectations.

| Flight | Telemetry processed |
| --- | ---: |
| JST221 | 11,965 |
| QFA124 | 11,535 |
| JST786 | 6,952 |
| QLK1579 | 9,040 |
| JST721 | 7,650 |
| JST724 | 7,459 |
| JST760 | 10,552 |
| QLK1274 | 10,519 |
| ANG2 | 15,694 |

## 16. Telemetry result

**91,366 expected = 91,366 independently counted raw = 91,366 processed.** No omitted pre-assignment records in these fixtures. Journal hashes remained unchanged. Per-flight coverage is in `migration/replay-coverage.json`.

## 17. Known visual/technical debt

- Retained globe rendering, labels, controls, letterboxing, Dispatcher cost and blue scrollbars differ from the concept. Long-flight map performance and a new renderer are deferred, not claimed solved.
- Some compact panels scroll internally, including alerts at 1920; small legacy map captions await the map checkpoint.
- Map trail covers the current visible interval; full recorded-track reconstruction is not implemented here.
- Current-flight labels still use transitional MainWindow adapters and existing formatting. The new query is separated; this is not a broad MainWindow refactor.
- Dispatch, Flight Tracking, Logbook, Flight tools and standalone legacy module interiors remain intentionally unmigrated.
- Native simulator checks and manual OS DPI/accessibility review remain release gates.
- Installer identity, storage paths and version remain compatible with 7.19.

## 18. Remaining demo/unavailable states

Explicit replay selection retains sample rotation/airports and clearly labels the preview. Existing standalone demo desks remain searchable. Live Operations never treats those as a persistent airline network. At-risk analysis, passenger connections, fleet availability, network alerts, airport weather observations and automated OCC decisions lack authoritative sources. Weather may independently be unavailable/stale. An empty assignment does not manufacture flights.

## 19. Next-checkpoint recommendation

**GO for a separately scoped map architecture/design checkpoint.** The migrated page now provides a stable replaceable map area; renderer presentation/performance is the largest remaining gap against the concept. Preserve this regression suite and obtain native simulator validation before claiming native connection coverage. No Map V2, additional workspace migration or persistent airline-world engine was started. Stop at 7.20.
