# 7.24C — JST771 review and bounded capability correction

User authority: preserved `docs/flight-tracking/evidence/7.24c-jst771-20260929/user-spec.txt` plus explicit clarification that A320/A321 variant choice was intentional. No 7.25 or premium UI work.

## Read-first findings (before production edits)

The original ZIP and entry working-tree/source hashes are preserved. All six screenshots, both export forms, supplemental local connection journals and read-only copies of history/profile files have been correlated. Detailed analysis is in `work/checkpoint-7.24c/jst771-review-20260929/{recovery-review,route-descent-review,fenix-capability-review}`.

- T1 enhanced shutdown was NEVER active, including before either interruption. Exact `FenixA321 IAE WF SC` selection rejected the intentionally chosen `FenixA320 IAE WF`.
- Same flight/session, assignment, OUT/OFF, route and persisted event history survived both interruptions. One completed row and one PIREP exist. Four fresh engine-running confirmations are repeated across connections; no duplicate OOOI. Final cold-start no-resurrection is not proven by the supplied evidence.
- Both combustion signals OFF at simulator 23:01:46.3106231; guarded manual confirmation available 10.009 seconds later. Manual confirmation produced effective IN 23:03:55.3247909. Raw master commands were not retained by the unsupported adapter, so their exact times cannot be inferred.
- Route progress gate passes. Separate genuine issues: filed FL350 versus actual FL330 blocks cruise/TOD; restarting resets the ETA blending clock (22:50 to 23:10). Document these separately; no unrelated production correction in this adapter patch.
- Installed Fenix version 2.4.0.4720 has 20 A319/A320/A321 presets sharing the two master bindings through the same cockpit. This is configuration evidence, not native qualification of all variants.

## Minimal correction and test sequence

1. Resolve candidate capabilities from known installed Fenix manifests, shipped presets, twin-jet/type configuration and shared cockpit provenance. Keep reviewed version pin; reject absent, ambiguous or inconsistent provenance. A320 base package must work without the A319/A321 expansion. Avoid display-name prefix authority.
2. Bind the family adapter to the resolved title/connection. Require fresh finite binary master and combustion signals, positive engine count 2 and fresh running observations for both required engines. Do not persist qualification across connections; wrong identity/engine coverage withdraws it. Preserve generic mechanical classifier, coordinator, thresholds and manual fallback.
3. Run failing regressions first: real package-selection behavior, default/missing/invalid/stale signals and JST771 sequence through the actual adapter, coordinator, recovery store and history database. Prove one IN/PIREP, no partial shutdown completion, no stale authority after either interruption.
4. Release build, full baseline, 9 historical replays/91,366 samples, JST776, JST771 historical evidence assertions, affected terminal/recovery/persistence/map scheduling/restoration checks. Preserve original failed native evidence and T1 package separately.
5. Deliver full requested review and a minimum parked variant/reconnect/restart capability procedure. Any corrected candidate remains unproven natively until that retest; the historical JST771 failure is not relabeled PASS.

Production boundaries: adapter/capability catalog and SimConnect optional-evidence construction only. No Map V2 scheduling change, Core classifier change, coordinator restructuring, presentation workaround, package auto-install or profile migration.
