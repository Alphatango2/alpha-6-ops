# Map restoration evidence ledger

## Baseline and architecture choice

The source hashes in `pre-lifecycle-source-hashes.json` were collected before production lifecycle changes. `baseline-red/summary.json` has 6 passes and the one intended failure: all six workspace returns changed the renderer/epoch. Cold-to-visible including initial diagnostic checks was 1,868 ms; diagnostic-inclusive returns were 1,440–1,537 ms (p50 1,476 ms; p95 1,535 ms). Seven renderer creations were recorded.

The baseline's independent retained-document trial used a real WebView2, paused animation callbacks only in that diagnostic document, and called the actual native TrySuspendAsync/Resume APIs. Suspension succeeded and IsSuspended was true. Its post-show resume/snapshot path took 43 ms. The trial kept five browser processes and approximately 439 MiB of browser working sets. This result justified retaining for short navigation, native suspension, and a five-minute dormant expiry rather than indefinite retention. The trial latency is not directly interchangeable with the integrated workspace result because it excludes different host/layout and diagnostic work.

## First production verification (before trail-preservation review)

`retained-green-1/summary.json`: 156 checks, zero failures. Eighteen Operations→Dispatch/Flights/Logbook→Operations cycles retained the same exact renderer reference and bridge epoch. All hidden states reached native suspension; hidden input produced no new visual snapshots. Current aircraft, camera, selection, layers, reduced motion and solar state were verified; obsolete ACKs could not reveal the hidden map. Twelve immediate hide/show pairs, minimize/restore, accelerated five-minute-expiry logic, Classic/V2 switching and final disposal passed. Source starts remained one. No browser process survived final disposal.

Diagnostic-inclusive return times were 336–347 ms (p50 343 ms; p95 347 ms), compared with baseline p50 1,476 ms. The revised collector also records the earlier correct-visible endpoint separately; the original baseline did not have that separate field, so comparisons must retain their endpoint labels. Cold remained approximately 1,857 ms, as expected.

Five live browser processes remained throughout all eighteen returns; browser working sets were 448.6 MiB on the first return and 447.3 MiB on the last. The hidden retained sample was 440.2 MiB. These bounded samples show no accumulation across this test, not proof of indefinite leak freedom. Process working sets may double-count shared pages; GPU residency was not independently measured. Dormant expiry releases the WebView and GPU context.

The original resource sampler combines host plus browser CPU: disposed-hidden baseline 1.67% of machine, production retained-hidden 2.76%, trial retained-hidden 0.95%. Different host presentation and sampling work prevent attributing these totals to the browser. A revised diagnostic records separate original-process browser and host CPU deltas; do not claim hidden CPU reduction from the aggregate numbers.

## Review follow-up pending

Review identified that reusing the old reset marker also discarded the already-observed flown trail. The new pump regression requires prior visible segments to survive, while all observations after the hidden interval start a separate segment. This must fail before the continuity-only seam is implemented. Final map checks also include a real delayed-image test for retained first paint and an in-flight delayed browser ACK navigation test.

## Lifetime and future compatibility

The coordinator and telemetry source remain independent of renderer lifetime. The host retains only presentation state and latest immutable overview. Hidden renderer ownership has one explicit pause/resume path, while real unload, failure, Classic selection and dormant expiry retain explicit disposal. This pattern can support a future persistent Home visualization; globe workload and GPU/memory budgets would still require their own measurements. No globe, new shell, UI animation or tracking-cadence changes are included.

All runs use isolated profiles and synthetic input. Physical installed dragging and native MSFS remain separate gates. This lifecycle result cannot establish the root cause or correction of the separate active-drag hitch.

## Final targeted green verification

`trail-red-2/summary.json` ran the real projection pump and failed exactly two of nine assertions: prior observed segment preservation and a separate post-return segment. `MapProjectionService.BreakContinuity` now clears interpolation/previous-observation state without deleting existing bounded trail segments. A separate pump visibility generation drops obsolete work and triggers this continuity break; the existing full reset remains for identity/reconnect/invalid-observation interruptions. `trail-green/summary.json` passes 9/9 against the fresh Release executable, with binary hashes recorded.

