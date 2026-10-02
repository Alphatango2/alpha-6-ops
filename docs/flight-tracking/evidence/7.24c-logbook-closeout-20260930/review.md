# 7.24C logbook correction — independent review

Date: 2026-09-30

Verdict: **PASS for the reviewed correction. No concrete correctness, scope, or security issue prevents closure.** The parent task's full baseline regression has passed; final supplementary verification and baseline freezing remain with that task. This review did not run builds, tests, or a simulator session.

## Scope and evidence

Reviewed the authoritative request in `request.txt`, the current production projection and its caller, the new recovery-check coverage, relevant history query/write paths, the retained red/green results, and disconnected native screenshots.

Comparison with `source-before.json` found only these existing source files changed after entry:

- `src/Alpha6Ops.Desktop/PilotLogbookWorkspace.xaml.cs`
- `tests/Alpha6Ops.RecoveryChecks/Program.cs`
- `packaging/PreviewSetup.cs`
- `packaging/Desktop-Quick-Start.txt`

Source-file enumeration found exactly these two additions outside the entry inventory:

- `src/Alpha6Ops.Desktop/PilotLogbookOperationalTimes.cs`
- `tests/Alpha6Ops.RecoveryChecks/LogbookChecks.cs`

The pre-existing `--terminal` and active-recovery test routes in `Program.cs` are inherited work documented by `entry.diff`; only `--logbook` belongs to this correction. The workspace's wider dirty state was not treated as this patch.

The packaging changes were added during review and inspected separately. `PreviewSetup.cs` changes only its visible `TERMINAL T2` label to `FINAL L1`; no installer mechanics changed. `Desktop-Quick-Start.txt` explains the distinct final baseline, authoritative logbook display, retained version/profile identity, preserved T2 evidence, and absence of a further validation-flight requirement. These are appropriate release metadata changes for the requested freeze.

## Correctness and fallback

The old presentation code selected the first event title containing `Liftoff` or `Takeoff`. This admitted `Takeoff roll` before the authoritative OFF milestone. OUT, ON, and IN also used title searches, so their apparent correctness did not establish an authoritative source.

The selected row still calls `ReadFlightEvents(row.Id)`. That query binds the persistent history ID and returns only that history's events. No callsign lookup or new cross-record join was introduced.

The new resolver takes each present `actualOut`, `actualOff`, `actualOn`, or `actualIn` from the stored `pirep_submitted` payload before considering fallback. Present null, wrong-type, or unparseable actuals remain unknown. Full `DateTimeOffset` precision is retained until visible UTC formatting to seconds.

For older payloads with an absent actual field, the resolver next uses structured `phase` values and their simulator timestamps. When those do not supply a usable timestamp, it uses exact historical tracking milestone titles: `Block-out / taxi out`, `Liftoff`, `Touchdown`, and `Block-in / flight complete`. It does not substitute recording/computer time. First OUT/OFF and last ON/IN selection match the current PIREP writer's milestone semantics. Descriptive takeoff-roll, shutdown, and session-completion text cannot satisfy the fallback.

These fallbacks are documented directly beside the implementation and are covered by synthetic and saved legacy cases. The FlyVirtual import display remains unchanged.

## Scope and security assessment

The production change is a read-only display projection. No phase detection, takeoff-roll detection, OFF semantics, terminal shutdown, PIREP writer, history writer, duration computation, event timeline mapping, Map V2, or responsiveness policy changed. The helper parses local persisted JSON as data; it adds no SQL, network access, file writes, or executable interpretation of stored values.

All **1,246** entries in `preservation-before.json` were independently rehashed during this review: **zero changed or missing files**. This includes the frozen T2 evidence represented by that manifest.

## Verification evidence reviewed

