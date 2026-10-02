# Checkpoint 7.26B Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a compact, archived, Sierra 4-native, fully verified pre-premium-UI baseline without weakening the validated flight-tracking foundation.

**Architecture:** Preserve the 7.25A candidate and authoritative replays first, then archive closed evidence outside the workspace and remove only verified source copies. Add a central storage identity with copy-and-verify legacy migration, bounded diagnostics retention, and a Sierra 4 installer/package identity while retaining internal namespaces where renaming adds risk.

**Tech Stack:** PowerShell 7, Git, .NET 10/WPF, C#, Windows installer bootstrap, Python/Node regression helpers.

**Spec:** `docs/checkpoint-7.26b/guideline.txt`

## Global Constraints

- The authoritative repository is `F:\Sierra4-Development` on `codex/checkpoint-7.24c`.
- Preserve frozen candidate `S4-7.25A-A350-20261001-6020896a402b` byte-for-byte.
- The pre-mutation ledger is `docs/checkpoint-7.26b/pre-mutation-ledger.json`.
- Archive to `F:\Sierra4-Archive`; verify before deleting any archived source.
- Preserve active source, tests, required replay fixtures, authoritative operational data, current frozen baseline, and all ambiguous material.
- Do not begin 7.27 or Hero Globe work.
- Do not install the new release candidate automatically.
- Work in the authoritative checkout because the checkpoint must consolidate its 43 tracked modifications and existing untracked evidence; a clean worktree would omit that state.

## Review Focus

- Interrupted migration must leave the legacy profile authoritative and intact; Task 2 tests staging failure and idempotent retry.
- Retention must never remove an active or newest unresolved journal; Task 3 tests protected and interrupted sessions under quota pressure.
- Replay consolidation must preserve every approved journal byte-for-byte; Task 1 verifies hashes before deleting source trees.
- The production package must omit diagnostic command harnesses while the development build retains them; Task 4 builds and inspects both modes.
- Old and new application processes must not open one logical profile concurrently; Task 2 tests dual single-instance identities.

---

### Task 1: Verified archive, replay consolidation, and workspace cleanup

