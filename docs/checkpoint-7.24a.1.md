# Checkpoint 7.24A.1 — responsiveness characterization

**Checkpoint complete. Decision: NOT READY FOR 7.24B.** The frozen final policy rejects a **996.3762 ms** heartbeat gap, despite all 20 primary window p95 values being below 80 ms. All **1,089 final flight inputs** were retained and the full-flight/recovery/lifecycle checks passed. Historical 7.24A also remains **NOT READY**. Map V2 remains opt-in; classic remains the default. No production optimization, installation, default change, globe work or 7.24B work is included.

## 1. Original failed window

The retained interval from 870.0554 to 900.0916 seconds contains 704 heartbeat observations: p50 **33.2759**, p95 **100.0243**, p99 **153.8838**, maximum **250.563 ms**. The 0.0243 ms exceedance is real under the original strict rule and is neither rounded away nor retroactively passed.

During that interval there were 33 independent flight inputs; recovery-save p95/max **4.8752/5.1167 ms**; presentation p95/max **25.3866/25.5837 ms**; and six resource collections with p95/max **23.5846/25.0929 ms**, approximately 137 ms total. Map offers/ACKs advanced by 53/52 without additional mailbox coalescing. Renderer recreation count remained nine at both boundaries. LayoutUpdated counted 5,674 events; this is activity, not CPU cost or evidence of causation.

Bridge queue and end-to-end ACK p95 were epoch-cumulative **63.0542/63.5939 ms**; post-to-ACK p95 was **9.1755 ms**. A one-second maximum was already present in the preceding transition and cannot be attributed to this interval. Minimize/restore, resizing and controls occurred around 840 seconds, approximately 30 seconds before the interval; another recreation followed 900 seconds. The next injected recovery lock was around 913.5 seconds. No injected lock or browser stall falls inside the failed interval. Neighboring reported steady p95 values were 48.0117 and 46.6083 ms.

**Conclusion:** the retained aggregates do not identify a causative production workload. Original individual heartbeat samples were not retained, so a histogram or exact callback correlation cannot be recovered. See [original-window analysis](map-v2/evidence/7.24a.1/original-window-analysis.md).

## 2. Fresh repeatability trials

Six fresh processes in preselected order full/light/light/full/full/light; each had a 15-second warmup and three measured minutes in six 30-second windows. Independent 1.1 Hz authoritative input, production dashboard clock, current-flight presentation, durable recovery and the composition renderer remained active. Full resource sampling was every five seconds; light every 30 seconds. Both initially retained a 50 ms monitoring loop and a 100 ms independent Input queue probe.

| Trial | Mode | Median window p95, ms | Worst window p95, ms | Worst heartbeat, ms | Heartbeat observations |
|---|---|---:|---:|---:|---:|
| 1 | Full | 54.0918 | 73.8862 | 214.0162 | 4,848 |
| 2 | Light | 77.2573 | 89.4830 | 224.7577 | 4,102 |
| 3 | Light | 59.1420 | 83.3794 | 202.1846 | 4,769 |
| 4 | Full | 70.4232 | 73.9427 | 196.7439 | 4,298 |
| 5 | Full | 55.0258 | 65.4651 | 175.0548 | 4,982 |
| 6 | Light | 72.5256 | 91.8188 | 253.3812 | 4,100 |

**66/66 integrity checks; 1,290/1,290 flight inputs; 27,099 heartbeat observations.** No trial was discarded. End-of-run preferences show the production startup fitter expanded the requested 1920×1080 window to **3440×1400**. Those initial trials did not record per-window dimensions. Later trials explicitly record actual size and host type; they must not be described as verified 1920×1080 tests.

## 3. Window-p95 distribution

Full: 18 windows, median **57.6480**, maximum **73.9427 ms**. Light: 18 windows, median **72.5256**, maximum **91.8188 ms**. **0/36 windows (0%) exceeded 100 ms**, so no exceedance magnitude exists in these six trials. Complete per-window p50/p95/p99/max/count, queue, presentation, save, sampling and map-update metrics are retained in the characterization evidence. These are distributions of window statistics, not pooled heartbeat percentiles.

