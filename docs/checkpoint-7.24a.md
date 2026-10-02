# Checkpoint 7.24A — Map V2 automated performance hardening

**Assessment complete — NOT READY FOR 7.24B under the predeclared automated performance gate.** All 2,126 functional/regression assertions passed; five of six sustained-performance gates passed. One of 47 recorded steady visible windows measured Input heartbeat p95 **100.0243 ms**, narrowly exceeding the required below-100-ms budget. The threshold was not relaxed or rounded into a pass. No flight-data loss or recovery failure was found. Map V2 remains opt-in; classic remains the default/fallback. FlightSessionCoordinator remains authoritative.

Base: `020bda9`; local branch `codex/checkpoint-7.24a`. Scope and acceptance criteria: [original specification](map-v2/checkpoint-7.24a-spec.txt). Raw evidence remains in `work/checkpoint-7.24a`; [retained evidence](map-v2/evidence/7.24a/README.md) includes final results and unsuccessful attempts. No installation, commit, push, default activation or 7.24B execution was performed.

## 1. Before-optimization baseline

Measured in the actual Operations workspace, using a diagnostic simulator source and isolated profile. Hardware: Ryzen 7 7800X3D (16 logical processors), RTX 5070 Ti, Windows 10.0.19045, reported 3440×1440 display at 239 Hz. The initial maximized client was approximately 3440×1400. No native simulator was running.

Each accelerated profile processes 160 authoritative inputs. The feed in these comparison arms waits for processing then delays 40 ms; it is explicitly **not** an independent real-time feed. The long soak uses independent production instead.

| Baseline stage | Input/s | Heartbeat p50 / p95 / p99 / max, ms | Recovery-save p95, ms |
|---|---:|---|---:|
| Runtime only | 11.97 | 31.03 / 44.66 / 47.67 / 129.54 | 3.77 |
| Classic Operations | 10.74 | 85.73 / 96.57 / 103.38 / 129.93 | 4.67 |
| Map V2 Operations | 11.78 | 93.72 / 593.64 / 844.53 / 1453.23 | 4.93 |
| Map V2, surrounding cards detached | 13.07 | 75.63 / 297.79 / 444.57 / 451.74 | 3.29 |

Initial Map V2 readiness: 855 ms; recreation: 488 ms. Map stage: five browser processes, host 283 MiB + browser 497 MiB = 780 MiB summed working set; CPU 6.99% of the 16-processor machine. Shared working-set pages can be counted twice.

Map projection / serialization / Dispatcher queue / post / end-to-end ACK p95: **0.13 / 0.16 / 478.02 / 0.17 / 496.02 ms**. Visual snapshots: 66 offered, 48 consumed, 18 coalesced, zero rejected, approximately 3.53 consumed/s. These visual counts are separate from the 160 retained flight inputs. The existing ACK measurement includes Dispatcher queue time; a new post-to-ACK metric separates that portion.

## 2–4. Sustained duration, result and resource trend

Final repeat with original-process-handle tracking, `soak-verified`: **3,600.097 seconds (60 minutes), 43/43 assertions passed**. All **3,941/3,941** independently produced inputs and timeline snapshots were retained at approximately 1.095 Hz. This represents 65m40s of simulator time over one hour of wall time. Production dashboard clock remained active. The run exercised 12 real 1.5-second JavaScript stalls, seven verified recovery replacement-lock retries, 12 navigation-away cycles and six minimize/restore cycles. The action loop logged 24 distinct visible epochs; the maximum recorded recreation counter was 31, including additional restoration epochs. There was one simulator-source start, no hidden live renderer, no unexpected restart/fallback, correct OUT/OFF/ON/IN, PIREP eligibility and one durable completed PIREP after database reopening.

Navigation, hidden intervals, follow/unfollow, selections, zoom/pan/World, resize, recreation and night updates ran throughout. Final owned-browser disposal took **236 ms**, with none remaining. A subsequent process audit also found zero processes associated with the isolated profile. Packaged DLL/JavaScript hashes matched before and after verification.

