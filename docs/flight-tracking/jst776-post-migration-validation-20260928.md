# JST776 — post-migration validation flight review

Reviewed 28 September 2026. **Current flight: PASS WITH FINDINGS. Full 7.24B closure: NO-GO. Map V2: REMAIN OPT-IN. Classic remains default.**

The migrated flight completed, recorded its milestones correctly, and persisted one completed local logbook entry with one PIREP. The flight also exposed unacceptable Map V2 drag responsiveness, avoidable map reconstruction on navigation, and existing route-progress/ETA defects. These findings are preserved; replay success does not override them. No production code, installer, settings, flight data or frozen build was changed during this review.

## 1. Flight summary

| Item | Finding |
|---|---|
| Flight | Jetstar JST776, Melbourne YMML → Adelaide YPAD |
| Aircraft | **Airbus A321**; simulator title `FenixA321 IAE WF SC`, registration VH-VWU |
| Assignment | Raw imported type `FENIX A321`, registration G-FBIG; identification accepted with registration advisory |
| Gates | Departure 45; arrival 23 |
| Filed route | `YMML/34 PEDNI1 PEDNI H345 MAVTO INTOG1V YPAD/05` |
| Dispatch | OFP 3, released 28 Sep 13:54Z for simulator flight date **27 Sep 2026** |
| Scheduled OUT / IN | 27 Sep 21:05Z / 22:45Z |
| Dispatch estimated IN | 27 Sep 22:36:12Z in the accepted assignment; 22:36Z on the OFP |
| Actual OUT / OFF | 27 Sep 21:04:45.6519165Z / 21:16:14.6521759Z |
| Actual ON / IN | 27 Sep 22:19:56.2668685Z / 22:27:29.2802352Z |
| Airborne / block duration | **1:03:41.615 / 1:22:43.628** |
| Taxi-out / taxi-in | 11:29.000 / 7:33.013 |
| Schedule difference | OUT 14.348 seconds early; IN 17:30.720 early |
| Current telemetry | **6,145 unique samples**, each with a corresponding tracking state |
| Final condition | Complete, 100%, 0 NM; both engines confirmed Off; PIREP closeout followed by session end |

The current flight was recorded on the computer on 28 September while the simulator flew 27 September. The assignment uses the simulator date consistently; this is not evidence of a new date conversion bug. The recorded and simulator clocks must remain separate.

## 2. Evidence reviewed and preservation

The authorized review specification and all 11 ZIP members were archived before analysis under `work/jst776-review/entry-20260928-154645/`. They comprise the JSON export, JSONL journal, 34-page OFP PDF, OFP text and seven screenshots. The OFP text was inspected and pages 1–3 rendered and visually checked for the schedule, aircraft, route, fuel and units. All seven screenshots were visually inspected.

Original ZIP SHA-256: `1B973BA31394314C89F1C47237FD5687A6F5CF494C80A8CFAC16C4471726DE67`. Archived ZIP matches. All 11 extracted members still match their entry hashes. See [source manifest](evidence/jst776-20260928/source-manifest.json).

**JSON and JSONL are equivalent exports of one flight.** All 12,349 parsed records compare exactly. The JSON envelope reports schema 1, `eventCount=12349`, `incompleteLastLine=false`, `sessionEnded=true`, export UTC `2026-09-28T15:38:31.0887316Z`. They are not two independent sample sets.

The installed payload was checked against frozen **S4-7.24B-LAYOUT-R3-20260927-666f495dce7e**, version 0.22.0: **514/514 expected files match**, none missing or mismatched. Source commit `666f495dce7efb3ccd70b99939501144d91ab606`. The flight journal records version 0.22.0, not a binary hash; exact identity is corroborated by the installed validation chain and this payload check, rather than inferred from the version string alone.

| Frozen artifact | SHA-256 |
|---|---|
| Alpha6OPS.dll | `DFF93EE7EBA76D08E4D51D43E7D79760FD2E29663869359F8D1998FDB4599D6B` |
| Alpha6Ops.Core.dll | `2668EA4D5E4FE402C793C91BC786DDDA580D6AE34C5750682F21751F997496A6` |
| Alpha6OPS.exe | `766046B7277B30E53E93A545A282366DC5F60A720EC0A8C3D8B83355CD6B3D9E` |
| R3 installer | `19FA4133006BFE69A6975F03467E8608630D1E140D9AFDB087FE8CAF55FB4490` |
| R3 portable ZIP | `AB94D2013983F0EE9672DDE22BA6B34498E8D4A6F6D351CE7DA891B24968E36D` |

The entry git status/diff and 215 existing source/test/build-script file hashes were captured; those files remain unchanged by this review. Existing dirty work was preserved. R3 and all earlier installed failures retain their original evidence. No new installer was produced.

Separate read-only current-profile evidence was collected using SQLite's backup API from a `mode=ro`, `query_only` connection, then queried from the consistent audit copy. The live database was not opened for writing. No settings, assignment, recovery or history records were edited. The 26 existing crash-report files all match the earlier R3 native baseline; no new report was found. Absence of a crash report does not negate the physical hitch.

Historical JST928/JST941 are used only through [the historical reference report](historical-reference-20260923-24.md). Their 24,981 samples are excluded from current flight and replay totals in this report.

## 3. Telemetry statistics and field coverage

