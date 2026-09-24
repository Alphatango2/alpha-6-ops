# Checkpoint 7.23 — Flight recovery persistence hardening

**PASS. The observed 7.22 persistence blocker is CLOSED.** Final verification: **2,025 checks passed, 0 failed, 2 native simulator checks unavailable; 9/9 recorded flights and 91,366/91,366 telemetry samples passed.** Classic and Map V2 final runs produced no unexpected recovery reports. Map V2 remains opt-in. This checkpoint is local; no installation, merge or GitHub publication was performed.

Authority: [user specification](recovery/checkpoint-7.23-spec.txt). Evidence: [final verification](recovery/evidence/7.23/verification.json), [investigation](recovery/investigation-7.23.md), [execution plan and rulings](superpowers/plans/2026-09-24-checkpoint-7.23.md). Base commit: `8ab65cc`; branch: `codex/checkpoint-7.23`.

## 1. Root-cause findings

The old single-attempt replacement is vulnerable to a **sufficiently characterized transient Windows replacement failure**. An independent probe, without application/renderer readers, failed **5/5,000** Move replacements with AccessDenied. Every failure succeeded after a 10 ms delay. Both complete destination and staging file remained present. Files were not read-only; C: is healthy local NTFS. This reproduces and expands 7.22's 3/500 result.

The alternative File.Replace failed **27/5,000** times and was slower (9.49 s versus 3.55 s for the raw loops). It also recovered after 10 ms. Switching APIs alone was not a fix. No particular antivirus, indexer, filter or external process was identified; attribution remains unresolved. Categories: C/D/G (Windows replacement / possible external transient holder / unresolved combination). Store-level concurrency/handle risks A/B also existed, but normal coordinator saves are already dispatcher-serialized. Accelerated tests increase exposure; they are not the sole cause. [Raw evidence](recovery/evidence/7.23/raw-replacement-probe.json).

## 2. Previous recovery save architecture

Coordinator observation/events/gate and schedule commands → fixed `.tmp` written through WriteAllText → one Move(overwrite). ReadAllText loaded the authoritative file. Delete removed authoritative and staging names. No store-level serialization, retries or explicit durable flush. Save failures were reported through `flight_recovery_save`.

## 3. New recovery save architecture

Same coordinator calls and synchronous ownership → validate/serialize complete JSON → per-normalized-path transaction lock → write and flush staging file → close handle → bounded rename retry. Load/Delete share the same lock. No background queue, coalescing, operational changes, map dependency, database migration or new persistence format.

## 4. Exact replacement strategy

Retain `active-flight-state.json.tmp` beside `active-flight-state.json`. FileStream uses Create/Write/FileShare.None, writes serialized UTF-8, Flush(true), then closes before `File.Move(temp, path, overwrite:true)`. Never delete/truncate the authoritative file as preparation. No cross-volume copy, delete-then-move fallback, File.Replace fallback or backup file. The fixed staging name also supports cleanup of old-format leftovers.

## 5. Concurrency behavior

Save/Load/Delete serialize on a case-insensitive full-path key; dot segments normalize. Unrelated profile paths have separate locks. Lock acquisition times out after two seconds rather than waiting indefinitely. Normal coordinator ordering is unchanged. Direct overlapping writers publish complete states in lock-acquisition order; there is no timestamp-based reordering. A final Save after concurrent work persists the final supplied state. The app's existing single-instance boundary remains responsible for preventing competing production processes.

## 6. Retry policy

Five attempts maximum, with **10/20/40/80 ms** waits: **150 ms total deliberate delay per file-operation stage**. Retry Windows sharing/lock violations (32/33) and the observed AccessDenied (5). Read-only files and directory targets fail without retry. Invalid paths and unrelated I/O errors are not retried. An indistinguishable permanent ACL denial may use the same finite budget, then fails visibly. Stage, attempts, path and HResult are included in the existing coordinator failure report. No error guard was weakened.

Stage/write, replace and cleanup each have a finite retry budget. A failed stage plus failed cleanup can therefore incur 300 ms of deliberate delay, plus actual I/O. This is a synchronous store; no cancellation queue was introduced. Windows I/O and device failure themselves cannot be given a hard real-time deadline by these managed retries.

## 7. Durability behavior

Flush(true) requests the staged file's buffers be flushed before publication; it is not a whole-volume sync. No additional flush/coalescing policy changes the existing per-sample save cadence. This costs measurable time: for 500 isolated 16 KB writes, buffered p50/p95 were **0.17/0.22 ms**, flushed **1.22/3.91 ms**. Complete recovery saves measured **1.79 ms p50, 4.39 ms p95, 5.05 ms p99, 25.67 ms maximum** over 5,000 replacements. Filesystem metadata/controller power-loss guarantees are not certified by these tests.

## 8. Crash/interruption behavior

