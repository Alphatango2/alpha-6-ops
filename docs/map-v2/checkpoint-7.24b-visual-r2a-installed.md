# 7.24B VISUAL R2A — installed identity and saved-data verification

**Installation PASS; exact installed identity PASS; saved-data preservation PASS. Physical visual/input retest PENDING; native MSFS HOLD.** Verified 27 September 2026 at approximately 00:45 UTC after the user supplied the successful upgrade screenshot. No application or simulator was launched during verification.

The setup screenshot identifies **0.22.0 • 7.24B VISUAL R2A** and reports successful normal upgrade. It was preserved byte-for-byte and hash-verified separately from earlier installation evidence.

The installed directory `C:\Users\DanniBaLs\AppData\Local\Alpha6Designs\Alpha6OPSPreview` matches **514/514** payload hashes for **S4-7.24B-VISUAL-R2A-20260927-87fa792a7c29**, source `87fa792a7c2924b9c6684a47ad76a6d6c053591b`. No missing or mismatched payload files were found. All 14 frozen handoff artifacts still match their recorded hashes.

All **219/219** saved-data files are byte-identical to the pre-reinstall snapshot, with zero changed, missing or added files. Only regenerable MapCache was excluded. This preserves the existing profile, history, assignment/recovery, gates, settings and diagnostic records without a repair or restore. All **26** prior reporter files remain unchanged; none were added. The hash-verified backup remains intact.

Preflight found no running OPS/MSFS process and no process/user/machine `SIERRA4_MAP_V2` override. Classic remains the startup default; Map V2 remains session opt-in.

**GO for the MSFS-closed [VISUAL R2A physical retest](checkpoint-7.24b-visual-r2a-retest.md).** Start normally and confirm saved data remains readable, then opt into Map V2. Test first-visible lighting during initial load, repeated Tracking → Operations returns, other workspaces, minimize/restore and fresh renderer creation, and repeat the eight input/focus checks. The installed compositor result is not yet known. Native testing must remain paused until the user results and new report/log inventory are reviewed.

Original left-pan failure, INPUT R1's eight user-reported input passes and separate visual failure, and the uninstalled R2 test-fixture failure remain distinct historical evidence. This installation pass does not reinterpret those outcomes or complete 7.24B.

Compact evidence: `evidence/7.24b-visual-r2a-installed/`. Raw read-only verification: `work/checkpoint-7.24b-first-paint-r2a/installed-20260927-004504/`. Frozen package artifacts were not modified.