| Journal kind | Count |
|---|---:|
| `telemetry` | 6,145 |
| `tracking_state` | 6,145 |
| `tracking_event` | 48 |
| `flight_milestone` | 4 |
| Session start, assignment, connection open, identification, monitor armed | 1 each |
| PIREP closeout, session end | 1 each |
| **Total** | **12,349** |

All sequence numbers are contiguous. Simulator sample timestamps strictly increase. Recording timestamps never decrease. Every telemetry record has a later tracking state with the same simulator timestamp. There are no duplicate connections, reconnects, monitor invalidations, recorded errors, paused samples or slewing samples in this flight journal.

| Channel group | Fields / observed result |
|---|---|
| Position and altitude | Latitude/longitude, indicated altitude, pressure altitude and AGL present throughout; no impossible position jump found. Indicated altitude 19.69–36,299.59 ft; pressure altitude peaks around FL360. |
| Heading and motion | Heading 0.485–359.485°; GS approximately 0–427.69 kt; IAS 0–291.92 kt; VS −3,115.79 to +3,144.37 ft/min. Position-derived speed peaks about 440 kt around the short interval after the sole gap. |
| Attitude | Pitch −12.30 to +3.53°, bank −13.80 to +30.03°. Preserve simulator sign conventions. |
| Ground controls | `onGround`, `parkingBrake`, `gearExtendedRatio`; ratios 0–1 and a complete gear cycle. A few onGround/phase disagreements occur only during the intended transition confirmation interval. |
| Flaps | Selected ratio, handle index 0–5, reported positions 5, left/right measured flap angles. Both angle channels are unavailable for the first two samples, then range 0–22°. Selection events match handle changes; measured surfaces follow later. |
| Slats | Left/right measured channels unavailable for two samples, then zero for all 6,143 remaining samples. This cannot prove actual slat retraction during deployed configurations; it matches the historical sensor limitation. |
| Fuel | Total pounds; significant preflight transient described below. OFP fuel is kilograms. |
| Per-engine readings | Two engine entries per sample: index, combustion, fuel flow in lb/h, N1%, N2%, starter, oil pressure in **psf**. Optional fields can be missing during initial acquisition. |
| Engine assessment | Per-engine condition and evidence source; aggregate `engineDataValid`, running count/mask and `enginesRunning`. Starting/running/shutdown/Off events are present. |
| Route | Raw simulator `routeProgress` is null in all 6,145 samples. Derived fraction, remaining NM, reliability, cross-track, ETA and phase are in `tracking_state`. |

No NaN/Infinity or geometric-bound violation was found in captured numeric telemetry. Field statistics, missing-value counts and bounds checks are in [telemetry summary](evidence/jst776-20260928/telemetry-summary.json).

Cadence: median **1.000278 s**, p99 **1.025970 s**. One interval exceeds 2 seconds: **3.025146 s**, simulator 21:33:30.667→21:33:33.692Z; corresponding recording interval 3.100892 s. None exceeds 5 seconds. Contiguous journal sequence numbers prove no omitted journal record, not that every upstream simulator packet was received. The gap's cause cannot be assigned to a mouse gesture without correlated timing.

`engineDataValid=false` in **1,343** samples: 1,290 through preflight/startup and 53 during shutdown. Count/mask are null in those samples. The aggregate is valid only when every engine has a settled Off or Running assessment; false does not mean all per-engine data is missing. Both engines reach confirmed Off before IN, with valid aggregate data for the last 11 samples. Tiny residual fuel flow (~0.22 lb/h per engine) remains after shutdown, so fuel flow alone is not an authoritative engine-running signal.

Fuel falls **17,649→6,614 lb** at 20:45:49Z, stays near 6,613 lb for 11 samples and rebounds to **17,816 lb** at 20:46:00Z. This cannot be physical fuel burn. The boundary currently accepts finite nonnegative fuel, so it records the source/loading transient. The package cannot distinguish aircraft initialization, loading/refuelling or a simulator sensor glitch. Do not turn it into burn statistics or silently rewrite the source. Approximate milestone fuel: OUT 17,726; OFF 17,231; ON 10,956; IN 10,694 lb. No evidence shows this transient corrupted the persisted PIREP actuals.

## 4. Lifecycle, timestamps and session integrity

| Transition | Effective simulator UTC, 27 Sep | First corresponding state | Result |
|---|---|---|---|
| AtGate | 20:45:13.643 | 20:45:13.643 | Joined at gate; departure observed |
| OUT / TaxiOut | 21:04:45.652 | 21:04:49.661 | Once |
| OFF / Airborne | 21:16:14.652 | 21:16:17.657 | Once |
| ON / TaxiIn | 22:19:56.267 | 22:19:59.269 | Once |
| IN / Complete | 22:27:29.280 | 22:27:33.277 | Once |

The state order is AtGate → TaxiOut → Airborne → TaxiIn → Complete, with no reversal or reset. The detector confirms a candidate after at least three seconds and preserves its original effective timestamp (`Core/Operations.cs`, `PhaseDetector.Observe`). Thus state publication several seconds after effective OUT/OFF/ON/IN is expected. Similarly, pushback is recorded before confirmed OUT, but OUT's effective time is one second earlier. This is not a recording-clock rollback.

