# Checkpoint 7.24B — pre-installation report

**Historical comparison reference (not validation evidence):** [JST928/JST941 field and lifecycle review](flight-tracking/historical-reference-20260923-24.md) documents the four user-supplied September 23–24 JSON/JSONL files. Its two flights and 24,981 historical samples are excluded from all current native/replay/sample pass totals. It changes no 7.24B gate status and authorizes no production change.

**Latest gate — LAYOUT R3 physical reconnect and workspace switching PASS:** [native result and log audit](map-v2/checkpoint-7.24b-native-r3-results.md) records three orderly connections, 68 new parked telemetry samples, no logged errors/new reports and 514/514 installed hashes matching. **GO for [one controlled validation flight](map-v2/checkpoint-7.24b-flight-r3.md) after accepting a current, correctly dated dispatch release.** No new build or default change. Actual stale-callback rejection/native handle identities are not logged and remain evidence limitations; full flight, recovery, PIREP/history and final classification remain pending. The preceding HOLD/FAIL statuses below are preserved historical records.

**Current gate — LAYOUT R3 physical stability PASS:** the user reports Classic PASS, Map V2 PASS and disconnected stability PASS, with no performance issue during connected/disconnected operation or renderer switching. [Separate physical result and session audit](map-v2/checkpoint-7.24b-layout-r3-physical.md): 514/514 installed hashes match, 40 parked native telemetry samples, one normal session close, no logged error or new reporter file. **GO for [controlled parked native reconnect](map-v2/checkpoint-7.24b-native-connection-r3.md); full flight remains HOLD.** Classic remains default, V2 opt-in. No application or package change was made. Earlier failed observations below remain failed historical evidence, not current R3 results.

## Preserved preceding gate updates

**Latest installed gate:** [LAYOUT R3 identity/data verification](map-v2/checkpoint-7.24b-layout-r3-installed.md) PASS — 514/514 payload files and 14/14 frozen companions match; 224/224 saved-data files are unchanged. GO for disconnected physical checks and, only if those pass, one parked Classic → V2 comparison within a single connection. Stop on flicker. Broader reconnect cycles and real-flight validation remain HOLD; R3 physical stability is pending and R2A's failed native observation remains preserved.

**LAYOUT R3 correction ready:** [Investigation and verification report](map-v2/checkpoint-7.24b-layout-oscillation.md) identifies repeated replacement of unchanged flight-table rows as the reproduced WPF scrollbar/size oscillation. The narrow correction passes 2,097 checks (0 failures; 2 native skips), 9 replays and 91,366 samples. Separate frozen build `S4-7.24B-LAYOUT-R3-20260927-666f495dce7e` is GO for normal upgrade and hash/data verification only. [Staged installed retest](map-v2/checkpoint-7.24b-layout-r3-retest.md). R2A's native physical failure remains preserved; R3 physical testing is pending. No reconnect cycles or validation flight yet.

**Current gate: native validation HOLD — physical Operations stability FAIL.** Connected live telemetry causes repeating whole-workspace layout/scrollbar flicker with both Classic and Map V2; disconnect immediately stops it. See [preserved physical evidence and investigation](map-v2/checkpoint-7.24b-layout-oscillation.md). The earlier input and terminator passes below remain valid historical results, but their GO for the next native gate is suspended. Do not run reconnect cycles or a validation flight.

**Latest gate:** [VISUAL R2A physical retest](map-v2/checkpoint-7.24b-visual-r2a-physical.md) PASS for all eight input checks and the explicitly confirmed day/night first frame, at 3440×1440 / 100% scaling. Installed identity still matches 514/514; no new reporter files. GO for the [parked native connection/reconnect test](map-v2/checkpoint-7.24b-native-connection-r2a.md). Both native gates and the real-flight portion remain pending; no default change or 7.25 work.

