# Checkpoint 7.24A plan and execution ledger

Goal: measured automated readiness for installed native pilot validation, without enabling Map V2 by default.
Spec: `docs/map-v2/checkpoint-7.24a-spec.txt` (authoritative user guidelines).
Architecture: existing application/coordinator/persistence and production Operations map; diagnostic-only harness exercises real paths with isolated profiles and test-only synthetic input.
Tech stack: .NET 10 WPF, WebView2 composition, bundled MapLibre, existing recovery and regression scripts.

## Global constraints
Keep classic default/fallback. No globe, workspace migration, operational algorithm/telemetry change, native MSFS control or durability weakening. Preserve all 2,025 checks, nine replays/91,366 samples, clean unexpected-report guards. Native gates stay unavailable unless an actual safe environment exists. Keep work local and do not start 7.24B.

## Tasks
1. Establish measured pre-optimization baseline using passive timing and a diagnostic entry point in the actual Operations workspace. Capture process/CPU/working-set, bridge distributions, UI/recovery timings, hidden state and startup/recreation. Compare controlled workload stages to separate runtime/presentation/map contributions.
2. Add failing targeted checks for measured waste or defects; make the smallest justified presentation-only changes, then compare identical workloads. No speculative cache/rewrite or weakened file flush.
3. Run final production-path 60-minute continuous soak with live coordinator input, navigation/hide/recreate, input, minimize/restore, solar updates, transient file locks and periodic resource samples. Capture partial progress safely throughout; report actual duration. Independently test synthetic 1/25/100/500/1,000/2,000 entity loads and controlled browser CPU throttling. Exercise lifecycle failures and compare complete operational outputs under stalls.
4. Run full production/package/recovery/map/replay regressions; inspect automated 100/125/150/175/200% rendering. Review code independently, fix material findings with RED→GREEN tests, preserve evidence and produce the 28-item report, complete 7.24B checklist and globe-readiness analysis. Stop.

## Initial readiness budgets (before measurement)
Current scope is one real aircraft, its route/trail and airport labels. For that scope: no missing state/events or recovery errors; bounded queues/restarts; no owned browser processes after final disposal; 1 Hz flight feed maintained; input-priority Dispatcher p95 below 100 ms and p99 below 250 ms during steady visible operation, with lifecycle transitions reported separately; warm renderer startup below 5 s; p95 bridge ACK below 500 ms. Transitions/stalls are explicit exceptions measured separately, not removed from raw evidence. Synthetic capacity is determined separately and is not airline-world support. Memory must plateau/oscillate after warm-up with bounded hidden/disposed resources; no claim of leak freedom.

## Review focus
1. Diagnostic inputs cannot enter ordinary product operation or alter source profiles.
2. Skipping presentation work must never skip authoritative telemetry, event/persistence work or catch-up when a workspace becomes visible.
3. Any equality/caching optimization must include fields that affect visible state and preserve selection/layer/follow behavior.
4. Soak/lifecycle handlers, renderer epochs and subprocess resource accounting cannot silently accumulate.
5. Evidence must distinguish injected input/scaling/throttling from physical/native validation; unknowns and failed guards remain visible.