PIREP closeout and session end occur at **computer UTC 28 Sep 15:38:30.996–.997**, with reason `pirep_submitted`. They intentionally have no simulator timestamp. Export is approximately 0.09 seconds later. There are no subsequent telemetry records in that journal.

The median recording-minus-simulator offset is 17h 10m 50.776499s, with a range of 50.725294–50.869741s in the seconds component. There is no simulator midnight crossing in this flight. Full dated actuals survive into the PIREP. The shell's real UTC clock and the flight's dated simulator UTC differ intentionally; this is documented in `MainWindow.Dashboard.cs:UpdateClock`.

One session, one assignment, one native open (attempt 1), one identity decision and one monitor arm are present. The assignment remains JST776 throughout. Screenshot workspace changes do not produce a new connection or re-arm record. The journal does not directly log workspace actions, callback generations or native handle identities; it cannot independently certify every internal ownership invariant. Earlier coordinator/reconnect regressions and the user's R3 physical passes remain separate evidence.

Automatic IN is correct here: the destination is within the allowed radius, the aircraft is stopped with brake set, and both engines have confirmed Off evidence. `FlightSessionCoordinator.Tracking.cs` and `PhaseDetector` explicitly allow that path. No pilot confirmation event is required in this case; `pilotConfirmedBlockIn=false` in the saved PIREP is consistent.

## 5. Detailed event timeline

The following lists all **48** detailed events in recording order. Times are simulator UTC on **27 September**, rounded down to the displayed second. [CSV with full timestamps and detail text](evidence/jst776-20260928/event-timeline.csv) preserves the exact evidence.

| Journal sequence | Simulator UTC | Event |
|---:|---|---|
| 7 | 20:45:13 | Assignment loaded |
| 8 | 20:45:13 | Aircraft identified |
| 9 | 20:45:13 | Preflight at gate |
| 10 | 20:45:13 | Flight monitoring armed |
| 2357 | 21:04:46 | Pushback / initial movement |
| 2365 | 21:04:45 | Block-out / taxi out |
| 2406 | 21:05:09 | Engine 2 starting |
| 2457 | 21:05:34 | Engine 2 running |
| 2548 | 21:06:19 | Engine 1 starting |
| 2597 | 21:06:43 | Engine 1 running |
| 2708 | 21:07:38 | Flaps 1+F selected |
| 2727 | 21:07:47 | Flaps 2 selected |
| 2892 | 21:09:09 | Taxi started |
| 3703 | 21:15:54 | Takeoff roll |
| 3751 | 21:16:14 | Liftoff |
| 3756 | 21:16:19 | Initial climb |
| 3779 | 21:16:30 | Landing gear retracted |
| 3868 | 21:17:14 | Flaps 1+F selected |
| 3887 | 21:17:23 | Flaps 1 selected |
| 3906 | 21:17:32 | Flaps UP selected |
| 4213 | 21:20:05 | 10,000 feet crossed |
| 6282 | 21:37:21 | Top of climb / cruise established |
| 8151 | 21:52:55 | Top of descent |
| 10214 | 22:10:06 | Off planned route |
| 10281 | 22:10:39 | 10,000 feet crossed |
| 10478 | 22:12:17 | Approach started |
| 10625 | 22:13:30 | Flaps 1 selected |
| 10800 | 22:14:57 | Flaps 1+F selected |
| 10817 | 22:15:05 | Flaps 2 selected |
| 10958 | 22:16:15 | Route rejoined |
| 10989 | 22:16:30 | Final approach |
| 10990 | 22:16:30 | Flaps 3 selected |
| 11015 | 22:16:42 | Landing gear extended |
| 11018 | 22:16:43 | Flaps FULL selected |
| 11412 | 22:19:56 | Touchdown |
| 11471 | 22:20:28 | Landing rollout slowed |
| 11504 | 22:20:44 | Taxi-in |
| 11601 | 22:21:32 | Flaps 3 selected |
| 11608 | 22:21:35 | Flaps 2 selected |
| 11625 | 22:21:43 | Flaps 1+F selected |
| 11630 | 22:21:45 | Flaps 1 selected |
| 11649 | 22:21:54 | Flaps UP selected |
| 12194 | 22:26:26 | Parking brake set |
| 12215 | 22:26:36 | Engine 1 shutdown |
| 12216 | 22:26:36 | Engine 2 shutdown |
| 12323 | 22:27:29 | Engine 1 shut down |
| 12324 | 22:27:29 | Engine 2 shut down |
| 12334 | 22:27:29 | Block-in / flight complete |

Takeoff, climb, cruise, descent, approach, gear/flap cycles, touchdown and shutdown are all represented. Touchdown is reported at 130 kt / −316 ft/min. Per-engine events avoid a premature aggregate “engines running” claim. Flap events describe selection; they do not claim measured slat deployment.

## 6. Tracking, progress, remaining distance and ETA

Position follows the observed aircraft while the planned line remains the filed route. Imported geometry totals **365.372 NM**, versus OFP ground distance 376 NM; the imported point geometry and OFP distance are different quantities. Progress is monotonic. The terminal state is correctly Complete/100%/0 NM, but the route fraction reaches its endpoint too early.

