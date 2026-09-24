# Checkpoint 7.20 — Operations workspace implementation plan

> Execution: inline using executing-plans; preserve the supplied specification and record verification evidence here for interruption recovery.

**Goal:** Replace the photographic Operations portal with a Sierra 4 operational overview, using only existing authoritative data or explicit unavailable/demo states.

**Architecture:** A native responsive `OperationsWorkspace` consumes an immutable `OperationsOverview` produced by a pure `OperationsOverviewQuery`. MainWindow connects existing commands, current-flight presentation and watchlist persistence. FlightSessionCoordinator remains the sole live-flight owner. A replaceable map host uses the unchanged FlightRouteMap renderer.

**Stack:** Existing .NET/WPF; existing Sierra resources; no added packages or schema changes.

**Spec:** `docs/superpowers/specs/2026-09-24-checkpoint-7.20.md` (user supplied).

## Constraints and decisions

- Start at verified 507f657; keep all work local. No Map V2, other workspace redesign, runtime/flight-algorithm changes, storage migration or distribution rebranding.
- Source scope is one monitored flight / assigned rotation, not an airline-wide world. Explicit replay selection can show the existing sample rotation with a prominent demo label.
- KPIs count only supported local scope. At-risk and passenger connections are unavailable; fleet catalog counts are not fleet availability.
- Alerts derive from typed connection, freshness, association, schedule trust and arrival mismatch. Activity shows recorded tracking events, preserving typed codes and timestamps. No text parsing for decisions.
- Airports use assignment/catalog facts. Existing IP-city model weather is labeled local city weather, never attributed to an airport.
- Reuse current-flight labels in a compact summary through temporary named-control adapters; avoid changing identity/timing decisions while migrating their visual arrangement.
- Move legacy advanced rotation details into Flight tools, preserve watchlist/preflight and demo desks through existing navigation. Remove photographic hero/tiles, demo-alert cards, fleet ring and company-message cards from Operations.
- The supplied concept governs composition and hierarchy; the existing globe remains a documented visual compromise until the dedicated map checkpoint.

## Tasks

- [x] 1. Establish a failing Operations workspace contract; add pure query checks for empty, trusted, stale, mismatched, replay and unavailable states.
  - Files: OperationsSmokeTest.cs; DashboardSmokeTest.cs.
  - Assert a dedicated workspace exists and has no legacy photographic layout; assert snapshot equality after render/navigation.
- [x] 2. Implement query/provenance records and native view.
  - Files: OperationsOverview.cs, OperationsWorkspace.xaml/.cs, OperationsMapHost.cs.
  - Query consumes snapshot, explicit demo flag, projected rotation and existing local weather; returns typed metrics/alerts/airports/activity/map context.
  - Native grid: heading/context, six compact KPIs, map with compact current-flight strip, alerts/weather rail, flights/activity. Reflow rail/bottom at narrow widths; never scale the entire page.
- [x] 3. Integrate commands and retire legacy composition.
  - Files: MainWindow.xaml, MainWindow.Operations.cs, MainWindow.Dashboard.cs, MainWindow.Shell.cs, MainWindow.FlightSession.cs and refresh entry points.
  - Preserve existing tracking/dispatch/preflight/watchlist handlers and legacy feature workspace instances. Operations sits outside the legacy minimum-size viewport.
- [x] 4. Update presentation contracts and inspect actual captures.
  - Files: ResponsiveSmokeTest.cs, DashboardSmokeTest.cs, OperationsSmokeTest.cs.
  - Replace obsolete photograph/fixed-layout assertions with native reflow/data-honesty assertions; retain operational checks.
  - Capture 1920x1080, 2560x1440, 1366x768, 640x480, 150% and 200%; inspect live/empty/demo/stale states. Polish after comparison.
- [x] 5. Full regression, review and completion report.
  - Run normal production/package build and full migration runner with `.work/replay-7.17-manifest.json`.
  - Require 9 replays and exactly 91,366 raw/processed samples; report two native simulator checks as unavailable if still unavailable.
  - Independent final review via requesting-code-review skill; fix concrete findings, rerun affected gates.
  - Update docs/checkpoint-7.20.md and regression checklist; record final evidence and commit locally. Stop at 7.20.

## Review focus

1. Unverified/stale assignment must not become live airline counts or an observed map position on the wrong route.
2. No current session must show an intentional empty state, not the bundled demonstration rotation by default in production.
3. Weather failure/age must remain visible, and city-level weather must never masquerade as airport observations.
4. Resizing/recreating/navigation must neither reconnect SimConnect nor reset flight truth; repeated rendering must be pure.
5. Compact layouts and long airport/status labels must remain reachable with native-sized text; map updates must not run while hidden.

## Execution ledger

- Initial inspection: 7.19 clean at 507f657; only unrelated untracked 7.11 bundle retained. Authoritative 7.20 spec copied without modification. Current source has a demonstration NetworkMap, and a geographic FlightRouteMap already used by Flights; reuse the latter without renderer changes.
- Ruling: supplied checkpoint authorizes implementation; proceed inline without requesting another stage approval. This follows the user's established checkpoint workflow and developer autonomy instructions.
- Created local branch `codex/checkpoint-7.20`; preserved the unrelated bundle. The new-workspace assertion failed before implementation (`work/checkpoint-7.20/red`), then passed after the native workspace was integrated.
- Completed query records, native page, existing map host and visible-control adapters. Removed old photo/tile/card composition; kept prior commands and demo destinations reachable. No Core, coordinator, simulator, renderer or persistence implementation changes.
- Actual WPF capture review prompted scoped native typography, tighter 1920 proportions, larger 2560 map area, cleaner empty states and explicit table minimum widths. `smoke-2` exposed compact column shrinkage; it was corrected. One development XAML duplicate attribute was corrected. `smoke-3` passed 524 dashboard assertions before later review coverage.
- Independent read-only review corrected four concrete issues: simulator-menu totals, unsupported map position claims without a route, wrong-airport on-time totals and the Operations demo destination alias. Added typed flight status and preserved all removed demo entry points. Review then found no remaining concrete blocker.
- Full runs `20260924-135520-17c4b360` and `20260924-135755-970b53e7` passed. A final map check was strengthened to keep its session key unchanged across hide/show, proving the hidden-interval reset itself. No operational behavior changed.
- Final authoritative run: `work/checkpoint-7.18a/20260924-135928-2615e172`; log `work/checkpoint-7.20-verified-suite.log`. Production/package/harness builds pass; 1,871 executed/pass, zero failures, two native unavailable. Nine replays; 91,366 raw/processed samples. 498 copied history flights, 1,003 events and 12 saved gates readable; original profile hashes unchanged.
- Final actual captures inspected at 1920×1080, 2560×1440, 1366×768, 640×480 and 150%/200%; empty/stale states also reviewed. These are WPF diagnostic renders, not a real MSFS session or manual OS DPI test. Capture fixture limitations and retained globe/scrollbar/legacy-workspace compromises are documented in `docs/checkpoint-7.20.md`.
- Completion report and rerunnable Operations checklist added. Final local commit and tested-artifact linkage are recorded in ignored `work/checkpoint-7.20/verified-source.json`. Stop at 7.20; no next-checkpoint work authorized by this plan.
