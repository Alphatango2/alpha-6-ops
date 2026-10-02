# 7.24B INPUT R1 — physical-input correction handoff

**GO for normal upgrade and installed physical retest, with MSFS closed. Native MSFS validation remains on hold.** This is a separate corrected build, not a replacement of the original failed result. Classic remains default/fallback and Map V2 remains session opt-in.

## Original failure and bounded correction

The original installed build `S4-7.24B-20260925-46a1b304c3d6` failed physical left-drag pan. Its installer incident, subsequent successful upgrade, physical failure, A/B diagnosis and archived evidence remain intact. The isolated physical probe recorded 0/4 moving drags with ordinary bubbling and 3/3 with containment; this did not constitute an installed production pass. See [original diagnosis](checkpoint-7.24b-physical-input.md).

The new production change is one left-button-only bubbling handler on `WebView2CompositionControl` in `WebMapRenderer`. The SDK processes the press first; the handler then marks it handled so the surrounding WPF ScrollViewer cannot take focus and cancel the browser gesture. It does not synthesize duplicate pointer input, change capture, or intercept preview events. Right/middle presses, wheel, mouse-up/move and controls outside the map retain their existing routing. MapLibre left-drag pan and right-drag rotation semantics are unchanged.

No changes were made to FlightSessionCoordinator, flight or aircraft logic, recovery, map contracts, renderer selection, MapLibre configuration, global scrolling, database/storage identity or default settings. All 14 browser asset files and the Core DLL are byte-identical to the original freeze. This correction does not resolve or reclassify the earlier composition-scheduling findings.

## Frozen build

| Item | Identity |
|---|---|
| Validation ID | `S4-7.24B-INPUT-R1-20260925-62b8c871dd05` |
| Version / installer caption | `0.22.0 / 7.24B INPUT R1` |
| Source commit | `62b8c871dd0558dd3deed0baa5024521444b00f6` |
| Parent | Original frozen source `46a1b304c3d6e97b78098379332d7b197edf7aee` |
| Local source ref | `codex/checkpoint-7.24b-input-r1-source` |
| Freeze time | `2026-09-25T21:30:22.4261956Z` |
| Archive directory | `outputs/Alpha6OPS-Desktop-S4-7.24B-INPUT-R1-20260925-62b8c871dd05/` |
| Installer | `Alpha6OPS-Setup-0.22.0-7.24B-INPUT-R1.exe` |
| Portable ZIP | `Alpha6OPS-Desktop-0.22.0-7.24B-INPUT-R1-win-x64.zip` |

| Artifact | SHA-256 |
|---|---|
| Installer | `C04F5B179331C393FCCEF745FAD620E7395C4B0A0B01579DA152087AB53AAB13` |
| Portable ZIP | `F9055F9F01B729267B030D29A0DC630ACA86AB49C56C472606F2CF3C7800ACE4` |
| Desktop DLL | `E9EA02070DC9628CDFC38D7D005A9AE5EE4DD563757E5AF5C90460E416D58D85` |
| Core DLL (unchanged) | `2668EA4D5E4FE402C793C91BC786DDDA580D6AE34C5750682F21751F997496A6` |
| Payload manifest | `469201015EC1905AD7092B450EFA00BC2EBB636E6322B1FE672660DF7DF44D69` |
| Exact-byte source ZIP | `DDC6E7E56CD6166551BF2B7BDE61FE450F60CEF7978FB2D5FCACC9D443432BA0` |

The published apphost EXE is unchanged, so its hash or version alone cannot distinguish these builds. Verify the entire payload against the R1 manifest, especially the Desktop DLL. The original and R1 installer filenames and archive directories are distinct. The current working branch/index were not moved; no GitHub push was performed.

## Verification results

Normal production Release solution and packaging succeeded. Solution, migration, Map V2 and recovery builds reported **0 warnings and 0 errors**; packaging emitted no compiler warning/error diagnostics. The full migration runner used the archived R1 executable; migration and recovery harnesses loaded hash-verified copies of its Desktop/Core DLLs. Map and input-contract runners also used that frozen executable.

