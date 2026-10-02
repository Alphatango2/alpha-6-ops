# 7.24C direct responsiveness acceptance implementation plan

> **For agentic workers:** Use superpowers:executing-plans inline. Preserve the existing worktree and all historical evidence.

**Goal:** Implement the user-approved direct acceptance model while retaining the exact historical heartbeat predicate as separately reported diagnostics.

**Architecture:** Extend existing explicit diagnostic entry points and add a fail-closed evidence assessor. Production runtime, map renderer, operational contracts and frozen T2 remain unchanged unless a demonstrated defect requires a separately documented minimal correction.

**Tech stack:** C# WPF/WebView2 integration diagnostics, Python evidence contracts, PowerShell normal verification.

**Spec:** `work/checkpoint-7.24c/direct-acceptance-20260929/request.txt` (user approval).

## Constraints

- Preserve `<500 ms` and `<=max(150 ms, baseline maximum+100 ms)` verbatim in meaning; historical failures remain failures.
- Direct failures block regardless of heartbeat. Missing direct evidence is not a pass.
- No new arbitrary timing budgets. Reuse the established 750 ms render-pressure ordering control and 1500 ms trusted-input correctness deadline; measure native-cadence telemetry before the next independent emission. Label their boundaries precisely.
- Frames mean MapLibre render/source readiness, not GPU/pixel presentation. Synthetic inputs are not installed physical certification.
- Frozen T2 and profile remain untouched; no installer or native simulator run; no 7.25/premium UI.

## Review focus

- A passing heartbeat must not mask a missed click, stale source or telemetry backlog.
- A failing heartbeat must remain reported without independently failing passing direct contracts.
- Correct-frame observation must include the current source geometry, not only an ACK/sequence.
- Dashboard refresh must sample current state after delay, not replay stale captures or mutate flight truth.
- Observers must not replace production callbacks or create a poller that makes a delayed background sentinel pass.

## Task 1: acceptance role and known-evidence assessor

Files: `scripts/direct_responsiveness.py`, `tests/test_direct_responsiveness.py`, diagnostic-only `MapContentionChecks.cs`.

- [x] Write assessor contracts: historical 4280.8266 ms handoff rejected; corrected native scheduling accepted; cached/out-of-bounds input rejected; missing evidence rejected; direct failure blocks with green heartbeat; historical heartbeat failure remains false while complete good direct evidence passes.
- [x] Run red, implement smallest assessor, run green.
- [x] Keep original heartbeat expression in diagnostics; emit an explicit non-blocking diagnostic record and surface it in final acceptance output. Keep positive measurement coverage as a functional assertion.

## Task 2: live direct diagnostic coverage

Files: new diagnostic partial `MapDirectResponsivenessChecks.cs`; diagnostic routing in `MapPerformanceChecks.cs`.

- [x] Use actual Operations, coordinator, independent 1 Hz source, WPF press forwarding, browser drag/select, browser receipt/ACK/render observation and existing native telemetry hook. Capture normal, interaction, navigation and reconnect processing with exact sample ordering.
- [x] Verify current render/source geometry and solar state after delivery; retain raw timings. Exercise known negative input and delayed browser/UI controls without modifying production.
- [x] Observe dashboard timer and deliberately delay a refresh under existing render pressure; check current clock/latest Operations state, no operational mutation and no stale replay.
- [x] Keep existing delivery-pressure, first-paint, restoration/resource and injected-stall checks; connect their results to the explicit direct acceptance report.
- [x] Prove negative controls fail at the intended endpoint, then run the unmodified production behavior successfully. Fix test setup errors openly; preserve every attempt.

## Task 3: complete verification and decision

- [x] Run fixed fresh sequence: direct diagnostics, full Operations/browser/input/lifecycle, scheduling and injected-stall detection, restoration/resources, terminal/recovery, full normal regression/build, 11 replays/100632 samples, supplementary evidence contracts.
- [x] Perform fresh review of this delta and acceptance boundaries; address substantive findings without changing budgets.
- [x] Verify source scope, frozen T2/profile/prior evidence hashes; archive new results separately.
- [x] Report each contract, exact heartbeat diagnostic, thresholds/provenance, dashboard behavior, remaining native gates, and T2 decision. Do not install.

## Execution rulings

- The user's approval and detailed implementation request authorize this plan; no duplicate approval round is needed.
- Continue in `codex/checkpoint-7.24c`; do not copy/revert the extensive inherited dirty state. No commit/push is requested.
- Execution ledger and raw results stay in `work/checkpoint-7.24c/direct-acceptance-20260929`; evidence must not be deleted at completion.