## Ledger
- Start: 020bda9. Only unrelated untracked 7.11 bundle; preserved.
- Ruling: continue in the prepared checkout on `codex/checkpoint-7.24a`; retain SDK, replay and hardware evidence paths. No merge/push/install is authorized by this checkpoint.
- Interfaces: timing is passive; diagnostic harness uses the existing coordinator and MainWindow constructor. Final soak uses a frozen output build so subsequent documentation work cannot alter its renderer assets.
- Before evidence: actual Operations profiling and loads saved under `work/checkpoint-7.24a/before`. Accelerated Map V2 Input heartbeat p95 593.64 ms versus recovery-save p95 4.93 ms. Initial/warm map startup 855/488 ms.
- RED/GREEN: unchanged Operations metric rows replaced their containers; now immutable record equality retains unchanged rows while changed values still update. Broad performance remains variable; no claim of a scheduling cure.
- Diagnostic correction before sustained testing: normal dashboard clock remains active; telemetry producer runs independently on a background task; file locks are released only on an observed actual replacement retry. Recursive report guard includes nested map/session locations.
- Full validation completed: baseline 1,871 checks, nine replays and 91,366 samples; recovery 21 including 5,000 durable replacements; map 121 plus 14 mailbox/browser assertions. The two native simulator gates remain unavailable. No operational changes or durability reduction.
- First attempted 60-minute soak stopped near 20 minutes on a diagnostic null renderer after an unscheduled hidden window. Its partial evidence is retained in `soak-final`; it is not counted as a pass. Added explicit visible-window restoration/readiness and passing hide/restore contract.
- RED/GREEN: reduced motion resubmitted unchanged aircraft/label data 24 times in 600 ms; clearing pending interpolation reduced this to zero. Focused contracts pass. Route hit-testing setup now zooms out to keep the midpoint in view; reduced-motion activation is idempotent across restored preferences.
- Review corrected final process discovery: record every renderer epoch, including input/restore epochs, before disposal; keep the bounded 10-second process exit check. The earlier fixed three-second disposal attempt is preserved as a failed development run, not hidden.
- Production solution/package and post-reduced-motion map suite pass. Frozen final package used by `soak-complete` began 2026-09-24 23:33 UTC. Final sustained result pending. Later diagnostic-only progress/command-latency fields do not change the frozen run or production behavior.
- Five 1920×1080 controlled scaling captures inspected: readable controls/selection/labels and visible focus, responsive reflow/scrolling; raster softness increases under LayoutTransform magnification. Actual monitor DPI transitions remain manual.
- `soak-complete` completed 3,600.093 seconds and every flight-body assertion (3,941 inputs, seven actual recovery retries, 12 browser stalls, 24 observed epochs, durable completed PIREP). The final process check then threw AccessDenied reopening a historical process ID: 39 passed, one failed. All six separate performance gates passed; this does not make the whole run pass. Windows PID reuse is a vulnerability of that checking method, not a proven explanation for which particular ID was inaccessible.
- Diagnostic ownership correction: retain original SYNCHRONIZE-only SafeProcessHandles while WebView reports ownership, use zero-time waits on those original objects, and dispose handles at run end. Tracking/query failures fail closed. Focused live/exited/repeated-query contracts plus existing input/scaling/hide checks: 22/22 passed. Handles add diagnostic bookkeeping but do not keep an exited browser workload running.
- New verification (`verification-owned-handles`) and a fresh 60-minute run (`soak-verified`) use the normal repackaged build. Source/package are held unchanged throughout these runs. Old failed evidence is retained. Current profile stages explicitly assert window/workspace/renderer visibility; DOM-command timings exclude artificial settling time and do not claim physical pixel latency.
- Final verification: 51/51 passed. Final soak: 3,600.097 seconds, 43/43 passed, 3,941/3,941 inputs/timeline samples, seven real retry exercises, 12 stalls, one source start, durable completed PIREP, zero remaining tracked processes after 236 ms. Post-exit profile process audit also zero. No recorded resource-sampling errors; package hashes unchanged.
- Resource trend: visible median 802 MiB; early/late medians 804/801 MiB, with oscillation and hidden-process exit, not monotonic growth. No leak-freedom claim.
- Readiness gate: 5/6 passed. One of 47 complete steady visible windows (ending 900.09 s) has p95 = 100.0243 ms against the predeclared below-100-ms budget. No rounding/waiver; decision NOT READY FOR 7.24B. No flight-data or recovery failure detected. Preserve raw marginal result and better earlier run rather than selecting a passing repetition.
- Review corrections: pilot restart instructions now use tray Exit and verify process termination; summary gates reject absent/non-finite/zero-count timings, proven by 5/5 fixtures. Per-row sampling validity and final partial-window timing flush remain disclosed diagnostic limitations.
- Final accepted totals: 2,132 check/gate executions; 2,131 passed; one performance gate failed; two native checks unavailable. All original 2,025 checks passed; 9/9 replays and 91,366/91,366 recorded samples. Complete 28-item report/checklist/globe analysis retained. Stop at 7.24A; no native pilot, default activation, globe, install, commit or push.