**VISUAL R2A installation follow-up:** [Installed identity and saved-data verification](map-v2/checkpoint-7.24b-visual-r2a-installed.md) now PASS: normal upgrade confirmed, 514/514 payload hashes match, 219/219 saved-data files unchanged and no new reporter files. Proceed with the MSFS-closed physical visual/input retest only. Native MSFS remains HOLD.

**Latest physical result:** INPUT R1's eight input checks were reported PASS by the user. Returning from Flight Tracking to Operations exposed a separate first-paint day/night flash; that visual gate is FAIL and native MSFS stays HOLD. Installed R1 was rechecked after resumption: 514/514 payload files match. The original screenshots and both earlier frozen packages remain preserved.

**Separate visual correction:** [First-paint investigation and VISUAL R2A report](map-v2/checkpoint-7.24b-first-paint.md) records the narrow reveal-ordering fix, preserved R2 candidate fixture failure, R2A verification, and exact package identity. [Installed VISUAL R2A retest](map-v2/checkpoint-7.24b-visual-r2a-retest.md) is the next physical gate. Classic stays default and Map V2 remains opt-in. No native MSFS test or later checkpoint is authorized by this handoff.

## Preserved earlier handoffs and original-build report

The statuses below describe the original installation and then the INPUT R1 handoff at the times recorded. The latest status is above; no earlier failed result has been overwritten or converted into a pass.

**Separate R1 correction handoff:** [7.24B INPUT R1 report](map-v2/checkpoint-7.24b-input-correction.md) records the authorized left-press containment fix, new freeze and automated results. Installed R1 physical retest is pending with MSFS closed. The original-build report and failed physical result below are retained as historical evidence; frozen original artifacts remain unchanged.

**R1 installation follow-up:** [Installed identity and saved-data verification](map-v2/checkpoint-7.24b-input-r1-installed.md) passed after the user reported the update: 514/514 installed payload hashes match; saved flight data is preserved. Physical input remains pending and native MSFS remains on hold.

**Current status: normal upgrade and installed identity PASS; physical left-drag pan FAIL; native validation HOLD.** The first upgrade failed while inspecting retained PID 23988. Windows restart cleared it, and the user completed the unchanged installer's normal upgrade. All **514 installed payload files** match the frozen build; **216 saved-data files** were unchanged immediately after upgrade. After manual Map V2 opt-in, physical left-drag failed with MSFS closed. An isolated physical A/B trace identifies a WPF parent-scroll-container focus conflict; no production fix has been applied. See `docs/map-v2/checkpoint-7.24b-physical-input.md`. This is not completion of 7.24B.

The user explicitly authorized a controlled installed pilot after the automated investigation. Historical 7.24A, 7.24A.1 and 7.24A.2 NOT READY decisions remain unchanged. No scheduling fix, operational change, default enablement, globe work, workspace migration or distribution rebranding was made.

## Frozen build and provenance

| Item | Value |
|---|---|
| Validation build | `S4-7.24B-20260925-46a1b304c3d6` |
| Version | `0.22.0` |
| Exact source commit | `46a1b304c3d6e97b78098379332d7b197edf7aee` |
| Source tree | `7b0625a5efd3ef681161cdf0804f33b368233053` |
| Local snapshot ref | `codex/checkpoint-7.24b-validation-source` |
| Parent/current working HEAD | `020bda9f7d1dd7e16c6ecfeec67c4e4e007c6215` |
| Frozen package | `outputs/Alpha6OPS-Desktop-S4-7.24B-20260925-46a1b304c3d6/` |
| Freeze time | `2026-09-25T18:19:15.4148938Z` |
| Default / fallback | Classic |
| Map V2 | Session opt-in; process/user/machine `SIERRA4_MAP_V2` overrides absent |

Before building, six existing current/prior binaries and packages were copied and hash-verified. A separate local Git snapshot, bundle, exact-byte source ZIP and 222-file manifest were archived before the build. The current branch and user's index were not moved. The exact-byte archive supplements Git newline normalization. No push was performed.

