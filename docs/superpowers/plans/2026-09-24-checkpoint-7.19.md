# Checkpoint 7.19 implementation plan

> Execute inline with the executing-plans workflow and one independent whole-change review. The user's supplied specification explicitly authorizes visual implementation. Preserve the prepared checkout/private replay paths and isolate changes on `codex/checkpoint-7.19`.

**Goal:** Install the Sierra 4 visual foundation and viewport-based WPF shell around existing feature workspaces.

**Architecture:** MainWindow composes `SierraShell` and existing workspace instances. A lightweight typed `ShellNavigation` owns only presentation destinations; MainWindow maps destinations to retained workspaces or existing dialogs. `FlightSessionCoordinator` remains application-owned and unchanged. Explicit S4 resources avoid restyling legacy pages by accident.

**Tech stack:** Existing .NET 10 / WPF; no new packages.

**Spec:** `docs/superpowers/specs/2026-09-24-checkpoint-7.19.md`.

## Constraints and decisions

- No Operations dashboard migration, map replacement, Core changes, storage/schema changes, native SimConnect changes, web frontend/API or airline-world engine.
- Keep package identity/version and storage directories intact. Change shell-facing branding only.
- Use the supplied S4 mark asset, near-black surfaces, neutral readable typography, quiet dividers, restrained yellow selection and primary actions.
- All original operational checks, nine journals and 91,366 samples remain gates. Native MSFS checks remain unavailable unless exercised.
- Existing layout assertions that mandate the old scaled 1920 reference surface conflict with the new viewport requirement. Replace only those presentation expectations; preserve operational assertions and document count changes.
- Legacy workspaces retain internal layouts inside a scrolling compatibility host with a readable minimum size. No whole-window Viewbox. Shell remains native-sized at narrow widths; later page checkpoints remove legacy horizontal scrolling.
- Existing standalone Fleet/Network/Reports/demo dialogs remain reachable through typed navigation. Airports is an honest unavailable placeholder. Search finds workspaces, with no fabricated global network search.
- Work is local; stop after 7.19.

## Review focus

- Repeated navigation, search and resizing cannot issue operational commands or create a second coordinator. Extend navigation purity checks.
- Connection status must reflect the snapshot on initial attach, including pending connection without telemetry.
- Narrow windows and high DPI must keep navigation, connection, profile/tools and exit usable; test bounds and keyboard reachability.
- Legacy Dispatch/Tracking/Logbook remain the same instances; verify assignment acceptance, PIREP and saved-data compatibility through the full suite.
- Keyboard search and overlays must close with Escape and restore focus, with no hidden keyboard traps or invented data.

## 1. Design resources and typed navigation

Files: `SierraDesignTokens.xaml`, `SierraComponents.xaml`, `SierraStatePanel.cs`, `ShellNavigation.cs`, `App.xaml`, Desktop project resource declarations; migration test additions.

- [x] Add a failing shell-presence/native-viewport contract to the migration harness and save red evidence.
- [x] Add explicit `S4.*` semantic brush, type, spacing, control-height, rail-width and radius resources. Add reusable buttons, input/dropdown, nav, tabs, grid, tooltip/dialog, metric/status/panel/header/alert and state presentation.
- [x] Introduce `ShellSection`, `ShellDestination` and `ShellNavigation`: static destinations, typed selection/request, text filtering. No dependency on Core or coordinator.
- [x] Validate resource resolution and keyboard focus/disabled states in a diagnostic component gallery; preserve legacy implicit styles.

## 2. Shell composition and legacy adapters

