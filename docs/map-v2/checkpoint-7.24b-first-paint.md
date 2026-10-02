# 7.24B — first-paint visual defect

## Preserved installed result, before correction

The user reported all eight INPUT R1 physical interaction checks PASS: left pan, wheel/buttons, available-object selection, right rotation, outside scrolling, focus escape/return, resize/minimize/restore, and workspace/menu interaction. The last item carries a separate visual defect: returning from Flight Tracking to Operations briefly displays an incorrect day/night terminator, then snaps to the current lighting state. Interaction success does not close this visual gate. Native MSFS testing remains HOLD.

Installed build `S4-7.24B-INPUT-R1-20260925-62b8c871dd05` was reverified at 22:32:16 UTC: **514/514 matching payload files**. Both user PNGs were copied byte-for-byte and SHA-256 verified into `evidence/7.24b-first-paint/` before any implementation change. Their shell clocks read approximately 22:10Z and 22:31Z: these are examples of the two states, not consecutive frames or a measurement of the transition duration. The roughly one-frame/few-millisecond duration is the user's observation.

## Investigation and bounded design

Current startup calls `updateSolar([1,0,0])`, sends the browser initialization handshake, and `WebMapRenderer.InitializeAsync` makes the view opaque before the projection pump supplies the current MapSnapshot. Updating the night image is asynchronous. Therefore browser initialization and receipt of a snapshot alone are insufficient proof that the correct lighting has been painted. This is a placeholder first-render ordering defect; the screenshots alone do not prove a cache or stale operational state defect.

Correction scope: use the existing first snapshot acknowledgment to gate the initial reveal, after MapLibre renders that snapshot and reaches idle. Remove the placeholder lighting initialization. Preserve subsequent acknowledgment cadence, solar generation, snapshots, interpolation, the coordinator, rendering architecture, R1 input handling and classic-default policy. Keep the opening indication until visible readiness. Existing bounded bridge failure/restart handling remains applicable.

Regression intent: fail if a fresh renderer becomes opaque/Ready before a snapshot, or before its image data and first rendered frame complete. Exercise initial startup, delayed first data, navigation away/back, minimize/restore, classic-to-V2, disposal/recreation and existing failure/restart paths. Compare authoritative state before/after presentation actions. Keep automatic observations separate from the later installed physical retest.

Ruling: the user explicitly requested the narrow first-paint correction and its lifecycle validation. Continue within that authorization without an extra design approval. Preserve both existing frozen builds and the current installed physical evidence. Any new validation package must be a separate freeze; no in-place installed edits and no MSFS launch.

