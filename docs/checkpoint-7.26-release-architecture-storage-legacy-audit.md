# Checkpoint 7.26 — Release Architecture, Storage & Legacy Consolidation

Audit date: 2026-10-02  
Mode: Phase 1 read-only audit. No source, packaging, Git, frozen artifact, or user-data mutation was performed. This report is the requested audit deliverable.

## Executive decision

The validated tracking foundation is preserved and recoverable, but the repository is not yet ready to start 7.27 implementation. The production payload is compact and internally consistent; the development workspace is large because it combines active source with many generations of raw verification runs, unpacked runtimes, packaged releases, native evidence, and repeated diagnostic output.

The recommended sequence is:

1. approve and create a verified external archive;
2. curate closed-checkpoint evidence into that archive;
3. remove only manifest-proven duplicates and reproducible outputs;
4. establish a clean Git baseline;
5. centralize Sierra 4 product/storage identity and implement a separately approved, rollback-safe data migration;
6. begin 7.27 only after the clean baseline and identity/storage decisions are frozen.

The point-in-time workspace is 46,318,789,447 logical bytes (43.137734 GiB). A conservative lower-bound active workspace is about 2.71 GiB. A practical post-cleanup target is 3–5 GiB, leaving 38–40 GiB recoverable from the active F: workspace after verified archival and approved cleanup. Most recoverable bytes require archival, not deletion.

## 1. Baseline ledger

Captured at 2026-10-02 08:16:38 America/Chicago.

| Item | Baseline |
| --- | --- |
| Repository | `F:\Sierra4-Development` |
| Branch | `codex/checkpoint-7.24c` |
| HEAD | `020bda9f7d1dd7e16c6ecfeec67c4e4e007c6215` |
| Git status | Dirty: 43 tracked modifications and 819 individual untracked files (862 file-level porcelain entries; 157 entries when untracked directories are collapsed) |
| Frozen candidate | `S4-7.25A-A350-20261001-6020896a402b` |
| Candidate source commit | `020bda9f7d1dd7e16c6ecfeec67c4e4e007c6215`, frozen from a dirty source tree |
| Installer SHA-256 | `2920F198578FA7792D8703BAA8817DFE5E367DF53F55DCDFD51929591DDDA709` |
| Portable ZIP SHA-256 | `5EF952567F3571E7AB91BBF8F8DA21C26067DB0FEBDC9AAF63669F0BD97FD5A3` |
| Source manifest SHA-256 | `6020896A402B6F985BB06FD3A969F497B896751A24DC324E101FF0E25CEA47B8` |
| Source snapshot SHA-256 | `D8D05B90A26D2492DA64E5396159B97F077CE94D4A3C5B4236003CBB843CBF3A` |
| Freeze integrity | PASS: 531/531 self-excluding freeze entries matched size and SHA-256 |
| Workspace | 46,318,789,447 bytes / 43.137734 GiB |
| Files | 308,863 |
| Directories | 95,778 |

The candidate directory contains 532 files because `final-freeze-manifest.json` does not list itself. The package payload and installer extraction each verify 514/514 files. Frozen smoke was 1,696/1,696 plus activation, and automated acceptance was 2,878 passed, 0 failed, and 2 documented native-only skips.

The immutable freeze record correctly says the candidate was uninstalled and still needed native validation at freeze time. The later `checkpoint-7.25a-engineering-acceptance.md` closes 7.25A by engineering acceptance after DAL9929 evidence. At audit time, `%LOCALAPPDATA%\Alpha6Designs\Alpha6OPSPreview` is installed; all 514 installed payload files match the frozen candidate, with only the installer-owned `.alpha6-preview` marker as an extra file.

All size figures in this report are logical file lengths. They do not measure NTFS allocation, compression savings, hard-link de-duplication, or reparse targets.

## 2. Top-level storage inventory

| Directory | Bytes | GiB | Files | Directories | Primary purpose |
| --- | ---: | ---: | ---: | ---: | --- |
| `work` | 31,034,196,782 | 28.9028 | 274,500 | 91,671 | Checkpoint runs, replay/native evidence, package extraction, smoke output, SDK/home state |
| `outputs` | 10,753,118,000 | 10.0146 | 11,241 | 689 | Expanded payloads, installers, ZIPs, source snapshots, frozen candidate records |
| `.work` | 3,378,642,036 | 3.1466 | 12,266 | 1,423 | Older local SDK, package extraction, replay/smoke scratch, some native captures |
| `src` | 691,803,808 | 0.6443 | 6,057 | 1,264 | Product source plus 663,369,606 bytes of `bin`/`obj` output |
| `tests` | 146,228,013 | 0.1362 | 268 | 79 | Test source plus 145,981,063 bytes of `bin`/`obj` output |
| `.git` | 145,496,476 | 0.1355 | 2,963 | 339 | Repository history/index/objects |
| `docs` | 100,762,932 | 0.0938 | 907 | 91 | Architecture/checkpoint documentation and curated evidence |
| `apps` | 58,311,522 | 0.0543 | 543 | 186 | Web prototype source plus generated dependencies/output |
| `prototypes` | 5,461,815 | 0.0051 | 68 | 21 | Prototype source plus generated dependencies/output |
| `assets` | 4,156,236 | 0.0039 | 4 | 4 | Source assets/reference data |
| Other repository content | 611,827 | 0.0006 | 46 | 6 | Datasets, packaging, scripts, samples, `.superpowers`, and root project/config files |