Files: `SierraShell.xaml/.cs`, `MainWindow.Shell.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `MainWindow.Dashboard.cs`, `MainWindow.Responsive.cs`, `MainWindow.Header.cs`, `MainWindow.FlightSession.cs`; supplied mark asset.

- [x] Add native-grid shell with S4 brand, typed rail, current page identity, workspace search, snapshot connection status, real clock, local messages, profile and secondary tools/settings/help.
- [x] Implement keyboard search (Ctrl+K, Enter, Escape), focus restoration and compact rail from actual available width. Keep minimum 36–40 DIP interaction targets.
- [x] Host existing pages without recreating them. Move existing weather/clocks/status controls into an accessible utility panel, retain flight tools and map code unchanged.
- [x] Centralize visibility changes in one typed navigation adapter; preserve legacy module entry points through that adapter. No command issued merely for navigation.
- [x] Change only shell title/footer/tray wording to Sierra 4; keep executable/version/data identities.
- [x] Build and run focused migration checks, including navigation/state purity and initial connection attachment.

## 3. Responsive verification, visual review and regression

Files: `ResponsiveSmokeTest.cs`, `HeaderSmokeTest.cs`, shell-specific expectations in `DashboardSmokeTest.cs`, `tests/Alpha6Ops.MigrationChecks/Program.cs`, new shell checks; `docs/checkpoint-7.19.md` and reusable checklist.

- [x] Replace old fixed-surface/branding assertions with shell viewport, readable font/target size, overflow and legacy accessibility contracts. Keep flight/data assertions intact.
- [x] Exercise 1920x1080, 2560x1440, existing narrow/portrait boundaries and 150%/200% scaling scenarios. Capture real WPF renderings and inspect them.
- [x] Verify all nine navigation entries, retained workspace instances, search behavior, focus restoration, honest unavailable state and reusable component variants.
- [x] Run `scripts/verify-migration-baseline.ps1 -ReplayManifest .work/replay-7.17-manifest.json` and inspect all reports/counts.
- [x] Independently review and address concrete findings. Repeat affected gates after changes.
- [x] Write the requested 13-part report with actual counts, evidence, visual compromises and GO/NO-GO for 7.20. Stop without beginning 7.20.

## Execution ledger

- Baseline source: `ccad431`; complete 7.18B evidence retained. Original unrelated checkpoint 7.11 bundle stays untouched.
- User approval is already supplied by the written 7.19 specification; no repeated design/implementation confirmation is needed.


### Resume audit — 24 September 2026

- Preserved the current branch and all valid implementation; no reset/revert or start of 7.20.
- **Complete implementation:** semantic resources and component foundation, typed presentation navigation, native Sierra shell and compatibility hosting, branding integration, scoped runtime-snapshot status, new shell/resource/search checks and updated responsive assertions.
- **Partially complete:** final visual validation and component review. `work/checkpoint-7.19/smoke-3` passed Desktop/dashboard/refresh/live-identity/index/monitor/tracking checks and produced screenshots. Those screenshots exposed squeezed compact rail icons; the correction and stronger bounds checks were already present at resume, awaiting validation.
- **Requiring correction:** duplicate implicit ToolTip style in SierraShell resources. The saved complete-run attempt at `work/checkpoint-7.18a/20260924-084150-26d88a22` built and packaged and passed Core 122, then failed at startup with the duplicate-key exception. The duplicate registration was confirmed and removed on resume. This was a 7.19 implementation defect, not a preexisting backend failure.
- **Not completed:** successful final aggregate regression/replay/coverage run, final independent review, completion report and checklist update. Existing evidence is retained, including failed development runs; none is treated as a final pass.
- Windows denied termination of the residual failed-test Alpha6OPS process (PID23988). Asked the user to close it so the existing full-runner activation guard can remain intact; no bypass of that guard.

- Resume investigation confirmed PID23988 has zero threads and zero handles: a terminated Windows entry, not a running app. The runner now ignores only entries proven terminated by both counts; missing/inaccessible liveness data still blocks. No production lifecycle code was changed.
- Read-only review confirmed runtime/navigation separation; corrected repeated search/status overlay focus return. Scoped Sierra control text resources prevent the legacy implicit font from leaking into new component templates. The compatibility scroll corner uses the shell background; scrollbar mechanics/templates stay unchanged.

- Full verification run `20260924-085223-10e8b844` passed build/package, Core, all Desktop groups, activation and all nine journals/91,366 samples. The isolated migration host then failed resource lookup because its test-only Application had not registered the new Sierra dictionaries. Added the same two dictionaries as production; focused rerun `work/checkpoint-7.19/migration-final` passed 31 checks with the same two native skips. Production runtime remained unchanged.
- Updated the Dispatch visual fixture to navigate through the actual typed route before rendering its draft; this prevents a diagnostic capture from showing Dispatch content with a stale Flights header. Final full gate follows after this test-host correction.

- Final complete gate `20260924-085644-1405e8a0`: build/package/test-host PASS (0 warnings/errors); 1,834/1,834 checks, 0 failures, 2 native unavailable; 9/9 replays and 91,366 raw/processed samples. Final captures reviewed and all 13 report sections written. Read-only follow-up review found no blockers. Stop at 7.19.
