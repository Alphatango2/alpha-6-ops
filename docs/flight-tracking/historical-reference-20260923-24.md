# Historical flight reference — JST928 and JST941

**Historical reference only. Not current Sierra 4 validation evidence.** Reviewed 28 September 2026 at the user's request. These records must not be added to 7.24B native passes, the nine recorded-flight replay results, or the 91,366-sample regression total. No replay was run and no application code, profile or original log was changed.

The user supplied these as pre-migration A6 reference flights. Both journals report `appVersion: "0.22.0"`; neither contains a source commit, installer identity or executable hash. Their exact binary/migration provenance cannot be independently established from the version string. Their historical-reference classification is retained without attributing them to LAYOUT R3 or another frozen validation build.

## Records and completion evidence

Each `.json` is an export of its same-named `.jsonl`, not a separate flight. Parsed event arrays match exactly, sequence numbers are contiguous, export event counts agree, `incompleteLastLine` is false and `sessionEnded` is true in both exports. Together they contain **two flights, 50,074 distinct journal records and 24,981 telemetry samples**.

| Observed item | JST928 | JST941 |
|---|---|---|
| Route | YBBN → YBCS | YBCS → YMML |
| Computer recording date | 23 September 2026 | 24 September 2026 |
| Simulator flight date | 22 September 2026 | 23–24 September 2026 |
| Raw simulator aircraft | FenixA320 IAE SL | FenixA320 IAE SL |
| Human-facing aircraft event | Airbus A320 | Airbus A320 · VH-YXV |
| Assignment / observed registration | G-FENX / VH-YXV | G-FENX / VH-YXV |
| Assigned gates | 30 → T2 | 20 → 41 |
| Planned route points | 21 | 24 |
| Telemetry / tracking-state records | 11,134 / 11,134 | 13,847 / 13,847 |
| All journal records | 22,326 | 27,748 |
| Detailed tracking events | 45 | 42 |
| Coarse flight milestones | 4, each once | 4, each once |
| Airborne duration, rounded | 2h 22m 31s | 3h 10m 19s |
| Block duration, rounded | 2h 39m 04s | 3h 24m 42s |
| Final tracking state | Complete, progress 1, remaining 0 NM | Complete, progress 1, remaining 0 NM |
| Closeout | `pirep_closeout_complete`, then `pirep_submitted` session end | Same |

Both demonstrate a **recorded end-to-end completed flight and application PIREP closeout**. That is useful historical success evidence, but is not proof that every sensor was valid, a PIREP persisted in a separate database, or an external service accepted it.

Source stems under `C:/Users/DanniBaLs/AppData/Local/Alpha6Designs/Alpha6OPS/TestLogs/`:

- `Alpha6OPS-20260923-133450-66b8b1cbbdc941b89cd3762beec4e440`
- `Alpha6OPS-20260924-142104-8e78fc8d558c4b41a32438d4d1ccea32`

All four original file hashes were verified unchanged after analysis. Exact paths, lengths and SHA-256 values are retained in [source-sha256.json](evidence/historical-20260923-24/source-sha256.json). [summary.json](evidence/historical-20260923-24/summary.json) retains full milestone/event timelines, availability counts and calculations; [detail-schemas.json](evidence/historical-20260923-24/detail-schemas.json) inventories observed field paths and types by event kind. No personal SimBrief username is copied into the summary.

## Data layers and structures

The export envelope has `schemaVersion`, `exportedAtUtc`, `incompleteLastLine`, `sessionEnded`, `eventCount`, and `events[]`. Every journal entry has `sequence`, `kind`, `recordedAtUtc`, nullable `simulatorUtc`, and a kind-specific `detail` object.