The top-level trees at or above 250 MiB are `work`, `outputs`, `.work`, and `src`.

The product, test, documentation, app, prototype, asset, dataset, packaging, script and sample roots contain tracked source or documentation; their generated subtrees are ignored. `work` and `.work` contain no tracked files. `work` is covered by the shared ignore policy, while `.work` is excluded only by the local `.git/info/exclude`. `outputs` is mostly ignored package material, with four tracked reference files: the handoff note, fleet SQLite/CSV data, and desktop quick-start text.

### Large `work` directories

Every immediate `work` child at or above 250 MiB is listed below.

| Directory | GiB | Files | Assessment |
| --- | ---: | ---: | --- |
| `checkpoint-7.24c` | 7.7390 | 104,880 | Mixed active replay inputs, accepted/failed native evidence, repeated checks and build output; must be curated, not bulk-deleted |
| `checkpoint-7.25a` | 2.3356 | 24,540 | Acceptance, freeze, package extraction and smoke evidence; archive accepted/final records, remove only proven intermediates |
| `dashboard-review` | 2.0658 | 7,590 | Timestamped rendered smoke screenshots and reports; reproducible/temporary after representative evidence is curated |
| `checkpoint-7.25` | 1.8380 | 27,423 | Repeated acceptance runs and baseline package verification; archive final accepted evidence |
| `checkpoint-7.24a` | 1.2769 | 9,960 | Map performance/soak evidence including failed and final runs; archive curated accepted/diagnostic set |
| `checkpoint-7.24a.2` | 1.2324 | 7,746 | Scheduling evidence, manifests and regression runs; raw evidence is referenced by reports |
| `checkpoint-7.24b-layout-oscillation` | 0.9729 | 4,473 | Closed layout correction evidence; archive |
| `checkpoint-7.24b-input-correction` | 0.9293 | 4,055 | Closed input correction evidence; archive |
| `checkpoint-7.24b-first-paint-r2a` | 0.9054 | 4,317 | Superseded checkpoint run; archive curated evidence |
| `dotnet` | 0.7517 | 5,577 | Current repository-local .NET SDK used by packaging/verification scripts; keep active until toolchain provisioning changes |
| `checkpoint-7.22` | 0.6973 | 7,800 | Historical Map V2 foundation evidence; archive |
| `checkpoint-7.24b` | 0.6921 | 3,558 | Historical/superseded package and evidence; archive |
| `checkpoint-7.24a.1` | 0.6793 | 7,081 | Historical map policy evidence; archive |
| `checkpoint-7.24b-installation-gate` | 0.6072 | 721 | Installation-gate evidence including failure/restart state; archive |
| `checkpoint-7.24b-first-paint` | 0.5993 | 4,784 | Historical first-paint evidence; archive |
| `installer-test` | 0.3678 | 978 | Reproducible extracted installer pair; safe-deletion candidate after hash/consumer check |
| `checkpoint-7.18a` | 0.3615 | 19,546 | Historical checkpoint evidence; archive |

`work/workspace-relocation` is below the 250 MiB threshold but was audited separately because the checkpoint brief names it. It is 136,793,176 bytes (130.46 MiB), with 509 files and 93 directories. It contains the authoritative relocation report, pre/post inventories, copy logs, Git-state captures and a migration smoke tree. No current script outside `work` references it. Archive the report, inventories and logs as migration provenance; treat the smoke binaries/output as reproducible only after the archived provenance is verified and deletion is separately approved.

Subdirectories at or above 250 MiB inside the recent checkpoints include:

| Directory | GiB | Role |
| --- | ---: | --- |
| `checkpoint-7.24c/input-gate-20260929` | 2.003 | Repeated/full input-gate evidence |
| `checkpoint-7.24c/direct-acceptance-20260929` | 1.118 | Direct responsiveness acceptance |
| `checkpoint-7.24c/map` | 0.846 | Map verification output |
| `checkpoint-7.24c/heartbeat-review-20260929` | 0.713 | Native/diagnostic heartbeat review |
| `checkpoint-7.24c/map-restoration` | 0.692 | Restoration checks |
| `checkpoint-7.24c/jst771-review-20260929` | 0.661 | Active replay input and review evidence |
| `checkpoint-7.24c/logbook-closeout-20260930` | 0.393 | Accepted closeout evidence |
| `checkpoint-7.24c/scheduling-continuation-20260928-152808` | 0.369 | Scheduling evidence |
| `checkpoint-7.24c/terminal-implementation-20260928` | 0.311 | Terminal behavior evidence |
| `checkpoint-7.25/20260930-132035-d723e7ff` | 0.828 | Initial full run |
| `checkpoint-7.25/acceptance-20260930-142820-421dd89e` | 0.497 | Acceptance run |
| `checkpoint-7.25/acceptance-20260930-143546-4f7e3293` | 0.498 | Acceptance run |
| `checkpoint-7.25a/acceptance-20261001-103146-d9d5f605` | 0.608 | Pre-final acceptance run |
| `checkpoint-7.25a/acceptance-20261001-103948-3046ce3f` | 0.606 | Final accepted automated evidence |
| `checkpoint-7.25a/freeze-20261001` | 0.361 | Freeze, extraction and packaged-smoke evidence |
| `checkpoint-7.24a/frozen-soak` | 0.251 | Frozen soak evidence |

### `.work` findings

`.work` is excluded only through `.git/info/exclude`, not the shared `.gitignore`. No current build or verification script references `.work`; current scripts use `work/dotnet` and timestamped `work/checkpoint-*` locations.

The directory contains an older 0.752 GiB .NET SDK, three approximately 205.66 MB installer extractions, four approximately 205.68 MB 7.16 payload extractions, many approximately 38 MB smoke trees, repeated replay results, and native review packages. `jst760-7.16` and `JST760-9-21-review` have identical sizes (50,013,757 bytes each), but size equality alone is not deletion proof. `qlk1274-review-20260922` is another likely irreplaceable native input. Treat `.work` as mixed historical scratch: archive unique native inputs/manifests first, hash-deduplicate, then approve deletion of SDKs/extractions/smoke output.

### Source build output

Generated material under active code trees totals 871,689,489 bytes (0.811824 GiB):

| Root | Generated bytes | GiB |
| --- | ---: | ---: |
| `src` | 663,369,606 | 0.617811 |
| `tests` | 145,981,063 | 0.135955 |
| `apps` | 58,274,376 | 0.054272 |
| `prototypes` | 4,064,444 | 0.003785 |

These are `bin`, `obj`, `node_modules`, or `dist` outputs and are reproducible, subject to the normal approval gate and a successful clean rebuild after removal.

## 3. Non-overlapping storage classification

This allocation is deliberately conservative and sums exactly to the 46,318,789,447-byte baseline. Mixed evidence trees are retained as historical provenance rather than optimistically labeled disposable.

| Class | Bytes | GiB | Basis |
| --- | ---: | ---: | --- |
| A. Active product source | 34,620,589 | 0.032243 | Non-generated source/assets/datasets/packaging/scripts/project files |
| B. Active development required | 1,322,364,250 | 1.231548 | `.git`, non-generated tests, current `work/dotnet` and `dotnet-home`, generic 0.22 expanded payload used by scripts, fleet outputs |
| C. Active regression/replay required | 1,074,813,747 | 1.000998 | Script-referenced 7.24C full/route/JST771/T2-final input roots |
| D. Irreplaceable native evidence | 53,270,384 | 0.049612 | Isolated `checkpoint-7.24c/native-input-20260928-144838-4a0d6019`; other native material is conservatively retained in E pending curation |
| E. Historical checkpoint provenance | 30,228,594,817 | 28.152573 | All remaining `work`, all `.work`, and `docs`; includes mixed raw/final/failed/reproducible material requiring manifest curation |
| F. Frozen release artifacts | 1,422,315,455 | 1.324634 | Current 7.25A candidate, frozen 7.25 baseline, and closed 7.24C final L1 |
| G. Superseded release artifacts | 8,878,138,798 | 8.268411 | Remaining `outputs` after active aliases, frozen releases and exact duplicates |
| H. Reproducible build output | 871,689,489 | 0.811824 | Active-tree `bin`/`obj`/`node_modules`/`dist` |
| I. Temporary diagnostic/Codex work | 2,218,169,562 | 2.065831 | `work/dashboard-review`; keep representative accepted output, regenerate the rest |
| J. Confirmed duplicate data | 214,796,163 | 0.200045 | Generic 0.22 installer and ZIP are byte-identical to 7.25A candidate copies; currently referenced by scripts/build conventions |
| K. Proven legacy Alpha 6 | 0 | 0 | No file was classified obsolete from naming alone; current runtime contracts still use Alpha6 identities |
| L. Unknown/requires review | 16,193 | 0.000015 | `.superpowers` local process material |
| **Total** | **46,318,789,447** | **43.137734** | |

