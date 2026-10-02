# Checkpoint 7.26B implementation report

Checkpoint 7.26B converts the validated 7.25A flight-tracking baseline into a compact, Sierra 4-native pre-UI foundation. It does not start 7.27 or change the validated lifecycle authority.

## Repository and preserved baseline

- Authoritative checkout: `F:\Sierra4-Development`
- Branch at entry: `codex/checkpoint-7.24c`
- Entry HEAD: `020bda9f7d1dd7e16c6ecfeec67c4e4e007c6215`
- Preserved frozen candidate: `S4-7.25A-A350-20261001-6020896a402b`
- Frozen installer SHA-256: `2920F198578FA7792D8703BAA8817DFE5E367DF53F55DCDFD51929591DDDA709`
- Frozen portable SHA-256: `5EF952567F3571E7AB91BBF8F8DA21C26067DB0FEBDC9AAF63669F0BD97FD5A3`
- Frozen integrity after cleanup: 531/531 entries

The pre-mutation ledger records the 43 tracked modifications and 820 untracked files present before 7.26B. Those changes cover the 7.24C terminal, responsiveness and recovery work plus the 7.25/7.25A predictive, A350, step-climb and map-history work.

## Archive and cleanup

The verified archive is `F:\Sierra4-Archive\checkpoint-7.26b-20261002`. Its immutable manifest SHA-256 is `8951D5D4EB76530DF6A8F0F2837959E7206A57D266FBDF9FA2E57797965F148D`.

| Measure | Result |
| --- | ---: |
| Archive units | 258 |
| Archived files | 271,806 |
| Archived bytes | 37,822,194,622 |
| Critical hashes compared | 65,047 |
| Initial cleanup targets | 291 |
| Initial cleanup bytes recorded | 44,207,965,999 |
| Post-package duplicate bytes removed | 452,178,087 |
| Workspace after implementation/package tests | about 3.19 GiB |

Every archived source inventory was compared with its verified archive record before removal. The generic 0.22 payload, ZIP and installer were removed later only after exact equality with the retained 7.25A candidate was established. The active toolchain, compact replay material and frozen candidate remain local.

Required replay inputs now have source-controlled gzip archives under `fixtures/flight-tracking-v1`. `scripts/materialize-replay-fixtures.ps1` recreates the ignored working copies and verifies their expected byte count and SHA-256 before a replay gate runs. The 16 logical fixtures total 248,881,010 bytes and compress to about 29.6 MiB; the largest tracked archive is about 12.5 MiB.

## Sierra 4 storage foundation

Production storage is rooted at `%LOCALAPPDATA%\Sierra 4` with these categories:

- `Data`: flight assignment, recovery, gate data, history and PIREPs
- `Settings`: general settings, window preferences and OFP viewer state
- `Diagnostics`: journals, crash reports, map diagnostics and the derived log index
- `Cache`: Map/WebView and SimBrief content
- `State`: process health and the migration completion marker

The legacy `%LOCALAPPDATA%\Alpha6Designs\Alpha6OPS` profile is copied into a unique staging directory. Every destination file is checked against its source with SHA-256 before the staging directory is atomically promoted. `State\migration.json` marks a completed migration. The old profile is never deleted. A copy failure, verification failure or conflicting canonical directory leaves the legacy layout active and intact. Repeated starts use the completed canonical profile without recopying later legacy changes.

Sierra 4 acquires both the new and legacy named mutex/event identities during the compatibility window. A running legacy process therefore blocks Sierra 4, and a running Sierra 4 process blocks both identities from opening the same logical profile concurrently.

## Retention foundation

Production startup applies bounded cleanup only after storage initialization. Explicit diagnostic roots remain isolated and are changed only when a test calls the retention service directly.

| Data | Policy |
| --- | --- |
| Completed raw flight diagnostics | Verified atomic gzip; retain 14 days or recent flights; 512 MiB cap |
| Interrupted diagnostics | Verified atomic gzip; retain 30 days or recent incidents; 1 GiB cap; newest unresolved is protected |
| Routine logs | 14 days; 100 MiB cap |
| Crash reports | 90 days or 20 reports; 100 MiB cap |
| Map/WebView cache | 30 days; 500 MiB LRU cap |
| SimBrief cache | 90 days or latest 20; 100 MiB cap; active/recovery OFP references are protected |

Flight history, PIREPs, active recovery and user-chosen exports are outside retention authority. Compression is written to a temporary file, decompressed and SHA-256 checked, atomically promoted, and only then replaces the source. The diagnostic SQLite index is rebuilt from files that still exist.

## Package identity

The new uninstalled package is Sierra 4 v0.23.0:

- Executable and assembly: `Sierra4.exe` / `Sierra4.dll`
- Install directory: `%LOCALAPPDATA%\Programs\Sierra 4`
- Ownership marker: `.sierra4`
- Registry identity: `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\Sierra4`
- Start menu shortcut: `Sierra 4.lnk`
- Production payload: 514 files, 236,617,544 bytes
- Installer SHA-256: `005E7D73D427560A687BF0866C6264315465F6AE756D71F65F073F39AEF5890D`
- Portable ZIP SHA-256: `0856F126857171289F5A2981A31FCE9BB2384443FD736BE9D5D05A2F78C2E40F`

Production builds compile without command-driven smoke, replay, probe and map-check harnesses. Development builds retain those harnesses for the checkpoint gates. Installer extraction, portable extraction and clean publish inventories match exactly. Same-channel replacement removes an obsolete seeded payload file and leaves the external profile unchanged.

## Verification

| Suite | Result |
| --- | ---: |
| Product storage and dual identity | 9/9 |
| Production retention | 7/7 |
| Recovery persistence | 21/21 |
| Terminal/Fenix capability | 203/203 |
| Active-flight restart | 74/74 |
| Logbook authoritative times | 40/40 |
| Package contract | PASS |
| Complete 7.25 + 7.25A gate | 2,876/2,876; 2 expected skips |
| Historical recorded telemetry | 11 replays; 100,632 samples |
| DAL40 authoritative replay | 54,061 samples |
| New frozen manifest | 527/527 |
| Preserved 7.25A manifest | 531/531 |

The frozen candidate is `S4-7.26B-PRE-UI-20261002-77e337c7c469`, bound to clean source commit `77e337c7c46911d0e86c576231131fd38cc5dfc5`. It was not installed. The complete machine-readable result is in `final-acceptance.json`.
