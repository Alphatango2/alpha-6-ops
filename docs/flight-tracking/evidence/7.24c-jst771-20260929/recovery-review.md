# JST771 recovery, terminal fallback, and persistence evidence review

Read-only investigation. No production/profile data were changed and no application was launched. All work is under this recovery-review directory. `preservation-manifest.json` records 30 source files, source paths, captured UTC times, lengths, and SHA256 before copy / copy / after copy; all matched. SQLite analysis used `mode=ro` connections to the preserved copies. Both SQLite integrity checks returned `ok`. No SQLite WAL/SHM sidecars existed in the inspected profile directory. `profile-directory-inventory.json` records the current directory inventory.

## Source boundaries

- **Supplied ZIP:** one final connection, `Alpha6OPS-20260929-140838-d803e26c9d8b4aec8cac7c52986c89b2.json` and `.jsonl`, 6,278 records / 3,121 telemetry samples, plus screenshots and OFP. JSON and JSONL contain identical records with consecutive sequences. This connection begins after the reported application restart. It alone cannot prove the earlier OUT/OFF or disconnect/restart continuity.
- **Supplemental current machine:** preserved TestLogs from 13:13, 13:20, 14:07, and 14:08 UTC, the current `Alpha6OPS-flights.sqlite` and `Alpha6OPS-logs.sqlite`, and current profile JSON. The supplemental 14:08 final JSON is byte-identical to the supplied final JSON (SHA256 match); it is not a separate flight or independent observation. Current state is explicitly distinguished from historical snapshots.
- The unrelated 00:50 test log and old `.bak` database were copied for preservation but excluded from JST771 operational counts.
- References to `sequence` below are also the line numbers in the corresponding JSONL; correlation verified all four journals have consecutive sequences and match their JSON exports.

## Conclusions

| Gate | Result | Basis |
|---|---|---|
| Operational identity and assignment recovery | PASS with supplemental evidence | Same complete assignment object across the 13:20, 14:07, 14:08 connections; one durable flight ID |
| Cruise disconnect/reconnect | PASS with supplemental evidence | Closed/reopened connection, no rearm/invalidation, smooth airborne route continuity, same history ID |
| Full application restart recovery | PASS with supplemental evidence | `application_exit`, new final connection, no rearm, original OUT/OFF in final PIREP, retained event prefix |
| OUT/OFF preservation, exactly-once OUT/OFF/ON/IN | PASS | Exactly one milestone of each phase in operational journals; final PIREP actuals exactly match them |
| Event history preservation | PASS; repeated confirmations disclosed | All 45 operational tracking entries match the 45 database tracking entries by simulator time and complete detail object |
| No duplicate operational flight/history/PIREP | PASS | One JST771 history row, 45 tracking entries, one PIREP entry |
| ON / TaxiIn | PASS | Native touchdown milestone and later taxi-in event |
| Terminal guards | PASS for supplied sequence | No terminal eligibility or Complete before pilot confirmation, including both-running and one-running holds |
| Enhanced Fenix adapter | NEVER ACTIVE in observed evidence | All 6,425 telemetry samples across four connections are Support=0, Provider=unsupported, Qualified=false, ExpectedEngineCount=0 |
| Automatic enhanced shutdown | FAIL | Unsupported evidence never becomes authoritative; no automatic IN after both combustion signals become zero |
| Guarded manual fallback | PASS | ON, YMML destination, valid session, stationary/on-ground/brake, fresh samples, >10 sec stable interval, explicit pilot confirmation |
| IN / Complete / PIREP eligibility and submission | PASS | Native milestone, eligible screenshot, one durable PIREP, closeout and journal termination |
| Durable completed history and cleared current profile | PASS with supplemental evidence | SQLite row/PIREP persisted; active-flight.json and active-flight-state.json absent |
| No resurrection after final post-submission cold restart | NOT PROVEN | No preserved post-submission live heartbeat with processStartUtc or final cold-start UI snapshot; stopped status alone lacks process start time |

## Operational identity

