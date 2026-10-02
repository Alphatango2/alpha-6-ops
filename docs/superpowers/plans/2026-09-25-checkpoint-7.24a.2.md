# Checkpoint 7.24A.2 investigation plan and ledger

> Execute inline with systematic-debugging, executing-plans and test-driven-development; one fresh independent review before final freeze. Preserve the existing checkpoint branch and all earlier evidence.

**Goal:** Explain composition scheduling gaps, apply only an evidenced minimal remedy, and make a new readiness decision.
**Architecture:** An explicitly invoked diagnostic harness hosts the existing coordinator, Operations workspace, composition renderer, bridge and durable recovery. A bounded in-memory trace observes Dispatcher hooks and existing activities. No telemetry payload tracing or production optimization before a reproduction.
**Tech stack:** WPF/.NET10, WebView2CompositionControl, MapLibre, PowerShell.
**Spec:** `docs/map-v2/checkpoint-7.24a.2-spec.txt` (all22parts/29reportitems).

## Constraints
- Preserve historical NOT READY decisions and raw artifacts. Archive original frozen diagnostic sources before evolving the harness; never edit prior reports/manifests/policy JSON.
- Frozen numerical limits unchanged. No default enablement, host replacement, operational changes, 7.24B or globe work.
- Each arm runs in a fresh process, isolated state and browser profile, actual3440×1400 host. No unrelated benchmarks run concurrently.
- Trace overhead must be measured and labeled. Callback envelopes measure UI-thread work, not GPU execution or exact measure/arrange internals.
- Authoritative inputs, recovery and connection generation are checked in every arm; full-flight truth is separately verified by existing contention/lifecycle suites.

## Review focus
1. Trace callbacks recursively tracing themselves or silently dropping busy-period evidence: bounded storage, dropped-count check, handler-cost totals.
2. Pending low-priority callbacks disappearing at stop: capture pending work and drain chronology; injected600ms stall must remain visible.
3. Ablations accidentally removing flight processing: source/timeline/final recovery/generation checks with explicit per-arm conditions.
4. Host comparisons changing multiple conditions: matched host pairs, one-factor arms and explicitly labeled clock/feed interaction.
5. Evidence written inside timing windows or altered frozen budgets: memory trace then disk; hashes/UTC freeze precede final run.

## Tasks
- [x] 1. Preserve source/evidence hashes; add diagnostic trace and reproduction. First run a >500ms injected-stall trace contract; verify missing trace/dropped evidence fails automated analysis. Add only explicit diagnostic dispatch in MapPerformanceChecks plus new MapSchedulingChecks/DispatcherSchedulingTrace files and a separate runner/analyzer script.
- [x] 2. Controlled matrix: first normal composition, clock-off/UI-paced composition and exactly matched native control; then remaining one-factor arms and repeat the composition baseline/problem arm. Five-second warmup,30s measured,tail/drain chronology6s;1Hz flight feed. Trace Dispatcher enqueue/priority/start/end with callback identity, activities, heartbeat/probe, render/layout notifications, ACK/status, sampler, thread CPU/state and GC/allocation observations. Trace all work through final drain.
- [x] 3. Analyze queue occupancy and callback durations around gaps. Classify synchronous blocking, queue starvation, idle/wakeup effects and measurement artifact only from trace. Use active/quiescent/hidden map, clock on/off, independent/UI feed, probe off/100ms, heartbeat20/100ms, sampler30/5s, polloff/50ms, collapsed metric/activity subtrees, detached runtime presenter and status UI callback ablations. Restore local experiment state and retain every arm.
- [x] 4. If a mechanism is established, create a failing reproduction test, implement the smallest remedy, verify repeated same-condition before/after and injected stall rejection. Otherwise make no production change and record remaining uncertainty.
- [x] 5. Review, then freeze tooling/procedure with unchanged policy JSON. Predeclared final sequence: full5min,light5min,lifecycle6min; if production remediation occurs use two full/light pairs to demonstrate repeatability. Retain failures; no retry-until-pass. Run affected Core/Map/performance/recovery tests if diagnostic-only, otherwise full Sierra suite including9replays/91,366samples.
- [x] 6. Final hashes/guards,29-item report,evidence index and READY/NOT READY decision; stop.