| Event kind | Structure and purpose |
|---|---|
| `session_started` | `appVersion`, `mode=live_simconnect`, `schemaVersion`, and a description distinguishing the two clocks. One per flight. |
| `flight_assignment` | Flight number, registration, origin/destination, aircraft type, planned departure/arrival UTC, source/import time, gates/source/confidence, filed route and `RoutePoints[]`, fuel/unit, cruise level, UTC offsets, OFP cache key and dispatch ETA. |
| `connection_opened` | Simulator message, `source=MSFS 2024`, `attempt=1`. One per flight; neither includes a connection error or reconnect event. |
| `telemetry` | Raw/normalized aircraft observations and engine evidence; 33 top-level detail fields listed below. |
| `flight_identification` | `Mode`, `Accepted`, `Issues[]`, observed registration, nullable simulator route, position, nearest airport with source/distance, and `PlanStatus`. Separates filed assignment from live identity evidence. |
| `monitor_armed` | Aircraft, numeric `initialPhase=0`, `departureObserved=true`, `joinedInProgress=false`. Both were monitored from the gate. |
| `tracking_event` | `Title`, `Summary`, `Detail`, `Kind` (`system` or `operation`). Human-readable milestones and selected telemetry context. No stable event-code field is serialized here. |
| `tracking_state` | `progress`, nullable `remainingNm`, `routeReliable`, `crossTrackNm`, nullable `estimatedIn`, coarse `phase`. One for each telemetry sample. |
| `flight_milestone` | Typed phase name plus display label. Four coarse operational milestones: TaxiOut, Airborne, TaxiIn, Complete. |
| `block_in_confirmation` | `source=Pilot` and destination. Both completed after an explicit pilot block-in confirmation. |
| `pirep_closeout_complete` | **Only `FlightNumber`** in the detail. This is a closeout marker, not the PIREP document. |
| `session_ended` | `reason=pirep_submitted`; simulator time is null. No further journal records after it. |

Route points contain `Ident`, `Latitude`, `Longitude`, `Kind`. Airport evidence includes airport identifier, name, city, position and provider (`MSFS airport facilities`), plus `DistanceNm`. These are distinct from the assignment's filed route. Both identifications report `No simulator flight plan` while assigned-route tracking still proceeds.

## Telemetry inventory

All 33 fields occur in every sample; availability is expressed through nulls, flags, empty arrays and sentinel values rather than omitted keys.

| Group | Exact fields |
|---|---|
| Aircraft and ground state | `aircraft`, `onGround`, `parkingBrake`, `paused`, `slewing` |
| Motion | `groundSpeedKnots`, `indicatedAirspeedKnots`, `verticalSpeedFeetPerMinute` |
| Position and attitude | `latitude`, `longitude`, `altitudeFeet`, `pressureAltitudeFeet`, `altitudeAboveGroundFeet`, `headingDegrees`, `pitchDegrees`, `bankDegrees` |
| Gear | `gearExtendedRatio` |
| Flaps/slats | `flapsExtendedRatio`, `flapHandleRatio`, `flapHandleIndex`, `flapHandlePositions`, `leftFlapDegrees`, `rightFlapDegrees`, `leftSlatDegrees`, `rightSlatDegrees` |
| Engines | `engineDataValid`, `engineReadings[]`, `engineStates[]`, `runningEngineCount`, `runningEngineMask`, `enginesRunning` |
| Fuel and optional route input | `fuelTotalWeightPounds`, `routeProgress` |

Each `engineReadings[]` entry has `Index`, `Combustion`, `FuelFlowPph`, `N1Percent`, `N2Percent`, `Starter`, and `OilPressurePsf`. Each `engineStates[]` entry has `Index`, numeric `Condition` and textual evidence `Source`. Oil pressure is labelled **psf**, fuel flow **lb/hour**, and total fuel **lb**; the filed trip fuel is **KGS**, so comparisons require unit normalization.

The current Core enum declares `Unknown=0`, `Off=1`, `Starting=2`, `Running=3`, `Stopping=4`; the historical journals only store numbers and sources, not that declaration. Preserve numeric originals and verify schema/version compatibility before interpreting future enum changes. Both historical files contain 0/2/3/4 and **no 1**.