Durable history ID: **4208cd32c171441c891cb4429b5f19d8**. Flight JST771, live source, YPAD→YMML, Airbus A320, app version 0.22.0. History `started_utc=2026-09-29T13:37:32.4182832+00:00`; `ended_utc=2026-09-29T14:59:54.4887436+00:00`; `final_phase=Complete`. Its source_detail points to the 13:20 departure journal, while later recovered-connection events append to this same ID.

Assignment at all three operational connections: JST771; G-FENX; YPAD→YMML; FENIX A320; departure gate 24 / arrival gate 45; imported 2026-09-29T13:19:41Z; OFP cache key 1790687981; scheduled OUT 2026-09-28T21:45Z / IN 23:15Z; route `YPAD/05 BENDO3 BENDO Y12 ARBEY ARBEY8A YMML/34`. Full assignment equality, including route points, is verified.

The initial 13:13 preparatory connection used an earlier import at 13:12:29Z / OFP key 1790687549, then closed at 13:20:16.2181695Z. It has no OUT/OFF and produced no extra history row. The operational flight uses the refreshed 13:19:41 assignment. Do not treat that predeparture import as lost cruise-recovery state.

Observed telemetry title is consistently `FenixA320 IAE WF`; final identification accepts the assignment with a registration advisory (simulator VH-VGF vs assignment G-FENX). User intentionally selected A320 rather than prior A321. No aircraft title change occurs across this flight's recovery boundaries.

## Timeline (computer UTC / simulator UTC kept separate)

| Event | Computer UTC 2026-09-29 | Simulator UTC 2026-09-28 | Source |
|---|---|---|---|
| First observed connection | 13:13:08.7851048 | no sample yet | supplemental 13:13 seq3 |
| First title / unsupported provider | 13:13:09.3039783 | 21:17:14.2522506 | supplemental 13:13 seq4 |
| Operational connection | 13:20:28.5899979 | no sample yet | supplemental 13:20 seq3 |
| Monitor armed at gate; not joined-in-progress | 13:20:29.2914287 | 21:24:34.2415084 | supplemental 13:20 seq6 |
| OUT (TaxiOut) | 13:37:32.4144385 | **21:41:34.3206481** | supplemental 13:20 seq2059 |
| OFF (Airborne) | 13:48:00.4241705 | **21:52:01.3083724** | supplemental 13:20 seq3325 |
| Cruise disconnect | 14:07:09.3623536 | null | supplemental 13:20 seq5630, connection_closed |
| Reconnect | 14:07:12.5306004 | no sample yet | supplemental 14:07 seq3 |
| Both engine classifiers running again | 14:07:17.3476623 | 22:11:22.3020019 | supplemental 14:07 seq12 |
| Application exit | 14:08:29.9544377 | null | supplemental 14:07 seq160, application_exit |
| Post-restart connection | 14:08:38.2912359 | no sample yet | supplied final seq3 |
| Recovered Airborne tracking | 14:08:38.4410761 | 22:12:43.295906 | supplied final seq7 |
| Both engine classifiers running again | 14:08:40.3327092 | 22:12:45.297821 | supplied final seq10 |
| ON (TaxiIn milestone) | 14:51:16.3415215 | **22:55:18.3102722** | supplied final seq5134 |
| Taxi-in event | 14:51:56.3616517 | 22:56:01.3021392 | supplied final seq5217 |
| Stationary, brake set, both combusting | 14:56:06.3904711 | 23:00:11.3273696 | supplied final seq5722 |
| Engine 1 combustion becomes zero; Engine 2 remains one | 14:57:12.3304143 | 23:01:17.310707 | supplied final seq5855 |
| Engine 1 shutdown tracking event | 14:57:15.439669 | 23:01:20.3297424 | supplied final seq5862 |
| Engine 2 combustion becomes zero; operating evidence false | 14:57:41.3603891 | 23:01:46.3106231 | supplied final seq5914 |
| Engine 2 shutdown tracking event | 14:57:44.3622062 | 23:01:49.3184509 | supplied final seq5922 |
| Manual confirmation becomes available | 14:57:51.3642059 | 23:01:56.3197174 | supplied final seq5937 |
| Pilot confirms YMML Block In | 14:59:49.5665872 | 23:03:54.3254623 | supplied final seq6175 |
| First eligible + pilot-confirmed tracking sample / IN candidate | 14:59:50.3725631 | **23:03:55.3247909** | supplied final seq6177 |
| IN / Complete published | 14:59:54.3732466 | milestone retains **23:03:55.3247909** | supplied final seq6185; current sample seq6184 is 23:03:59.3148422 |
| PIREP submitted | 15:00:38.5526266 | PIREP contains four actuals above | supplemental DB event46; payload submittedUtc15:00:38.5503212 |
| Closeout | 15:00:38.5588559 | null | supplied final seq6277 |
| Journal ended, pirep_submitted | 15:00:38.5593315 | null | supplied final seq6278 |

