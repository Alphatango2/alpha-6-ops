# Sierra 4 pre-premium-UI handoff

This is the boundary before `7.27 — Premium UI Foundation`. No 7.27 implementation or Hero Globe work is included.

## Build identity

- Product: Sierra 4 v0.23.0
- Branch: `codex/checkpoint-7.24c`
- Frozen source commit: `77e337c7c46911d0e86c576231131fd38cc5dfc5`
- Candidate: `S4-7.26B-PRE-UI-20261002-77e337c7c469`
- Executable: `Sierra4.exe`
- Installer SHA-256: `005E7D73D427560A687BF0866C6264315465F6AE756D71F65F073F39AEF5890D`
- Portable ZIP SHA-256: `0856F126857171289F5A2981A31FCE9BB2384443FD736BE9D5D05A2F78C2E40F`
- Frozen manifest: 527/527 verified
- Installation state: not installed

The candidate is at `outputs/Sierra4-Desktop-S4-7.26B-PRE-UI-20261002-77e337c7c469`. Its source snapshot, source manifest, acceptance summaries, package contract, payload, installer, ZIP and self-excluding frozen manifest are inside the candidate directory.

## Build and verification

Use the repository-local .NET 10 SDK when a system SDK is unavailable:

```powershell
./work/dotnet/dotnet.exe restore Alpha6Ops.slnx --configfile NuGet.Config
./work/dotnet/dotnet.exe build Alpha6Ops.slnx -c Release --configfile NuGet.Config -nodeReuse:false
./scripts/verify-checkpoint-7.25a.ps1 -DotNetRoot ./work/dotnet -OutputDirectory <fresh-evidence-directory>
./scripts/test-production-package.ps1 -DotNetRoot ./work/dotnet
```

Packaging uses `packaging/build-desktop.ps1`. The default development build contains diagnostic harnesses used by the acceptance suite. Packaging explicitly sets `IncludeDiagnosticHarnesses=false`; the shipped `Sierra4.dll` contains none of the smoke, replay, probe, or map-check command entry points enforced by `scripts/test-production-package.ps1`.

The final gate passed 2,876/2,876 assertions with two expected migration skips. It covered 11 historical flights and 100,632 historical telemetry samples, 19 observed-cruise native checks, and the 54,061-sample authoritative DAL40 replay. The production payload passed matching publish, ZIP, installer extraction, and replacement inventories at 514 files and 236,617,544 bytes.

## Architecture overview

The desktop WPF shell calls the Core projects directly. Flight lifecycle, OOOI, recovery, tracking, PIREP persistence, replay and map authority remain in the existing internal `Alpha6Ops` namespaces for compatibility. Product-facing application, executable, installer, shortcut, uninstall and storage identities are Sierra 4.

Map V2 remains session opt-in and depends on the Microsoft Edge WebView2 Evergreen Runtime. Classic map fallback remains required. Required replay inputs are materialized from tracked gzip archives in `fixtures/flight-tracking-v1`; verification must not depend on an archived checkpoint tree or an old absolute workspace path.

## Checkpoint history

- 7.24C established terminal shutdown, generic Block In, recovery, PIREP and JST771 authority.
- 7.25 consolidated lifecycle, OOOI, observed-cruise, shallow step-climb, TOD/ETA continuity, map history/scheduling/restoration, terminal, Block In, Logbook and historical replay gates.
- 7.25A added the realistic A350 observed-cruise profile, greater-than-2,048-point map/TOD checks and authoritative DAL40 replay. Its frozen 531/531 candidate remains preserved.
- 7.26 audited release architecture, storage, legacy identities and historical artifacts.
- 7.26B archived and cleaned approved material, made regression fixtures portable, introduced Sierra 4 storage/migration and bounded retention, consolidated product/package identity, and froze the clean pre-UI baseline.

## Validated tracking guarantees

Preserve assignment acceptance, lifecycle phase transitions, OOOI actuals, recovery, PIREP ownership, observed cruise, qualified shallow step climbs, TOD/ETA continuity, flight-scoped map history, map scheduling/restoration, direct input responsiveness, terminal shutdown, generic Block In and Logbook authoritative times. Recorded replay sample totals remain distinct from synthetic assertion counts and native evidence.

Internal database tables, durable journal fields, recovery formats, crash prefixes and compatibility namespaces remain intentionally unchanged. Visual work must not move authority into the presentation layer or reinterpret these persisted contracts.

## Storage and process invariants

1. `%LOCALAPPDATA%\Sierra 4` is the canonical product root, split into `Data`, `Settings`, `Diagnostics`, `Cache`, and `State`.
2. Migration keeps `%LOCALAPPDATA%\Alpha6Designs\Alpha6OPS` intact.
3. A canonical profile becomes authoritative only after staging copy and SHA-256 verification complete and the directory is atomically promoted.
4. A failed or conflicting migration uses the legacy layout without partial adoption.
5. Both Sierra 4 and legacy single-instance identities are acquired before profile migration or retention, preventing concurrent profile writes.
6. `Data`, flight history, PIREPs, active recovery and user exports are outside diagnostic retention authority.
7. The newest unresolved journal and active SimBrief references cannot be removed for quota compliance.
8. Diagnostic gzip output must verify before the source is deleted.
9. The diagnostic SQLite index is derived state and is rebuilt from files that still exist.

## Packaging behavior

The installer targets `%LOCALAPPDATA%\Programs\Sierra 4`, owns it with `.sierra4`, registers `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\Sierra4`, and creates `Sierra 4.lnk`. It is unsigned and per-user. Same-channel replacement stages the new payload, removes obsolete program files, and leaves the external profile unchanged. The portable ZIP changes no registry or shortcut state.

## Git exclusions and retained local material

Generated build directories, `outputs/`, `work/`, `.work/`, dependency folders, SQLite sidecars, cache trees and raw private journals are intentionally ignored. The compressed replay fixtures under `fixtures/flight-tracking-v1` and their registry are tracked. Retain the external archive at `F:\Sierra4-Archive\checkpoint-7.26b-20261002`, the frozen 7.25A candidate, the frozen 7.26B candidate, the local SDK/tool roots, and the materialized replay work cache.

## Cousin merge notes and limitations

The branch consolidates the 7.24C–7.25A work that was present at checkpoint entry. Merge the final 7.26B commits as the reviewed baseline; do not replay archived working directories. Expect internal `Alpha6Ops` names to remain after the user-facing Sierra 4 rename. Review conflicts around `App.xaml.cs`, storage-path call sites, `SingleInstance.cs`, package scripts, replay materialization, `LogFileDatabase.Refresh`, and diagnostic compile conditions against the invariants above.

The candidate is extraction-tested but uninstalled. Migration tests use isolated roots and never touch the live user profile. The installer is unsigned; clean-machine, SmartScreen reputation, ARM64, and a new native simulator flight under the Sierra 4 package identity remain future validation work. See `known-limitations.md` and `final-acceptance.json` for the full accepted boundary.

The next planned checkpoint is **7.27 Premium UI Foundation**. It has not started.