The historical hour had **1/47 steady windows (2.13%)** exceeding 100 ms by 0.0243 ms. Windows on one machine are correlated; neither this nor zero new exceedances establishes a population tail probability.

## 4. Diagnostic overhead and observer effects

Measured resource work fell from **1,364.5 to 245.9 ms** across nine measured minutes per mode, approximately **82%**, while the light arm's median heartbeat p95 was higher. Median presentation-handler p95 was **24.4348/24.0534 ms** and resource-call p95 **13.1081/11.4566 ms**, full/light. Median independent queue p95 was **34.6095/49.4219 ms**. The ordered experiments do not prove light sampling causes slower response, but do not support subtracting diagnostic cost to excuse the original miss.

The existing broader performance sequence exposed a materially different result:

| Existing diagnostic profile | Heartbeat p95 | p99 | Maximum | Dispatcher queue p95 |
|---|---:|---:|---:|---:|
| Composition, real-time/full | 827.3045 | 1,548.0258 | 3,546.2491 | 1,240.6219 |
| Composition, real-time/light | 812.1076 | 1,689.0028 | 7,703.4431 | 1,379.7197 |
| Standard native host, diagnostic control | 45.5981 | 63.8993 | 101.9272 | 0.1349 |
| Composition, light repeat | 816.4496 | 1,765.9527 | 3,750.4905 | 1,626.6911 |

All values are milliseconds. All 160 inputs per profile were retained. Presentation p95 remained approximately 25 ms and resource-call p95 16–19 ms; browser post-to-ACK p95 stayed around 9 ms in composition profiles. Thus resource sampling alone does not account for the long queued waits. The native-host comparison narrows investigation but is not an authorized replacement: its airspace/overlay behavior differs.

These older diagnostic profiles stop the dashboard clock, feed inputs from UI-paced continuations, recreate renderers between profiles and follow accelerated workloads. The six fresh trials keep the clock active and independently feed inputs. Therefore the profiles demonstrate repeatable waits in the composition presentation path under a diagnostic sequence, **not proof that a normal flight experiences identical stalls**. No causal conclusion is drawn from layout counts alone.

Additional probe-disabled and fully passive characterization (three measured minutes/six windows each):

| Extra diagnostic work | Median/worst window p95 | Worst heartbeat | Heartbeat observations | Windows >100 ms |
|---|---:|---:|---:|---:|
| No Input probe; 50 ms monitoring retained | 65.1912 / 71.0692 ms | 367.4245 ms | 4,701 | 0/6 |
| No Input probe or frequent monitoring | 75.3719 / 100.1097 ms | 354.2482 ms | 4,564 | 1/6 (16.67%) |

The fully passive exceedance was **0.1097 ms**. Both retain production work, **215/215 inputs and 11/11 integrity checks**. These single additional trials suggest monitoring cadence matters enough to preserve as a caveat, but do not establish a causal observer effect. The six fully passive windows do not meet the required final sample duration and are not substituted for acceptance. All 48 exploratory windows are retained, including the one exceeding 100 ms; there is no best-run selection.

## 5. Production issue identified?

**Specific production defect established: NO. Repeatable scheduling symptom observed: YES**, in the existing composition presentation path under the broader diagnostic workload. There is not enough evidence to certify the path free of unresolved scheduling problems or to select a safe production fix. The different clock/feed conditions and possible observer wakeups are material limits. A favorable steady-trial result cannot override these findings.

## 6. Production optimization

**None.** Only isolated diagnostic/acceptance tooling changed relative to the starting 7.24A working tree. Flight algorithms, authoritative input handling, recovery durability, simulator integration, renderer technology and operational semantics are preserved.