Classes E and G mean “remove from the active workspace only after verified archive,” not “delete.” Class J is not immediately removable because the generic 0.22 paths are current script/build endpoints; repoint and verify consumers first. Class H requires a successful clean rebuild. Class I requires selection of representative screenshots and final reports before deletion.

## 4. Packaged candidate inventory

### Checkpoint packages

| Candidate | Bytes | Status/evidence | Disposition |
| --- | ---: | --- | --- |
| `S4-7.24B-20260925-46a1b304c3d6` | 451,638,511 | Superseded 7.24B package, manifests present | ARCHIVE |
| `S4-7.24B-INPUT-R1-20260925-62b8c871dd05` | 507,184,624 | Installed/input evidence present; superseded | ARCHIVE |
| `S4-7.24B-LAYOUT-R3-20260927-666f495dce7e` | 507,242,486 | Installed/physical layout evidence present; superseded | ARCHIVE |
| `S4-7.24B-VISUAL-R2-20260925-acc4f98f3a08` | 451,649,984 | Visual candidate; superseded by R2A/later checkpoints | ARCHIVE |
| `S4-7.24B-VISUAL-R2A-20260927-87fa792a7c29` | 507,237,079 | Installed/physical visual evidence present; superseded | ARCHIVE |
| `S4-7.24C-C1-20260928-64e864462ace` | 473,567,625 | NO-GO physical drag/native restart evidence retained | ARCHIVE |
| `S4-7.24C-T1-20260928-00e3f628b90e` | 498,332,474 | NO-GO parked shutdown/scheduling/native recovery evidence retained | ARCHIVE |
| `S4-7.24C-T2-20260929-f421af15530c` | 473,735,425 | NO-GO corrected A320/native cold-start gate; evidence retained | ARCHIVE |
| `S4-7.24C-FINAL-L1-20260930-318659425b88` | 473,996,936 | Closed PASS/disconnected package verification | KEEP FROZEN, then long-term archive |
| `S4-7.25-TRACKING-20260930-e7ee5c18d8ae` | 474,126,031 | Immutable 7.25 baseline, automated PASS | KEEP FROZEN, then long-term archive |
| `S4-7.25A-A350-20261001-6020896a402b` | 474,192,488 | 531/531 frozen; 7.25A closed by engineering acceptance; currently installed payload matches 514/514 | KEEP ACTIVE/FROZEN |

The parked `S4-Fenix-Shutdown-Capture-7.24C-20260928` folder is 16,779 bytes, explicitly records `nativeCaptureRun:false`, and should remain with 7.24C provenance.

### Historical version packages

| Version | Expanded payload | ZIP | Installer | Disposition |
| --- | ---: | ---: | ---: | --- |
| 0.12.1 | — | 94,637,688 | 95,574,016 | ARCHIVE |
| 0.13.0 | — | 94,955,758 | 95,891,968 | ARCHIVE |
| 0.14.0 | — | 95,532,751 | 96,468,992 | ARCHIVE |
| 0.15.0 | — | 95,534,744 | 96,471,040 | ARCHIVE |
| 0.16.0 | — | 97,923,598 | 98,860,032 | ARCHIVE |
| 0.17.0 | — | 97,926,480 | 98,862,592 | ARCHIVE |
| 0.18.0 | 206,040,567 | 97,936,295 | 98,872,832 | ARCHIVE |
| 0.18.1 | 205,598,982 | 97,793,576 | 98,729,472 | ARCHIVE |
| 0.18.2 | 205,601,066 | 97,794,347 | 98,730,496 | ARCHIVE |
| 0.19.0 | 205,606,393 | 97,796,873 | 98,733,056 | ARCHIVE |
| 0.20.0 | 205,659,425 | 95,432,598 | 96,368,640 | ARCHIVE |
| 0.20.1 | 205,659,941 | 97,815,176 | 98,751,488 | ARCHIVE |
| 0.20.2 | 205,664,549 | 97,816,168 | 98,752,512 | ARCHIVE |
| 0.20.3 | 205,664,549 | 97,816,155 | 98,752,512 | ARCHIVE |
| 0.21.0 | 205,682,123 | 97,822,471 | 98,758,656 | ARCHIVE |
| 0.22.0 generic alias | 237,381,924 | 106,930,051 | 107,866,112 | KEEP ACTIVE until current scripts/build outputs are repointed; ZIP/installer duplicate 7.25A exactly |

Also archive the 27,217,925-byte 7.11 bundle. Historical package hashes and source snapshots should travel with their package; an expanded payload need not be retained when the installer/ZIP, manifest, source snapshot and extraction verification are all present.

