# Checkpoint 7.24A.1 execution plan and ledger

> Execution: superpowers:executing-plans, inline; preserve the existing local checkpoint branch and uncommitted 7.24A work.

**Goal:** Characterize the narrow responsiveness miss and make a new evidence-based readiness decision.
**Architecture:** Extend the explicit diagnostic-only performance entry point; keep production work, renderer, recovery and flight semantics unchanged unless repeatable evidence justifies a small fix.
**Tech stack:** WPF/.NET 10, WebView2, PowerShell, JSON evidence.
**Spec:** `docs/map-v2/checkpoint-7.24a.1-spec.txt`.

## Constraints and review focus
- Historical 7.24A remains NOT READY, with its 100.0243 ms failure intact.
- No default enablement, install, push, globe, 7.24B or other workspace migration.
- All trials retained, including failures. Full/light arms retain production presentation, clock and recovery writes.
- Missing/invalid metrics, lost inputs, hidden renderer, contaminated transitions and unexpected reports must fail closed.
- Freeze any new acceptance policy and its automatic evaluator before the final run; never tune against that final result.

## Tasks
- [x] 1. Inspect failed window and neighboring records, lifecycle and producer schedule. Store precise observations and limits; preserve source hashes.
- [x] 2. Add diagnostic-only steady-trial mode: independent 1.1 Hz input, active dashboard clock, 20 ms Input heartbeat, independent bounded Input queue probe, 30-second windows, complete raw timing samples. Full resource sampler every 5 s; light every 30 s. No UI detachment. Validate trial completeness and malformed evidence.
- [x] 3. Six fresh 3-minute measured trials after 15-second warmup in fixed full/light/light/full/full/light order (36 windows). Same production composition host, current flight, route/trail/night, no measurement-window navigation. Requested1920x1080 was expanded by production startup fitting to3440x1400; preserve and report actual workload. Keep all results. Compare window distributions and sampling costs, not just best runs.
- [x] 4. Derive and freeze a policy from characterization. Store JSON limits, rationale, evaluator hash and UTC timestamp before final run. Test missing, nonfinite, insufficient, boundary, sustained-tail, isolated-spike, lost-input and dirty-guard cases.
- [x] 5. Final fresh full and light trials, 5 measured minutes each (20 windows), evaluated without policy edits. Run existing relevant Map V2/performance/recovery checks plus bounded lifecycle/flight soak. Full production suite only if production code changes; otherwise prove production source hashes unchanged.
- [x] 6. Independent review, evidence index and all 15 requested report items. Stop at READY/NOT READY; no next checkpoint.

## Ledger
- Repository inspection: existing `codex/checkpoint-7.24a` contains complete prior uncommitted work. Work stays in this checkout to preserve it. No reset or new worktree.
- User supplied execution authority and detailed scope; proceed without a redundant planning approval.
- Baseline source/script/evidence hashes captured in `work/checkpoint-7.24a.1/before-files.json` before changes.
- No acceptance limits for new policy chosen yet. Repeated observation plan fixed before data collection.
- Task 2/3: Six runs, 66/66 integrity checks, 1,290/1,290 independent inputs, 27,099 heartbeat observations, all 36 windows below100ms. Full/light p95 medians57.648/72.5256ms. Source-hash comparison: only existing diagnostic MapPerformanceChecks.cs and verify-map-performance.ps1 changed; operational production unchanged.
- Review: independent reviewer identified window-boundary callback censoring. Fixed before freeze with unfinished-age capture plus final Input drain. Three evaluator cases RED (23/26) -> GREEN(26/26); real600ms boundary injection7/7. Characterization retained with explicit terminal-coverage limitation; no actual hidden >500ms stall demonstrated. No numerical policy tuning for this fix.
- Regression: Core122/122. First Map suite120/121 plus14mailbox/browser checks; only accelerated contention heartbeat failed537.7024ms vs500ms. Full operational hashes matched. Failure retained, separate from the original100.0243ms issue. Bounded rerun underway; no threshold changes.
- Final lifecycle run selected as6minutes to include minimize/restore at minute4 and its following steady window; no need to repeat the unchanged one-hour endurance scope.
- Regression completed: Map full repeat121/121 plus14; focused third attempt73/73 (control305.0301/stressed245.6561ms), with initial failure retained. Performance51/51 functional assertions but three composition real-time profiles p95~812–827ms, including both full/light sampling. Native-host control45.5981ms. Production clock/feed conditions differ from fresh trials; no safe causal optimization established. Core122, recovery21/5000cycles, legacy summary5, policy27, boundary7 pass.
- Additional observer ablations: probe-disabled50ms-polling run11/11,215/215inputs,median/max p95 65.1912/71.0692. Truly passive(no extra probe/polling)11/11,215/215inputs,75.3719/100.1097ms;1/6 over100 by0.1097ms. Production clock/input/recovery/presentation retained. No numerical policy changes.
- Final tooling/source freeze3 stored at2026-09-25T14:16:19.4729297Z before final-full started. Policy JSON remains A01563BF7783F357E68700CB834C526D694DC4FFD3B6FE4BACE5922D9DF9CB65. Earlier pre-final freezes retained; no final-result tuning allowed.
- Final full/light20windows all p95<80ms (max79.2282ms),694/694inputs,22/22integrity checks,16,225heartbeats. Frozen final assessment FAILED: lightwindow9 recorded996.3762ms heartbeat during required producer/probe drain. Failure retained, no threshold/source changes or rerun.
- Final six-minute lifecycle16/16,395/395inputs,360.0463seconds,three locked-save observations,full milestones,durable PIREP,clean disposal/report guard. Four supplemental steady windows passed;final aggregate has only the maximum-stall failure. Overall NOT READY FOR7.24B. Stop; no production optimization or rollout.
- Verification inventory702regression/integrity assertions,701pass/1initial contention timing fail,plus separate failed final policy assessment. All14strict report guards clean;Map/recovery intentional negatives separately matched. Historical9replays/91,366samples retained,not rerun. Complete report and hashed evidence index stored locally.
