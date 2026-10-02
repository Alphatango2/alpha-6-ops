# JST771 route, descent and screenshot review

Read-only investigation. No production or test files changed. All six supplied JPGs were opened with `view_image` and visually examined. `analyze.cjs` and `analysis.json` in this directory contain the reproducible evidence calculations.

## Evidence scope and clocks

- Supplied ZIP: `Alpha6OPS-20260929-140838-d803e26c9d8b4aec8cac7c52986c89b2.jsonl`, matching JSON, `latest-ofp.json`, six screenshots. This journal has 6,278 records, 3,121 telemetry records and 3,121 tracking states.
- **Supplemental local TestLogs**, not supplied ZIP: `Alpha6OPS-20260929-132028-8b82910e17c643edb0daaadcaf382fe3.jsonl` (departure through disconnect; 2,800 telemetry/states), `Alpha6OPS-20260929-140712-45a372128d6c48869b91cd6e6a66176e.jsonl` (reconnect through application exit; 77 telemetry/states).
- **Supplemental preflight context only**: `Alpha6OPS-20260929-131308-d680402c26844bf09eea58a565043062.jsonl` is entirely AtGate and uses an earlier FL330 / 404.567 NM assignment. The later actual-flight assignment uses FL350 / 388.085 NM. Do not combine the earlier preflight assignment with the flown route's progress denominator.
- Supplemental files read from `C:/Users/DanniBaLs/AppData/Local/Alpha6Designs/Alpha6OPS/TestLogs`.
- Event simulator date is **2026-09-28**; computer recording date is **2026-09-29**. All times below are UTC. Screenshot upper-right clock is computer UTC; the dashboard observation/event times are simulator UTC.

## Six screenshot observations and correlation

| Screenshot | Visual observation | Journal correlation |
|---|---|---|
| `S4FT - 1 - Disconnect Reconnect.JPG` | Connected; JST771 YPAD→YMML; Airbus A320; one active assignment, Airborne; OUT 21:41Z; ETA 22:50Z /25 min early; engine-running entries 22:11:22Z and earlier 10k-up 21:55:53Z retained. The exact dashboard observation time is clipped. Computer clock 14:07Z. | Supplemental reconnect journal engine events sequences 13/14, simulator 22:11:22.3020019; wall 14:07:17.3478174/.3585114. ETA becomes 22:50 at 22:11:32.2956771 after retaining 22:52 immediately after reconnect. |
| `S4FT - 2 - Exit and Restart - Reconnect.JPG` | Connected; same JST771/route/OUT/gates and earlier events; observation 22:12:56Z; ETA 23:10Z /on time. Computer clock 14:08Z. | Supplied telemetry 34/state 35:22:12:56.2874908; wall 14:08:51.3552362/.3554362; progress 35.8555701%,248.9350065 NM. The changed ETA is a separate recovery defect detailed below. |
| `S4FT - 2 - Not recognizing Top of Descent.JPG` | Connected and route has advanced beyond the planned TOD label; dashboard observation 22:34:34Z; activity still ends at engine-running 22:12:45Z, without a descent event; ETA 22:52Z /23 min early. Computer clock 14:30Z. | Telemetry 2630/state 2631:22:34:34.296112; wall 14:30:29.3912034/.391348; altitude 19,994.33 ft, pressure altitude 19,507.35 ft, VS−2,176.08 fpm, GS 406.29 kt; progress 78.6438047%,82.8802223 NM. Actual sustained descent already underway for over 6 min. |
| `S4FT - 3 - 10k ft captured.JPG` | Observation 22:42:28Z; newest activity 10,000 feet crossed 22:41:18Z; no top-of-descent event; ETA 22:58Z /17 min early. Computer clock 14:38Z. | Telemetry 3579/state 3580:22:42:28.3000411; wall 14:38:23.4000653/.4001821; altitude 8,615.08 ft, VS−1,341.71 fpm;88.6438751%,44.0714343 NM. The ten-down event is seq 3439 at 22:41:18.3048553, wall 14:37:13.3603439; altitude 9,985 ft and VS−1,176 fpm. |
| `S4FT - 4 - 99.9 complete no submit after3 to4 seconds..JPG` | LANDING / TAXI IN,0 kt,379 ft,brake set; CONFIRM PARKED available;99.9%,0.2 NM; IN remains estimated 23:03Z; no Submit PIREP. Event list shows brake 23:00:11, engine 1 shutdown 23:01:20, engine 2 shutdown 23:01:49. Computer clock 14:58Z. | Brake seq 5723 at 23:00:11.3273696; E 1 seq 5862 at 23:01:20.3297424; E 2 seq 5922 at 23:01:49.3184509. Confirm available seq 5937 at 23:01:56.3197174 /wall 14:57:51.3642059, unsupported provider. During screenshot minute, progress remains≈99.9418014%, remaining≈0.2258603 NM, TaxiIn. ETA 23:03 until simulator 23:03:00 (wall 14:58:55), constraining screenshot to earlier in that minute. |
| `S4FT - 5 - Manually clicked confirmed parked.JPG` | BLOCK-IN / COMPLETE; actual IN 23:03Z; event 23:03:55Z;100%,0 NM and yellow SUBMIT PIREP visible. Computer clock 15:00Z. | Pilot confirmation seq 6175 at 23:03:54.3254623 /wall 14:59:49.5665872. Complete milestone seq 6185 uses IN 23:03:55.3247909, emitted wall 14:59:54.3732466; event 6186 wall 14:59:54.4732252. First complete state 6187 at sample 23:03:59.3148422 /wall 14:59:54.4882527:100%,0 NM,TerminalBlockInEligible=true,PilotConfirmedBlockIn=true. Screenshot establishes readiness; closeout is separately journaled at wall 15:00:38.5588559. |