## 5. Actual end-user footprint

The development workspace is not an application footprint.

| Component | Measured size | Notes |
| --- | ---: | --- |
| Frozen installer | 107,866,112 bytes / 102.87 MiB | Contains the verified payload |
| Portable ZIP | 106,930,051 bytes / 101.98 MiB | SHA-256 equals embedded payload |
| Clean installed application | 237,381,956 bytes / 226.39 MiB | Current installed 514-file candidate payload plus 32-byte ownership marker; installed under LocalAppData, not Program Files |
| Bundled .NET runtime | 179,244,862 bytes / 170.94 MiB | 479 files; dominant installed component |
| Map V2 / MapLibre assets | 3,484,289 bytes / 3.32 MiB | Includes land GeoJSON and MapLibre modules/licenses |
| Fleet operational reference DB | 348,160 bytes / 0.33 MiB | Read-only bundled catalog |
| Main desktop assembly | 26,672,128 bytes / 25.44 MiB | Includes production UI and compiled smoke/diagnostic harnesses |
| WebView2 Evergreen runtime | External prerequisite | Adapter assemblies are bundled; browser runtime itself is not silently downloaded or bundled |

The release package contains no source tree or test project, but test/smoke/diagnostic classes are compiled into `Alpha6OPS.dll` and exposed through diagnostic command paths in `App.xaml.cs`. External distribution should move non-production smoke harnesses into a separate development assembly/tool unless retaining them is an explicit support feature.

### Current user/profile data

`%LOCALAPPDATA%\Alpha6Designs\Alpha6OPS` currently contains 999,270,325 bytes (952.98 MiB), 586 files and 89 directories:

| Category | Bytes | MiB |
| --- | ---: | ---: |
| `TestLogs` | 939,780,282 | 896.24 |
| `MapCache` | 40,430,334 | 38.56 |
| `SimBrief` | 18,200,282 | 17.36 |
| Flight-history SQLite | 647,168 | 0.62 |
| Log-index SQLite | 131,072 | 0.13 |
| Crash reports | 49,730 | 0.05 |
| Preferences/recovery/status/gate state | Small | Less than 0.1 MiB in the current snapshot, excluding databases |

The complete `%LOCALAPPDATA%\Alpha6Designs` footprint, including the installed app, is 1,236,652,281 bytes (1.1517 GiB).

A pristine first-run profile was not created because this audit could not modify LocalAppData. Empty database/settings evidence indicates the non-browser first-run core is below 1 MiB; opening Map V2 creates a variable WebView profile/cache. Current MapCache is 38.56 MiB, so first-run reporting should state “under 1 MiB core plus variable browser cache” until a separately approved clean-profile measurement is run.

Current operational growth is unbounded. The observed development profile has 896.24 MiB of journals. The 15-hour DAL40 evidence added approximately 121.79 MB JSONL plus 187.68 MB summary JSON (309.47 MB uncompressed) and compressed to 30.09 MB. A long-haul flight can therefore add roughly 0.3 GB under current behavior; compression reduced that example by about 90%.

## 6. Recommended Sierra 4 storage architecture

### Installation

Use a signed, maintained installer. Preserve the current no-admin, per-user deployment model unless a real machine-wide requirement appears:

```text
%LOCALAPPDATA%\Programs\Sierra 4\
    Sierra4.exe and production assemblies
    Runtime\
    Map\Web\
    Assets\
    Fleet\aircraft.sqlite
    licenses and notices
```

`Program Files\Sierra 4` is appropriate only for an elevated, machine-wide installer/updater. Runtime state must never be written beside binaries in either model.

### Mutable user data

Use one canonical local profile root initially:

```text
%LOCALAPPDATA%\Sierra 4\
    Data\
        Flights\history.sqlite
        Flights\Active\assignment.json
        Flights\Active\recovery.json
        Gates\history.json
    Settings\
        preferences.json
        general-settings.json
        dashboard-state.json
        ofp-viewer-state.json
    Diagnostics\
        Logs\
        CrashReports\
        Pending\
    Cache\
        Map\
        SimBrief\
        Updates\
    State\
        program-status.json
        migration.json
```

Keep flight history, PIREP records, operational databases and recovery local and durable. Store diagnostic exports in a user-chosen location such as Documents; exports are user-owned and not automatically deleted. Keep immutable configuration defaults with the application.

Do not use Roaming AppData until a sync/roaming requirement is defined. If introduced later, restrict it to small, merge-safe preferences; never roam SQLite databases, journals, caches, secrets or active recovery. ProgramData is unnecessary for the present single-user design and should remain unused unless a machine-shared immutable catalog or policy becomes real.

## 7. Proposed retention and rotation policy

