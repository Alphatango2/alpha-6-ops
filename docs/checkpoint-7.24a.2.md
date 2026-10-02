# Checkpoint 7.24A.2 — Composition scheduling investigation

**Decision: NOT READY FOR 7.24B.** Long scheduling gaps were reproduced and narrowed to delayed Dispatcher work around the visible composition host. The exact native scheduling mechanism is still unresolved. No production change or relaxation of the frozen acceptance policy is justified. Map V2 remains opt-in; classic remains the default/fallback. Neither 7.24B nor globe work has started.

## 1. Root-cause investigation summary

The investigation distinguishes **execution cost** from **ready work waiting to execute**. A visible but quiescent composition map still stalled. Removing the normal presentation handler reduced CPU/allocation while worsening latency. During representative long gaps, short Render-priority callbacks continued while already-runnable Input work waited. Traced callbacks occupied only a small portion of the elapsed gap, and sampled UI-thread state was predominantly Wait/UserRequest.

These results narrow the contributors to the visible composition integration and Dispatcher/native wakeup conditions. They do not establish a particular application callback, allocation burst, recovery save, or final-drain instruction as the cause. This is specification **case C**, not a speculative optimization or a claim that the old failure was harmless.

Historical 7.24A and 7.24A.1 remain **NOT READY**. Their 100.0243 ms and 996.3762 ms failures remain failures. Historical reports, raw results and recorded hashes are unchanged. There is one **binary-retention exception**: the mutable 7.24A.1 build-output DLL was rebuilt and had not been separately archived; see section 21. Full historical artifact preservation therefore cannot be claimed.

## 2. Representative reproduction

Fresh isolated processes host the real Sierra WPF shell/Operations workspace, FlightSessionCoordinator, durable recovery, production presenter, WebView2CompositionControl, MapLibre, map projection bridge and ACK path. The authoritative source is synthetic cruise telemetry at 1 Hz, independent of presentation except in explicitly labeled UI-paced controls. Each short arm uses five seconds of warmup, 30 seconds of measurement and six seconds of tail observation at **3440×1400 DIP**.

The 21 short matrix arms include three retained pilot traces and 18 revision-two arms. Their **189/189 integrity checks passed; 855/855 authoritative inputs were retained**. Short instrumented windows are characterization, not substitutes for the frozen readiness trials. An additional run traces the original broader performance sequence, including accelerated history, renderer recreation and four longer realtime profiles.

## 3. Did the 996.3762 ms behavior reproduce?

**The class of long post-producer scheduling gap reproduced; the exact historical mechanism was not proved.** The dedicated drain arm's measured window had heartbeat p95/max **76.92/216.68 ms**, followed by tail gaps of **680.5554, 690.8336 and 1422.4075 ms** after source/probe shutdown. The application and clock stayed active. Continuous-flow arms also produced long gaps, so producer shutdown is not a necessary condition.

The historical event cannot be retroactively assigned a cause using new traces. Its original failure remains accepted evidence.

## 4. High-resolution trace findings

Bounded in-memory QPC traces retain operation identity, priority, post/promotion/begin/end/abort, heartbeat/probe timing, presentation/recovery activities, layout notifications, resize, map status/ACK delivery, resource calls and drain chronology. No raw telemetry payloads are traced. Pending, aborted and begun-but-unclosed operations remain visible, with boundary ages. Pre-trace gaps and unclosed execution are explicitly marked as incomplete attribution.

In the quiescent arm, a **1112.2019 ms** gap contained **55.4056 ms** of completed Dispatcher callback envelopes; an Input marker waited **1108.1736 ms**. In the drain tail, **1422.4075 ms** contained **14.5365 ms** of completed callback envelopes, while Input hit-test work waited **1420.9249 ms**. These are observed queued waits, not an inference from heartbeat elapsed time alone.

Time outside those envelopes is **unattributed**, not proven idle time. Native WPF/COM message execution and kernel wait stacks are not captured. Projection/serialization remain existing aggregate bridge metrics; exact separate spans for those workers are unavailable. Dashboard timer envelopes include UpdateClock/Operations refresh; RuntimeChanged has its existing activity span.

## 5. Dispatcher priority findings

