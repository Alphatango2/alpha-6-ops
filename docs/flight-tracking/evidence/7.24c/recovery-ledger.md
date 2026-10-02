# 7.24C active-flight recovery correction

## Scope and reproduced failures

The existing JSON recovery guard already protected whole-file publication, durable flush, bounded sharing retries and cold-process loading. It did not run an active recovered coordinator through landing, durable event history and PIREP submission.

The new process test first ran against the uncorrected production implementation. Its initial 39 checks returned **19 passed / 20 failed** (`red/summary.json`, `red.log`). It demonstrated these linked failures:

1. Recovery omitted both active and completed SQLite history IDs. Milestones OUT/OFF survived, but subsequent ON/IN detailed events did not append to the original history row, that row never became Complete, and PIREP submission failed.
2. Reopening FlightHistoryDatabase lost the in-memory sequence cache. Appending to a saved ID attempted sequence 1 and failed with SQLite result `0x13` (constraint), so storing the ID alone was insufficient.
3. ConnectSimulatorAsync reset the original session/departure-location verification. An airborne recovered flight with no simulator flight plan lost accepted assignment association. An explicit disconnect also discarded the history ID.
4. The tracking monitor omitted observed per-engine conditions from its saved state. Each unchanged Running engine generated another timestamp-keyed running event after restart.
5. SubmitPirep selected an arbitrary recent Complete flight with the same number when the exact ID was absent. A legacy checkpoint could submit against an unrelated prior run.
6. A Complete checkpoint can be published from AddTrackingEvent while the exact row ID still occupies the active-ID field, before EndFlightHistory transfers it to the completed-ID field. A crash at that boundary, or a failed final checkpoint replacement after SQLite completes, left the recovered Complete flight unable to submit. This additional boundary was reproduced and corrected in the follow-up evidence below.
7. Clearing the loaded IDs before attempting completion reconciliation could lose that exact identity if SQLite failed. A later gate-change save then replaced the checkpoint with null IDs. A real SQLite writer-lock regression now protects identity retention and subsequent retry.

This is synthetic evidence of production coordinator/storage behavior. It is not native SimConnect or installed-MSFS evidence.

## Narrow corrections

- Append optional metadata to the existing recovery record: exact active/completed history IDs; joined-in-progress status; observed session start/departure context; accepted assignment airports; pilot-confirmation and arrival-review state. Existing files without the new fields continue to deserialize.
- Restore a saved history ID only when its actual database row is a live flight with the same flight number and airports. Restore accepted context only when the saved assignment airports and valid observed starting context agree. Current samples still pass aircraft/time/position continuity and route reconciliation; conflicting routes do not inherit acceptance.
- Preserve operational context and the history ID across transport reconnect. Backward clocks, different aircraft and impossible position jumps invalidate the prior observed session.
- When the database sequence cache is absent, read the existing maximum sequence. Advance the cache after a successful append. Keep it aligned with PIREP insertion.
- Submit only against the exact completed history ID. A legacy checkpoint without an unambiguous saved ID remains open and fails submission safely; it cannot attach its actuals to a prior same-number flight.
- Persist a detached dictionary of previously observed per-engine conditions so steady engines do not emit duplicate state-change events on recovery.
- Reconcile a Complete checkpoint's exact verified ID with SQLite on restore. A null/Interrupted row becomes Complete; an already Complete row retains its original end time; an Invalid row is never revived. Promote the ID to completed metadata and atomically save the corrected checkpoint. Repeat restoration does not append events or create history rows.
- Advertise PIREP eligibility only when a completed history identity is available. A completed legacy checkpoint without it explicitly says that its history identity needs review and that recorded flight data is retained.
- Retain a separate pending completion identity while database verification/reconciliation is unavailable. Save that exact ID through the existing recovery active-ID field so a later checkpoint cannot erase it. Pending identity does not enable submission or live event/end writes. Successful reconciliation and assignment/session resets clear it.