Immediately after normal packaging, the entire 514-file publish payload, installer and portable ZIP were copied to the unique frozen directory, hashed and used for application tests. Later harness builds could only replace ordinary build output. Migration and recovery harnesses loaded hash-verified copies of the frozen Desktop/Core DLLs. The final installer extraction, ZIP entries and frozen portable directory each match all **514/514** payload hashes with no extra or missing files. This was workspace extraction only, not installation.

| Artifact | SHA-256 |
|---|---|
| `Alpha6OPS.exe` | `766046B7277B30E53E93A545A282366DC5F60A720EC0A8C3D8B83355CD6B3D9E` |
| `Alpha6OPS.dll` | `2E21728391F7A8B724175EE9A18AF85035D950444C6905715724121567131265` |
| `Alpha6Ops.Core.dll` | `2668EA4D5E4FE402C793C91BC786DDDA580D6AE34C5750682F21751F997496A6` |
| Installer `Alpha6OPS-Setup-0.22.0-7.24B.exe` | `BAB3DFC44BF4506ADEA62CAFBBF748B25602AF0386DD89A198A541A77D2C1356` |
| Portable ZIP `Alpha6OPS-Desktop-0.22.0-7.24B-win-x64.zip` | `69B9265C2F740B3283D39CD249F738116CC45FEDB6BB11D8BBBA9A5517F217BE` |
| Payload manifest | `99E5C8A1A943E65EFD428607F802796DC376F3A2FE3F4DAAF8AA4D4762BDE72D` |
| Exact-byte source ZIP | `46FE73E108DE7181493FDC7BC1D50140A7C35434F590D31107F37E61BE5FADCF` |
| Source manifest | `5E9C7CB3F1A09F8E056600C9710F30CDD24E3F2390B70345764AB96D9EF5BB54` |

Every Map V2 browser asset hash is recorded in `validation-build.json` and `payload-manifest.json` beside the installer. The application DLL is byte-identical to the archived final 7.24A.2 DLL. The normal published executable/apphost differs from the ordinary bin executable, as archived separately; the delivered published executable was tested directly. WebView2 runtime detection and classic fallback remain intact.

## Pre-install verification

Release solution: **PASS, 0 warnings, 0 errors**. Normal production packaging: **PASS**, installer and portable ZIP generated, no compiler warning/error diagnostics. Migration, Map V2 and recovery harness builds: **PASS, 0 warnings, 0 errors**.

| Suite | Executed | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Existing full migration baseline (Core/Desktop/Dashboard/refresh/identity/lifecycle/tracking/activation/compatibility/fleet/replays) | 1,871 | 1,871 | 0 | 2 |
| Map V2 host/lifecycle/fallback/contention | 121 | 121 | 0 | 0 |
| Map mailbox | 7 | 7 | 0 | 0 |
| Browser/map math | 7 | 7 | 0 | 0 |
| Recovery, corrected apphost launch | 21 | 21 | 0 | 0 |
| **Final selected runs** | **2,027** | **2,027** | **0** | **2** |

Nine recorded-flight validations processed **91,366 / 91,366** eligible telemetry samples, independently matched to raw records. Input journal hashes remained unchanged. Recovery completed 5,000 sequential durable replacements plus the existing concurrency, interruption, corrupt/legacy schema and coordinator failure checks. Map contention retained identical operational/recovery/PIREP/history hashes and unchanged connection generation; this run's stressed dispatcher heartbeat maximum was 245.4229 ms. This single passing result does not erase earlier intermittent failures or establish physical responsiveness.

The two skipped checks remain **NATIVE_CONNECTION_LIFECYCLE** and **NATIVE_RECONNECT**. Synthetic tests do not satisfy them. Physical DPI, actual mouse/keyboard, installed startup and real flight are also pending, not silently counted as passes.

No new production regression tests were required: this checkpoint reused existing suites and added local package/source identity verification. Package hash comparisons and report guards are reported separately, not inflated into assertion counts.

## Preserved verification issues