Independent review found a measurement defect: a Normal-priority window-ending continuation could snapshot/reset before overdue Input heartbeat/probe callbacks ran. The corrected diagnostic records pending callback ages before yielding, drains Input work and stops/drains the final probe producer. A deliberately blocked 600 ms UI turn proves the unfinished stall is visible. Three new evaluator cases went from failing to passing; the real boundary diagnostic passes 7/7. Initial characterization remains retained with its original limitations: terminal gaps were heartbeat 16.6–62.1 ms and queue 16.6–109.6 ms, without a demonstrated concealed >500 ms stall. Output serialization gaps totaled approximately 15–18 ms per trial (largest 8.68 ms); final windows retained 33 recovery operations.

## 7. Original acceptance policy

Every steady visible window must have Input heartbeat p95 **strictly below 100.0000 ms**. Historical 7.24A failed at **100.0243 ms** and remains **NOT READY FOR 7.24B**. Its report and evidence are unchanged.

## 8. New frozen policy

See [policy specification](map-v2/checkpoint-7.24a.1-policy.md), [machine-readable limits](map-v2/checkpoint-7.24a.1-policy.json) and [freeze manifest](map-v2/evidence/7.24a.1/policy-freeze.json).

Two fresh five-minute trials, one full and one light, require at least 20 complete 30-second windows. Each window needs at least 500 heartbeat samples, 200 queue probes and 20 presentation/save observations, complete flight input, the same visible composition renderer and recorded 3440×1400 dimensions. Median window p95 must be **<80 ms per mode**; pooled nearest-rank 95th percentile of window p95 **<100 ms**; at most **5% isolated windows >100 ms**, none consecutively, none **>120 ms**. Every window p99 **<250 ms**, heartbeat and pending-boundary maximum **<500 ms**, queue p95 **<100 ms** and maximum **<500 ms**. Bridge ACK p95 **<500 ms**, startup **<5 s**. Missing, nonfinite, incomplete or inconsistent raw evidence fails closed.

A separate six-minute full-flight/lifecycle run requires complete input, recovery retry, minimize/restore, browser stall, OUT/OFF/ON/IN, durable PIREP, clean disposal/report guard and at least four supplemental steady windows satisfying tail safeguards. All affected regressions and absence of an unresolved repeatable scheduling problem remain separate readiness requirements.

Final tooling/source freeze: **2026-09-25T14:16:19.4729297Z**, revision 3. Policy JSON SHA-256: **A01563BF7783F357E68700CB834C526D694DC4FFD3B6FE4BACE5922D9DF9CB65**. Initial freezes are retained as superseded pre-final records. Boundary and viewport validity corrections and passive diagnostic additions occurred before any final trial, with no numerical latency/duration/sample budget changes. No threshold was adjusted against final results.

## 9. Rationale and limits

The original worst-window threshold depends strongly on observation count and treats a microsecond crossing like a sustained problem. The new rule preserves the 100 ms target for at least 95% of observed windows, adds a tighter central budget, caps isolated excursions, rejects adjacent excursions, and adds explicit p99, maximum-stall, pending-callback and sample-coverage safeguards. The 20 ms isolated allowance is one nominal heartbeat period; it was not selected by rounding 100.0243.