The route's planned TOD map point is navigation data, not itself the detector trigger. However, this screenshot is a genuine missed detection: telemetry proves prolonged actual descent, so passing the map point is not the sole evidence.

## Top-of-descent result: FAIL, additional event-detection defect

Pilot clarification added after initial review: the original accepted SimBrief OFP specified FL330; the pilot reissued it for a runway/routing change, did not notice the accompanying FL350 change, and flew FL330. Supplemental assignment records and the recovered original OFP text corroborate FL330/runway 23 changing to FL350/runway 05. This explains the mismatch; it does not remove the detector defect or make recovery its cause. See the durable design-only note `docs/flight-tracking/7.24c-jst771-simbrief-revision.md` for the exact material differences, display-evidence limits and proposed revision preservation model.

The entire flown assignment specifies `CruiseAltitudeFeet=35000`. Maximum observed pressure altitude is 33,037.00390625 ft. All three flight journals contain **zero** samples within 600 ft of filedFL350, and **zero** cruise-established or top-of-descent events. There is no evidence that recovery lost an already-fired cruise event: the event never qualified before the first disconnect.

Source: `src/Alpha6Ops.Desktop/FlightTrackingEvents.cs:91-105` chooses pressure altitude, restricts cruise candidacy to filed altitude±600 ft when a plan exists, and requires `fired.Contains("cruise")` before accumulating descent candidates. `observedLevel` rounds the observed altitude, but cannot enable the blocked cruise candidate. The no-plan 120-sample level-flight fallback is only available when planned altitude is null.

Actual stable high-level flight (altitude≥18,000 ft,|VS|<300 fpm,unpaused,airborne) exists for:

- 130 consecutive samples 22:09:04.3039779→22:11:13.3056182 before disconnect; pressure altitude≈32,976–33,018 ft.
- 77 consecutive samples 22:11:18.3003387→22:12:34.3051452 during reconnect.
- 929 consecutive samples 22:12:43.295906→22:28:11.3091354 after restart.

Actual uninterrupted descent VS≤−500 fpm runs for **1,375 consecutive samples**,22:28:13.3138504→22:51:07.3014144. First descent sample is seq 1868,wall 14:24:08.4079852,VS−528.22 fpm. The existing 30-sample threshold would be satisfied at seq 1926,22:28:42.3010101 /wall 14:24:37.3186575,VS−996.86 fpm, if the cruise prerequisite had been established. At the screenshot, descent is still being suppressed solely by the absent cruise latch.

Independent 10k crossing works because line 106 does not depend on cruise/descent. Approach detection recovers via destination proximity at line 112: seq 3878,22:44:57.3087234 /wall 14:40:52.3870858,5,844 ftAGL. Final approach seq 4602,22:50:58.3105926 /wall 14:46:53.357167,2,453 ftAGL. These events prove monitoring remains active. This is not delayed UI refresh, normal debounce, or loss of all recovered event history.

Source consequence (inference, not a tracking-page screenshot observation): `AirbornePhase` at line 116 would remain `Climb` during this descent until the independent Approach event. Persisted `tracking_state.phase=Airborne` is the broader lifecycle phase and does not contradict that inference.

Small safe correction to consider separately: allow sufficiently sustained observed level flight at an actual cruise level differing from filed altitude to establish honest observed-level evidence, then detect sustained descent from that observed level. Do not announce plannedFL350 as achieved. Retain thresholds/one-shot guards, recovery of observed phase context, and pause/slew behavior. Regression cases should include filedFL350/actualFL330 across reconnect/restart then descent, plus a temporary climb level-off and no false descent. No implementation performed in this review.