Final visible summed working set: **684–874 MiB**, median **802 MiB**. Early (5–15 minutes) median **804 MiB** versus late (last 15 minutes) **801 MiB**; early/late p95 **840/852 MiB**. Memory oscillated rather than growing monotonically. Hidden browser-count median was zero; immediate post-hide samples could still show five during asynchronous exit. Recorded cumulative resource-sampling errors: **zero**. This bounded observation does not establish leak freedom; working sets may double-count shared pages and do not measure GPU memory.

Independent input emission lateness p95/max: **1.15/14.14 ms**. Emission-to-observed-snapshot delivery median/p95/max: **26.57/199.24/254.31 ms**, including the presentation handler before the observing callback. This is separate from map ACK and physical input latency.

An earlier attempt stopped after approximately 20 minutes: the diagnostic harness dereferenced a renderer after its window became hidden. Flight processing had continued and the unexpected-report guard was clean, but this was **not a completed soak**. The cause of that unscheduled hide was not established. The harness now explicitly restores/shows the window and waits for a visible ready renderer; a focused hide/restore regression passes. Failed evidence is retained, not replaced or counted as a pass.

A subsequent run, `soak-complete`, ran **3,600.093 seconds**, processed **3,941/3,941** inputs, exercised 12 browser stalls and seven verified replacement retries across 24 observed visible renderer epochs, and produced a durable completed PIREP. Its final process-cleanup diagnostic then failed with Windows AccessDenied while reopening an old process ID. Result: **39 passed, one failed**, despite all six separate duration/input/performance gates passing. Original-handle tracking replaces stale-ID lookup; tracking/query errors remain failures. That failed run is preserved and is not included in accepted green counts.

In that full-duration development run, visible summed working set oscillated from 681 to 864 MiB. The early 5–15-minute median was 782 MiB and the final 15-minute median 804 MiB. The corrected repeat above supplies the final resource/disposal decision.

## 5–7. Dispatcher latency and contributors

One bounded production optimization retains the four immutable dashboard row collections when their values have not changed. A targeted test first failed on container replacement, then passed after the correction; changed values still update. The broad timing result is variable, so this is **not proof that scheduling latency is fixed**.

Intermediate after-change accelerated Map V2 runs measured heartbeat p95 421.96 and 518.48 ms (baseline 593.64). The final visible-asserted run measured **p50/p95/p99/max 79.94/100.01/109.11/111.20 ms**, with queue p95 255.17 ms, end-to-end ACK p95 255.74 ms and recovery-save p95 4.92 ms. Detached-card p95 was 62.02 ms. This is improvement in observed runs, not an isolated causal speedup claim: harness corrections, environment variability and both bounded presentation changes are disclosed.

Final real-time profile arms (160 samples each, processing acknowledgment plus nominal 1-second delay) found:

| Profile arm | Heartbeat p95 / p99, ms | Queue p95, ms | Post-to-ACK p95, ms |
|---|---|---:|---:|
| Composition, sample resources each input | 48.03 / 92.15 | 21.02 | 9.36 |
| Composition, sample resources every fifth input | 48.06 / 76.94 | 20.02 | 9.34 |
| Standard native WebView host, same lighter sampling | 46.19 / 63.13 | 0.09 | 9.60 |
| Composition repeat, same lighter sampling | 47.81 / 74.01 | 21.03 | 9.31 |

Real-time p50 is approximately 31 ms, with maxima 108.65–125.86 ms in these final arms. Their save p95 is 3.01–4.94 ms; resource sampling p95 is 23.28–26.39 ms. Every profile retains its 160 inputs and asserts the expected workspace and renderer remained visible. These UI-paced rates are 0.95–0.97 Hz; only the soak independently proves the ≥1 Hz feed requirement.