1. **Recovery launcher error, corrected without rebuilding the package.** The first private launcher used `dotnet Alpha6Ops.RecoveryChecks.dll`. Four subprocess checks failed because the harness uses `Environment.ProcessPath` to spawn itself; that launch made its child target `dotnet.exe` with harness-only arguments. Evidence remains in `work/checkpoint-7.24b/recovery/` (17/21 passed). Launching the already built `.exe`, matching the normal runner's apphost behavior, passed all 21 in `recovery-apphost/`. No application/test source or frozen payload changed. Across both recovery attempts and the other suites, **2,048 assertion executions: 2,044 passed, 4 failed; 2 native checks skipped**. Final-run totals above exclude the retained invalid-launch attempt explicitly.
2. **Expected-report inventory omission, retained as an audit discrepancy.** The predeclared six-report baseline policy missed the existing `OBSERVER_FAILURE_ISOLATED` test's deliberately thrown `Diagnostic subscriber failure`. The immutable guard therefore remains **false with one unmatched report**; it was not relaxed after execution. Source at `tests/Alpha6Ops.MigrationChecks/Program.cs:281`, already present in parent commit `020bda9`, establishes the deliberate injection and exact exception. Its report is confined to `preinstall/migration/application-owner/`, with source `flight_snapshot_subscriber`, and that test passed. The remaining baseline reports, 16 deliberately injected map reports and 12 recovery reports per attempt match the predeclared rules. No orphan report text or nonempty error-output files were found. **No unexpected production fault was identified**, but the guard discrepancy is not described as an entirely green report gate. It is an incomplete initial expected-fixture inventory, not a new application error whitelisted after the fact.

The original policy, guard result, first recovery attempt, launcher correction and source evidence remain available for independent review. No user profile writes or data repairs were made to obtain a pass.

A fresh independent read-only review found no material blocker to the controlled Part 4 installation handoff. It independently verified source/package/binary hashes and the retained issue explanations. Its request for explicit Map V2 executable provenance was addressed in `execution-provenance.json` and `map-process-provenance.json`. This recommendation does not turn the report guard green or close any native gate.

## Thirty-field checkpoint report (installation-dependent fields pending)