| Data | Proposed default |
| --- | --- |
| Active-flight journal/recovery | Never truncate the active flight solely to meet a quota. Segment writes, reserve free-space margin, and preserve until successful PIREP/explicit abandonment. |
| Successful-flight raw JSONL/summary | After successful PIREP and clean close, retain compact flight history permanently; compress raw diagnostics immediately, retain 14 days or the last 3 flights, and cap the pool at 512 MiB. |
| Interrupted/crashed flight evidence | Compress and retain 30 days or the last 3 incidents, capped at 1 GiB; never remove the newest unresolved recovery package. |
| Routine application logs | Rotate at 5–10 MiB per file, retain 14 days, total cap 100 MiB. |
| Crash reports | Retain 90 days or 20 reports, cap 100 MiB. |
| Map/WebView cache | LRU cleanup at 500 MiB or 30 days; safe to rebuild. |
| SimBrief cache | Retain the latest 20 OFPs or 90 days, cap 100 MiB; preserve any OFP referenced by an active/recovery flight. |
| Recovery snapshots | Keep current plus one verified predecessor; remove completed-flight recovery only after durable PIREP/history commit. |
| Update staging/old payload | Keep one rollback payload until the new version passes first-launch health checks; remove staging after success or after 7 days. |
| Diagnostic exports | Never auto-delete; user chooses location and lifecycle. |
| Extended diagnostics | Explicit opt-in, visibly time-bounded, with predicted/actual size and one-click export. |

All cleanup must be crash-safe and index-consistent. Delete from indexes only after the file operation succeeds; use atomic replacement for policy metadata. Existing development/native evidence is outside this future production policy.

## 8. Alpha 6 legacy inventory and identity strategy

Alpha6 naming is still an active technical contract. It is not proof of obsolete material.

| Occurrence class | Current examples | Assessment |
| --- | --- | --- |
| Internal project/namespace | `Alpha6Ops.slnx`, `Alpha6Ops.*` projects, `namespace Alpha6Ops.*` | Active internal identity; low user impact, high rename churn |
| Executable/assembly | `Alpha6OPS.exe`, `Alpha6OPS.dll`, `Alpha6Ops.Core.dll` | Active package/process/tooling contract |
| Product metadata | Assembly product `Alpha 6 OPS`, company `Alpha 6 Designs` | Obsolete external brand for final Sierra 4 distribution |
| Install identity | `Alpha6OPSPreview`, `Alpha6OPS-Desktop-Preview-*`, `.alpha6-preview` | Active preview upgrade/uninstall ownership contract |
| User data | `%LOCALAPPDATA%\Alpha6Designs\Alpha6OPS` | Active authoritative data root |
| Single instance | `Local\Alpha6Designs.Alpha6OPS.*` | Active concurrency contract |
| Data filenames | `Alpha6OPS-flights.sqlite`, `Alpha6OPS-logs.sqlite`, `Alpha6OPS-*.jsonl`, crash prefixes | Active readers/indexers/support-tool contract |
| UI titles/shortcuts | Some windows and Start Menu still say Alpha 6 OPS | Safe staged rename candidates after identity layer exists |
| Historical docs/evidence | Older checkpoint reports/packages | Preserve as historical provenance |
| Sample tenant key | `alpha6` in fixtures/domain examples | Not necessarily brand debt; classify by semantic use |

### Staged migration

1. Add a central product/storage identity service and compatibility tests. Do not rename assemblies or namespaces yet.
2. On first Sierra 4-native launch, detect the legacy profile, require no old/new process to be active, copy a stable snapshot into `%LOCALAPPDATA%\Sierra 4`, verify hashes plus SQLite integrity/history counts, and write a migration marker. Never move or delete the legacy source during this stage.
3. Acquire both old and new single-instance identities through the rollback window so Alpha6OPS and Sierra4 cannot open the same logical profile concurrently.
4. Introduce a distinct Sierra 4 installer/registry/shortcut channel. Detect the owned preview install, provide an explicit upgrade path, keep one rollback payload, and preserve user data.
5. Change executable/product/shortcut/Add-Remove-Programs/window/log/crash names for new writes. Readers and indexes should accept both old and new filenames during the compatibility period.
6. After at least one validated release and rollback exercise, retire legacy discovery. Internal project/namespace/database filenames can remain indefinitely unless a separate rename has measurable value.

Renaming the internal namespaces before 7.27 would add broad XAML/resource/reflection/test risk with little user benefit. User-facing product/install/data identity should move first; internal code identity should be last and optional.

## 9. Installer/update/rollback assessment

The current preview installer already performs a useful atomic pattern: validate an owned install marker, extract to staging, move the old install to a backup, move staging into place, restore the backup if activation fails, then best-effort delete the backup. Whole-directory replacement prevents obsolete app files from accumulating. User data survives because it is outside the install directory. Uninstall removes the owned install, shortcuts and HKCU registration, leaving user data intact.