| Final suite | Executed | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Core/Desktop/dashboard/refresh/identity/lifecycle/tracking/activation/compatibility/fleet/replays | 1,871 | 1,871 | 0 | 2 |
| Map V2 host/input/lifecycle/fallback/contention | 137 | 137 | 0 | 0 |
| Map mailbox | 7 | 7 | 0 | 0 |
| Browser/map math | 7 | 7 | 0 | 0 |
| Browser input, scaling and resource contracts | 22 | 22 | 0 | 0 |
| Recovery | 21 | 21 | 0 | 0 |
| **Total** | **2,065** | **2,065** | **0** | **2** |

All **9 recorded flights** passed, processing **91,366 / 91,366** independently counted telemetry samples. Replay input hashes remained unchanged. Recovery completed 5,000 sequential durable replacements and existing interruption/corruption/concurrency checks. Copied-profile compatibility read 498 historical flights, 1,003 events and 12/12 gates; its source-profile hash check passed.

The two skipped checks are `NATIVE_CONNECTION_LIFECYCLE` and `NATIVE_RECONNECT`. No native simulator validation was attempted. The 22 browser-contract checks include three existing process-identity checks; their assertions, browser gestures and controlled scaling are automated evidence, not physical mouse or Windows DPI certification.

**16 new assertions** cover three view creation/disposal cycles: left-press containment, unchanged right/middle/wheel routing and outside-map parent behavior (15), plus one initialized-browser check proving the contained left press still arrives. A focused pre-fix run failed exactly the three left-containment checks; all 15 passed after the correction. Preliminary harness compilation/counter corrections and red/green runs are retained separately and excluded from final-suite totals. No production regression was changed to force a pass.

Report audit passed: 7 baseline fixture reports, 16 injected renderer-failure reports and 12 recovery-failure reports matched the policy declared before this run; input contracts produced none. There were **0 unmatched reports**, no orphan report text and no nonempty error outputs. This run's predeclared policy includes the already-known deliberately thrown subscriber fixture. The original 7.24B policy mismatch and invalid recovery-launch attempt remain preserved in their original evidence; this is not a retroactive waiver.

Independent read-only review of the immutable original-to-R1 patch found no blocking or important findings. It confirmed the narrow scope and emphasized the remaining physical gate.

## Integrity and preservation

- Corrected portable directory, installer extraction and ZIP each matched **514/514 files**, with no extra/missing payload files. Both package hashes and all 222 frozen source hashes matched after testing.
- Original frozen portable payload **514/514**, its eight handoff artifact hashes, and the still-installed original payload **514/514** remained unchanged. Original evidence was not rewritten.
- A pre-reinstall backup captured and hash-verified **217 saved-data files**, excluding only regenerable MapCache, under `work/checkpoint-7.24b-input-correction/pre-reinstall-profile-backup/`. Source files were read-only. This current snapshot is distinct from the earlier upgrade's 216-file snapshot.
- Final preflight observed no OPS/MSFS process and no process/user/machine Map V2 startup override. R1 has **not been installed** by the agent.

## Changed files and retained evidence

Exactly four source/package inputs differ from the original frozen source:

1. `src/Alpha6Ops.Desktop/MapV2/WebMapRenderer.cs` — minimal left-press containment.
2. `src/Alpha6Ops.Desktop/MapV2/MapV2Checks.cs` — explicit routed-input/SDK-forwarding regressions.
3. `packaging/PreviewSetup.cs` — distinct R1 caption only.
4. `packaging/Desktop-Quick-Start.txt` — distinct R1 identity and physical-test hold instructions.

New documentation consists of this report, the [installed retest checklist](checkpoint-7.24b-input-r1-retest.md), compact evidence under `evidence/7.24b-input-r1/`, and the implementation plan. The checkpoint overview links to this separate supplement. Previous unrelated working-tree changes were preserved.

Full raw results, logs, isolated test state, development attempts, source bundle and exact-byte source archive remain under `work/checkpoint-7.24b-input-correction/`. The frozen output directory contains source archives, read-only installed-payload verifier, report, retest checklist and handoff hashes. Compact evidence includes suite summaries, provenance, immutable patch, red/green results, policy/guard and integrity results; private profile copies are not included in documentation or the distributed package.

## Stop point

**Implementation and automated preparation complete. Installed physical retest pending.** Use the exact R1 normal installer, verify installed identity and saved data, then perform every item in the retest checklist with MSFS closed. Selection that cannot be exercised is unavailable, not passed. Any failed/unavailable required physical item keeps the gate open. Native testing resumes only after the installed input/focus gate is explicitly reviewed and passed. No default enablement or next checkpoint is authorized by these results.