No schema migration, renderer change, native connection implementation, installed profile access or production persistence-policy change was made by this recovery work. FlightRecoveryStore's `Flush(true)`, per-path locking, retry schedule, rename publication, cleanup and diagnostic behavior are retained. Existing unrelated dirty changes remain in place.

## Automated evidence

Commands were run sequentially in the coordinator-approved exclusive Desktop build slot using `work/dotnet/dotnet.exe`.

1. RED: `run --project tests/Alpha6Ops.RecoveryChecks -c Release -- --active-restart work/checkpoint-7.24c/recovery/red` — 39 checks, 19 pass, 20 fail. An earlier compile attempt found the route agent's in-progress syntax typo; it was fixed before the actual RED execution. Preserve that distinction; a compiler error was not counted as RED evidence.
2. GREEN: `run --project tests/Alpha6Ops.RecoveryChecks -c Release -- --active-restart work/checkpoint-7.24c/recovery/green` — **48/48 pass**, zero failures. The additional nine assertions cover wrong clock/aircraft/position in both shutdown scenarios, joined-in-progress cold restoration and prior-journal immutability. Build output contained no compiler warnings/errors. See `green/summary.json` and `green.log`.
3. Existing guard: `scripts/verify-flight-recovery.ps1 -OutputDirectory work/checkpoint-7.24c/recovery/durability-guard` — **21/21 pass**, **5,000 sequential durable replacements/round trips**, **1,001 concurrent replacements**, **122,586 overlapping reads**, **zero unexpected reports**. Existing negative-case report allowlist was unchanged. See `durability-guard.log`, `durability-guard/checks.log`, `durability-guard/summary.json`.
4. Changed-file whitespace check reported no whitespace errors. Git emitted its existing Windows LF→CRLF advisory for touched files.

The initial harness started 18 child processes: two full continuation scenarios (normal exit and a killed process), separate route/clock/aircraft/position rejection copies, plus an airborne-join scenario. The final harness adds ten child processes for the completion-boundary scenarios and five for the failed-reconciliation retry scenario, for **33 child processes**. Each full active-flight scenario uses 24 fixed synthetic readings, with OUT at +1 second, OFF at +6 seconds, ON at +7,203 seconds and IN at +7,211 seconds relative to simulator `2026-09-17T02:05:00Z`. The initial interruption occurs after a fully saved Airborne checkpoint. The added scenarios interrupt the actual Complete checkpoint publication described below. Neither is a claim of distributed atomicity between every individual JSON and SQLite instruction.

For each full scenario, a cold coordinator restores the active state; it reconnects without a simulator route; disconnects/reconnects again; completes ON/IN; a later process submits the recovered Complete flight; a final process audits durable history. Assertions establish one original history row, one PIREP, exact OUT/OFF/ON/IN once, unchanged old database events, contiguous appended sequences, unchanged old transport journal, unchanged accepted assignment until closeout, preserved route/fixture sources, and cleanup surviving a final cold restart. Deliberate kill produces exactly one expected `previous_unclean_exit` report in its main profile; normal exit produces none. No other persistence reports are allowed.

Legacy frozen assignment/recovery fixture hashes and every synthetic telemetry source remain unchanged. Source logs/profile data from JST776 and the historical flights were never opened for mutation. Telemetry evidence stays in the synthetic recovery pool and must not be added to the 9-flight historical replay or JST776 sample totals.

## Completion-boundary and eligibility follow-up

The deterministic completion tests observe the existing `Alpha6Ops.Recovery` ActivitySource. Immediately after the real AddTrackingEvent save publishes `Phase=Complete`, `FlightHistoryId=<original ID>`, `CompletedFlightHistoryId=null`, one child stops so the test parent can kill it before EndFlightHistory. A second child opens a Windows read handle that denies overwrite: SQLite subsequently reaches Complete, but the final recovery replacement exhausts the unchanged five-attempt retry policy and retains the prior complete active-ID checkpoint. This uses the production I/O; no production test hook or weakened durability policy was added.