These are engineering progression criteria, not a universal Windows SLA or input-to-pixel guarantee. [Microsoft documents DispatcherTimer queue dependence](https://learn.microsoft.com/en-us/dotnet/api/system.windows.threading.dispatchertimer); that does not invalidate high-resolution timestamps or prescribe these budgets. No diagnostic-time subtraction is applied. A passing policy component is necessary, not sufficient, for readiness.

## 10. Final readiness run

Two frozen primary trials completed: **694/694 inputs**, **22/22 integrity checks**, **20/20 windows**. Full median/worst window p95 **54.4385/77.0161 ms**; light **59.0877/79.2282 ms**. Pooled window-p95 median **56.2288 ms**, nearest-rank p95 **78.6269 ms**; **0/20 windows exceeded 100 ms**.

**The primary numerical assessment FAILED.** `final-light window 9 tail/stall budget failed`: maximum heartbeat **996.3762 ms**, against frozen **<500 ms**. Its p50/p95/p99 were **33.0599/55.8087/87.6779 ms**; the 31.0457-second window remained within the allowed duration. This demonstrates why a percentile-only pass is insufficient.

The retained raw trace locates the gap between approximately 315.4455 and 316.4419 seconds, during required final Input drain. The last flight input was consumed at 314.5720 seconds and the last independent probe completed at 315.4456 seconds. The source/probe producers had finished, while the normal application clock and map remained active. Presentation max was 25.4597 ms, recovery max 7.8704 ms, resource call 13.311 ms and independent queue max 60.0766 ms in that window. These observations do not establish the cause or prove delayed pilot input. The gap is nevertheless included exactly as the frozen policy requires; it was not excluded, rounded, rerun away or reclassified after the result. See [raw-trace analysis](map-v2/evidence/7.24a.1/final-stall-analysis.json).

Six-minute lifecycle: **16/16 checks passed; 395/395 inputs; 360.0463 seconds**. Navigation-away disposal, real JavaScript stall, minimize/restore, three locked-save observations with recovery retry, OUT/OFF/ON/IN, PIREP eligibility/submission and durable PIREP after database reopening all passed. One simulator source start; zero hidden live-renderer observations; no restart/fallback; final recovery contains the final input. Its four complete steady visible windows had p95 **75.8514, 61.8311, 62.4552 and 62.5342 ms** and p99 at most **124.1131 ms**; all supplemental safeguards passed.

Final primary observations: **16,225 heartbeats and 5,965 queue probes**. Bounded probe skips totaled 8 full/14 light, within each window's fixed limit; authoritative input was never coalesced. All **1,089/1,089 final inputs** were retained across the two trials and lifecycle run. Browser disposal completed in 327.4863/325.2396/302.9150 ms, respectively, leaving zero owned browser processes. Strict final report guards were clean.

The [automatic final assessment](map-v2/evidence/7.24a.1/final-assessment.json) verified the freeze and returned **ready:false**, with exactly one failure: `final-light window 9 tail/stall budget failed`; lifecycle failures were empty. The retained unchanged one-hour flight/recovery/lifecycle soak plus fresh characterization, ten final steady minutes and six final lifecycle minutes cover this bounded investigation without repeating the hour. This does not re-certify other hardware or replace native MSFS validation.

## 11. Build and regressions

Release solution build: **passed, zero warnings/errors**. Diagnostic-only scope uses affected Map/performance/recovery checks plus Core and source-hash audit; the complete Sierra suite and recorded replays are not unnecessarily repeated.

| Check | Result |
|---|---|
| Core | 122/122 |
| First Map V2 suite | 120/121; one timing failure retained |
| Repeated Map V2 suite | 121/121 |
| Third focused contention invocation | 73/73 |
| Mailbox + browser mathematics, each Map invocation | 7/7 + 7/7 |
| Existing performance functional checks | 51/51; large timing observations above still count |
| Recovery | 21/21, including 5,000-cycle durability coverage |
| Existing performance-summary contracts | 5/5 |
| New policy contracts, accepted set | 27/27 |
| Real pending-boundary diagnostic | 7/7 |
| Six characterization processes | 66/66 |
| Two observer-ablation processes | 22/22 |
| Final steady trial integrity | 22/22; separate numerical responsiveness assessment FAILED |
| Final six-minute lifecycle | 16/16 |

**702 recorded regression/integrity checks executed: 701 passed, 1 failed, 0 skipped within these groups.** This total includes all three contention attempts and all characterization trials, plus one accepted policy-contract set; it excludes repeated evaluator development RED/GREEN iterations. **The final aggregate numerical policy assessment additionally failed** and is not disguised within the integrity count. Native/installed MSFS validation was not executed; the complete unchanged Sierra suite/replays were retained rather than rerun under the diagnostic-only scope.

The first accelerated contention timing test failed at **537.7024 ms** against an unchanged <500 ms gate (control 325.2277 ms). The full repeat passed at **350.2546 ms** (control 330.1620 ms). The third focused invocation passed at **245.6561 ms** (control 305.0301 ms). All operational/timeline/event/recovery/history/PIREP hashes matched in all three attempts and all 395 inputs per arm were retained; 50 inputs were processed during each one-second browser stall. This is an intermittent timing failure, not a discarded attempt or proof of input loss. No further repeat-until-pass attempts were made.

The historical **9/9 flight replays and 91,366/91,366 replay telemetry samples** remain retained baseline evidence, **not newly executed results**. Expected failing RED fixtures used to validate the evaluator are development evidence, not hidden production-suite failures. Native MSFS/installed validation is unavailable to this automated checkpoint and remains 7.24B scope.

## 12. Unexpected-report guards

**All 14 strict report guards were clean with zero reports**, including both final steady trials, final lifecycle, broader performance run and focused contention. Existing Map negative tests intentionally produced 16 renderer-failure JSON reports per full suite (eight scenarios, initial failure plus one restart); these were matched to those cases, with zero unexpected reports. Recovery produced exactly 12 expected fault-injection JSON reports across its five allowlisted locations/sources, with zero unexpected reports or count mismatches. These allowances do not apply to final trials. See the explicit guard inventory in [verification summary](map-v2/evidence/7.24a.1/verification-summary.json).

## 13. Remaining risks

- Repeated long composition-host scheduling waits under one diagnostic workload remain unexplained; steady trials and a lighter sampler cannot erase them.
- The final policy also caught a 996.3762 ms gap at producer/probe drain. Its causal relationship to production idle scheduling, diagnostic continuation behavior or user input remains unproven.
- Frequent diagnostic wakeups may alter scheduling; passive evidence must be considered alongside active probe results.
- One intermittent accelerated contention maximum failed. Passing repetitions do not establish flake freedom.
- One Windows machine/monitor, correlated windows and short trials cannot establish a fleet-wide tail-latency guarantee. No controlled OS-load isolation or user input-to-pixel measurement was performed.
- Actual MSFS/SimConnect, installed package, other GPUs/DPI/resolutions and native pilot input are outside this checkpoint.
- The six-minute lifecycle collector retains four complete steady visible windows; its final partial interval is not claimed as a complete responsiveness window. Operational input/recovery/disposal are checked through completion.

## 14. Readiness decision

**NOT READY FOR 7.24B.** The final frozen stall safeguard failed. Repeated composition-host scheduling waits under the broader diagnostic sequence and the intermittent contention failure also remain unresolved. Next engineering work should isolate the effects of clock/feed/wakeup cadence and final drain on composition-host queue scheduling, establish a representative reproduction and only then choose a small justified production optimization or a separately characterized measurement correction. No such follow-up is implemented here. Do not begin 7.24B or enable Map V2 by default.

## 15. Exact evidence and changed files

Raw local run directories are under `work/checkpoint-7.24a.1/`. The [evidence index](map-v2/evidence/7.24a.1/README.md), [artifact SHA-256 manifest](map-v2/evidence/7.24a.1/artifact-manifest.json), [verification counts](map-v2/evidence/7.24a.1/verification-summary.json) and [source audit](map-v2/evidence/7.24a.1/source-audit.json) identify accepted, failed and exploratory evidence. The audit compares 252 files captured before checkpoint work; only the two existing diagnostic files below changed. Historical 7.24A report/evidence and operational source are unchanged. All seven final frozen files still match their hashes.

Changed existing diagnostic files: `MapPerformanceChecks.cs`, `verify-map-performance.ps1`. New diagnostic files: `MapResponsivenessChecks.cs`, `MapResponsivenessPolicy.psm1`, `summarize-map-responsiveness.ps1`, `test-map-responsiveness-policy.ps1`. New specification/policy/report/evidence and execution-ledger documents are confined to this checkpoint. Prior uncommitted 7.24A work and the unrelated 7.11 bundle are preserved.