## Lifecycle, clocks and progression

The observed overall sequence is connection → identity/monitoring → gate telemetry → taxi-out → airborne → taxi-in → pilot block-in confirmation → complete → PIREP closeout → journal end. Initial identity and monitor events are interleaved; an exact event count/order for all display events is not a universal flight contract.

Coarse state is **AtGate → TaxiOut → Airborne → TaxiIn → Complete**, with no backwards phase transition in either flight. Climb, cruise, descent, approach, final approach and landing rollout are richer **tracking events**, not additional values in these journals' coarse `tracking_state.phase` field.

The table uses effective **simulator UTC**, rounded to milliseconds. Durations are calculated from these milestone times, not computer recording time or the first later state snapshot.

| Milestone | JST928 | JST941 |
|---|---|---|
| OUT / TaxiOut | 22 Sep 19:58:18.939Z | 23 Sep 20:57:27.770Z |
| OFF / Airborne | 22 Sep 20:08:02.937Z | 23 Sep 21:06:38.765Z |
| ON / TaxiIn | 22 Sep 22:30:33.940Z | 24 Sep 00:16:57.730Z |
| IN / Complete | 22 Sep 22:37:22.941Z | 24 Sep 00:22:09.714Z |

`recordedAtUtc` is when the computer appended an event, `simulatorUtc` is the in-simulator operational clock, and `exportedAtUtc` is export time. Median recording-minus-simulator offsets are about **18h 02m 27.399s** and **17h 47m 43.016s** respectively. Thus file date/recording date is not the flight's simulator date. JST941 explicitly exercises midnight rollover. Its scheduled arrival and ETA must retain the next day's date.

Both telemetry clocks strictly advance with no duplicate/backward sample timestamp or interval over two seconds. Median simulator intervals are **1.000511 s** and **1.000953 s**. This describes captured journal cadence, not proof of smooth UI rendering or original SimConnect callback frequency.

Some milestones are logged with a simulator event time roughly three to four seconds before the state snapshot first reflects the confirmed transition. For example, JST928 OUT is effective at 19:58:18.939Z while its first TaxiOut snapshot is at 19:58:21.956Z. A subsequently recorded event can therefore have an earlier effective timestamp than a preceding event. Preserve both clocks and sequence; do not flag this alone as clock rollback or reorder authoritative events based on display text.

Progress is a **0–1 fraction**, starts at zero, never decreases by more than 1e-9, and finishes at 1. Initial computed distances are about **889.276 NM** and **1,321.416 NM**; both finish at zero. Initial gate/taxi-out ETA is null. At completion, `estimatedIn` becomes the effective IN timestamp and remains stable through closeout.

Detailed event coverage includes pushback, both engines starting/running, flap selections, taxi, takeoff roll/liftoff, gear retraction/extension, initial climb, 10,000-foot crossings, top of climb/cruise, top of descent, approach/final, touchdown, rollout/taxi-in, parking brake, both engine shutdowns and flight completion. Full timestamps and messages are retained in the machine-readable summary.

## Historical limitations that must not become requirements