Before staging or during an incomplete staging write, the previous authoritative file remains the only load candidate. Before/during a blocked rename, forced child-process termination preserved that old file and a new process restored it. After successful publication, forced termination/new-process load restored the new state. Partial and fully serialized orphan staging files were both tested and never promoted. A crash inside the operating system's rename or actual power loss was not fault-injected; same-directory rename and prepublication flush are the protection, with that limit stated explicitly.

## 9. Temp/backup cleanup

Successful rename consumes staging. Save's finally block removes remaining temp; Delete also removes it. A cleanup failure is separately reported without masking the primary exception. A locked leftover is bounded to **one fixed file**, overwritten by the next successful Save or removed by Delete. No backups accumulate. Restore never treats staging as flight truth.

## 10. Existing-format compatibility

Authoritative path, profile location, JSON property names, numeric enum representation and named floating-point values remain unchanged. Historical fixture round trips preserve phase, UTC actuals, events and NaN telemetry. Null/omitted optional monitor state remains supported; valid non-null monitor state restores. Missing required recovery members, null event collections, invalid phases and malformed monitor Fired collections are rejected with a load diagnostic. The independent review found the malformed-monitor startup hazard; a failing startup test now passes after the targeted validation.

## 11. Deterministic contention

All **21 focused checks** passed. Actual file handles reproduce a 30 ms transient lock, permanent replacement lock, locked staging/cleanup and external read-handle contention. Tests assert the original bytes remain complete, finite failure, diagnostic detail, eventual success after release and bounded leftovers. Even a delete-shared external read handle can deny Move here; the tested promise is complete old state plus visible bounded failure, followed by successful saving after release. [Recovery results](recovery/evidence/7.23/recovery-checks.json).

## 12. Sequential replacement stress

**5,000/5,000 complete replacements and reload comparisons**, no failures, corruption, unexpected reports or leftover temp. Total save/load comparison loop: **10.64 seconds**. This is ten times the prior 500-operation reproduction and uses the real production store with durable flush, not a mock.

## 13. Concurrent stress

Four writers performed 1,000 overlapping saves; **181,336 reads** checked complete states. A final ordered Save brought the total to **1,001**, and its exact state survived. Another run overlapped 100 saves, 100 deletes and 100 loads, followed by final deletion and recreation. Independent profile saving proceeded while another profile was locked. No unexpected reports or file corruption.

## 14. Recovery round trips

All 5,000 sequential round trips compared complete serialized state. Historical fixtures and new monitor state restored through the real coordinator. Cold child processes reloaded pre/post-interruption files; current assignment gates and latest recovery remained readable after owner disposal/recreation. Existing migration checks also read **498 historical flights, 1,003 attached events and 12/12 gates** from profile copies. Source profile hash checks and recorded-journal hashes remained unchanged.

## 15. Flight-runtime performance impact

Both accelerated 395-sample flights retained all **395 telemetry samples, 27 ordered events and four OUT/OFF/ON/IN transitions**, completed normally and persisted identical history/PIREP hashes. Compared with retained 7.22 evidence, **every one of the 395 per-sample ETA/progress/remaining-distance/phase records matched in each run**. No coalescing or missing operational observations was introduced.

| Measurement | 7.22 baseline / stalled map | 7.23 baseline / stalled map |
|---|---:|---:|
| Feed completion | 7,983 / 8,020 ms | 8,007 / 8,074 ms |
| Background Dispatcher heartbeat p95 | 83 / 82 ms | 163 / 178 ms |
| Background heartbeat maximum | 121 / 140 ms | 271 / 321 ms |

Throughput changed approximately **+0.3%/+0.7%**, without loss or state differences. **UI scheduling delays increased in this run**; these are not acceptable evidence for claiming an improvement in responsiveness. Flush cost is measured, but the run-to-run heartbeat difference cannot all be attributed to it from this experiment. Sustained profiling remains a 7.24 priority. [Comparison](recovery/evidence/7.23/runtime-comparison.json), [contention results](recovery/evidence/7.23/contention.json).

## 16. Classic-map verification

Normal packaged desktop/classic, workspace navigation, refresh purity, callback generation, assignment, history, gates and animation-independent persistence checks passed. The default renderer is unchanged. **Zero unexpected recovery reports** in the full classic regression run.

## 17. Map V2 verification

**119/119** production host/model/lifecycle/fallback/visual/contention checks, **7/7** mailbox and **7/7** browser/math checks passed. The real browser was stalled for **1,000 ms**; 50 authoritative flight samples processed during it. SimConnect ownership/generation, ordered flight state and persisted results remained unchanged. Map V2 is still explicitly opt-in; classic fallback is retained. No Map V2 implementation files were changed.

## 18. Unexpected-report guard

**CLEAN.** `verify-map-v2.ps1` is unchanged and completed successfully with zero CrashReports. Classic final verification contained zero `flight_recovery_*` reports. The separate negative recovery suite deliberately produces and asserts exact failure reports in isolated directories; those are neither hidden nor applied as exceptions to the classic/Map V2 guard.

## 19. Files created/modified