Primary references: [MapLibre idle event](https://maplibre.org/maplibre-gl-js/docs/API/type-aliases/MapEventType/), [pinned 6.11.2 image-source implementation](https://github.com/maplibre/maplibre-gl-js/blob/v6.11.2/src/source/image_source.ts). These document the distinction between asynchronous image loading and the completed render.

## Implemented correction and scope

This is safely addressable within 7.24B's initialization boundary. Browser initialization now establishes only the bridge. The first valid snapshot installs its actual lighting image; its acknowledgment waits for that image to load and the first MapLibre idle frame. Only that validated acknowledgment reveals the WPF view and raises visible Ready. The host retains its opening indication until then. The placeholder sun is no longer installed. Subsequent snapshots retain the existing acknowledgment cadence.

INPUT R1's left-button containment is unchanged. Solar calculations, snapshot contracts, interpolation, FlightSessionCoordinator, operational flight logic, default renderer, MapLibre input semantics and page scrolling are unchanged. No dependencies were updated. The existing three-second acknowledgment failure/restart/fallback deadline remains in force.

Seven source/package files differ from INPUT R1: `MapV2/WebMapRenderer.cs`, `MapV2/Web/map.mjs`, `OperationsMapHost.cs`, `MapV2/MapV2Checks.cs`, new `MapV2/MapV2Checks.FirstPaint.cs`, and the two packaging identification/instruction files. New regression coverage adds 15 real-browser first-paint checks and 10 host lifecycle/state-purity checks.

## Preserved development and verification results

- The pre-fix focused run (`red-confirmed`) failed 12 of 15 assertions against INPUT R1. The initial runtime correction passed all 15; its isolated host lifecycle run passed all 10.
- The first private helper initially shut down its WPF application after closing its first window; its later-cycle initialization timed out. A separate early lifecycle helper attempted to read a renderer before navigation had created it. Both helper attempts and their corrections remain under `work/checkpoint-7.24b-first-paint/`. Neither required a product behavior change.
- Authentication interrupted the initial review before any findings. Resuming preserved the source freeze and existing evidence; the independent review subsequently completed.
- Candidate **S4-7.24B-VISUAL-R2-20260925-acc4f98f3a08** was built, archived and never installed. Its baseline passed 1,871 checks; all nine replays covered 91,366 samples. Its full map suite executed 162 checks: 160 passed, **2 failed**, both the new fixed-250-ms observation. Mailbox and math checks each passed 7. The input-contract/recovery sequence stopped at that failure. These results are retained unchanged.
- A focused trace with MainWindow present reproduced the failing observation: **loads=0, sequence=0, opacity=0, Ready count=0**. At 250 ms the first snapshot had not arrived and the renderer was correctly concealed. Correct-lighting reveal and subsequent-frame assertions passed. This identified a fixed-delay assumption in the new fixture, not evidence of an early visible frame.
- Final **VISUAL R2A** replaces the test's timer with an explicit image-loading barrier: wait for actual load entry, verify concealment, release the image, then verify the loaded current-lighting frame precedes Ready. Early Ready still fails. The focused corrected fixture passed 15/15. Only this diagnostic fixture and package labels changed relative to R2; production runtime code is identical. R2's failed package/results were not replaced or relabeled as passes.

Raw evidence is retained in `work/checkpoint-7.24b-first-paint/` (earlier candidate/development) and `work/checkpoint-7.24b-first-paint-r2a/` (final candidate). Screenshots and installed INPUT R1 identity remain in `evidence/7.24b-first-paint/`. No automated result supersedes the original physical failures.

## Final candidate identity

Validation ID: **S4-7.24B-VISUAL-R2A-20260927-87fa792a7c29**, version **0.22.0**, source **87fa792a7c2924b9c6684a47ad76a6d6c053591b**. The source snapshot contains 223 files; the working branch and index were preserved. The separate package lives at `outputs/Alpha6OPS-Desktop-S4-7.24B-VISUAL-R2A-20260927-87fa792a7c29/`.

| Artifact | SHA-256 |
|---|---|
| `Alpha6OPS-Setup-0.22.0-7.24B-VISUAL-R2A.exe` | `0D7BEFD881B230213EC01CE5FD601A5141711C2F7BF9FDBEBD0AB70A5ABDDAF9` |
| Portable ZIP | `EE60752AD580C7D6DC41455CA87C3BA39ABC0856EBFBEE31A3053E1392E8B079` |
| `Alpha6OPS.dll` | `BE0FD396DF307795654E1CEF50755BB6FE22EC9F3780D5E8473694D2F892270C` |
| `Alpha6Ops.Core.dll` — unchanged | `2668EA4D5E4FE402C793C91BC786DDDA580D6AE34C5750682F21751F997496A6` |
| Apphost EXE — unchanged | `766046B7277B30E53E93A545A282366DC5F60A720EC0A8C3D8B83355CD6B3D9E` |

## Final frozen-build verification

The normal production Release solution and installer packaging succeeded. Production and verification builds reported zero warnings and errors. The checks below ran against the frozen R2A application; migration and recovery harnesses loaded hash-matched frozen assemblies.

| Final selected suite | Executed | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Core/Desktop/migration/compatibility/fleet/replay baseline | 1,871 | 1,871 | 0 | 2 |
| Map V2 input, first paint, lifecycle, fallback, load and contention | 162 | 162 | 0 | 0 |
| Map mailbox | 7 | 7 | 0 | 0 |
| Browser/map math | 7 | 7 | 0 | 0 |
| Map input/contracts | 22 | 22 | 0 | 0 |
| Recovery persistence | 21 | 21 | 0 | 0 |
| **Total** | **2,090** | **2,090** | **0** | **2** |

The two skipped checks are `NATIVE_CONNECTION_LIFECYCLE` and `NATIVE_RECONNECT`; neither is replaced by a synthetic pass. All **9/9** recorded-flight validations passed, with **91,366/91,366** eligible samples processed and matched independently to the raw journals. Input hashes stayed unchanged. Recovery completed 5,000 durable replacements. Map contention retained identical authoritative flight, recovery, PIREP and history results with unchanged connection generation. Its passing result does not erase prior scheduling failures or establish installed responsiveness.

All 25 new assertions passed within the 162 map checks. First-paint evidence includes the held image load, no early reveal, actual loaded lighting before readiness, hidden DayNight preference and subsequent normal streaming. Nine creation/recreation paths cover initial disconnected Operations, four Tracking returns, minimize/restore, classic → V2, unload/reload and rapid recreation; the tenth assertion confirms unchanged authoritative state. Existing suites add repeated navigation, resizing/scaling, input routing, browser failure/restart/fallback and disposal checks. Diagnostic screenshots were generated at 1366×768, 1920×1080, 2560×1440 and simulated WPF 150%/200% layouts; these are not certification of physical monitor/DPI behavior or single-frame compositor output.

Archive verification passed: **514/514** files match in the frozen portable tree, portable ZIP and installer extraction. The extraction test did not install the application. All **223** source inputs still match the freeze; source ZIP and Git bundle verify. Both the original package (8 handoff artifacts, 514 payload files) and INPUT R1 (14 handoff artifacts, 514 payload files) remain unchanged. The failed R2 candidate's package hashes and 514 payload files also remain intact. The Core binary and 13 of 14 map assets are byte-identical to INPUT R1; only `map.mjs` differs among map assets.

The report guard matches the policy declared before this run: 7 deliberate baseline reports, 16 injected renderer reports and 12 recovery reports, with **0 unexpected reports**, no orphan report text and no nonempty error outputs. Earlier failures remain separately recorded above; counts in this table include only the final selected run, and do not inflate totals with hash audits or repeated development checks.

At final preflight, no OPS/MSFS processes or Map V2 startup overrides were present. A new read-only, hash-verified backup preserves **219 saved-data files**, excluding only regenerable MapCache, at `work/checkpoint-7.24b-first-paint-r2a/pre-reinstall-profile-backup/`. INPUT R1 remains installed; no installation or profile repair was performed. Compact results are archived under `evidence/7.24b-visual-r2a/` and package companions include full manifests, hashes, source archives and the read-only installed-payload verifier.

**Recommendation: GO for normal VISUAL R2A installation and the MSFS-closed physical retest only. Native MSFS and completion of 7.24B remain HOLD.**

## Review and remaining gates

Independent read-only reviews of both the runtime correction and the R2A fixture delta found no blocking or important findings. Existing `Ready`, `ReadyMs` and `WaitReadyAsync` still measure bridge initialization; they must not be presented as time-to-visible. Automated render/event-order observations cannot certify the final compositor pixels or the installed physical flash. Exceptionally slow first frames may still invoke existing fallback handling.

The final installed physical retest must cover initial opt-in, repeated Tracking → Operations returns, other workspaces, fresh renderers, minimize/restore, classic → V2, DayNight preference and the eight input/focus checks. Follow [the VISUAL R2A retest sheet](checkpoint-7.24b-visual-r2a-retest.md). Preserve profile/history/settings and use the normal installer upgrade, then verify the exact installed payload before launch. **Native MSFS remains HOLD. Classic remains default; Map V2 remains opt-in.** Prior composition-scheduling concerns and the broader 7.24B native gates are not closed by this correction.