1. **Identity advisory:** filed `G-FENX` differs from observed `VH-YXV` in both flights. They are accepted as `ASSIGNMENT WITH ADVISORY`. JST928 first records temporary `FREE FLIGHT` while identity is unverified, then becomes accepted on the next sample. Imported `AircraftType="FENIX A320"` and raw simulator title coexist with a normalized `Airbus A320` display event; they are not a reason to restore add-on branding as the aircraft type.
2. **Engine uncertainty:** `engineDataValid=false` in 1,896/11,134 JST928 samples and 1,709/13,847 JST941 samples. Aggregate count/mask are null in those samples. Both show individual engine start/running and shutdown events but no confirmed per-engine Off condition or “shut down” completion event. Nonzero fuel-flow values can remain after shutdown; no single fuel-flow signal is a sufficient historical truth oracle. “Engine data unavailable” and “shutdown in progress” at completion are observations to investigate when relevant, not target behavior to reproduce.
3. **Flap/slat distinction:** both show UP/1/1+F/2/3/FULL selection sequences, with actual left/right flap angles reaching about 22°. However, all available slat-angle readings stay zero. First handle index is -1; the first JST928 surface sample is null. Handle index reaches 5 while `flapHandlePositions` reports 5. Do not assume every sensor is valid or infer confirmed slat movement from a selection label.
4. **Independent calculated route state:** raw `telemetry.detail.routeProgress` is null in every sample. Useful progress is in `tracking_state`. JST928 has five airborne states at 20:17:05–20:17:09 simulator UTC with `routeReliable=false`, null remaining distance and null ETA, then recovers. JST941 is always route-reliable. Those five unavailable states are not automatically a migration regression, and neither their timing nor the historical maximum cross-track distance is a universal threshold.
5. **Pause evidence:** JST928 has 24 paused telemetry samples, three “Simulator paused” events and one “Simulator resumed” event; JST941 has none. These logs are not a clean balanced pause/resume event-count fixture. Do not reproduce this imbalance merely for parity.
6. **Human-readable details:** event title/summary/detail strings embed rounded speeds, altitude, landing-rate/fuel comparisons and ETA text. They do not replace numeric telemetry or structured milestones and should not be treated as exact operational identifiers. First-to-last fuel subtraction is also unsafe because the files include preflight loading/refuelling.
7. **PIREP/storage limits:** the closeout marker contains no PIREP ID, full payload, server acknowledgement, database transaction result, grade or durable-history snapshot. These four files alone cannot establish persistence after restart, uniqueness of database records, external submission success, recovery cleanup or recovery continuity.
8. **UI/native limits:** no map renderer/build hash, workspace lifecycle trace, native generation/disposal/stale-callback trace, WPF dimensions, input timing or first-paint frames are included. They cannot validate Map V2 responsiveness, migration isolation, input/focus, default readiness or the fixed layout defect.

## Comparison with the upcoming post-migration flight

Use these records to ask whether expected flight behavior remains present, not to require byte-identical events or the same route, timings, fuel, ETA, aircraft-specific sensor values or UI wording.

| Comparison | What to check in the new flight |
|---|---|
| Provenance | Establish the actual installed frozen-build identity separately; keep this reference out of current pass/sample totals. |
| Integrity | Parse complete journals; check export agreement when applicable, sequence continuity and no unexplained missing samples. Distinguish deliberate reconnect journals from duplicated flights. |
| Clocks | Keep recording/simulator/export clocks separate; use simulator UTC dates for operational milestones and schedule comparisons, including midnight rollover. |
| Identity and assignment | Confirm correct route/type/subtype and the intended accepted assignment; preserve explicit uncertainty/advisories until live evidence resolves them. |
| State/lifecycle | Account for each expected OUT/OFF/ON/IN once per completed leg and explain effective-time/confirmation differences. Preserve rich tracking events without turning their display strings into business logic. |
| Telemetry | Compare aircraft-supported channels, units, null/sentinel handling and validity. Investigate new loss of evidence against the same aircraft configuration rather than copying old unknown values. |
| Tracking | Explain progress, remaining distance, route reliability, ETA and observed positions together. Check correct final Complete/100%/zero state without declaring every temporary unavailable ETA a fault. |
| Closeout and persistence | Look for the closeout/end markers, then independently inspect current PIREP/history/recovery storage; markers alone cannot close those gates. |
| Presentation | Use current physical observations and current diagnostics for rendering, navigation, focus and responsiveness. Historical telemetry cannot prove these. |

Potential regressions include missing/duplicated milestones, unexplained phase resets, mixed clock/date calculations, loss of supported engine/flap evidence, unqualified unknown-state displays, incomplete closeout or failure to persist the completed flight. Historical quirks are not migration acceptance requirements. Any eventual correction must be justified by current evidence and the intended contract; **this review authorizes no production change**.