Evidence supports a substantial **UI scheduling wait**, with diagnostic workload contributing. Earlier real-time composition repetitions ranged as high as p95 891 ms and p99 1,189 ms; that variability is retained in the evidence rather than erased by the better final run. Native-host ablation reduces queue delay but does not establish a complete causal explanation. LayoutUpdated frequency was high in some composition intervals, but event frequency does not measure layout cost. Resource collection and the presentation handler each take roughly 24–27 ms p95, while projection/serialization/post are sub-millisecond for the current flight. ACK handling/status callbacks were not independently CPU-profiled; post-to-ACK includes IPC, browser work, callback handling and scheduling. The diagnostic standard host is not a production replacement because WPF overlay/airspace requirements remain.

Projection, serialization, post calls and recovery writes are too short in these captured samples to explain the hundreds-of-milliseconds queue waits by themselves. ACK completion after actual posting is generally much shorter. The recovery flush comparison separately measured durable Flush(true) p95 1.64 ms versus 0.21 ms without durable flush; **production durability was not changed**. No flight-input coalescing or algorithm changes were made.

The installed stable WebView2 SDK does not expose a supported public FpsDivider setter. No private-reflection workaround, SDK upgrade or speculative frame-rate knob was applied. A broader scheduling remedy remains subject to measured evidence. Timing channels retain at most 4,096 samples; longer profile heartbeat distributions therefore describe the retained tail. The soak resets timing every 30 seconds. Its 119 flushed timing records cover 59m30s, including **47 complete steady visible windows**; the final half-minute has input/resource evidence but no separately flushed heartbeat summary. Window-percentile distributions are not pooled percentiles.

Final soak steady-window heartbeat p95 median/max: **47.33/100.0243 ms**; p99 median/max: **139.88/171.03 ms**. Maximum reported renderer ACK p95: **85.85 ms**; maximum observed startup: **1,561 ms**. The single failed window ends at **900.09 s** (approximately 14m30s–15m): 704 heartbeat observations, p50/p95/p99/max **33.28/100.0243/153.88/250.56 ms**. In that same window save p95 was **4.88 ms**, presentation-handler p95 **25.39 ms**, resource sampling p95 **23.58 ms**, with 5,674 layout notifications. These observations do not establish which layout/render activity caused the isolated miss; frequency alone is not a cost profile. The earlier full-hour development run stayed below 61.82 ms p95, showing the need to characterize variability rather than select the better run.

## 8. Changes made

- Retain unchanged metric/alert/airport/activity visual containers using complete immutable-record equality.
- Clear pending interpolation when reduced motion is active. RED measured 24 redundant source updates in 600 ms; GREEN measured zero. Latest observed positions and telemetry remain unchanged.
- Add passive timing for recovery saves/retries and presentation handlers, p50/p99 bridge metrics and separate post-to-ACK timing.
- Add isolated production-path diagnostics, independent soak production, verified transient replacement locks, lifecycle/process tracking, pointer/scaling tests, resource/performance evidence and recursive unexpected-report guards.
- Expand stall comparison to hash every input's operational state and final recovery state, in addition to existing timeline/event/history/PIREP comparisons.

## 9. Flight isolation

Baseline and stalled-map arms each retain all 395 inputs. Timeline, ordered events, every input's phase/actual times/ETA/progress/PIREP eligibility, history, submitted PIREP and final recovery hashes match. The browser stalls for one second while 50 authoritative samples continue through the coordinator. Both runs reach all four OUT/OFF/ON/IN milestones and durable completed PIREP state without changing connection generation. Visual projection and bridge snapshots may coalesce; authoritative samples do not.

## 10–12. Loads, presentation budget and constrained profile

Actual Operations renderer, test-only synthetic aircraft, up to 25 planned routes plus 25 trails of 64 points, up to 50 airport markers, capped 64 labels and day/night data. Each stage offers updates nominally at 5 Hz for approximately eight seconds. Earlier baseline loads lacked airport markers; their raw evidence is preserved but is not treated as an identical-workload speed comparison.