Production modifications:
- `src/Alpha6Ops.Desktop/FlightRecovery.cs` — store hardening/validation.
- `src/Alpha6Ops.Desktop/FlightSessionCoordinator.Tracking.cs` — additional context on the existing save failure report only.
- `src/Alpha6Ops.Desktop/Alpha6Ops.Desktop.csproj` — friend access for the recovery test assembly.

New verification: `tests/Alpha6Ops.RecoveryChecks/{Alpha6Ops.RecoveryChecks.csproj,Program.cs}` and `scripts/verify-flight-recovery.ps1`.

New documentation: this report, `docs/recovery/{checkpoint-7.23-spec.txt,investigation-7.23.md}`, `docs/superpowers/plans/2026-09-24-checkpoint-7.23.md`. New evidence under `docs/recovery/evidence/7.23/`: `raw-replacement-probe.json`, `initial-red.json`, `monitor-red.json`, `recovery-checks.json`, `baseline.json`, `build-hashes.json`, `map-checks.json`, `contention.json`, `runtime-comparison.json`, `verification.json`.

The preexisting untracked 7.11 bundle was left untouched.

## 20. Build/package

Complete Release production solution: **success, zero warnings/errors**. Normal production packaging: **success, not installed**. Local outputs retain version **0.22.0**: `Alpha6OPS-Setup-0.22.0.exe`, desktop folder and `Alpha6OPS-Desktop-0.22.0-win-x64.zip`. [Package hashes](recovery/evidence/7.23/build-hashes.json) identify this build; no version/rebranding change was requested.

## 21. Regression counts

| Group | Passed | Failed |
|---|---:|---:|
| Core | 122 | 0 |
| Desktop smoke | 18 | 0 |
| Dashboard | 531 | 0 |
| Refresh | 1,010 | 0 |
| Identity / diagnostic index / monitor / tracking | 108 | 0 |
| Single-instance activation | 1 | 0 |
| Recorded-flight validations | 9 | 0 |
| Migration contracts | 31 | 0 |
| Large diagnostic index | 35 | 0 |
| Fleet import | 6 | 0 |
| Map V2 | 119 | 0 |
| Map mailbox / browser math | 14 | 0 |
| New recovery checks | 21 | 0 |
| **Executed total** | **2,025** | **0** |

**Two unavailable/skipped native checks**: actual SimConnect connection lifecycle and actual native reconnect. They are not counted as passes. No new disabled tests. Development compile corrections, intentional RED runs and the corrected external-handle test expectation are retained in local evidence and excluded from final counts. A terminated old process entry with zero threads/handles was handled by the existing baseline runner; no active user application was terminated.

## 22. Replays

**9/9 passed**: JST221, QFA124, JST786, QLK1579, JST721, JST724, JST760, QLK1274 and ANG2.

## 23. Telemetry

**91,366 raw / 91,366 processed recorded-flight samples**. Respectively: 11,965; 11,535; 6,952; 9,040; 7,650; 7,459; 10,552; 10,519; 15,694. The two 395-sample synthetic contention flights are additional and not included in that recorded-flight total.

## 24. Remaining risks

External lock ownership remains unproven. Persistent locks/permissions/device errors still correctly fail and report; short retries cannot make an unavailable disk reliable. Sudden power loss and forced termination inside the OS rename were not certified. Junction/extended-path aliases, explicit ACL-deny cases, network filesystems and competing processes remain outside tested coverage. Locks are retained per distinct profile path for process lifetime; normal production uses one profile path. Native simulator reconnect remains a manual gate. Higher UI heartbeat delays and sustained map performance remain open profiling work.

## 25. 7.22 blocker status

**CLOSED for the characterized transient recovery replacement failure**: larger production-store stress, deterministic contention, compatibility, full regression and final unsuppressed map report guard all passed. This does not close the independent performance/manual gates for making Map V2 default.

## 26. Checkpoint 7.24 recommendation

**GO for a separately scoped sustained performance and manual-validation checkpoint.** Profile file-save and Dispatcher latency with a real simulator flight, assess longer Map V2 sessions, and exercise native reconnect/input/DPI on representative hardware. Keep Map V2 opt-in until those separate gates justify a default decision. Do not combine this with workspace migration or new airline-world functionality.

## Rerun

From the prepared repository checkout, with the normal application closed:

```powershell
& scripts/verify-flight-recovery.ps1
& scripts/verify-migration-baseline.ps1 -ReplayManifest .work/replay-7.17-manifest.json
& scripts/verify-map-v2.ps1 -ApplicationPath (Join-Path $PWD outputs/Alpha6OPS-Desktop-0.22.0/Alpha6OPS.exe)
```

Keep all unexpected-report guards active; inspect classic output for recovery reports as well as the map runner's automatic guard. Record unavailable native gates separately. Raw final runs: `work/checkpoint-7.23/recovery-reviewed`, `work/checkpoint-7.18a/20260924-165510-e129176b`, `work/checkpoint-7.23/map-final`. Checkpoint 7.23 ends here.