- `red/logbook-checks.json`: 10 checks, 2 passed and 8 failed against the old view. The core failure displayed OFF `20:00:15Z` from takeoff roll instead of the later milestone.
- `green-2/logbook-checks.json`: 40 checks, 40 passed, zero failed. Coverage exercises the production WPF view/query, all four authoritative fields versus descriptive event timestamps, full timestamp precision, phase and legacy fallback, absent versus null fields, malformed inputs, imported history, same-callsign isolation, and preservation of rows/event payloads.
- Saved-flight coverage verifies successful JST500, JST771, seven PIREPs containing actuals, seven legacy histories, durations, all 502 history/event sets, the unchanged coordinator snapshot, and unchanged original saved-database hash.
- JST500 resolves to `2026-09-29T20:07:33.8616943Z`; the native screenshot displays `OFF 20:07:33Z`.
- The independently selected earlier JST500 screenshot remains `INTERRUPTED`, with OUT `20:00:32Z` and unknown OFF/ON/IN. Both screenshots show `Disconnected`.
- `../full-20260930-120544-40822fea/summary.json`: full baseline **1,958 passed, zero failed, two native skips**. The skips explicitly concern native connection lifecycle and native reconnect; this review does not claim those were newly exercised.

The review inspected existing evidence rather than rerunning it while the parent task executes the remaining verification and freeze. The retained green evidence identifies desktop assembly SHA-256 `CE078474AAE79B2C795F3C57A2A80E37550FA8FA2C0A8B98F1FC7202DCCCDE71`; final packaging must retain its own distinct identity and must not be described as the original frozen T2 candidate.

Actionable findings: **none**.

## Final closeout report addendum

Reviewed `docs/flight-tracking/7.24c-logbook-correction-closeout.md` against the completed supplementary results, final-package Logbook evidence, package hashes, and diagnostic-control datasets. **No material report correction or additional code blocker remains.** The minor wording concern that the denied replacement was necessarily caused externally was corrected to an unresolved underlying cause.

The closing-set arithmetic is correct: **1,958 + 40 + 203 + 69 + 21 + 199 + 2 = 2,492 passing checks**, with the baseline's two native skips retained. This excludes failed/development invocations and repeated investigations as explicitly stated. Historical samples reconcile to **91,366 + 6,145 + 3,121 = 100,632** across 11 replays; final JST500 native evidence stays separate.

The initial recovery wrapper's failure remains disclosed even though its 21 assertions passed. Its saved unexpected `previous_unclean_exit` report is present under `recovery/invalid-monitor/CrashReports`. The controls independently reconcile to one extra report in 20 corrected-build cases and two in 30 frozen-T2 cases, with no assertion failures. The T2 first-chance records contain four `0x80070005` access-denied exceptions with stacks identifying `ProgramMonitor.WriteStatus`, `Stop`, and coordinator disposal. The corrected control records contain two denied file moves, one followed by an extra report; those earlier probe records do not include the expanded stack field. This evidence supports carrying the health-marker problem as inherited; it does not identify the underlying cause or establish that the issue is fixed.

The unchanged recovery confirmation records **21/21**, 5,000 replacements and zero unexpected reports. Independently rehashed `ProgramMonitor.cs`, `FlightSessionCoordinator.cs`, and `verify-flight-recovery.ps1` match entry-state hashes, supporting the report's statement that neither production monitor behavior nor wrapper acceptance was relaxed. The report correctly separates this diagnostic-marker limitation from flight/history publication and discloses it among carried limitations.

Final disconnected evidence records **40/40** using desktop SHA-256 `1ACB88B971FAF7FECFBFB27591D75F4AAD5CD04B97AA24A68515A939D32C6C4B`. Independently hashing the frozen portable desktop DLL produced the same value. The installer and portable ZIP hashes also match the stated FINAL L1 identities. T2 and FINAL L1 remain explicitly distinct, and no fresh live-flight or physical pilot-installation claim is made.

The evidence index/archive was still being assembled during this review and must be completed by the parent task before delivery so the report's evidence link resolves. This is known artifact finalization work; the requested Logbook correction has no remaining review finding. No builds, tests, or production edits were performed by this reviewer.