| Aircraft | Serialization p95 | Queue p95 | Post p95 | End-to-end ACK p95 | CPU, machine % | Summed MiB |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 0.11 | 60.02 | 0.19 | 61.63 | 2.02 | 754 |
| 25 | 1.01 | 56.91 | 3.16 | 89.55 | 3.98 | 806 |
| 100 | 1.05 | 62.05 | 3.35 | 95.38 | 4.31 | 820 |
| 500 | 1.64 | 61.66 | 5.93 | 97.87 | 4.91 | 826 |
| 1,000 | 2.41 | 62.50 | 9.38 | 97.56 | 5.20 | 852 |
| 2,000 | 4.43 | 63.81 | 15.36 | 112.52 | 7.05 | 881 |

Times are milliseconds. Each stage offered 37 load snapshots; the renderer acknowledged 38 including initial production presentation. One older initialization projection was rejected in each stage, with zero coalesced snapshots in this repetition. These counters must not be presented as lost authoritative input. The earlier baseline coalesced substantially more, demonstrating run-to-run variability.

**Supported scope remains one observed current flight**, its planned/completed/observed route and airports. Retain the existing bounded label/trail/payload policies. Synthetic 2,000-aircraft completion over eight seconds is not a supported airline-world capacity claim. Predeclared current-flight gates: independent ≥1 Hz input without loss, steady visible Input-priority heartbeat p95 <100 ms and p99 <250 ms, warm start <5 s, end-to-end ACK p95 <500 ms, bounded queues/restarts, clean recovery and final process disposal. Report transitions/stalls separately while retaining raw measurements.

Chromium CPU throttle factor 4, 100 aircraft: 8.93 s, latest snapshot consumed; ACK p95 153.15 ms, post-to-ACK p95 108.85 ms, heartbeat p95/p99 64.07/79.88 ms, CPU 10.73%, summed working set 881 MiB. No resource-sampling errors were recorded in these final profiles/loads. This is software-constrained evidence on the same high-end machine, not certification of a lower-end computer.

## 13–14. Lifecycle and recovery