## Route progress result: PASS for the 7.24C premature 100% gate

Across **5,998** tracking states in the three actual-flight journals:

- No progress≥1 before Complete.
- No unreliable route states.
- No finite incomplete remaining-distance estimate smaller than independently calculated great-circle distance from telemetry toYMML (tolerance 1e-8 NM).
- Supplied journal:3,076 incomplete states,45 complete states.
- Max physically airborne progress:99.8764005%,remaining 0.4796714 NM,seq 5126,22:55:17.3273162; latest telemetry remainsOnGround=false.
- Max lifecycle-Airborne progress:99.8957228%,remaining 0.4046843 NM,seq 5132,22:55:20.3033523. At this time the physical ground contact is already being debounced before theTaxiIn transition.
- Max incomplete progress occurs during taxi:99.9771533%,remaining 0.0886646 NM,seq 5551,22:58:45.3187332 /wall 14:54:40.4016356. The formatting floors to 99.9%, and positive sub 0.1 NM distance is shown as<0.1 NM.
- At final stand,99.9418014% and≈0.2258603 NM correctly render 99.9% and 0.2 NM. This is geographical offset from airport endpoint; it is not evidence that terminal completion is delayed by progress.
- First 100%/0 NM only at Complete state 6187 after pilot confirmation. IN is 23:03:55.3247909; detection state is 23:03:59.3148422.

Source: `RouteProgressTracker.cs:66-75` retains direct endpoint distance as a lower bound; `FlightTrackingEvents.cs:307` provides the related remaining-distance presentation calculation; `FlightTrackingWorkspace.xaml.cs:94-96` prevents rounding<100% up to 100%; coordinator tracking lines 157-165 force 100% only for Complete with no arrival mismatch. PIREP button visibility uses `pirepReady` independently at workspace line 66. The screenshot's 99.9% is correct and is not the cause of missing automatic shutdown completion.

Route continuity across disconnect:32.4234331%/262.2546205 NM at 22:11:13.3056182→32.5899980%/261.6082070 NM at 22:11:18.3003387. Across restart:35.1235853%/251.7757308 NM at 22:12:34.3051452→35.4230678%/250.6134838 NM at 22:12:43.295906. Both follow elapsed flight movement without reset tozero or jump toendpoint.

## Separate ETA restart discontinuity: confirmed

The first two screenshots expose an actual 20-minute ETA discontinuity:

- Last pre-exit state 159 in supplemental reconnect journal: simulator 22:12:34.3051452 /wall 14:08:29.3739102,ETA 22:50.
- First recovered state 7 in supplied journal: simulator 22:12:43.295906 /wall 14:08:38.4410761,ETA 23:10.
- Only 8.991 simulator seconds elapse, speed remains≈466 kt and remaining distance decreases normally by 1.162247 NM. Route continuity cannot account for a 20-minute later ETA.

Source trace: coordinator owns a new `ArrivalEstimator` (`FlightSessionCoordinator.cs:25`). `FlightRecoveryState` (`FlightRecovery.cs:15-24`) stores phase events and monitor state but no arrival-estimator state; restore does not restore `airborneSince`,speed,published ETA. On the first recovered sample, `ArrivalEstimator.cs:41` sets `airborneSince=sample.At`; lines 46-50 restart the 20-minute schedule blend at zero observed weight. The restored OUT 21:41:34.3206481 plus dispatch duration 23:12:59−21:45:00=87m 59s gives 23:09:33.3206481, rounded 23:10. This exactly reproduces the newETA.

The short disconnect/reconnect retains estimator state and preserves 22:52 immediately across that gap, then legitimately shifts 22:50. Restart recovery is the affected path. The post-restart ETA gradually returns toward observed data (23:08 at 22:13:48;22:52 at 22:29:56), before later slowing on descent changes it to 22:58. No pre-Complete ETA in the supplied journal is at or behind the sample time. Taxi predictions advancing one minute at a time after 23:00 follow the estimator's explicit expired-taxi allowance behavior; actualIN ultimately replaces the prediction.

Potential narrow follow-up: preserve/reconstruct the elapsed airborne blending clock from recovered OFF rather than beginning a new 20-minute blend on application restart. Fresh current groundspeed/route evidence should still drive the observed component. Keep this finding separate from route percentage and terminal shutdown. No implementation performed.

## Limits

This review does not independently certify database durability, adapter selection or entire recovery identity. Parent investigation owns those gates. Earlier journals are supplemental local evidence and must be labeled accordingly in the final validation. The user explicitly states the FenixA320 IAEWF variant choice was intentional; nothing in these screenshots warrants treating it as identity corruption.