| Work | Observed/source priority |
|---|---|
| Authoritative source/presentation dispatch | Normal (9) |
| Render callbacks | Usually Render (7), with WPF internal promotions |
| Heartbeat, independent probe, drain, WPF Input markers | Input (5) |
| Map bridge send and dashboard clock | Background (4) |
| Timer before its due-time promotion | Inactive (0) |

Analysis separates Inactive timer residence from runnable queue delay. Input work waited while higher-priority render callbacks continued, but those callbacks did not consume the full gap as CPU work. There is no evidence that lower-priority application work synchronously monopolized the Dispatcher. Native-input gating, render scheduling and composition message/wait behavior remain unresolved contributors; changing priorities would currently risk masking the symptom.

## 6. Composition versus standard host

The matched revision-two fresh baseline/native pair had heartbeat p95 **79.64/61.93 ms** and maximum **442.33/90.81 ms**. However, cumulative bridge queue p95 was **75.28/834.94 ms**. The pilot clock-off/UI-paced pair, both with the rendering observer enabled, also showed substantial native bridge delay. Thus the earlier native-host advantage is workload-dependent and was not consistently reproduced under these new conditions.

Native WebView remains a diagnostic control. Its airspace/overlay behavior still prevents treating it as an authorized replacement. Bridge distributions include warmup and must not be confused with independent Input-probe distributions in the final policy.

The separately retained **original broader sequence** reproduced the earlier host split under its clock-off, UI-paced, accelerated-history conditions:

| Profile | Heartbeat p95 / max, ms | Bridge queue p95, ms |
|---|---:|---:|
| Composition realtime/full | 1065.12 / 2768.93 | 1417.49 |
| Composition realtime/light | 937.21 / 2312.32 | 1394.67 |
| Native realtime control | 43.82 / 111.97 | 0.11 |
| Composition realtime/light repeat | 1030.90 / 2312.47 | 1578.29 |

All **59/59** functional/trace-integrity checks passed, including all **1280/1280** profile inputs. These checks do not assert acceptable latency. The three composition realtime traces retained 96, 92 and 89 completed heartbeat gaps over 500 ms, versus zero in the native control. At the 2768.93 ms maximum, completed traced callbacks occupied approximately 106.62 ms. This is repeatable scheduling evidence under the diagnostic sequence, not proof that normal flights have identical latency. Per-profile tracing/output and an end-of-profile Input drain are additional labeled observer work.

## 7. Active, quiescent and hidden map

| Revision-two arm | Heartbeat p95 | Measured maximum |
|---|---:|---:|
| Active baseline, first | 79.64 ms | 442.33 ms |
| Quiescent visible host | 220.50 ms | 1112.20 ms |
| Hidden/disposed host | 62.64 ms | 77.21 ms |
| Active baseline, last | 215.18 ms | 525.97 ms |

The quiescent control disposes the visual projection pump before ongoing input; its initial map does not receive new flight snapshots. It therefore demonstrates that new snapshots, serialization and aircraft interpolation are not required for this symptom. The hidden control keeps the layout slot and surrounding Operations presentation. These short single controls narrow hypotheses but do not certify repeatability across hardware or isolate scene complexity perfectly.

## 8. Dashboard, clock and presentation ablations

Collapsing metric cards produced p95/max **199.58/813.11 ms**; collapsing alert/activity lists **233.42/504.47 ms**. Models still refresh in these visual-subtree controls. Detaching RuntimeChanged and retaining a map-only snapshot presenter produced **875.11/2031.09 ms** despite less CPU and allocation. The normal clock remains active in that arm. Suppressing the map context/status-line consumer produced **212.45/532.58 ms**; lifecycle/error handling remains intact.

No single category removal established a safe fix. The complete condition table and all runs are in [matrix results](map-v2/evidence/7.24a.2/matrix-table.md) and [machine-readable analysis](map-v2/evidence/7.24a.2/matrix-analysis.json).

## 9. Layout and render findings

Visible composition produced roughly 13,000 LayoutUpdated notifications per 30-second window, compared with roughly 1,760 in hidden/native controls. These are notifications, **not layout duration**. In the first revision-two baseline, 9,545 RenderMessage callbacks totaled about 615 ms and 7,927 AnimatedRender callbacks about 361 ms across the whole trace; p95 callback envelopes were approximately 0.114 and 0.131 ms. The longest RenderMessage callback was approximately 30 ms.