**Files:**
- Create: `docs/checkpoint-7.26b/archive-plan.json`
- Create: `docs/checkpoint-7.26b/archive-verification.json`
- Create: `docs/checkpoint-7.26b/cleanup-ledger.json`
- Create: `scripts/replay-fixtures.json`
- Modify: `scripts/verify-checkpoint-7.25.ps1`
- External: `F:\Sierra4-Archive\checkpoint-7.26b-20261002\`

**Interfaces:**
- Consumes: 7.26 classes C through J and the pre-mutation ledger.
- Produces: verified archive, compact `work/regression-fixtures/flight-tracking-v1`, portable replay manifest resolution, and a measured active workspace.

- [ ] **Step 1: Freeze the archive/delete plan**

Enumerate every archive unit, retained path, replay source and cleanup candidate into `archive-plan.json`; no source path may be deleted unless it appears there with an audit class and disposition.

- [ ] **Step 2: Consolidate replay inputs**

Copy the approved baseline-nine, JST776, JST771, supplemental JST771 history, final history database and DAL40 journal into `work/regression-fixtures/flight-tracking-v1`; record source and destination SHA-256 values and require equality.

- [ ] **Step 3: Verify portable replay resolution fails before the change**

Run the existing 7.25 verifier and retain its `Replay unavailable: C:\...` failure as RED evidence.

- [ ] **Step 4: Implement repository-relative replay resolution**

Add `scripts/replay-fixtures.json` and update `verify-checkpoint-7.25.ps1`/`verify-checkpoint-7.25a.ps1` to resolve the consolidated fixtures without an old-workspace absolute path.

- [ ] **Step 5: Copy and verify the archive**

Copy explicit archive units, compare per-unit file/byte counts, verify critical hashes and deterministic representative samples, and write `archive-verification.json`. Any mismatch stops the task before deletion.

- [ ] **Step 6: Remove verified source copies and reproducible output**

Delete only archive-plan entries marked verified/removable plus audited `bin`, `obj`, `node_modules`, `dist`, installer extraction and dashboard temporary trees. Record every removed root and measured bytes in `cleanup-ledger.json`.

- [ ] **Step 7: Verify retained assets**

Revalidate 531/531 frozen entries, replay-fixture hashes, retained Git files, workspace counts/sizes, and archive restore readability.

### Task 2: Sierra 4 storage identity and safe profile migration

**Files:**
- Create: `src/Alpha6Ops.Desktop/ProductStorage.cs`
- Create: `tests/Alpha6Ops.RecoveryChecks/ProductStorageChecks.cs`
- Modify: `tests/Alpha6Ops.RecoveryChecks/Program.cs`
- Modify: `src/Alpha6Ops.Desktop/App.xaml.cs`
- Modify: `src/Alpha6Ops.Desktop/CrashReporter.cs`
- Modify: `src/Alpha6Ops.Desktop/SingleInstance.cs`
- Modify: `src/Alpha6Ops.Desktop/FlightSessionCoordinator.cs`
- Modify: `src/Alpha6Ops.Desktop/MainWindow.xaml.cs`
- Modify: `src/Alpha6Ops.Desktop/MainWindow.Dashboard.cs`
- Modify: `src/Alpha6Ops.Desktop/UserPreferences.cs`
- Modify: `src/Alpha6Ops.Desktop/GeneralSettings.cs`
- Modify: `src/Alpha6Ops.Desktop/ProgramMonitor.cs`
- Modify: `src/Alpha6Ops.Desktop/SimBriefImporter.cs`
- Modify: `src/Alpha6Ops.Desktop/OperationsMapHost.cs`
- Modify: `src/Alpha6Ops.Desktop/OfpViewerWindow.xaml.cs`

**Interfaces:**
- Produces: `ProductStorage.Initialize()`, `ProductStorage.Current`, `ProductStorage.Migrate(string legacyRoot, string canonicalRoot)`, and categorized path properties for Data, Settings, Diagnostics, Cache and State.
- Consumes: existing explicit diagnostic directories unchanged for test isolation.

- [ ] **Step 1: Write failing migration/path tests**

Cover fresh profile creation, byte-exact legacy copy, categorized destinations, preserved SQLite sidecars, idempotent second launch, canonical-root conflict, interrupted-copy fallback, and legacy source preservation.

- [ ] **Step 2: Run focused tests and verify RED**

Run the RecoveryChecks storage mode; expected failure is missing `ProductStorage` behavior.

- [ ] **Step 3: Implement the storage service and migration**

Use `%LOCALAPPDATA%\Sierra 4` as canonical profile root, `%LOCALAPPDATA%\Alpha6Designs\Alpha6OPS` as legacy root, staging plus SHA-256 verification before atomic promotion, and `State\migration.json` as the completion marker. On failure, keep the legacy layout active and untouched.

- [ ] **Step 4: Route production stores through categorized paths**

Use Data for flight/history/gate data, Settings for preferences/UI state, Diagnostics for logs/crashes/index, Cache for Map/SimBrief, and State for program status. Explicit diagnostic roots retain the current flat test contract.

- [ ] **Step 5: Add dual process identity**

Acquire both Sierra 4 and legacy Alpha6 mutex/event identities during the compatibility window; release both atomically.

- [ ] **Step 6: Run focused and existing recovery suites**

Require storage tests and all recovery/terminal/logbook modes to pass.

### Task 3: Bounded production diagnostics retention

**Files:**
- Create: `src/Alpha6Ops.Desktop/ProductionRetention.cs`
- Create: `tests/Alpha6Ops.RecoveryChecks/ProductionRetentionChecks.cs`
- Modify: `tests/Alpha6Ops.RecoveryChecks/Program.cs`
- Modify: `src/Alpha6Ops.Desktop/App.xaml.cs`
- Modify: `src/Alpha6Ops.Desktop/LogFileDatabase.cs`

**Interfaces:**
- Produces: `ProductionRetention.Apply(ProductStoragePaths paths, DateTimeOffset now, IReadOnlySet<string>? protectedPaths = null)` returning a report of compressed/deleted/preserved files.
- Consumes: `ProductStoragePaths` from Task 2.

- [ ] **Step 1: Write failing retention tests**

Cover completed-journal compression, active-journal preservation, newest interrupted-session preservation, 14/30/90-day age rules, 512 MiB/1 GiB/100 MiB caps, Map 500 MiB LRU, SimBrief latest-20/90-day/100 MiB behavior, atomic output and missing-file index cleanup.

- [ ] **Step 2: Run focused tests and verify RED**

Expected failure is missing `ProductionRetention` behavior.

- [ ] **Step 3: Implement minimal bounded retention**

Compress journals with atomic `.gz` promotion, delete oldest eligible files until both age/count and byte caps hold, preserve active/recovery/authoritative data, and tolerate individual cleanup failures without corrupting policy state.

- [ ] **Step 4: Run retention during ordinary startup**

Apply after storage migration and crash reporting initialization, before opening the production session. Diagnostic test roots are excluded unless a test invokes retention explicitly.

- [ ] **Step 5: Refresh the derived diagnostic index**

Rebuild its file rows from current diagnostics so deleted files cannot remain visible; flight history and PIREP tables remain untouched.

- [ ] **Step 6: Run focused and full recovery suites**

Require all new tests and existing persistence/history checks to pass.

### Task 4: Sierra 4 package and installer identity

**Files:**
- Modify: `src/Alpha6Ops.Desktop/Alpha6Ops.Desktop.csproj`
- Modify: `src/Alpha6Ops.Desktop/App.xaml.cs`
- Modify: `src/Alpha6Ops.Desktop/DashboardData.cs`
- Modify: `packaging/PreviewSetup.cs`
- Modify: `packaging/build-desktop.ps1`
- Modify: `packaging/Desktop-Quick-Start.txt`
- Create: `scripts/test-production-package.ps1`

**Interfaces:**
- Produces: Sierra 4 v0.23.0 per-user package at `%LOCALAPPDATA%\Programs\Sierra 4`, `.sierra4` ownership marker, Sierra4 registry/shortcut identity, and a production build mode without diagnostic command harnesses.
- Consumes: storage migration and dual-instance compatibility from Task 2.

- [ ] **Step 1: Write a failing package contract test**

Assert Sierra 4 filenames/metadata/install path/marker/registry/shortcut, absence of source/test/evidence content and diagnostic command strings, exact ZIP/payload equality, safe same-channel replacement, and preservation of external profile data.

- [ ] **Step 2: Run the package test and verify RED**

Expected failures name the Alpha6 preview identity and shipped diagnostic harnesses.

- [ ] **Step 3: Implement user-visible Sierra 4 identity**

Rename the executable/assembly/package/install/registry/shortcut/product metadata to Sierra 4 while retaining internal namespaces and legacy profile compatibility.

- [ ] **Step 4: Add production diagnostic exclusion**

Keep harnesses in development builds and omit their entry points/classes from packaging builds through `IncludeDiagnosticHarnesses=false`.

- [ ] **Step 5: Verify clean replacement and footprint**

Run extraction and upgrade tests in fresh temporary roots, compare manifests, ensure no obsolete seeded file survives directory replacement, and measure payload bytes/files.

### Task 5: Git hygiene and checkpoint documentation

**Files:**
- Modify: `.gitignore`
- Create: `docs/checkpoint-7.26b/implementation-report.md`
- Create: `docs/checkpoint-7.26b/pre-premium-ui-handoff.md`
- Create: `docs/checkpoint-7.26b/known-limitations.md`

**Interfaces:**
- Consumes: archive, cleanup, storage, retention and packaging results.
- Produces: deliberate tracked baseline through 7.26B and explicit exclusions for external archive, work evidence, generated output, installers and caches.

- [ ] **Step 1: Update ignore policy**

Add `.work/`, Sierra 4 generated packages, bundles, local databases/caches and 7.26B generated evidence while leaving source, compact manifests and required scripts trackable.

- [ ] **Step 2: Review all prior dirty changes**

Compare the pre-mutation ledger to the retained tree, classify every original tracked modification and untracked source/test/script/report, and stage only intentional checkpoint content.

- [ ] **Step 3: Write implementation and handoff documents**

Record build identity, architecture, checkpoint history, tracking guarantees, known limitations, migration/rollback/package behavior, exclusions, cousin-merge notes and `7.27 — Premium UI Foundation` as the next checkpoint.

- [ ] **Step 4: Commit coherent baseline**

Commit the preserved 7.24C–7.25A work and 7.26B foundation without rewriting history; generated evidence remains ignored.

### Task 6: Full regression, release freeze, and final gate

**Files:**
- Create: `scripts/freeze-release-candidate.ps1`
- Create: `docs/checkpoint-7.26b/final-acceptance.json`
- Update: `docs/checkpoint-7.26b/implementation-report.md`
- Update: `docs/checkpoint-7.26b/pre-premium-ui-handoff.md`

**Interfaces:**
- Consumes: portable replay fixtures, clean source commit, package contract and complete regression suite.
- Produces: immutable `S4-7.26B-PRE-UI-*` candidate, hashes/manifests/source snapshot, final clean commit and a GO/NO-GO decision.

- [ ] **Step 1: Run the full 7.25 plus 7.25A gates**

Require lifecycle, OOOI, PIREP, recovery, observed cruise, step climbs, TOD, ETA continuity, Map V2 history/scheduling/restoration/responsiveness, terminal shutdown, Block In, Logbook, 11 historical replays and DAL40. Keep native skips and telemetry counts separate.

- [ ] **Step 2: Build and inspect the production package**

Require zero build/test failures, package contract PASS, no shipped diagnostic harnesses, and measured clean footprint.

- [ ] **Step 3: Commit the clean source baseline**

Record a source commit with no unexplained modifications; this commit becomes the candidate source identity.

- [ ] **Step 4: Freeze the new candidate**

Create a separate uninstalled candidate with source manifest/snapshot, payload manifest, installer/ZIP hashes, acceptance summary and self-excluding final freeze manifest.

- [ ] **Step 5: Reverify the old and new frozen candidates**

Require old 531/531 and new N/N integrity, exact source commit, `sourceIsDirty=false`, and installed-footprint measurement from extraction only.

- [ ] **Step 6: Final review and handoff commit**

Review the complete branch, resolve Critical/Important findings through RED→GREEN tests, record deferred minors/rulings, commit final reports, and confirm 7.27 remains unstarted.