## Ledger/rulings
- Existing local codex/checkpoint-7.24a work is the intended base; preserve uncommitted work and do not reset/recreate it.
- User supplied detailed execution authority. Continue without a redundant plan-approval pause. The plan is an investigation sequence; unknown remedies are deliberately evidence-dependent, not speculative code changes.
- Original7.24A.1 frozen diagnostic files are copied under work/checkpoint-7.24a.2/before. Earlier policy/evidence remains immutable; fresh final evidence uses a new manifest pointing to the unchanged numerical policy.
- Initial hypothesis only: frequent Normal wakeups may alter dispatch behavior in a continuously rendering composition host. Need posted/promoted/started/completed trace and UI CPU/state, not inference from elapsed intervals or LayoutUpdated count.
- Trace revision1 pilot arms01–03 retained. WPF source shows Rendering subscriptions request a render and keep rendering active; revision2 observes existing Dispatcher render operations by default and adds an explicit render-hook control. Timer identity now reads DispatcherOperation argument; analysis separates inactive timer delay from runnable wait and retains pre-trace gaps as incomplete mechanisms. No frozen readiness collector changed. Removed intermediate trace-window disk output before tail; both pilot differences labeled.
- Revision2 matrix predeclared in work/checkpoint-7.24a.2/matrix-order.json:18 fresh arms,04–21, ending with ui-clock-off and baseline repeat. No tests run concurrently. Analysis contract microtests are trivial tooling validation; no benchmark runs concurrently.
- Ruling: run the original broader performance sequence once with per-profile traces after the fresh matrix. Fresh30s arms differ from its accelerated-history/recreated-renderer conditions, so cannot alone resolve the historical seconds-long waits. This adds runtime but directly investigates a known reproducer. Original frozen readiness collection and numerical limits remain unchanged.
- Task1 complete: representative diagnostic/trace; injected-stall pilot6/6; archive174historical artifacts verified. Revision2 build0warnings/errors.
- Task2 complete:21fresh matrix arms,189/189 integrity checks,855/855 authoritative inputs. Trace includes failures and tail gaps; no readiness claims from these short instrumented arms.
- Task3 findings: quiescent1112.2019ms; detached presenter2031.09ms; drain tail1422.4075ms with14.5365ms in completed callbacks and1420.9249ms ready Input wait. UI snapshots mostlyWait/UserRequest. These support scheduling/wakeup sensitivity, not expensive UI callback monopolization or a proven harmless drain artifact. Historical-sequence instrumentation was started once at this stage (completion recorded below).
- Final review: fixed pending/aborted/unclosed operation omission and promoted-priority reporting; four additional fixtures RED then GREEN, final analyzer12/12. Fixed legacy runner deadline1200s independently of nominal arm seconds. No production findings. No second review dispatched. Offline analysis de-duplication changed toHashSet after stopping only the redundant summary process; all raw traces preserved.
- Ruling: no production remediation at this stage. Short callbacks and correlated scheduling do not identify a safe application change; retainCaseC/NOT READY unless remaining evidence establishes a specific repair. Do not discard drain gaps or raise priorities to mask the symptom.

- Original broader sequence completed:59/59 checks,1280/1280 inputs; repeated composition realtime p95 around0.94–1.07s versus native0.044s. Instrumentation and prior workload differences remain explicit.
- Affected regressions completed:Core122/122,Map120/121 plus14/14 mailbox/browser,recovery21/21 with5000 durable replacements. Retain the Map contention failure515.8971ms; no rerun to obtain a pass. Operational hashes match.
- Final freeze stored at2026-09-25T16:19:47.2566131Z. Unchanged frozen policy/source collectors, diagnostic sources and built binary hashes recorded; one full5min/light5min/lifecycle6min sequence declared and started (completion recorded below).


- Final sequence complete without retries:full11/11,light11/11,lifecycle16/16; unchanged frozen numerical assessment PASS across20 windows;1089/1089 inputs. OverallNOT READY remains because root cause is unresolved and separate contention timing failure remains.
- Final verification count627executed/626passed/1failed/0skipped in diagnostic scope.28strict report/disposal guards clean;16renderer and12recovery negative-case reports match exact source/count patterns.
- Historical audit exception:173prior report/data artifacts match;oldmutablebin/Alpha6OPS.dll was replaced by the required build and was not separately archived. Original hash and all seven source/tool archives are preserved; exactoldbinary unavailable. Do not claim174/174 preservation.70earlier7.24Aevidence files and3published build artifacts unchanged.
- Stopped after7.24A.2; no production remediation,default enablement,installer,push,7.24B or globe work.


