# 7.24B physical-input correction implementation plan

> Execute inline with the executing-plans skill. The user's approved scope and physical A/B diagnosis are authoritative; no additional design approval is needed.

**Goal:** contain Map V2 left-button press bubbling and deliver a separate frozen validation package for installed physical retest.

**Architecture:** add a map-view-local bubbling handler after the CompositionControl SDK processes mouse-down. Handle only the left button. Keep the control type, browser configuration, renderer selection, data contracts and all flight logic unchanged.

**Spec:** current user instruction; `docs/map-v2/checkpoint-7.24b-physical-input.md`; original `checkpoint-7.24b-spec.txt`.

## Scope and review focus

- Preserve original failed installed result and original frozen package/evidence.
- Test host event containment, unchanged right/wheel routing, and outside-map parent behavior. Verify initialized browser still receives the left press.
- Physical pointer, selection, focus escape, real resize and outside scrolling must be retested after installation; automation cannot close that gate.
- Preserve default classic / session opt-in Map V2. No scheduling investigation or operational changes.
- Work in the existing checkpoint branch, preserving dirty prior work. Freeze a new source snapshot with a separate index/ref and manifest rather than changing the user's branch/index.

## Steps

- [x] Add routed-input checks to the existing explicit `MapV2Checks` diagnostics. Run the focused helper against unchanged production and retain the expected failing check.
- [x] Add the minimal left-only host containment in `WebMapRenderer`. Run focused checks and verify initialized SDK forwarding in full map diagnostics.
- [x] Give package instructions/caption a distinct `7.24B INPUT R1` identifier, retaining version/storage identity. Archive exact source and binaries into a new unique directory.
- [x] Run full existing migration baseline (Core/Desktop/isolation/history/persistence and 9 replays/91,366 samples), affected Map V2/lifecycle/input checks, browser input checks and recovery against frozen binaries. Declare expected reports before execution, including the prior known deliberate subscriber fixture; retain the original guard result unchanged.
- [x] Verify installer extraction, ZIP, portable payload, source manifest and original package hashes; review the small delta independently.
- [x] Write corrected-build report and an installed physical retest checklist. Stop at reinstall handoff with MSFS closed.

## Ledger

- Entry: original build `S4-7.24B-20260925-46a1b304c3d6` remains installed; physical gate failed. All 222 original source inputs and 514 installed files were verified after diagnosis. Prior A/B probe: 0/4 A drags moved; 3/3 B drags moved. No production fix applied before this task.
- Ruling: user explicitly authorized implementation of this diagnosed fix, so no second design approval. Preserve prior dirty work and local evidence; do not clean up frozen artifacts or start another checkpoint.
- Completion: freeze `S4-7.24B-INPUT-R1-20260925-62b8c871dd05`; 2,065/2,065 selected final assertions, 2 native skips, 9 replays / 91,366 samples. Corrected payload matches 514 files across portable/ZIP/extraction; original archive and installed payload unchanged. Read-only independent review found no blocker. Installed R1 physical validation remains pending; stop before installation/native testing.