Before external distribution:

- replace the hand-built preview installer with a signed, maintained installer/update toolchain;
- give Sierra 4 a stable product/upgrade identity distinct from the Alpha6 preview channel;
- keep a manifest and one rollback payload until first-launch health checks succeed;
- make stale staging/backup cleanup explicit and observable;
- version database schemas and perform transactional migrations with verified pre-migration backups;
- make user-data removal an explicit uninstall choice, defaulting to preserve;
- test upgrade from the last Alpha6 preview, repair/reinstall, downgrade refusal, interrupted install, rollback, locked files, low disk, and old/new profile coexistence.

No updater should be implemented as part of the audit.

## 10. Git/source-control hygiene

The repository currently has 332 tracked files totaling 35,421,371 bytes (33.78 MiB). The largest tracked files are 1–2.3 MiB runtime/source assets; Git LFS is not justified for the present tracked set.

Findings:

- 43 tracked files are modified and 819 individual files are untracked. This is not cousin-merge or baseline-push ready.
- Active 7.24C/7.25/7.25A source, tests, scripts and reports are still mixed into one dirty tree and must be reviewed into a coherent baseline without normalization or loss.
- `work/`, `bin`, `obj`, `node_modules`, `dist` and common package outputs are ignored.
- `.work/` is ignored only in `.git/info/exclude`; that local rule is not portable. After `.work` is curated, either remove the directory or add a shared policy intentionally.
- The 27.2 MB `outputs/Alpha6OPS-v0.16.0-checkpoint-7.11.bundle` is untracked and does not match the current output ignore patterns.
- Untracked evidence totals at least 120.6 MiB at the individual-file level, led by native traces and evidence archives. Decide which compact reports/manifests belong in Git and archive large raw evidence externally.
- Keep source assets, small deterministic test fixtures, manifests and human-readable checkpoint summaries in ordinary Git.
- Keep installers, portable ZIPs, source snapshots, raw telemetry, large traces, repeated screenshots and databases out of normal Git history. Use the external archive with cryptographic manifests.
- Consider Git LFS only if a retained binary fixture is both required for routine checkout/tests and materially large. Current tracked assets do not meet that threshold.

Before the cousin merge, create an approved commit set for the current 7.24C–7.25A source/test/report state, verify the frozen source manifest against it, then merge from a clean baseline. Do not use bulk `git clean`, reset, or checkout against the present tree.

## 11. Archive strategy

Recommended external root:

```text
F:\Sierra4-Archive\
    releases\
        7.24C\FINAL-L1\
        7.25\TRACKING\
        7.25A\A350-ENDURANCE\
        historical\0.12.1-0.21.0\
    checkpoints\
        7.18-7.24B\
        7.24C\
        7.25\
        7.25A\
    native-evidence\
        flight-id-or-date\
    manifests\
```

Each archived unit should contain a short README, source/build identity, SHA-256 manifest, validation disposition, links to the corresponding report, and a verification timestamp. Copy first, verify file counts/sizes/hashes, test-read the archive, then request separate approval to delete the source copy. Preserve one authoritative copy of duplicate payloads and record all former paths in the manifest.

Keep active:

- source, tests and build scripts;
- current repository-local SDK until toolchain provisioning is replaced;
- exact replay fixtures/manifests used by current gates;
- current 7.25A candidate;
- current generic 0.22 paths until consumers are repointed;
- compact current checkpoint reports and manifests.

Archive:

- closed checkpoint raw/final evidence;
- 7.24C final L1, 7.25 baseline and all historical installers/packages;
- irreplaceable telemetry, databases, screenshots/video and native traces;
- source snapshots paired with frozen builds;
- failed runs explicitly cited by final reports.

Delete only after archival/verification where applicable:

- manifest-proven duplicate aliases with no remaining consumer;
- `bin`, `obj`, `node_modules`, `dist` and package extraction trees;
- stale SDK/home caches after build scripts are repointed;
- repeated smoke/dashboard outputs after representative accepted evidence is curated;
- failed/incomplete runs not referenced by any report or manifest.

## 12. Recoverable-space estimate

No bytes are counted twice.

| Disposition | Bytes | GiB | Meaning |
| --- | ---: | ---: | --- |
| Must remain active, lower bound | 2,905,991,074 | 2.7064 | A+B+C plus current 7.25A candidate |
| Should archive | 40,108,126,966 | 37.3536 | Historical provenance, irreplaceable native evidence, superseded outputs, and older frozen releases |
| Reproducible / temporary / duplicate cleanup candidates | 3,304,655,214 | 3.0777 | H+I+J; still requires approval and prerequisite verification |
| Unknown | 16,193 | 0.000015 | Local process metadata |
| Current workspace | 46,318,789,447 | 43.137734 | Baseline |