**JST776-PROGRESS — confirmed misleading distance/progress, pre-existing algorithm.** At **22:11:45.270Z**, tracking sequence **10414**, the aircraft is still Airborne at **8,101 ft**, **20.148 NM from YPAD**, but progress becomes **1.0** and remaining becomes **0 NM**. It stays at 100% for 955 states. This is **8:10.997 before touchdown and 15:44.010 before IN**, not early flight completion.

Root cause is established in `Core/RouteProgressTracker.cs:27–64`: segment projection clamps to the endpoint; a candidate within 25 NM is eligible; accumulated along-route distance advances monotonically and cannot retreat during continuous tracking. The changed arrival puts YPAD's endpoint within that broad corridor. Cross-track equals endpoint distance at saturation. `FlightSessionCoordinator.cs:54–55` then derives remaining distance solely from fraction. The endpoint state can remain “reliable” and 100% as the aircraft approaches from a different direction. Frozen replay reproduces the result. These Core files are byte-identical between checkpoint 7.17 commit `6c60a8e` and frozen R3; this is **not an established migration-introduced algorithm change**.

Route reliability is false for **124** states in two spans: 22:07:32–22:07:43 (12) and 22:09:51–22:11:42 (112). During those spans distance and ETA correctly become unavailable. Maximum cross-track is 25.663 NM. The “Off planned route” and “Route rejoined” events use their own sustained-event criteria and need not coincide with every reliability toggle. There is no basis for forcing reliability true merely to keep a populated ETA.

**JST776-ETA — confirmed expired estimate during taxi-in, pre-existing algorithm.** Ninety TaxiIn states display an ETA already in the past, in spans between 22:24:00 and 22:26:29Z; maximum lateness is **59.268 seconds**. `Core/ArrivalEstimator.cs` calculates a five-minute taxi allowance, then rolls the raw estimate forward when that allowance expires. It rounds the result to a minute and publishes only when it differs by at least two minutes. That threshold can retain an expired published value even while the raw estimate is in the future. Actual IN bypasses estimation and remains exact. The recorded airborne estimates range approximately 22:18–22:38Z and settle to actual IN at completion.

Smallest corrective directions, **not implemented here**: distinguish filed-route completion from distance still to fly during vectors/reversed arrivals; validate endpoint confidence against actual position before publishing zero distance; allow an expired published ETA to advance despite normal anti-churn hysteresis. Merely capping the bar at 99% would hide the bad distance. Route and arrival-estimator contracts must be tested separately against amended approaches, loops, go-arounds, taxi delays and all replay baselines. Existing 7.24B instructions prohibit changing flight algorithms during the pilot validation; this review does not silently fold those fixes into a map patch.

## 7. PIREP and durable history findings

**PIREP closeout: PASS. Durable local logbook persistence: PASS.** This conclusion uses additional current storage evidence, not just the ZIP's closeout marker.

The actual `Alpha6OPS-flights.sqlite` contains exactly **one JST776 row**, ID `4bd22e4aee0a4f3dbcd33b9774a0c6ba`, final phase **Complete**, aircraft **Airbus A321**, source `live`. Its source paths reference the supplied journal session. There are **49 child events: all 48 tracking events plus exactly one `pirep_submitted`**. Their parsed details and all significant simulator timestamp digits match the journal. SQLite pads fractional trailing zeros; that representation difference is not data loss. The audit initially compared timestamp strings too strictly; the raw diagnostic is retained in the work archive and comparison now preserves every significant 100 ns digit.

The persisted PIREP has all four exact actuals, original route, status Completed, eventCount 48 and `pilotConfirmedBlockIn=false`. Its submission UTC is 28 Sep 15:38:30.9904582Z. Raw imported `AircraftType=FENIX A321` remains in the PIREP provenance; the logbook and flight display use normalized Airbus A321. There is no evidence here of a UI regression to vendor-as-aircraft naming.

The current installed app process started **15:40:59.7059Z**, after submission; the database was independently read afterward. This proves the record survived an app restart and is not just a pending in-memory event. SQLite `quick_check` returned **ok**. `FlightHistoryWindow.RefreshRows` reads this same history database. No logbook screenshot was supplied, so the result is storage/read-path verification rather than a fresh human UI click-through.

The database `started_utc=14:15:40Z` records history creation at OUT. Earlier preflight events were backfilled with their original simulator times. `ended_utc=15:38:24Z` records Complete before the PIREP event. Neither should be substituted for simulator OUT/IN. Active-assignment/recovery files were absent at audit, consistent with closeout cleanup. This post-completion restart is **not** the still-pending active-flight recovery/restart test. No external airline-server PIREP receipt is claimed.

See [persistence evidence](evidence/jst776-20260928/persistence.json).

## 8. Comparison with historical JST928 / JST941

| Property | Historical reference | Current JST776 |
|---|---|---|
| Unique telemetry | JST928 11,134; JST941 13,847 | 6,145, separately counted |
| JSON / JSONL | Equivalent pair for each flight | Equivalent pair |
| Ordered OUT/OFF/ON/IN | Present once | Present once |
| Closeout / end reason | Present / pirep_submitted | Present / pirep_submitted |
| Durable history independently read | Not established by historical package | **Established in actual current SQLite database after restart** |
| Registration mismatch advisory | Observed | G-FBIG versus VH-VWU, accepted with advisory |
| Engine aggregate unavailable intervals | Present | Present during unsettled start/stop state |
| Confirmed final engine Off | Not observed; pilot confirmation used | Both engines confirmed Off; automatic IN |
| Slat channels | Zero despite selected configurations | Same limitation |
| Raw simulator routeProgress | Null; calculated state separate | Same contract |
| Off-route unreliable states | JST928 5; JST941 0 | 124 under changed arrival |
| Clock separation | Simulator date/time offset from computer | Same architecture; full dates retained |
| Map input/frame timing | Not supplied | Physical hitch report; no gesture trace |