`retained-green-2/summary.json` passes 157/157, with no crash reports. It additionally delays an in-flight browser ACK by one second, navigates away/back during that pending work, then verifies current state without renderer restart or ACK timeout. The final Release correct-visible endpoint is 309.4–331.6 ms (p50 326.5; p95 329.9). Diagnostic-inclusive return times remain 324.7–345.9 ms. Use baseline Debug versus first production Debug for a same-configuration comparison (p50 1,476→343 ms, approximately 77% lower); the final Release numbers are a separately labelled confirmation.

During 5.224 seconds of native suspension, all five browser processes consumed **31.25 ms combined CPU**, equivalent to **0.0374% of this 16-logical-processor machine** (0.60% of one core). Host CPU was 2,890.6 ms, or 3.46% of machine; retaining the map is not a claim that the rest of the WPF application is idle. Browser working set was 436.5 MiB hidden; first/last return values were 444.9/445.2 MiB, and all eighteen returns had exactly five live browser processes. Native suspension plus stopped projection/animation bounds active work; five-minute expiry bounds how long that retained memory persists.

The full map suite, including the new delayed-solar restored-frame regression, remains an integrated gate to be recorded by the primary agent. No production change is planned unless that gate finds a real failure.

## Preserved invalid diagnostic attempt

`trail-red/` is **excluded** from test/performance totals. An old Debug binary recognized the diagnostic startup switch and restoration prefix but lacked the newly added pump-only dispatch, so it entered the existing diagnostic profile-comparison branch. The confirmed PID 3888 ran with an explicit `--map-performance-checks <workspace>/trail-red restoration-pump 0` command. Diagnostic startup set CrashReporter root before creating any window; its coordinator/window storage was `trail-red/session` and its browser profile was `trail-red/browser`. The process was stopped after verifying its exact command line; files were preserved. Code-path isolation was verified, but no OS-wide file-access trace was captured. The inventory and exact completed diagnostic stages/timestamps are in `trail-red-attempt-audit.json` and `trail-red-attempt-files.json`. This was not normal production app startup. Subsequent runs use the exact Release path supplied by the recovery build, and record executable/DLL hashes.

## Late projection publication race (review R4)

Independent review identified a second race: a projection can pass the initial visibility check, then take long enough that hide→resume creates a fresh renderer mailbox before it finishes. Its newer sequence would pass the renderer sequence check, and could reveal stale state. The production enqueue boundary was extracted without changing behavior, then tested with a real previously produced snapshot given a newer sequence and an obsolete captured visibility generation. `publish-race-red/summary.json` failed exactly that one assertion (9/10 pass).

`PublishIfCurrent` now rechecks visible state, observation continuity and visibility generation while holding `producerSync`, and performs the nonblocking renderer enqueue in the same lock. This closes the gap between eligibility and publication without holding the lock across projection work or browser ACK waits. `publish-race-green/summary.json` passes 10/10. The independent reviewer confirmed the timeline is closed. No additional full restoration workload repeat is needed for this enqueue-only change; the final integrated map suite covers the complete renderer boundary.

The first full map suite (`full-map-final/summary.json`) ran 191 checks: 190 passed and one recovery-comparison assertion failed. All cold/retained first paint, deliberate delayed solar loading/stale ACK, real unload/reload, repeated navigation, Classic fallback/failure and map input checks passed. The one failure was `Contention: final complete recovery matches excluding wall-clock save metadata.` The recovery owner is investigating the newly introduced per-run history identity in that semantic comparison. This failing run remains preserved; the final full-map rerun must be recorded separately.

## Integrated map rerun status

`full-map-final-2/summary.json` executed 193 checks: **192 passed / 1 failed**. The recovery identity comparison and new exact per-run identity assertion passed after the recovery agent's correction. All lifecycle, retained/cold first paint, publication-generation, trail preservation, minimization, unload, failure fallback and browser load/contention assertions passed. The unchanged unexpected-crash-report guard found zero reports.

