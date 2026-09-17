# Build consolidation review — 16 September 2026

## Inputs and history

| Input | Commit | Content |
| --- | --- | --- |
| Dan's requested build | `36c5e6a1a14c0d7404bc29d6c9bc288a6656181f` | Checkpoint 7.11, desktop version 0.16.0; includes his current main at `370484c` |
| Reece's published fork main | `a6ffb72` | Replay and test-log export allocation reductions |
| Reece's preserved local work | `a041285` | Account/identity/server foundation, personal pilot workspace, launch scripts and tests |

The merge retains both parents and their full histories. Older work from `reece/orientation` and `integration/dan-6208a4f` is already ancestral to this consolidated build. GitHub has one fork, `ReeceCz/alpha-6-ops`, of `Alphatango2/alpha-6-ops`; the other names are branches, not separate copies of the product.

The agreed shared home is **`Alphatango2/alpha-6-ops`, branch `main`**. The current GitHub account has ADMIN permission on Reece's fork and READ permission on Dan's repository. The fork is the staging location for the combined branch; [upstream pull request #5](https://github.com/Alphatango2/alpha-6-ops/pull/5) requires a maintainer merge. Existing upstream PR #4 contains checkpoint 7.11, which is included in this integration. Existing branches remain available as recovery/history references.

## Dan's changes retained

- Embedded Dispatch with SimBrief import, release comparison, route/fuel/payload review and acceptance.
- Continuous OFP viewer with bookmarks, zoom and optional cached PDF.
- Expanded local logbook and PIREP closeout.
- Flight tracking refinements: route rendering, higher-detail coastlines, schedule/timezone handling, flight phases, ETA smoothing and operational events.
- Observed aircraft registration remains distinct from the assignment; mismatches are advisories instead of discarding a matching route.
- Checkpoint telemetry and importer changes, including indicated altitude, gear normalization and alternate SimBrief fuel fields.

## Integration review and fixes

1. Resolved four conflicting files: manual assignment, main window, dashboard navigation and SimBrief importer. Manual setup uses Dan's current dialog; SimBrief import now lives in Dispatch.
2. Carried account/workspace isolation into Dispatch's saved username, JSON cache, OFP text/PDF cache and viewer state. The new UI must never fall back to another pilot's global cache. Regression checks cover separate workspaces and the viewer's actual file access.
3. Routed personal-pilot Dispatch actions to the new release workspace and cleared selection/visibility consistently when changing views.
4. Preserved account authorization guards when starting/reconnecting flights, per-workspace history/preferences and Reece's allocation fixes.
5. Fixed a dispatch acceptance bug found during review: the host previously returned when a flight was running, but Dispatch then displayed “accepted.” Rejected acceptance now propagates to the existing failure UI, with a regression check. The assignment is saved before changing in-memory state.
6. Updated the current-build README and quick-start version; historical release notes remain available.

## Validation

- Release builds: desktop and cloud solutions, zero compiler warnings or errors.
- Domain/flight tests: 85 checks.
- Desktop identity tests: 27 checks, including protected session storage and offline authorization.
- Accounts: 64 checks, including real disposable PostgreSQL integration.
- Server integration: zero failures, including signed-token validation, tenant authorization, antiforgery and rate limits.
- Auth0 Action unit checks passed.
- Packaged desktop: 18 desktop UI, 456 dashboard, 33 identity/workspace UI and 24 live identity diagnostic checks passed. Reports are recorded under `work/consolidation-verification/` and summarized in `outputs/Desktop-0.16.0-Consolidation-Validation.json`.
- Packaged single-instance activation passed: a second launch restored the tray-hidden primary window and exited.
- React dashboard: TypeScript validation and Vite production build passed.

These checks use isolated fixtures and disposable databases. Real Auth0 tenant login, hosted deployment and a live MSFS flight are not established by them.

## Working from one main branch

Use Dan's `main` as the shared source of truth. The combined local `main` contains the pending integration and tracks `origin/main`; until the upstream pull request is merged it is ahead of the shared branch. Send future changes to a short-lived feature branch and integrate them through pull requests; avoid continuing from the old integration branches. Once a maintainer merges the consolidation upstream, fetch both remotes and fast-forward the working main to the agreed shared main. Do not force-push either repository or remove recovery branches to make histories appear identical.