Different routes, aircraft, flight durations and ATC instructions are not interchangeable test inputs. Historical behavior guides comparison without becoming a fixture the current flight must imitate.

## 9. Migration regressions and confirmed current defects

No migration-caused loss of milestones, event history, flight identity or PIREP persistence was established. That does **not** mean the migrated experience is clean.

| Finding | Evidence and cause classification | Tracking/data risk |
|---|---|---|
| Map V2 left-drag hitches ≥0.5 s | Authoritative physical failure; exact current-flight cause unresolved; prior composition scheduling evidence is relevant, not a substitute for gesture correlation | UI can share a dispatcher with ingestion; cannot declare rendering-only |
| Opening/reloading map on Operations return | Screenshot 3 and confirmed intentional destroy/recreate policy | Restarts visual resources/trail, not the flight session |
| 100% / 0 NM far from destination | Journal + frozen replay + unchanged pre-migration route math | Misleading progress/ETA input; milestones and actuals remain intact |
| Expired taxi-in ETA | Journal + two-minute publication threshold in unchanged pre-migration estimator | Estimate presentation; actual IN remains intact |
| Preflight fuel jump | Raw source/loading transient, cause not isolated | Do not use as physical burn; no demonstrated PIREP corruption |

Do not conflate the drag hitch with the earlier whole-Operations scrollbar oscillation. R3 fixed that table-refresh layout defect; the user subsequently passed connected Classic/V2 stability, reconnects and switching. No new screenshot or journal proves that specific oscillation returned during JST776.

## 10. Differences that are not established regressions

- A321 rather than historical A320; imported vendor string versus normalized display type.
- Automatic block-in with valid Off evidence rather than pilot-assisted confirmation.
- Schedule/actual dates on 27 September while computer/export dates are 28 September.
- Effective milestone times preceding their confirmed publication.
- More off-route samples after an ATC arrival amendment; null ETA while unreliable is appropriate.
- Original SimBrief arrival continuing to display under the current accepted-plan contract.
- Slat zeroes, aggregate engine validity during transitions and residual fuel flow repeat known sensor/evidence limitations.
- Shell “Preflight” text beneath the flights grid is a navigation **button**, not a stale flight-phase label. Local Muskogee weather is explicitly labelled approximate IP-city weather, not an airport METAR.

## 11. Screenshot review

All screenshots are current physical visual evidence; approximate shell times below are **computer UTC**. They cannot quantify frame timing or prove persistence.

| Screenshot | Observation / relation to journal |
|---|---|
| 1, ~14:29Z | Active JST776/A321, YMML→YPAD, gates 45/23, AIRBORNE, observed simulator 27 Sep 21:18:43Z. Map shows the initial route and aircraft near departure. No identity/phase conflict. |
| 2, ~14:57Z | Cruise and approximately mid-route progress; recorded top-of-climb event 21:37:21Z. Planned and flown geometry visible. |
| 3, ~15:05Z | Map region explicitly says **Opening offline map…**, while other panels retain the connected flight. This captures reconstruction on return; a still image does not establish its duration. |
| 4, ~15:09Z | Map is visible again during descent, with remaining planned route and observed track. The four-minute screenshot spacing is **not** a four-minute load-time measurement. |
| 5, ~15:19Z | Aircraft/observed track diverges from filed arrival near RIDMA. Consistent with the ATC amendment and growing cross-track. |
| 6, ~15:22Z | “Off planned route” event 22:10:06Z; ETA pending and unavailable on-time/delayed classification are consistent with temporary route unreliability. |
| 7, ~15:28Z | Approach at simulator 22:17:10Z, heading around 231°, gear/flaps landing configuration; original planned route remains visible. Progress looks full although still AIRBORNE, corroborating the premature saturation. Heading is consistent with a reversed arrival but does not identify the exact amended STAR/clearance. |

No supplied screenshot shows post-flight PIREP/logbook or Complete. Those results come from journal and database inspection. The selected flight row has low text contrast; document as future cosmetic/accessibility work, not a reason to redesign Operations now. Screenshot dimensions are captures, not proof of a new DPI/monitor configuration; retain the previously reported 3440×1440 / 100% physical environment without inventing additional monitor coverage.

## 12. Map V2 drag-hitch investigation

**Physical input responsiveness: FAIL for JST776. Exact root cause: OPEN.** The user's approximately half-second-or-longer stalls are authoritative. No exact gesture times, browser frames or native scheduling trace were recorded in the journal, so this review cannot honestly attribute a particular stall to serialization, GC, map layers or recovery I/O.

Relevant path and evidence:

1. `FlightSessionCoordinator.Connection.cs:50–56` posts simulator readings to the WPF dispatcher. `ObserveLive` records telemetry, updates tracking/recovery and publishes the snapshot there. `MainWindow.FlightSession.cs` refreshes Operations synchronously. A dispatcher stall can therefore delay ingestion/publication; the map is not on an entirely separate operational scheduler.
2. `OperationsMapHost` offers snapshots to a worker projection pump and bounded latest-only visual mailbox (200 ms). JSON serialization is off the UI thread; the final bridge post uses the dispatcher. Visual coalescing is not flight-sample coalescing.
3. `map.mjs` compares geometry signatures, updates route/airport/waypoint sources when progress/trail changes, animates aircraft at about 20 Hz, applies layer visibility and refreshes a quantized 512×256 day/night image. These are candidates for browser work, not measured causes of this flight's stalls.
4. Recovery saves, journal flushes, history writes and Operations presentation can run synchronously on the UI path. Their mere presence does not establish a 500 ms blocking call. Earlier profiling did not find measured projection, serialization, GC or completed managed callback time sufficient to explain the longest composition waits.

Prior measured evidence, carefully separated from JST776:

- [7.24A](../checkpoint-7.24a.md) measured single-flight projection/serialization/post p95 around **0.13 / 0.16 / 0.17 ms**, versus dispatcher queue p95 **478.02 ms**.
- [7.24A.2](../checkpoint-7.24a.2.md) recorded a **1,112.20 ms** Input gap with a visible but quiescent map, including a 1,108.17 ms queued Input wait and only 55.41 ms of completed callback envelopes. Hidden/disposed control max was 77.21 ms in that comparison. A separate 1,422.41 ms gap contained 14.54 ms of completed dispatcher callbacks; measured GC pauses did not explain it.
- Frozen 7.24B contention evidence retained **395/395** synthetic inputs despite a deliberately stalled browser. The earlier one-hour soak retained **3,941/3,941**. These prove those test workloads' counts, not latency or losslessness during this physical drag.

The strongest existing evidence makes WPF/WebView2 composition scheduling an important lead, but not a proven native mechanism. It also shows that “too many telemetry-driven route rebuilds” alone is insufficient: long waits existed with the visible map quiescent. Swapping renderers, changing dispatcher priority, slowing telemetry, hiding scrollbars or changing durability would be speculative and would violate this validation's protection boundaries.

For JST776 specifically, all logged samples are processed once, the recording-minus-simulator offset varies by only about 144 ms, and no sustained logged backlog is apparent. There is one 3.025 s sample gap. These observations do not establish the number of upstream packets, capture sub-sample stalls or correlate a drag. **No demonstrated lost lifecycle data; rendering-only impact remains unproven.**

Next targeted diagnostic: use an isolated profile and unchanged authoritative input cadence, with timestamped physical drags under visible live-updating V2, visible quiescent V2 and hidden/disposed control. Correlate input press/move/release, WPF Input queue age and callback duration, native receive→coordinator processing→journal time, snapshot publication, JS apply/render frames, CPU/GC and durable-write durations on one monotonic clock. Use WPF/Win32/COM scheduling stacks if dispatcher waits remain unexplained. The existing scheduling harness provides most counters; it lacks current physical-gesture correlation. Do not rerun the entire exploratory matrix as if it supplied that missing evidence.

Affected components: WPF dispatcher/CompositionControl host, map bridge, browser updates and Operations refresh; coordinator ingestion is a shared-boundary risk, not a requested rewrite. No production hitch correction is justified by the current attribution evidence.

## 13. Map V2 workspace-return/reload investigation

**Cause confirmed: intentionally destructive visibility lifecycle.**

`MainWindow.Shell.cs:PresentWorkspace` collapses Operations while retaining the workspace object. `OperationsWorkspace.xaml.cs:IsVisibleChanged` calls `MapHost.EndVisibleTrail`. `OperationsMapHost.cs:129–135` cancels the visible lifetime, disposes the projection pump and WebMapRenderer/WebView2, and clears V2. On return, `ActivateAsync` creates a new renderer/epoch, installs a fresh control, sets “Opening offline map…”, navigates the offline document and rebuilds MapLibre resources. R2A's correct-first-frame gate then waits for the current snapshot/night image and MapLibre idle before reveal. Removing that gate would reintroduce the already-fixed terminator flash.

Camera/follow, zoom, selection, layer preferences and reduced-motion state are retained in the host. The latest flight snapshot survives and reconstructs route/aircraft sources. The persistent `MapCache` directory can preserve browser disk cache; it does **not** retain a WebView, DOM, decoded geography, GPU scene or JS bridge epoch. The bundled land GeoJSON is 2,313,250 bytes. The pump-local observed trail is explicitly reset on hide, so relevant visual history can disappear even though authoritative telemetry/history remains intact.

[Frozen R3 lifecycle measurements](../map-v2/evidence/7.24b-layout-r3/map-lifecycle.json) show **12 renderer creations, 567.46–882.11 ms readiness, mean 813.21 ms**, with five browser processes while live. This metric stops at the JS handshake, before snapshot/lighting/visible-first-paint readiness. It is not a measured physical navigation-to-visible duration. Existing lifecycle tests require disposal and final browser process exit, so retaining the map would intentionally change that policy and need revised tests.