Arithmetic lower-bound active workspace after complete approved archival/cleanup: 2.71 GiB. The classified archive-plus-cleanup pool is 43,412,782,180 bytes (40.4313 GiB). A realistic target is 3–5 GiB because some current documentation, rollback material and representative evidence should remain locally. Estimated practical recoverable space is therefore 38–40 GiB, of which only about 3.08 GiB is presently a reproducible/temporary/duplicate candidate; the remainder must be moved to a verified archive or further curated.

## 13. Proposed 7.26 implementation stages

These stages require project-owner approval before execution.

1. **Freeze the audit ledger.** Review this report, resolve any desired retention exceptions, and define the archive root/media/backup requirement.
2. **Build archive manifests.** Produce file-level inventory and SHA-256 manifests for closed checkpoint evidence and release artifacts. Identify cross-tree duplicates by hash.
3. **Copy and verify archive.** Copy approved units to the archive, verify counts/hashes, and perform restore/read tests. No source deletion in this stage.
4. **Repoint active consumers.** Make replay fixtures and generic package paths explicit; remove current script dependence on duplicate aliases and broad checkpoint directories.
5. **Clean reproducible output.** Remove approved `bin`/`obj`/dependency/extraction/smoke trees, rebuild from clean state, and run the full current acceptance gate.
6. **Clean archived source copies.** Delete only archive-verified closed evidence/packages approved by the owner; remeasure workspace and compare to this ledger.
7. **Establish Git baseline.** Update ignore policy, review/commit the 7.24C–7.25A source/test/docs set, verify no required evidence was lost, and prepare the cousin merge.
8. **Implement storage/identity foundation.** In a separate design/implementation gate, add the Sierra 4 identity abstraction, data migration, retention service and production installer transition with rollback tests.
9. **Pre-UI release gate.** Rebuild/package, validate upgrade/rollback and profile migration, freeze a clean pre-UI baseline, then decide whether 7.27 may start.

## 14. Operations requiring project-owner approval

The following are not authorized by this audit:

1. creating the external archive structure as the authoritative retention location;
2. copying/moving checkpoint evidence or release artifacts into that archive;
3. deleting any file or directory from `work`, `.work`, `outputs`, `docs`, source/test build trees, or LocalAppData;
4. applying NTFS compression, archive recompression, hard-linking or content-addressed de-duplication to existing evidence;
5. changing `.gitignore`, `.git/info/exclude`, Git LFS, Git configuration, tracked/untracked state, staging, commits, branches or history;
6. repointing verification scripts, replay manifests, package paths or toolchain roots;
7. deleting the generic 0.22 installer/ZIP or any expanded payload, even when byte-identical to the candidate;
8. rebuilding, replacing, renaming, installing or uninstalling the frozen 7.25A candidate;
9. changing the installer toolchain, registry identity, install directory, Start Menu shortcuts, Add/Remove Programs identity or update channel;
10. renaming executables, assemblies, projects, namespaces, mutex/event names, logs, databases or crash-report prefixes;
11. creating, renaming, copying, migrating or deleting `%LOCALAPPDATA%\Alpha6Designs`, `%LOCALAPPDATA%\Sierra 4`, Roaming AppData or ProgramData content;
12. enabling automatic retention cleanup against any existing development or user evidence;
13. running a one-time user-profile/database migration or changing schema/version authority;
14. beginning 7.27 premium UI work or Hero Globe work.

## 15. Pre-UI readiness

| Area | Status | Reason |
| --- | --- | --- |
| Validated tracking rollback | READY | Frozen 7.25A, 7.25 baseline and 7.24C final artifacts/manifests exist and verify |
| Package cleanliness | PARTIAL | Payload is coherent and verified, but uses preview/Alpha6 identity and compiles diagnostic harnesses into the production assembly |
| Source/build separation | NOT READY | Generated output and raw checkpoint evidence dominate the active workspace |
| Storage architecture | DESIGN READY | Proposed layout and retention model exist; implementation/migration not approved |
| Legacy naming plan | DESIGN READY | User-facing migration can be staged; internal rename should wait |
| Git hygiene | NOT READY | 43 tracked modifications and 819 untracked files remain |
| Cousin merge | NOT READY | Requires an approved clean baseline and evidence/archive decisions |
| Clean pre-UI baseline | NOT READY | Requires archival/cleanup, clean rebuild, full verification and frozen identity |
| 7.27 implementation | BLOCKED | Start only after the owner approves and completes the relevant 7.26 stages |

Checkpoint 7.26 Phase 1 is ready for project-owner review. No cleanup, migration, rename, package rebuild or premium UI work has been performed.