- `completion-red/summary.json`, `completion-red.log`: **64 checks / 57 passed / 7 failed** before the follow-up production fix. Both interruption modes could not submit or close out. The killed case also left its original database row uncompleted. Legacy missing-ID recovery incorrectly advertised eligibility and lacked a review explanation.
- `completion-green/summary.json`, `completion-green.log`: **64/64 passed**, zero failures after correction. The Release build completed without compiler warnings/errors and included the other agents' then-current changes.

Both completion-boundary cases preserve all four milestones and the exact old durable event prefix, reconcile the same history ID, tolerate two successive cold restores without altering end time/events, submit one PIREP and durably clear the active assignment/recovery. Each has exactly one expected report: `previous_unclean_exit` for the killed process, or `flight_recovery_save` with `stage=replace` and five attempts for the deliberately held replacement. No other report is allowed in these cases.

The existing 21-check/5,000-write durability run above predates these follow-ups. Its evidence remains unchanged; the parent task coordinates the final full suite against all final source changes. The 64-check result is superseded by the 69-check result below without discarding the earlier evidence.

## Failed-reconciliation retry and map-test identity follow-up

The retry fixture creates the real Complete/active-ID checkpoint, kills that child, and then holds SQLite `BEGIN IMMEDIATE` in a separate connection. A cold coordinator can read the exact row but its completion UPDATE fails. That child subsequently executes the real ApplyGates command, forcing another recovery checkpoint save while history remains unavailable. After the parent releases the SQLite writer lock, further cold processes restore, reconcile, submit and audit the same original row.

- `retry-red/summary.json`, `retry-red.log`: **69 checks / 66 passed / 3 failed**. The forced save erased the history ID, preventing later completion retry and PIREP closeout.
- `retry-green/summary.json`, `retry-green.log`: **69/69 passed**, zero failures, with no compiler warnings/errors. Pending identity remains ineligible during failure, survives the gate save, and produces one original-row PIREP after the lock is released.
- The test permits exactly one intentional `previous_unclean_exit` report and one `flight_history_recovery` report. It confirms that startup/read failures were not substituted for the intended completion UPDATE failure.

The map contention comparison also required a test correction after optional history IDs became part of recovery JSON. Independent baseline/stressed runs intentionally have different generated row IDs. Its verifier now first asserts that each checkpoint has no active ID and that its completed ID equals that run's sole Complete live history row with matching flight number/airports. Only that successfully verified completed ID is normalized for the cross-run recovery hash; every other recovery field remains compared. The earlier full-map failure is preserved under `work/checkpoint-7.24c/map-restoration/full-map-final`. The lifecycle agent owns its final integrated rerun.

## Explicit cross-store evidence limit

SQLite event appends and recovery JSON replacements remain separate commits. A successful event append followed by a failed checkpoint replacement or crash can leave an older monitor state that re-emits an event under a new sequence after restart. Conversely, a database event-write failure is reported and swallowed while a later JSON save can advance the phase/monitor state; restart does not generally repair that missing durable event.

The specified cold-restart, completion-promotion and reconciliation-retry tests do **not** establish atomicity or exactly-once delivery across arbitrary combinations of event-write and checkpoint failures. No blanket claim of zero duplicated/lost events under every storage-failure ordering is warranted. That broader cross-store limitation remains unresolved and would require a separately scoped design; this correction does not redesign persistence or weaken the existing durability policy.

## Remaining gate

**Native active-flight recovery/restart remains PENDING.** No MSFS launch, native connection, installed application action, real-profile mutation or user flight interruption was performed. The exact separately frozen installed-build procedure is in `native-procedure.md`. Full solution/regression/replay verification is coordinated by the parent task after all agents' final changes; the targeted recovery results above do not substitute for that combined run.

Legacy recovery without a saved history ID deliberately cannot submit a PIREP by flight-number guess. It remains recoverable as phase/event data, but requires an explicitly designed/manual reconciliation workflow outside this correction if durable identity is absent. No historical record is rewritten to conceal that limitation.