| Option | Evidence / trade-off | Decision |
|---|---|---|
| Current cached camera/settings + recreate | Measured release of browser resources; repeatedly pays initialization; loses visual trail | Preserve as rollback until a replacement is measured |
| Renderer fully active while hidden | Avoids recreation but retains expensive browser/scene and update work | Reject as an unmeasured blanket fix |
| Retained WebView with suspended visual work and bounded hidden lifetime | Could restore quickly while stopping hidden updates; hidden CPU, browser/GPU resources and focus must be measured | **Preferred next experiment**, not yet a production selection |
| Cached model/trail + fresh WebView | Can preserve visual state but still pays browser/MapLibre startup | Secondary option if retaining a hidden renderer fails resource/input gates |

A bounded dormant renderer is the smallest promising lifecycle experiment. Preserve view state and first-paint correctness; stop visual work while hidden; dispose on expiry, shutdown, failure or explicit Classic selection. Compare navigation-to-correct-visible-frame, hidden CPU/memory/processes, other-workspace input latency and rapid switching against current disposal. Test initial load, repeated return, minimize/restore, renderer failure/recreation, Classic→V2, focus/input containment and trail continuity. Do not adopt it until measured; prior quiescent-visible scheduling gaps show that simply pausing snapshots is not enough.

Operational truth should remain untouched because the coordinator owns the session. Lifecycle correction nevertheless risks stale bridge messages, leaked controls/processes, focus regressions and the R2A first-frame bug. It requires isolated lifecycle/resource tests and a separately frozen physical retest, not a silent change to the validated R3 package.

## 14. SimBrief arrival amendment

**Expected under current architecture; FUTURE ENHANCEMENT.** ATC changed the arrival in the simulator, but the accepted dispatch route remains the original INTOG1V / YPAD05 plan. A simulator ATC instruction does not itself create or fetch a new SimBrief OFP.

Dispatch fetches only on explicit Import/Refresh. The result is a draft until acceptance. The coordinator's accepted plan feeds route projection, tracking and PIREP provenance. `AcceptAssignment` rejects replacement while connected and resets tracking/recovery when allowed; it must not be reused as an in-flight route-sync command.

Future explicit **Refresh / Sync SimBrief Route** should verify same-flight identity, show the proposed change, preserve the original accepted plan, append a revision with source/generated/effective times, and atomically update the active route with a defined progress rebase. It must preserve session identity, telemetry, OUT/OFF/ON/IN, events, statistics, recovery and PIREP state. It also needs a revised SimBrief plan to fetch; cockpit/ATC-only amendments require another explicit route-input source. Not implemented or added to 7.24B.

## 15. Fixes made and files changed

**Production fixes: NONE.** Root cause of navigation reload is clear, but choosing retention without hidden-resource/input measurements would be unsafe. Drag-hitch attribution is incomplete. Route/ETA issues are existing flight-algorithm defects, not evidence authorizing historical-parity changes during the map pilot gate.

Review additions only:

- This report.
- `docs/flight-tracking/evidence/jst776-20260928/`: evidence manifest, telemetry statistics, full event CSV, persistence verification, replay summary, findings, audit assertions and provenance.
- Local `work/jst776-review/`: preserved package/spec/git entry state, read-only profile snapshots, rendered OFP pages, analysis scripts/results, isolated replay output and component-investigation notes.

No build, installer, database schema, coordinator, simulator integration, map renderer configuration/default, UI layout or operational algorithm changed. Prior dirty repository changes were not reverted or committed.

## 16. Automated coverage added

**Production regression tests added: NONE**, because this review does not implement a correction.

A rerunnable **read-only evidence audit** (`work/jst776-review/verify_review.py`) adds 20 consistency assertions: export equivalence, sequence/sample pairing, timestamps, milestone/closeout ordering, actual durable history/PIREP identity and payloads, SQLite health, source preservation and frozen replay coverage. **20 passed / 0 failed.** These are evidence checks, not 20 new application regression tests.

Separate product-quality findings remain FAIL for premature route saturation and expired ETA; the physical map failures remain open. They are explicitly retained in [audit results](evidence/jst776-20260928/audit-checks.json), rather than converted to passes because the files are internally consistent.

Required correction tests: amended/reversed-arrival distance confidence; unexpired taxi ETA despite hysteresis; timestamped gesture/ingestion latency; correct first-paint after dormant map restore; bounded hidden resources; camera/selection/trail continuity; no session reset/reconnect or stale bridge callback. Add them with the relevant correction rather than codifying current failures as desirable behavior.

## 17. Build and full regression suite

No production build was needed or run for this read-only review. The exact frozen R3 build's recorded production verification remains **build PASS, 0 warnings / 0 errors; 2,097 executed / 2,097 passed / 0 failed / 2 skipped**. Source: `work/checkpoint-7.24b-layout-oscillation/final-verification.json` and its raw suite artifacts. It includes Core, Desktop, migration/compatibility, map, input, browser math/mailbox and recovery checks.

Those are **prior frozen-build results, not a fresh full-suite run today**. The skips are automated native connection lifecycle and reconnect, which cannot be replaced with synthetic claims. Subsequent user-observed R3 native passes retain separate evidence in [native results](../map-v2/checkpoint-7.24b-native-r3-results.md); they do not rewrite historical automated skip counts. Original installer, physical input, first-paint and R2A layout failures remain preserved separately.