The evidence does not identify a 500–2000 ms synchronous layout/render pass. Exact internal measure/arrange, GPU execution and unhooked native composition cost remain unmeasured.

## 10. Final-drain findings

The trace retains final source completion, probe cancellation/completion, pending heartbeat age, Input drain request/execution/resumption and six seconds of continued application activity. Tail gaps remain in the evidence even when the preceding measurement window is fast. The longest dedicated drain-tail gap was **1422.4075 ms**; sampled UI CPU stayed almost flat across most of that span, with short render callbacks still observed.

This supports wakeup/scheduling sensitivity after normal diagnostic producers stop. It does **not** prove that final drain manufactures a nonexistent responsiveness problem. Similar delays during continued input prevent excluding drain gaps or redesigning the frozen shutdown procedure on that assumption. The frozen collector is unchanged.

## 11. Clock/feed/wakeup matrix

The bounded matrix varies one category where practical: clock on/off; independent/UI-paced feed; heartbeat 20/100 ms; extra Input probe off/100 ms; Normal polling off/50 ms; resource sampling 30/5 seconds; continued operation/final drain. The clock-off/UI-paced combination is explicitly labeled, not represented as a one-factor comparison.

The 50 ms Normal polling arm recorded heartbeat p95 **99.83 ms**; the Input-probe-only arm **258.37 ms**. The 100 ms heartbeat arm naturally changes the measured interval distribution and recorded p95 **301.32 ms**; it is not evaluated against a nominal 20 ms heartbeat as an equivalent workload. Five-second resource sampling did not remove the tail. Large first/last baseline differences limit causal effect estimates. No production polling was added.

## 12. OS and thread scheduling

The environment is Windows build 19045, 16 logical processors, .NET Windows Desktop 10.0.11 and WebView2 SDK 1.0.4191.47. The worker records host/UI-thread and initially owned browser-process CPU, working memory and UI-thread state every 250 ms. Observed UI CPU ranged roughly 3–13% of one core in the fresh arms; the worst detached-presenter arm used about 4%.

Long-gap snapshots commonly show Wait/UserRequest while short Render callbacks continue. This argues against one continuously executing managed callback as a sufficient explanation. It does not prove OS descheduling, GPU starvation, global CPU saturation or the native wait site. A Windows recorder is installed, but this process token is not administrator; no kernel/context-switch capture or heavyweight profiler was added. Browser per-process samples are retained; GPU/composition CPU is not separately attributed.

## 13. GC and allocation

Instrumented host allocation was approximately **1.55–4.97 MiB/s** across fresh arms, including trace storage and sampling. Observed Gen2 increments were generally 6–7, versus two in the detached presenter. Latest observed GC pauses were below approximately 44 ms. During the 1422 ms drain-tail gap, the latest recorded pause components were **0.153 and 2.797 ms**, and worker samples continued throughout it.

There is no evidence of a GC stop-the-world pause accounting for that representative gap. Latest-pause snapshots can miss intervening collections, and event marks are listener delivery times; this is not a universal exclusion of GC contributions. Lower allocation with worse latency in the presenter ablation does not justify an allocation optimization.

## 14. Production defect identified?

**Specific safely remediable production defect: NO. Reproduced scheduling problem: YES.** Visible composition and wakeup cadence are implicated, but the exact native/Dispatcher mechanism is unresolved. Flight truth remains distinct from visual scheduling.

## 15. Diagnostic defect identified?

**YES, in newly added diagnostic tooling; NO established diagnostic cause for the historical frozen drain failure.** A side effect in the new pilot instrumentation was identified: subscribing to CompositionTarget.Rendering itself requests/maintains rendering. Revision two defaults that observer off and includes an explicit observer control. This does not explain away earlier failures, whose collector did not use that subscription.

Review also found omissions in the new offline analyzer's pending-work representation and an insufficient default timeout for the legacy sequence. These diagnostic-tool defects were corrected before final verification.

## 16. Exact remediation