## Recovery integrity and its caveats

Exactly four phase milestones across the three operational journals: TaxiOut=1, Airborne=1, TaxiIn=1, Complete=1. No monitor_invalidated, no joined-in-progress alert, no monitor_armed after the cruise disconnect. The database has exactly one JST771 row and exactly 46 event rows with consecutive sequence 1–46 (45 tracking events plus PIREP). The final PIREP's actualOut/actualOff/actualOn/actualIn exactly reproduce the four native milestone times above; pilotConfirmedBlockIn=true, status=Completed, eventCount=45.

At cruise reconnect, last/first telemetry have a 5.008701s computer gap and 4.994720s simulator gap; displacement is 0.64641353NM and altitude changes +0.95ft. Progress advances 0.3242343311→0.3258999797. At app restart, gaps are 9.039435s computer / 8.990761s simulator, displacement 1.16224706NM, altitude -0.38ft, progress 0.3512358530→0.3542306777. Both boundaries retain Airborne phase and routeReliable=true. These are consistent with the observed ~466kt cruise. The ETA changes 22:50→23:10 on full restart even though route continuity holds; this is a separate estimator/presentation observation, not lost operational flight identity.

**Repeated engine confirmations must be disclosed:** each engine has three `Engine N running` entries in the durable history: original engine start, cruise reconnect (22:11:22.3020019), full restart (22:12:45.297821). There are four additional running-state confirmations, not four new engine starts proven by telemetry. All are new timestamps and match their journals; no prior events were copied a second time. Source inspection explains the presentation behavior: FlightTrackingEventMonitor restores ObservedEngineStates, but assigns Unknown warm-up states to that dictionary; the later Unknown→Running stabilization emits another time-keyed confirmation (`FlightTrackingEvents.cs` lines125–141). This does not satisfy an interpretation requiring every engine-running message to occur once per flight. It does satisfy exactly-once OUT/OFF/ON/IN and retained history; retain the caveat when marking those gates PASS.

The screenshots corroborate this: `S4FT - 1 - Disconnect Reconnect.JPG` shows active JST771, preserved OUT21:41Z, verified assignment, new engine confirmations at22:11:22Z and earlier climb history; `S4FT - 2 - Exit and Restart - Reconnect.JPG` shows active JST771, preserved OUT, new22:12:45 confirmations and retained22:11:22 confirmations. The persisted database, not these screenshots alone, is the basis of the recovery verdict.

## Terminal guards / fallback

The aircraft remained stationary with the parking brake set throughout the staged shutdown. Both engines continued combusting for ~65.98 simulator seconds after parking; Engine 2 continued combusting for ~29.00sec after Engine 1 ceased. The telemetry's limited-provider `OperatingEvidence` remains true during both intervals. No tracking sample is TerminalBlockInEligible or Complete before pilot confirmation.

