# Checkpoint 7.23 implementation plan

Goal: close the recovery replacement blocker without changing flight behavior or the recovery contract.
Spec: `docs/recovery/checkpoint-7.23-spec.txt` (user supplied, authoritative).
Architecture: retain synchronous coordinator saves, the existing JSON and authoritative filename; harden only the store with per-path serialization, flushed temporary writes and bounded replacement retries.
Tech stack: .NET 10 / Windows NTFS, existing Desktop + console regression harness.

## Global constraints
- Keep profile location, schema, coordinator sequencing, all operational decisions and UI unchanged.
- Keep Map V2 opt-in and the unexpected-report guard enabled.
- Test in isolated directories; never rewrite source profiles or recorded journals.
- Preserve evidence of failed runs. No 7.24 work, installation or GitHub publication.

## Review focus
1. Failure between write/flush/rename must leave the old authoritative JSON intact.
2. A failed cleanup must not grow unbounded temporary files or mask the primary failure.
3. Concurrent Save/Load/Delete, normalized paths and lock timeouts must not corrupt recovery.
4. Retry classification must be bounded and must not conceal persistent permissions failures.
5. Valid old optional/null fields must remain readable; incomplete JSON must never restore partial state.

## Task 1 — characterize and reproduce (before production edits)
Files: RecoveryChecks project/Program; investigation evidence.
Trace every call and handle lifetime, inspect environment, compare 5,000 raw Move and Replace operations.
Write and run failing real-file checks for transient lock recovery, failed-save cleanup and incomplete JSON rejection.
Expected: reproducible failures, with old file still present; no attribution to a particular external process without evidence.

## Task 2 — smallest store hardening
Files: `FlightRecovery.cs`; focused test suite.
Interfaces: Save/Load/Delete signatures and schema unchanged. Add only private persistence helpers.
Use a shared lock per normalized recovery path for all operations. Keep one same-directory `.tmp`, close and flush before rename. Retain same-volume Move(overwrite) with bounded retries (10/20/40/80 ms); do not switch to Replace without evidence of benefit.
Validate required recovery members before accepting load; preserve optional monitor/telemetry and named NaN.
Run deterministic actual-lock tests, at least 5,000 sequential replacements, overlapping writers/readers, deletion/recreation, cold process reloads, failed writes and controlled interruption stages.
Expected: every new contract check passes; persistent failures visible and bounded, no partial authoritative file.

## Task 3 — verification and independent review
Files: `scripts/verify-flight-recovery.ps1`; evidence and report.
Run complete normal production/package regression, nine recorded flights/91,366 samples, all Map V2 checks including real renderer stall and unchanged crash-report guard. Compare classic/map runtime event, phase, progress/ETA, history and PIREP outcomes.
Expected: 2,004 existing assertions plus recovery checks pass; two native simulator gates remain explicitly unavailable.
Request one independent whole-change review per executing-plans skill, resolve material findings with failing tests first. Report actual counts, measured timings, remaining attribution/durability limits and blocker status. Stop at 7.23.

## Execution ledger
- Baseline: `8ab65cc`; only preexisting untracked 7.11 bundle, preserved.
- Ruling: use existing prepared checkout on `codex/checkpoint-7.23` — preserves local SDK, journals and package evidence; no main-branch edits, no extra checkout needed.
- Shared interfaces: Task 1 tests call unchanged store; Task 2 changes store only; Task 3 verifies packaged same code. No incompatible interfaces.
- Task 1 complete: raw Move failed 5/5,000; Replace failed 27/5,000. All succeeded on retry after 10 ms. Three RED checks fail for intended reasons. Evidence: `work/checkpoint-7.23/{investigation/probe.json,red/summary.json}`.
- Ruling: retain same-directory Move with bounded retry — Replace is slower, fails more here and documents additional partial-failure states; no backup/schema/recovery-precedence change. External holder remains unattributed.
- Ruling: an external delete-shared read handle can still deny Move on this Windows installation — the contract test accepts a complete old state plus bounded visible failure, then requires success after closing the reader. It does not promise publication through an indefinitely held external handle.
- Task 2 complete: three initial RED cases now pass. Twenty-one focused checks pass, including 5,000 replacements/round trips, concurrent readers/writers, controlled process exits, cleanup failures, permanent-lock reporting and real coordinator restoration. Evidence: `work/checkpoint-7.23/recovery-reviewed`.
- Independent review: one material malformed-monitor finding. `MonitorState:{}` crashed restoration. Added `MALFORMED_MONITOR_REJECTED_WITHOUT_STARTUP_CRASH`, observed ArgumentNullException RED, added null Fired validation, then GREEN including valid-monitor restoration. No other material findings.
- Review coverage limits (deferred): explicit ACL-deny and junction/extended-path aliases are not exercised. Read-only/sharing failures and normalized dot/case paths cover the current single-profile caller, not every Windows alias or permission arrangement.
- Task 3 complete: full production/package/classic verification passed 1,871 checks and all 91,366 samples; Map V2 passed 119+7+7 with unchanged unexpected-report guard clean. Combined with 21 recovery checks: 2,025 passed, zero failed, two native simulator checks unavailable. Independent historical comparison matched all 395 ETA/progress/phase records in each accelerated run. Evidence captured in `docs/recovery/evidence/7.23/`.
- Performance ruling: close the observed persistence blocker, retain opt-in status and explicitly carry higher UI heartbeat delays to the next scoped performance checkpoint — operational throughput/state remained complete and equivalent, but this is not a responsiveness approval.
- Ruling: retain local branch and normal uninstalled package — the checkpoint does not authorize a merge, publication or change to Map V2 enablement. No subsequent checkpoint work.