**No production remediation.** New diagnostic tooling provides bounded traces, condition-controlled reproductions, runnable-priority analysis and preserved incomplete/aborted/pending operations. Window evidence is buffered until tracing ends. The longer diagnostic gets its own 1200-second timeout. Offline operation-key de-duplication uses a HashSet to avoid quadratic analysis cost.

The frozen readiness collector, evaluator and numerical policy remain unchanged. No flight algorithms, authoritative ordering/coalescing, recovery durability, SimConnect behavior, renderer technology or default map setting changed.

## 17. Why these changes are justified

Rendering-observer isolation follows WPF's documented implementation and avoids adding an unrequested render loop to the principal trace. Pending-work fixtures demonstrated the analyzer omission before correction. A fixed sequence containing more than 640 seconds of realtime work cannot use a 150-second default runner deadline. These changes improve diagnostic validity; none is presented as a cure for production scheduling.

Primary references and detailed inference limits are retained in [investigation notes](map-v2/evidence/7.24a.2/investigation-notes.md). Referenced WPF repository-main code is background evidence, not a native stack from this installed runtime.

## 18. Deliberate stall detection

**Passed.** Both actual UI trace-contract runs passed 6/6 checks. The final trace records a deliberately blocked interval of **600.1762 ms**, a pending heartbeat age of **841.8515 ms**, and the subsequent **855.8564 ms** heartbeat. That heartbeat includes pre-trace time and is not claimed as fully attributed execution; the explicit injection interval establishes the block.

The unchanged frozen collector's separate boundary test passed **7/7** checks: after a 600 ms UI block, it observes heartbeat and Input-probe ages of at least 550 ms **before their callbacks execute**, then drains the probe. Its numerical policy therefore still rejects this real stall. The 27 policy contracts also verify rejection of missing, censored and over-limit evidence. Detection was not weakened to obtain a pass.

## 19. Frozen-policy final trials

**The fresh numerical assessment PASSED. The checkpoint remains NOT READY.** The policy assessment's `ready: true` describes only the frozen numerical/lifecycle evaluation; it does not resolve the root-cause requirements in specification Part 20.

One sequence was frozen at **2026-09-25 16:19:47 UTC**: full five minutes, light five minutes, then lifecycle six minutes. Every declared run completed once; there were no final retries, exclusions, threshold changes or collector changes. All 20 complete 30-second primary windows were evaluated.

| Metric | Full | Light | Frozen limit |
|---|---:|---:|---:|
| Median window p95 | 55.2217 ms | 77.4229 ms | <80 ms per mode |
| Highest window p95 | 60.7789 ms | 83.3287 ms | ≤120 ms |
| Highest window p99 | 90.2071 ms | 129.8040 ms | <250 ms |
| Maximum heartbeat gap | 246.2508 ms | 250.8316 ms | <500 ms |
| Maximum boundary heartbeat age | 43.3665 ms | 78.8109 ms | <500 ms |
| Maximum boundary pending-queue age | 2.5982 ms | 28.0192 ms | <500 ms |
| Highest window Input-queue p95 | 41.3197 ms | 82.3017 ms | <100 ms |
| Maximum Input-queue delay | 223.8109 ms | 204.8721 ms | <500 ms |
| Highest cumulative bridge ACK p95, including queue | 50.4700 ms | 64.1986 ms | <500 ms |
| Renderer startup | 941.8374 ms | 890.7402 ms | <5000 ms |

The pooled nearest-rank p95 of window p95 values was **83.2989 ms**, below 100 ms. No window exceeded 100 ms. The six-minute lifecycle run passed all 16 functional checks and all four supplemental steady-window safeguards; its steady-window p95 values were **66.9358, 58.2198, 62.0295 and 79.0726 ms**. Raw/derived results remain in the [final assessment](map-v2/evidence/7.24a.2/final-assessment.json).

These successful fresh trials do not erase the repeated instrumented composition delays, this checkpoint's separate 515.8971 ms contention failure, or either historical failed assessment.

## 20. Operational integrity

**1,089/1,089 final inputs retained:** 347 in full mode, 347 in light mode, and 395 in the full-flight lifecycle. Each source started once; map activity never reconnected it. Final recovery contains the final input. The lifecycle reached OUT/OFF/ON/IN, preserved PIREP eligibility, submitted a PIREP and verified persistence after reopening the database. It exercised a real JavaScript stall, navigation, minimize/restore and three locked-save observations followed by recovery; no unexpected renderer restart/fallback occurred.