At both-combustion-off, seq5915 explicitly has HasOn=true, AtDestination=true, SessionValid=true, TerminalEligible=false, CanConfirm=false, reason `Confirming destination parking`. Exactly 10.0090943 simulator seconds later, seq5937 changes CanConfirm=true with reason `Automatic shutdown intent unavailable; confirm Block In`. The adjacent telemetry confirms onGround=true, speed0.0000819kt, parkingBrake=true, paused=false, slewing=false, near (-37.675252105,144.847420765). This is the assigned YMML destination, also explicitly validated by the event.

`BlockInEligibilityPolicy.Common` requires a valid session, prior ON, TaxiIn, destination, on ground, finite speed0≤GS<0.5kt, brake, no pause/slew, and fresh sample. The manual stable interval is10sec with shutdown intent unavailable; `TryConfirm` rechecks these conditions and connection generation/identity. CurrentBlockInContext defines destination within5NM of the assigned airport and validates the active session. The actual seq5937 and seq6175 contexts meet those checks; no off-destination bypass is evidenced.

At seq6175 the explicit pilot confirmation names YMML. Seq6177 first exposes TerminalBlockInEligible=true and PilotConfirmedBlockIn=true; the phase detector commits Complete after its normal stable transition. The post-manual screenshot shows Block-in/Complete, actual IN23:03Z,100% and enabled Submit PIREP. Its engine-data-unavailable text is consistent with the unchanged generic telemetry and does not negate the separately recorded pilot confirmation.

## Provider/requalification evidence relevant to the root-cause agent

All 427 preparatory + 2,800 original-flight +77 reconnected +3,121 recovered telemetry samples report unsupported, Support0, Qualifiedfalse, ExpectedEngineCount0. Thus the enhanced provider was never observed active, including long before disconnect. Distinct fresh ConnectionIds:63ae50622d874cedb7f385447b064577 (preflight), e3e001fecc284d75bcea1172f74bc22a (operational departure), eeb2825c667149b088090e6669686d4e (cruise reconnect),5fe3e14c2cd649fa937fe563a2372e33 (restart). Generic both-running classifier samples:1,660 before disconnect;73 after reconnect;2,915 after restart. Fresh running evidence clearly existed on every operational connection; this is not merely a lack of engines-running observations following recovery. This review does not independently establish whether Fenix-specific LVar values were received; the unsupported exports contain no such signals.

## Final persistence limit

The preserved current database durably proves Complete and one submitted PIREP. Current profile contains neither `active-flight.json` nor `active-flight-state.json`, nor related active-flight sidecars. The preserved program-status record says stopped at15:05:05.1499781Z, application_exit, PID31464, Disconnected, samples0, null aircraft/simulator UTC. Samples0 is consistent with a later disconnected app invocation (ProgramMonitor samples increment without reset), but **is not direct evidence of its start time**. ProgramMonitor's live heartbeat contains processStartUtc; Stop intentionally overwrites that record without processStartUtc. There is no preserved live heartbeat or Sept29 crash report containing it, and no final-cold-start screenshot in the ZIP. Do not claim observed absence of resurrection after a verified post-submission cold start from these files.

Minimum remaining persistence observation, if needed: preserve current DB/counts and cleared files, cold-launch the existing application without accepting an assignment, capture a live program heartbeat including processStartUtc later than15:00:38Z plus empty active-flight UI, close cleanly, verify still one JST771 Complete row / one PIREP and no active-flight recovery files. No additional flight is required for that gate. This review did not execute that procedure.

## Reproducible artifacts

- `correlate_recovery.py` recomputes journal/JSON equality, continuity, assignment equality, event/PIREP/milestone counts and exact DB event matching from preserved copies. It opens SQLite read-only and asserts all Boolean checks.
- `verification-assertions.json` contains the machine-readable results and the explicit cold-start limitation.
- `correlation-results.json` contains exact source sequences/timestamps, connection statistics, transitions, and identity.
- `jst771-history.json` contains the one relevant history row and all46 decoded events.
- `terminal-context.json` contains terminal events and their immediately preceding telemetry/tracking records.
- `evidence-summary.json` is the broader preserved-data extraction; it is working evidence, not a new user-supplied artifact.

No production or test changes were made by this review. No regression suite or native app execution was performed.