No wholesale suite or single-instance activation run was performed against the user's open installed app. The specific newly supplied flight was exercised through the isolated diagnostic replay entry point, which does not open SimConnect or write the active profile. This resolves a concrete new validation need without reinstalling or rebuilding the frozen application.

## 18. Fresh replay results

Fresh execution during this review used the untouched frozen R3 executable and verified primary binary hashes. All outputs went to a new audit directory. **Exit code 0; 10/10 replay validations passed** under their existing assertions:

| Replay | Samples | Result |
|---|---:|---|
| JST221 | 11,965 | PASS |
| QFA124 | 11,535 | PASS |
| JST786 | 6,952 | PASS |
| QLK1579 | 9,040 | PASS |
| JST721 | 7,650 | PASS |
| JST724 | 7,459 | PASS |
| JST760 | 10,552 | PASS |
| QLK1274 | 10,519 | PASS |
| ANG2 | 15,694 | PASS |
| **Baseline nine** | **91,366** | **9/9 PASS** |
| **Current JST776** | **6,145** | **PASS under existing replay assertions** |

JST776 replay actual IN exactly matches `2026-09-27T22:27:29.2802352Z`; maximum one-sample progress increment is 0.01368467. Replay reproduces airborne progress 1.0, exposing a coverage gap: existing assertions check no >10-point jump, sufficient progress, approach/final events and block-in, but not premature 100%, expired ETA, physical input or durable PIREP. A passing replay therefore does not clear those defects. [Replay evidence](evidence/jst776-20260928/replay-summary.json).

## 19. Telemetry validation result

**6,145 / 6,145 current supplied samples parsed, paired and processed by replay.** One JSON/JSONL pair, one session, one flight. The 91,366 legacy regression samples and 24,981 historical-reference samples are not counted as additional current flight telemetry.

Core lifecycle and persisted actuals pass. Telemetry review is **PASS WITH FINDINGS** because of the 3.025 s gap, fuel transient and unsupported slat/transition validity limitations. There is no malformed record or demonstrated lifecycle loss. There is also no proof of zero upstream loss or zero ingestion latency during a map stall. Future validation must preserve this distinction.

## 20. Remaining risks and next corrective work

1. **Map drag/input:** capture a correlated physical gesture/scheduling/ingestion trace; isolate the responsible wait/work; implement the smallest supported correction and verify input plus ingestion cadence. No speculative renderer swap or telemetry suppression.
2. **Operations return:** profile bounded dormant retention versus current disposal, including correct first visible frame, hidden resource costs, other-workspace responsiveness and visual state/trail. Adopt only a measured lifecycle solution; freeze separately and physically retest it.
3. **Tracking presentation:** resolve the documented premature zero distance/100% and expired taxi ETA through separately scoped route/estimator corrections and regression coverage. Do not hide them or rewrite successful historical inputs to force agreement.
4. **Native active-flight recovery/restart:** still pending from the original 7.24B checklist. A completed-flight database surviving restart does not exercise reconnect/resume of an active flight. Perform a controlled short recovery test after corrected-build readiness; preserve milestones, identity, event count, history uniqueness and PIREP continuity.
5. **Physical validation limits:** JST776 demonstrates a complete flight and map use across climb/cruise/descent, but does not timestamp continuous map-visible duration, every control action or the drag stalls. Earlier R3 input/focus/Classic/reconnect passes stand; new performance corrections need targeted installed retest and unexpected-report review. Additional monitor/DPI coverage is not invented from screenshots.
6. **Data quality:** preserve the fuel excursion and cadence gap, correlate future recurrence with native receipt timestamps. Do not infer burn from loading transients or slat movement from zero optional channels. No current evidence warrants changing engine detection or fuel behavior in this checkpoint.

The next work should be a focused corrective/diagnostic continuation of 7.24B, not premium UI, globe/network implementation, automatic SimBrief refresh or another full flight simply to collect a green result.

## 21. Final 7.24B gate assessment

| Gate | Result |
|---|---|
| Corrected R3 installation / payload identity | PASS; current 514/514 recheck |
| Prior R3 parked Classic/V2/disconnected stability | PASS, existing physical evidence preserved |
| Prior R3 reconnect 1/2 and workspace switching | PASS, existing physical/native evidence preserved |
| Current flight identity, ordered milestones, closeout | PASS |
| Current PIREP and durable local history after restart | PASS |
| Current map drag responsiveness | **FAIL / cause unresolved** |
| Current map return experience | **FAIL / recreation policy confirmed** |
| Current route-distance and ETA presentation | **FINDINGS / existing defects requiring corrective disposition** |
| Active-flight recovery/restart | **NOT YET VALIDATED** |
| Unexpected crash/error reports | No new report found; does not negate physical defects |

**7.24B: NO-GO for closure.** Resolve and retest the map responsiveness and return defects, correct the route/ETA findings through an explicitly scoped change with replay protection, and finish the separate native active-flight recovery test before requesting a clean close. Each code correction needs its targeted checks, appropriate full regression/replay verification, separately frozen package and installed physical result. Preserve this failed R3 responsiveness observation as its own evidence.

**Map V2 remains opt-in; Classic remains default.** Current evidence does not support candidate-for-default status, and no observed session corruption currently justifies declaring it disabled for all use. No default change, premium UI migration, globe work or Checkpoint 7.25 begins here.