The independent browser-contention comparison retained **395/395 inputs in each arm**, including 50 while the browser was deliberately stalled. Per-sample phase/time/ETA/progress/eligibility, telemetry/event sequence, recovery, history and PIREP hashes matched. Both finished Complete with 27 events and the four flight-phase events. Visual coalescing did not discard authoritative input. Existing map checks also passed their obsolete-callback/epoch and disposal safeguards.

Additional investigation coverage retained **855/855 matrix inputs** and **1280/1280 original-sequence inputs**. Recovery regression passed **21/21**, including **5,000 sequential durable replacements**. These are synthetic diagnostic inputs, separate from historical recorded-flight replay coverage.

## 21. Files created or modified

Relative to the checkpoint entry manifest, the only changed existing source file is `src/Alpha6Ops.Desktop/MapV2/MapPerformanceChecks.cs`, adding opt-in diagnostic routing and optional profile tracing. Existing non-diagnostic source and prior checkpoint work are preserved.

New diagnostic files: `DispatcherSchedulingTrace.cs`, `MapSchedulingChecks.cs`, `scripts/verify-map-scheduling.ps1`, `scripts/SchedulingTraceAnalysis.psm1`, `scripts/summarize-map-scheduling.ps1`, `scripts/test-scheduling-trace.ps1`. New documents are this report, the supplied specification copy, execution ledger and `docs/map-v2/evidence/7.24a.2/` evidence index/summaries/manifests. Raw runs remain under `work/checkpoint-7.24a.2/`. The unrelated 7.11 bundle is untouched.

The final entry-state audit checked **390 files** and found only that existing diagnostic source change. All seven original frozen files remain hash-identical in the checkpoint-entry archive. Every new freeze entry still matches after the trials. The 7.24A.1 manifest's **173 historical report/data artifacts match**. Its remaining entry points into the mutable build output, `src/Alpha6Ops.Desktop/bin/Release/net10.0-windows10.0.17763.0/Alpha6OPS.dll`: that DLL necessarily changed in the diagnostic rebuild, but its old binary was not archived. No matching copy was found. The original hash and source archive remain, but this is a historical binary-retention limitation, not a clean 174/174 artifact-preservation result. The original manifest was not edited. See [source audit](map-v2/evidence/7.24a.2/source-audit.json).

The current tested DLL and apphost are now separately archived under `work/checkpoint-7.24a.2/frozen-tested-build/`, byte-identical to the pretrial freeze. The [archive record](map-v2/evidence/7.24a.2/tested-build-archive.json) distinguishes this new retained build from the unavailable historical binary.

## 22. Build

**Complete Release solution build passed: zero warnings, zero errors.** The later Map V2 Desktop build also passed with zero warnings/errors. The final freeze records the built application and assembly hashes, as well as source/tool/policy hashes; the readiness runs use that build directly. Build logs are indexed with the evidence.

## 23. Regression counts

**627 verification checks executed: 626 passed, 1 failed, 0 skipped within the invoked scope.**

| Group | Passed / executed |
|---|---:|
| Core | 122 / 122 |
| Map integration/contention | 120 / 121 |
| Map mailbox and browser/math | 14 / 14 |
| Recovery | 21 / 21 |
| Fresh scheduling matrix, 21 arms | 189 / 189 |
| Original broader sequence and trace integrity | 59 / 59 |
| Two actual injected-stall trace contracts | 12 / 12 |
| Frozen collector boundary/process contracts | 7 / 7 |
| New offline trace-analyzer contracts | 12 / 12 |
| Frozen policy contracts | 27 / 27 |
| Existing performance-summary contracts | 5 / 5 |
| Final full/light integrity checks | 22 / 22 |
| Final lifecycle integrity checks | 16 / 16 |

The retained failure is **Map contention heartbeat maximum 515.8971 ms**, against <500 ms and within 100 ms of the baseline (baseline **304.5679 ms**). All operational comparisons passed. It was not retried to obtain a favorable result. This continues the intermittent timing risk; it is not silently converted to a skip or expected failure.

