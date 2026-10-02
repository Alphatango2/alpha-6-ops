# 7.24C terminal shutdown implementation

User-authorized specification: `work/checkpoint-7.24c/terminal-implementation-20260928/user-spec.txt`. Continue the existing dirty checkpoint branch; preserve all prior frozen/native evidence. Implement inline, then independent final review. The prior written architecture and current explicit implementation instructions authorize this work without another approval round.

## Design and tasks

1. Core: immutable per-engine terminal intent/combustion evidence and enhanced/limited/invalid evaluation, separate from EngineEvidenceTracker. A shared BlockInEligibilityPolicy consumes observed ON, TaxiIn, destination, stopped/braked telemetry, connection identity and freshness. Manual confirmation rechecks the same policy and ten-second parked interval. Add targeted tests first; prove RED then GREEN.
2. Native boundary: adapter registry, first Fenix A321 IAE mapping using the captured master channels. Native optional requests are isolated from primary SimConnect errors and retain per-field freshness/errors. Positive running/master-ON qualification is required per connection before accepting cutoff; no zero-default support inference. Keep generic classifier intact. Replay the preserved native capture through the adapter.
3. Runtime: live coordinator always opts into the new policy; PhaseDetector receives a nullable explicit terminal decision so historical samples without the new policy retain their original semantics. Keep the existing three-second phase confirmation. Reset pending completion/manual confirmation on interruptions/recovery and revalidate on command execution. Add normalized evidence and decision provenance to diagnostics. Test actual coordinator/history/PIREP, not UI mocks.
4. Compatibility/regressions: adapt synthetic live completion fixtures to supply explicit normalized test evidence (never infer enhanced intent in production from enginesRunning). Preserve every existing lifecycle/history assertion. Run Release, Core, terminal/native-capture, recovery, full baseline, 9 historical journals/91,366 samples, JST776, unchanged map contention, restoration/resource and telemetry checks.
5. Review/document/package: independent focused review; repair demonstrated issues. Document adapter extension, validation scope, conservative qualification and fallback. If green, freeze a separately labeled local candidate with hashes and exact parked checks; do not overwrite C1 or install automatically. No airborne validation or premium UI.

## Safety decisions

- Legacy nullable policy is for old recordings/Core replay semantics only. New live coordinator always supplies an explicit decision, including for recovered flights.
- Unsupported aircraft receive guarded manual fallback, not automatic completion from combustion or mechanical Off alone.
- Enhanced evidence that is stale, missing, contradictory or unqualified stays invalid; it must not silently become unsupported/manual-permitted.
- Qualification after a new connection requires positive native master-ON/running observations. A reconnect after all masters are already OFF cannot reuse prior intent or a partial timer. Record this conservative limitation explicitly.
- Destination resolution is shared by the live policy and arrival-mismatch check; catalog airport is authoritative when available, route endpoint is the fallback. Never use manual confirmation to bypass proximity.
- Existing ON/IN and completed history/PIREP identity remain authoritative; no new persistence schema migration. Stored evidence is diagnostic and never restores pending completion authority.

## Review focus

Startup zero defaults; time/identity/reconnect freshness; duplicate samples preserving debounce; direct manual command bypass; four-engine/missing-engine coverage; historical fixtures accidentally opting into new authority; native exception routing; exact history/PIREP once across restart.

## Ledger

- Entry status/diff/source hashes preserved. Original native capture remains immutable. No implementation started before inspecting current lifecycle, recovery, replay and native packet boundaries.
- All five tasks completed. Core policy/evidence, adapter/native boundary, coordinator reset/manual guards, versioned replay compatibility and tests implemented. Independent review's three findings reproduced RED and corrected; final focused checks 132/132.
- Final acceptance: 2,579 passed / 0 failed / 2 native skips; 9 historical replays / 91,366 samples plus JST776 / 6,145. Map scheduling limits unchanged, full map 197 and restoration 157 pass. Original capture/C1/mechanical-classifier hashes preserved.
- The final packaged smoke attempt had a transient Windows dashboard-state replacement denial. Preserved failure; same binary resumed only incomplete stages in a fresh directory and passed. No production workaround introduced.
- Separately froze S4-7.24C-T1-20260928-00e3f628b90e with 514 payload checks, source/ZIP/embedded installer hashes and a parked test procedure. Not installed. Stop for user's installed parked evidence; no airborne or next checkpoint work.
