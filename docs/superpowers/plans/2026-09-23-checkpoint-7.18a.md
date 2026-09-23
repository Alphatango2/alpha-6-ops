# Checkpoint 7.18A implementation plan

> Execute inline using the executing-plans workflow. The user's checkpoint specification is the authority; no additional implementation approval is needed for these tests and documentation.

**Goal:** Establish a reproducible pre-migration baseline without changing production behavior.

**Architecture:** Exercise the unchanged production solution, existing diagnostic entry points and private flight journals. Add a separate WPF regression executable that references production assemblies and confines reflection to tests, following the existing Desktop smoke harness pattern.

**Tech Stack:** .NET 10, WPF, PowerShell, existing SQLite/JSON/JSONL stores.

**Spec:** User's Checkpoint 7.18A request in this task.

## Constraints

- No UI redesign, MainWindow refactor, runtime rule changes or 7.18B implementation.
- Report existing failures before any production repair; record migration-contract failures without repairing them here.
- Use isolated diagnostic directories and copies of private data. Never connect to or launch the simulator from synthetic checks.
- Preserve the production version and data formats. Keep private journals and generated evidence out of Git.
- Count executed report assertions explicitly; distinguish failures, skips and unavailable hardware validation.

## Tasks

1. Build `Alpha6Ops.slnx -c Release` with the configured SDK and run `tests/Alpha6Ops.Tests`. Run `scripts/verify-desktop.ps1 -Configuration Release` and inspect every component report.
2. Run `packaging/build-desktop.ps1`; exercise its executable against the nine original journals. Independently count telemetry records eligible for replay, hash inputs and compare the processed total to 91,366.
3. Add `tests/Alpha6Ops.MigrationChecks` with an external STA WPF host. Exercise view recreation/unloading, navigation, refresh purity, hidden-workspace progression, queued reconnect callbacks, dispatch/PIREP animation dependencies, text-driven decisions, frozen-format data readability and the optional large diagnostic-index workload. Report all checks even when a migration contract fails.
4. Add `scripts/verify-migration-baseline.ps1` to run/count the complete baseline, require the exact nine flights and capture machine-readable results. Missing reports or unexpected replay coverage must fail.
5. Create `docs/sierra4-migration-regression-checklist.md` and `docs/checkpoint-7.18a.md` with actual results, limitations, rerun commands and the go/no-go recommendation. Review the test-only diff and stop.

## Review focus

- An old dispatcher callback reaching a newly connected session must be tested explicitly; synthetic callback tests do not establish native DLL lifecycle behavior.
- Render refreshes must compare estimator internals, event lists and persisted recovery as well as visible values.
- Animation requirements must inspect when persistence and closeout occur, and let pending handlers finish before teardown.
- Data compatibility must use frozen fixtures or copies, not only a same-version write/read roundtrip.
- Raw telemetry records and replay-eligible records must both be counted so skipped pre-assignment data is visible.

## Execution notes

- Initial Release build: passed, zero warnings/errors. Core: 122 passed.
- Initial Desktop run: all component reports passed, including activation. Larger 1,000-file diagnostic workload is off by default and will be invoked explicitly.
- Production packaging succeeded. Nine recorded replay reports passed; independent coverage reconciliation follows.
- No production changes are planned. The new migration requirements may expose existing coupling failures; those are baseline evidence, not permission to start isolation.

## Completion and resumed work

- Production solution, normal packaging, Core, all Desktop reports, activation, fleet import and nine replay validations completed before interruption. These results were retained after verifying unchanged production source, package hashes and journal hashes.
- Review strengthened three test boundaries: invoke the actual production received callback, assert hidden-workspace phase/history/recovery advancement, and test first-render purity separately from settled-refresh idempotence.
- Resumed work corrected a test-only reflection ambiguity with WPF's inherited `Render` method, then reran only the affected migration harness (including its large-index and coverage sections). No production failure was repaired or production file changed.
- Final consolidated result: 1,779 assertion executions, 1,772 passed, seven failed migration contracts, two skipped native simulator gates. Nine flights and 91,366 samples reconciled exactly.
- Checklist and final report completed. New harness reports failures honestly; it exits 1 on the existing migration issues. Checkpoint 7.18B was not started.