The sole failure is the existing `Contained WPF left press still reaches initialized browser` assertion. It passed in the previous full suite. Its fixture raises a `Mouse.PrimaryDevice` event and reads a DOM flag after 150 ms; it does not record the device's current position, focus or forwarding latency. No root cause is claimed from that fact, and the failure must not be ignored or described as a full-suite pass. The primary agent has the exact source location and will investigate the input fixture within the separate interaction work. Original R1 left-button containment production code remains unchanged.

This run's Release DLL SHA-256 is `296C21E92BDECF6892449655F994525CFEE0506A3244ABC4A07E75023D4946E6`; the executable hash is in its `application.json`. All prior failed, interrupted, targeted green and full-suite evidence remains separate and preserved. The latest production source hashes are in `verified-source-hashes.json`.

## Published candidate: functional checks pass, performance gate remains open

`full-map-published/summary.json` ran the exact published `outputs/Alpha6OPS-Desktop-0.22.0/Alpha6OPS.exe`: **193 checks, 192 passes, one performance failure**. The executable SHA-256 is `766046B7277B30E53E93A545A282366DC5F60A720EC0A8C3D8B83355CD6B3D9E`; DLL SHA-256 is `005591611C3C73822D3B913260C3067EA53F96E60FA78AC53D9A9A5BFCBEBF46`. The process exited with code zero, but the JSON summary is explicitly failed; exit code alone is not a passing gate.

The sole failure is the unchanged contention heartbeat requirement: below 500 ms and within 100 ms of the baseline, with a 150 ms floor. Baseline maximum was **183.8978 ms** and the browser-stalled arm maximum was **350.4527 ms**. Both were below 500 ms, but the stressed maximum exceeded the applicable 283.8978 ms limit by **66.5549 ms** (baseline delta 166.5549 ms). The measurement covers each arm's accelerated complete-flight feed; the diagnostic does not record enough heartbeat timing to attribute that maximum to a particular sample or moment within the browser stall. No threshold was changed and no rerun-to-green was performed. This is an open performance gate, consistent with the separate unresolved active-drag investigation.

Both contention arms recorded all 395 authoritative samples and 27 ordered events, reached Complete, durably submitted a PIREP, and had equal timeline, event, operational, recovery, history and PIREP hashes. Fifty authoritative samples were processed during the stressed arm's actual one-second JavaScript block. The stressed projection queue coalesced 354 of 395 offers into 41 consumed projections; the renderer acknowledged 35 snapshots after six further coalesces, with neither queue pending. This verifies bounded visual coalescing and authoritative correctness for this diagnostic workload; it does not override the failed heartbeat gate.

All retained/cold first-paint, delayed current solar, stale ACK, projection publication, trail preservation, navigation, minimize/restore, real unload, failure fallback and ownership checks passed. `unexpected-report-guard.json` records zero unexpected CrashReports. The owned-WebView exit assertion passed, the launched process completed, and a subsequent command-line-filtered process inspection found no Alpha6OPS/WebView process using this run's directory.

### Input forwarding investigation

The R1 fixture now records actual WPF coordinates, view bounds, visibility, focus, opacity, browser suspension, the received DOM event and elapsed time. It waits for the actual DOM condition for at most 1,500 ms instead of using the previous blind 150 ms sleep. This is a functional delivery check with measured latency, not an unchanged 150 ms performance assertion. The original failure remains preserved without attribution.

The focused `input-probe` passed in 3.646 ms. The published full suite's exact preceding BridgeChecks/Escape path passed in **1.6431 ms**. Both used `Mouse.PrimaryDevice` without moving the global pointer; both reported WPF-relative coordinates (-247,-396), and the actual trusted left-button DOM event arrived at (0,0). Thus out-of-view WPF coordinates alone do not explain the earlier missed event. In the published run the view was visible, unpaused and not natively suspended, with keyboard focus initially outside it and opacity still zero; its prior layout-only wait had not established completion of the current first-frame gate. Delivery still succeeded. With no position/focus/timing trace in the original failed run, neither coordinates nor the old timeout is established as the cause. No production input behavior was changed and this evidence does not certify physical pointer dragging.

Final source hashes, including diagnostic instrumentation, are captured separately in `published-source-hashes.json`; prior source/hash evidence is preserved.