| # | Requested result | Evidence/status at Part 4 |
|---:|---|---|
| 1 | Exact source commit | `46a1b304c3d6e97b78098379332d7b197edf7aee`; archived source/bundle/manifest. |
| 2 | Archived binary hashes | Listed above; full payload manifest retained. |
| 3 | Installer/package hashes | Listed above; all extracted payload bytes match tested publish. |
| 4 | Installed validation version | **PASS**, 0.22.0 / `S4-7.24B-20260925-46a1b304c3d6`, verified through all 514 payload hashes. |
| 5 | Installation | First attempt **FAILED** at process inspection for PID 23988; after restart, normal frozen-installer upgrade **PASS** with user completion screenshot and installed identity verification. |
| 6 | Existing-profile compatibility | All 216 saved-data files unchanged after actual upgrade; pilot profile loads, saved gate/settings JSON readable. Earlier copied-profile read: 498 flights, 1,003 events, 12/12 gates. User-visible logbook/gates/settings confirmation **pending**. |
| 7 | Installed startup | Installed process/window and Operations/classic/disconnected accessibility state **verified**; 0 new reports. Launcher duration about 1.862 s, not exact render-ready timing. Visual confirmation and Map V2 opt-in baseline **pending**. |
| 8 | Physical map input | **FAIL in installed build**: left-drag interrupted by WPF parent focus. Physical isolated A/B: A 0/4 pans, B 3/3 pans; user confirms A fails/B works. Left-click + and wheel zoom work. Right rotation is a separate MapLibre default. Production correction/retest pending. |
| 9 | Physical DPI/window/monitor | **Pending** actual resolution/scaling/monitor checks. |
| 10 | Native connection lifecycle | **OPEN / pending MSFS**. |
| 11 | Native reconnect | **OPEN / pending MSFS**, perform parked before primary flight. |
| 12 | Real flight | **Pending selection and execution**; no synthetic flight substituted. |
| 13 | Real telemetry/map | **Pending real SimConnect**. |
| 14 | Human responsiveness | **Pending**, use A/B/C/D observation scale. |
| 15 | Visible hitch timeline | **Pending**; no real-user observations invented from dispatcher timings. |
| 16 | Flight phases/milestones | **Pending native flight**; historical replay evidence is separate. |
| 17 | Recovery/restart | Automated 21/21 pass after launcher correction; **native restart pending** in a safe separate session or chosen safe point. |
| 18 | PIREP | Automated behavior intact; **real submission pending**. |
| 19 | History/logbook | Copied historical data readable; **new native flight durability pending**. |
| 20 | Classic fallback | Automated failure/lifecycle tests passed; **installed manual switch pending**. |
| 21 | Longer map observation | **Pending**, including duration, memory/browser trend and visible degradation. |
| 22 | Post-flight data | **Pending** duplicate/recovery/assignment/profile audit. |
| 23 | Report/log audit | Pre-install reports audited as above; installed startup has **0 new reporter files** through 18:55:42 UTC. Native/post-flight audit pending. |
| 24 | Unexpected errors | **Physical left-drag pan failure**, isolated to host mouse bubbling/focus; not a crash or native failure. Prior installer preflight Access denied resolved by reboot, retained separately. Earlier automation retains one expected-report inventory omission and four corrected test-launcher failures. |
| 25 | Native gates closed/open | **0 closed, 2 open**. |
| 26 | Remaining risks | Confirmed host focus conflict blocks installed physical panning; correction/retest pending. Separate intermittent composition scheduling delay unresolved; native/physical gaps remain. Retained 7.24A.2 515.8971 ms contention failure remains historical evidence. |
| 27 | Map V2 classification | **Provisional REMAIN OPT-IN**; final 7.24B classification deferred until installed evidence. |
| 28 | Classification evidence | User-authorized pilot, frozen build identity and passing final automation support controlled installation only. Open native/physical/real-flight gates prevent candidate-for-default approval. |
| 29 | Globe observations | **Pending user evidence**; no globe changes made. |
| 30 | 7.25 recommendation | **HOLD** until 7.24B installed/native report is complete and reviewed. No 7.25 implementation started. |

## Files and artifacts

New 7.24B production-input changes: `packaging/PreviewSetup.cs` (pilot caption only) and `packaging/Desktop-Quick-Start.txt` (validation identification/instructions). All 222 source inputs match their frozen manifest after verification. Pre-existing A-series dirty source, documents and diagnostics remain intact; no unrelated changes were reverted.

New documentation: this report, `docs/map-v2/checkpoint-7.24b-spec.txt`, `docs/map-v2/checkpoint-7.24b-installed-pilot.md`, `docs/superpowers/plans/2026-09-25-checkpoint-7.24b.md`, and compact `docs/map-v2/evidence/7.24b/` records. The earlier `checkpoint-7.24b-pilot-checklist.md` remains a historical A-series artifact and was not rewritten.

Raw test output, isolated profile copies, source archive/bundle, runner adaptations, retained failures and audit helpers are in `work/checkpoint-7.24b/`. The frozen package directory includes `validation-build.json`, `payload-manifest.json` and the read-only `verify-installed-build.ps1` companion. Do not rebuild or replace that package midway through the pilot.

## Handoff

Installation and installed-file identity pass. Part 6 fails due to left-drag panning; native testing remains on hold. Physical A/B tracing identifies the parent ScrollViewer stealing map focus; the narrow host-boundary correction should be made and verified in a separately frozen validation revision before continuing 7.24B. No production fix or replacement package was applied in this diagnosis. The original frozen build remains intact. Follow `docs/map-v2/checkpoint-7.24b-physical-input.md` for evidence and corrective scope. Default settings remain unchanged throughout.