Existing lifecycle/failure suite passes for repeated creation/disposal, hidden workspace, recreation, stale epochs, bounded restart, context loss, renderer process failure and classic fallback. Final resource checking retains original SYNCHRONIZE-only process handles discovered through the owned WebView environment and allows a bounded 10-second exit window. Zero-time waits inspect those original process objects without reopening historical PIDs. Handles are diagnostic-only and closed at run end. Discovery/termination-query errors fail closed. An earlier fixed 3-second check failed with a browser process that subsequently exited; that failed attempt remains documented. [Windows wait semantics](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-waitforsingleobject) support this ownership check; retained handles remain valid after [process termination](https://learn.microsoft.com/en-us/windows/win32/procthread/process-handles-and-identifiers).

Resource metric sampling and termination proof are separate. Sampling exceptions increment a cumulative `samplingErrors` counter; any affected totals are incomplete and must not be interpreted as memory release. Retained process handles add diagnostic bookkeeping but do not keep terminated browser workloads running. The focused live-child, exited-child and repeated-query contracts pass.

Recovery regression: **21/21**, including **5,000/5,000** complete save/load cycles and concurrent readers/writers, transient/permanent failure, crash-before/after-publication and profile compatibility. The soak additionally holds actual Windows read handles until an observed replacement retry releases them, checking the prior document remains valid and final state/PIREP persists. No error whitelist or suppressed recovery failure was added.

## 15–16. Input and scaling

Injected browser pointer tests pass for aircraft, airport and route selection, drag pan and wheel zoom; DOM actions exercise zoom, Follow/Unfollow, World, Focus and Layers. Existing browser/WPF checks cover keyboard controls and Escape focus bypass. Reduced-motion update quiescence is directly checked. These are synthetic inputs, not certification of physical pointer, touch or cross-monitor focus.

The eight final DOM control commands completed in 0.25–1.50 ms (each followed by a separately disclosed 400 ms settling wait). This measures browser command completion, not physical input-to-pixel latency. Final process disposal after this verification took 229 ms with no owned browser processes remaining.

All five controlled Operations LayoutTransform captures (100/125/150/175/200%) were inspected at a 1920×1080 window. Map controls, selection detail, route/airport/aircraft labels, yellow focus outline and classic fallback remain visible. Cards reflow and content scrolls at higher scales; lower sections are intentionally below the viewport. Browser raster text becomes softer under magnification, particularly at 175–200%. The shell itself is not scaled by this test and actual monitor DPI remains 96; real Windows DPI and monitor moves remain manual gates. Existing overlay screenshots check WPF shell search over the composition map. No visual redesign was performed.

## 17–18. Native gates and pilot checklist

**UNAVAILABLE:** NATIVE_CONNECTION_LIFECYCLE and NATIVE_RECONNECT. No real MSFS test was substituted with a synthetic pass. The complete [7.24B manual pilot checklist](map-v2/checkpoint-7.24b-pilot-checklist.md) covers installation/profile, native connection/reconnect, physical map input, monitors/DPI, a complete flight/PIREP, recovery/restart and evidence capture. It is preparation only; 7.24B has not started.

## 19–23. Build and regression totals

Final production solution and normal **v0.22.0 installer/portable packaging pass**, with zero build warnings/errors. Artifacts remain local at `outputs/Alpha6OPS-Setup-0.22.0.exe` and `outputs/Alpha6OPS-Desktop-0.22.0-win-x64.zip`. No installation, release or default-enable change was performed. Post-run hashes confirm the tested package stayed unchanged.

| Accepted suite / gate group | Executed | Passed | Failed | Unavailable |
|---|---:|---:|---:|---:|
| Full baseline, including recorded-flight replays | 1,871 | 1,871 | 0 | 2 |
| Recovery, including 5,000 save/load cycles | 21 | 21 | 0 | 0 |
| Map V2 (121 + 7 mailbox + 7 browser/math) | 135 | 135 | 0 | 0 |
| Final performance/input/scaling/lifecycle verification | 51 | 51 | 0 | 0 |
| Final full-hour soak | 43 | 43 | 0 | 0 |
| Performance-summary validation fixtures | 5 | 5 | 0 | 0 |
| Sustained performance gates | 6 | 5 | 1 | 0 |
| **Total checks and gate assertions** | **2,132** | **2,131** | **1** | **2** |

All original **2,025** checks remain represented and passed. Excluding the six performance gates, **2,126/2,126** assertions passed. Counts are check executions, not unique requirements; preliminary attempts and duplicate focused reruns are excluded from the accepted total but retained in the failure history. No new tests were disabled. The two native checks are recorded as skipped by the baseline runner and explicitly classified **UNAVAILABLE**, not passed.

Recorded flights: **9/9 replays passed; 91,366/91,366 telemetry samples processed**. The independent soak's 3,941 synthetic samples are additional and are not counted as recorded-flight samples. Normal-runtime unexpected-report guards for the final verification and soak are **CLEAN, zero reports**. Recovery negative-case fixtures remain intentional isolated tests; no normal-runtime failure whitelist was added.

## 24. Remaining risks

The unresolved acceptance issue is the isolated **0.0243 ms** exceedance of the predeclared steady-window p95 budget. This is a marginal measurement, not evidence of lost flight data or a newly identified severe freeze; nevertheless the strict gate is not green and no tolerance was retroactively added. UI queue variability remains only partly attributed. Constrained/high-count stages are short. Native connection, installed-profile behavior with MSFS, physical input, mixed monitor DPI, driver diversity and representative lower-end hardware remain outside automated proof. Controlled scaling shows magnified raster softness. Resource measurements are bounded and may double-count shared memory; per-row sampling validity and flushing the final partial timing window remain diagnostic improvements. The first soak's unscheduled window hide has no established external cause; restoration is now covered by a regression.

## 25. Globe readiness

[Globe analysis](map-v2/checkpoint-7.24a-globe-readiness.md) covers all requested layers, geometry, aircraft/airports, camera/follow, night shading, dateline/poles, labels, picking, performance and bridge contracts. Existing immutable geographic snapshots can support a future **presentation experiment**. Flat-world camera/fit assumptions and the Mercator night raster need separate work. No globe, provider, city lights or operational rewrite was implemented.

## 26–28. Decision, supporting evidence and next scope

**NOT READY FOR 7.24B under the predeclared automated gate.** The full functional suite, actual hour of runtime, memory trend, lifecycle/disposal, recovery, fallback and report guards support stability for the tested current-flight scope. They do not turn the one failed performance gate into a pass.

Exact decision evidence: [accepted totals](map-v2/evidence/7.24a/accepted-suite-summary.json), [final performance gates](map-v2/evidence/7.24a/soak-verified/readiness-metrics.json), [43 passing soak checks](map-v2/evidence/7.24a/soak-verified/summary.json), [final disposal](map-v2/evidence/7.24a/soak-verified/final-resources.json), [clean guard](map-v2/evidence/7.24a/soak-verified/unexpected-report-guard.json), and the final compact soak record at 900.09 seconds. Five of six performance gates pass; one of 47 observed steady visible windows misses p95 by 0.0243 ms. This is the reason for holding the next checkpoint, rather than the two deliberately deferred native checks.

Recommended follow-up before 7.24B: characterize that narrow scheduling miss with targeted Dispatcher/layout cost measurements and a documented reproducibility/tolerance policy, then re-evaluate the same acceptance gate. Do not make a speculative renderer rewrite or weaken recovery durability. If a tolerance is justified, document it as an explicit acceptance-policy change rather than silently modifying this result.

Once readiness is established and 7.24B is separately authorized, keep its scope to the [complete installed/native/physical pilot checklist](map-v2/checkpoint-7.24b-pilot-checklist.md): installation/profile readability, real connection/reconnect, physical input, monitor/DPI behavior, one complete flight/PIREP, recovery/restart and evidence capture. Default activation requires a separate decision after that validation. No 7.24B, globe, other workspace or airline-world implementation was started.

## Files and rerun instructions

Production changes: `OperationsWorkspace.xaml.cs`, `MapV2/Web/map.mjs`; passive measurement changes: `FlightRecovery.cs`, `MainWindow.FlightSession.cs`, `MapV2/MapBridgeMetrics.cs`, `MapV2/WebMapRenderer.cs`. Diagnostic changes: `App.xaml.cs`, new `MapV2/MapPerformanceChecks.cs`, expanded `MapV2/MapContentionChecks.cs`, fixture visibility in `MapV2/MapV2Checks.cs`. Scripts: `verify-map-performance.ps1`, `summarize-map-performance.ps1`, `test-map-performance-summary.ps1`. Documentation: this report, the copied specification, implementation ledger, manual checklist, globe analysis and compact evidence.

Run the normal `packaging/build-desktop.ps1`, existing `verify-migration-baseline.ps1` with the nine-journal manifest, `verify-flight-recovery.ps1`, `verify-map-v2.ps1`, then `verify-map-performance.ps1 -Mode verify` and `-Mode soak -Minutes 60` against isolated fresh output directories. Run UI-intensive suites sequentially. Aggregate with `summarize-map-performance.ps1` pointing to the completed soak. Its five test fixtures in `test-map-performance-summary.ps1` reject empty, missing and non-finite timing measurements. The six performance gates are separate from functional completion and the unexpected-report/disposal gates. Inspect all of them and raw failed attempts before deciding readiness. The unrelated pre-existing 7.11 bundle is preserved.