Counts include all declared matrix arms and both trace-contract executions. Development RED/GREEN analyzer iterations are retained separately and are not counted again. The numerical assessment and provenance/report audits are separate from this check count. No checks were disabled. The full unchanged Sierra suite and recorded replays were not invoked under the specification's diagnostic-only exception; privileged native scheduling capture was unavailable. See [verification summary](map-v2/evidence/7.24a.2/verification-summary.json).

## 24. Replay and telemetry result

This is diagnostic-only scope. The complete unchanged Sierra suite and recorded replays were not unnecessarily rerun. Historical **9/9 flight replays and 91,366/91,366 replay telemetry samples** remain retained baseline evidence, not newly executed results. New synthetic input coverage is reported separately above. Native/installed MSFS validation remains outside this checkpoint.

## 25. Unexpected-report guards

**Zero unexpected reports.** All **28 strict application runs** had zero crash/unobserved-task reports and no owned browser processes remaining after disposal. Map regression retained exactly **16 expected renderer reports** from eight deliberately injected failure cases; recovery retained exactly **12 expected reports** with the existing path/source/count allowlist. These narrow exemptions do not apply to final trials or normal runtime failures.

The manual audit initially expected renderer restart counters 1/2. Source inspection confirmed reports are written before increment, so the exact negative-case pattern is eight at 0 and eight at 1. The corrected audit validates that pattern; the initial audit is retained. No runtime guard, raw report or acceptance threshold was changed. See [report audit](map-v2/evidence/7.24a.2/unexpected-report-audit.json).

## 26. Remaining risks

- Ready Input/Background work can still wait too long around visible composition; no specific production repair has been verified.
- Trace hooks, worker sampling, storage allocations and native message activity can affect timing. Hook cost is not total observer overhead, and no cost subtraction is used.
- Short, ordered controls on one machine have substantial variability. Native-host and UI-paced conditions cannot be generalized to normal flights or other hardware.
- Native wait stacks, message-queue gating, context switches, GPU timing and precise internal layout costs remain unavailable.
- A successful percentile or functional check cannot override a maximum-stall failure or an unresolved scheduling mechanism.
- The old mutable 7.24A.1 DLL was not archived before the new build. Its original hash remains, but the exact historical executable artifact is unavailable; future checkpoints should archive binaries as well as source and raw results before rebuilding.

## 27. Readiness decision

**NOT READY FOR 7.24B.** The final frozen numerical assessment passed, but no production or diagnostic cause explaining the repeated delays has been safely remediated. That fails the root-cause prerequisite, independently of this checkpoint's retained contention timing failure and binary-retention limitation. Classic remains default. Map V2 stays opt-in.

## 28. Exact supporting evidence

- [Evidence index](map-v2/evidence/7.24a.2/README.md), [hash manifest](map-v2/evidence/7.24a.2/artifact-manifest.json), [source audit](map-v2/evidence/7.24a.2/source-audit.json).
- [Matrix table](map-v2/evidence/7.24a.2/matrix-table.md): quiescent 1112.2019 ms, presenter-detached 2031.09 ms, and drain-tail 1422.4075 ms; raw traces remain indexed locally.
- [Original sequence](map-v2/evidence/7.24a.2/legacy-comparison.json): repeated composition p95 around 0.94–1.07 seconds versus native 43.82 ms under the matched longer workload.
- [Contention comparison](map-v2/evidence/7.24a.2/contention.json): identical flight truth but retained 515.8971 ms timing failure.
- [Final policy assessment](map-v2/evidence/7.24a.2/final-assessment.json), [freeze](map-v2/evidence/7.24a.2/policy-freeze.json), [verification counts](map-v2/evidence/7.24a.2/verification-summary.json), and [strict report audit](map-v2/evidence/7.24a.2/unexpected-report-audit.json).

## 29. Recommended next action

Use the quiescent/presenter and producer-stop reproducers for a short, appropriately privileged native WPF/Win32 wait/message/ETW investigation. Determine why runnable Input work remains deferred while Render callbacks proceed; then reproduce a proposed narrow fix under the same conditions before changing production. Do not add synthetic wakeups, raise measurement priority, remove the drain gap, replace the host or loosen the gate to obtain a pass.

**Stopped at 7.24A.2.** No installation, default enablement, 7.24B or globe implementation is included.
