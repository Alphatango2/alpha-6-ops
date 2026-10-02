# SIERRA 4 — PROJECT CONTINUITY

| Field | Value |
| --- | --- |
| Project | Sierra 4 |
| Engine identity | Powered by RYNEL |
| Authoritative development workspace | `F:\Sierra4-Development` |
| Current validated checkpoint | **7.26B — PASS / CLOSED** |
| Premium UI checkpoint | **NOT STARTED** |
| Document purpose | Architectural continuity and developer handoff |

> [!IMPORTANT]
> **7.27 PREMIUM UI HAS NOT BEGUN.** This file records the validated pre-UI foundation. Future UI work must consume the operational contracts described here rather than replace them.

This is the first document to read before changing Sierra 4 architecture. The detailed checkpoint records remain authoritative evidence; this document connects those records to the current implementation and identifies the boundaries that later work must preserve.

## 1. Current project state

### Git and product baseline

| Item | Current value |
| --- | --- |
| Repository | `F:\Sierra4-Development` |
| Handoff branch | `codex/pre-premium-ui-7.26b` |
| Handoff tag | `sierra4-7.26b-pre-ui` |
| Accepted repository HEAD before this documentation-only handoff | `1a7a45a7a67edcf3681e9879ea6c4f1807556ff7` |
| Frozen candidate source commit | `77e337c7c46911d0e86c576231131fd38cc5dfc5` |
| Product/version | Sierra 4 `0.23.0` |
| Validation | 7.26B PASS / CLOSED; full 7.25A gate 2,876/2,876 with two expected native-only skips |
| Entry status for this handoff | Clean; no upstream was configured on the historically named local branch |
| Remote | `origin` → `https://github.com/Alphatango2/alpha-6-ops.git` |
| External archive | `F:\Sierra4-Archive` |
| Canonical runtime profile | `%LOCALAPPDATA%\Sierra 4` |
| Installed-package state | The 7.26B candidate was built and extraction-tested, but was not installed by the checkpoint |

Three identities must remain distinct:

- **SOURCE BASELINE:** the reviewed Git history through the 7.26B acceptance record. Commit `77e337c...` is the clean source commit bound into the frozen candidate. Commit `1a7a45a...` adds the final acceptance/report record without changing production behavior. The handoff branch and tag add this continuity document only.
- **FROZEN RELEASE CANDIDATE:** `S4-7.26B-PRE-UI-20261002-77e337c7c469`, stored locally under ignored `outputs/` content. Its installer SHA-256 is `005E7D73D427560A687BF0866C6264315465F6AE756D71F65F073F39AEF5890D`; portable SHA-256 is `0856F126857171289F5A2981A31FCE9BB2384443FD736BE9D5D05A2F78C2E40F`; manifest SHA-256 is `A23ECB635310B2894A2EE9C23B22C59F1FA563F583878EFC6DB6213B876C14DC`; 527/527 manifest entries passed. It is immutable evidence, not the working tree.
- **HISTORICAL ARCHIVE:** `F:\Sierra4-Archive`, including the verified 7.26B archive unit. It contains closed-checkpoint evidence and development material removed from the active workspace after verification. It is outside Git and is not required for an ordinary clone, build, or runtime.

The earlier 7.25A frozen candidate, `S4-7.25A-A350-20261001-6020896a402b`, remains preserved as historical engineering evidence. See the [7.26B final acceptance record](checkpoint-7.26b/final-acceptance.json), [implementation report](checkpoint-7.26b/implementation-report.md), and [pre-UI handoff](checkpoint-7.26b/pre-premium-ui-handoff.md).

## 2. Build and development instructions

### Required environment

- Windows is required for the WPF desktop, WebView2, and native SimConnect work.
- The repository pins the .NET 10 SDK at `10.0.100` with `latestFeature` roll-forward in [`global.json`](../global.json). An SDK is required, not only a runtime.
- This prepared workspace has a local SDK at `work\dotnet\dotnet.exe`. `work/` is ignored and is not part of a clone. On another machine, install a compatible .NET 10 SDK and replace the local executable path below with `dotnet`.
- Map V2 runtime use requires the Microsoft Edge WebView2 Evergreen Runtime.
- Native SimConnect use requires the MSFS SDK/runtime configuration. `simconnect-sdk-path.txt` is machine-specific and ignored. The packaging script accepts `-SimConnectDll`; its current default is `M:\SimConnect SDK\lib\SimConnect.dll` and it warns, rather than fails, when that optional path is absent.
- The full checkpoint gate also uses Python and Node. Its defaults point to the prepared Codex runtime. Pass `-PythonPath` and `-NodePath` on another machine. The legacy web dashboard requires Node 22.12+ or 24 and pnpm 11.19.0; it is not required to run the WPF shell.
- The installer bootstrap uses the Windows .NET Framework x64 compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.

### Entry points

[`Alpha6Ops.slnx`](../Alpha6Ops.slnx) contains Core, API, Pilot, Desktop, and the executable Core regression project. The primary product is [`src/Alpha6Ops.Desktop`](../src/Alpha6Ops.Desktop/Alpha6Ops.Desktop.csproj). Active UI/product development is desktop-first; the React dashboard remains a reference/demo client.

From the repository root:

```powershell
.\work\dotnet\dotnet.exe restore Alpha6Ops.slnx --configfile NuGet.Config
.\work\dotnet\dotnet.exe build Alpha6Ops.slnx -c Release --no-restore -nodeReuse:false
```

Run the dependency-free Core regression executable with `dotnet run`, not `dotnet test`:

```powershell
.\work\dotnet\dotnet.exe run --project tests/Alpha6Ops.Tests -c Release --no-build
```

Representative desktop checks after the Release build:

```powershell
.\scripts\verify-desktop.ps1 -Configuration Release
.\scripts\verify-map-v2.ps1 -DotNetRoot .\work\dotnet
.\scripts\verify-flight-recovery.ps1 -DotNetRoot .\work\dotnet
```

The authoritative consolidated regression process is:

```powershell
.\scripts\verify-checkpoint-7.25a.ps1 -DotNetRoot .\work\dotnet
```

That script materializes and hash-checks the source-controlled replay fixtures, runs the 7.25 gate, executes the 19 observed-cruise native-profile checks, and replays the 54,061-sample DAL40 journal. Its accepted 7.26B result is 2,876 passed, zero failed, two expected skips, 11 historical replays/100,632 samples, plus the DAL40 replay. Exit Sierra 4 before running the gate. Evidence is written below ignored `work/` paths.

### Production packaging

Build the current self-contained-layout package with the private .NET runtime:

```powershell
.\packaging\build-desktop.ps1 -DotNetRoot .\work\dotnet -RuntimeVersion 10.0.11
```

Expected generated outputs are ignored by Git:

- `outputs\Sierra4-Desktop-0.23.0\` — publish payload
- `outputs\Sierra4-Desktop-0.23.0-win-x64.zip` — portable package
- `outputs\Sierra4-Setup-0.23.0.exe` — unsigned per-user installer

The portable ZIP is produced directly from the publish directory. The setup executable embeds that ZIP; the payload also contains `Uninstall.exe`. Production publish sets `IncludeDiagnosticHarnesses=false`, excluding smoke, replay, probe, and map-check command handlers.

Verify publish, portable, installer extraction, replacement semantics, identity, and diagnostic exclusion with:

```powershell
.\scripts\test-production-package.ps1 -DotNetRoot .\work\dotnet -SkipBuild
```

Omit `-SkipBuild` to rebuild first. The verifier inventories and hashes the publish tree, extracts both delivery forms, requires identical fingerprints, exercises same-channel replacement, protects an external profile, and confirms that development builds retain diagnostic harnesses.

[`scripts/freeze-release-candidate.ps1`](../scripts/freeze-release-candidate.ps1) freezes a candidate only from a clean commit and verified acceptance/package records. It creates a source snapshot, candidate metadata, and a file-by-file `manifest.json`, then rechecks every entry. Do not rerun it merely to reproduce the already frozen 7.26B candidate.

## 3. High-level architecture

### Components and responsibilities

- **Core** (`src/Alpha6Ops.Core`) owns domain records, telemetry contracts, phase detection, rotation logic, route/arrival calculations, and terminal-shutdown domain values. It is independent of WPF.
- **Desktop shell** (`src/Alpha6Ops.Desktop`) is the primary product. WPF owns navigation, operator actions, view composition, window/tray lifetime, native connection control, and presentation of immutable session snapshots.
- **SimConnect source** reads simulator data and normalizes native values into the shared telemetry model. Aircraft-specific adapters may add qualified evidence, but do not bypass coordinator or lifecycle rules.
- **FlightSessionCoordinator** serializes the active assignment, accepted association, connection generations, normalized readings, phase recorder, operational events, recovery checkpoints, history writes, and PIREP closeout. It publishes `FlightSessionSnapshot` values to views.
- **Lifecycle/state machine** derives At Gate, Taxi Out, Airborne, Taxi In, and Complete milestones from accepted telemetry with confirmation and invalid-data protections. It owns OOOI progression; views and maps do not.
- **Event monitor** derives operational events such as engine changes, gear/flap activity, cruise/TOD, and connection state. Events are attached to the current flight history and persisted in recovery.
- **WebView2/MapLibre Map V2** renders the map only. WPF creates and owns the browser, projection queue, visibility, and lifecycle. C# serializes versioned map snapshots; local MapLibre GL JS applies sources, layers, camera, selection, lighting, and interaction in the browser.
- **History/persistence** uses local SQLite plus atomic JSON recovery and append-style diagnostic journals. Completed and interrupted operational records remain distinct from disposable diagnostics.
- **PIREP** is a single idempotent event tied to a completed authoritative flight-history row. Presentation can request submission; identity and duplicate prevention remain in persistence.
- **SimBrief** imports and caches a dispatch assignment. The accepted plan supplies planned provenance, route geometry, schedule, aircraft, gates, and references. It does not own actual telemetry or lifecycle milestones.
- **Pilot Logbook** reads authoritative history and resolves OOOI values from durable milestone records rather than display-time estimates or incidental journal order.
- **Packaging/storage** separates program files from the canonical `%LOCALAPPDATA%\Sierra 4` profile and applies bounded retention only to disposable or compressible diagnostic/cache classes.

### Authority model

| Domain | Examples | Rule |
| --- | --- | --- |
| **Authoritative operational state** | Accepted assignment/session identity, normalized telemetry, lifecycle phase and OOOI, flight-history row, recovery checkpoint, completed-session identity, PIREP submission | May change flight outcome. Must be serialized, recoverable, and protected by regression evidence. |
| **Derived/predictive state** | Route progress, remaining distance, observed-cruise classification, TOD event, predicted Gate IN, schedule variance | Derived from authoritative inputs. May be persisted for continuity, but cannot authorize OOOI, Complete, identity, or PIREP. Invalid optional state is discarded or reconstructed. |
| **Presentation state** | Map projection, sampled flown trail, selected object, camera, layers, WPF cards, labels, colors, animations | May be rebuilt without changing the flight. It consumes snapshots and never calls back as lifecycle authority. |

## 4. Checkpoint history

### 7.24C — PASS / CLOSED

7.24C corrected Map V2 scheduling and restored the map after visibility/lifecycle transitions without allowing stale work to win. It corrected route progress and the ETA behavior known at that stage, hardened active-flight recovery, and established a terminal-shutdown policy that separates Fenix intent from generic engine mechanics. The validated Fenix path allowed automatic IN/Complete after fresh master/cutoff and combustion evidence without waiting for turbine spool-down. Guarded manual Block In remained available outside qualified enhanced support. It also corrected the Pilot Logbook to use authoritative OOOI, and closed with native flight/destination evidence rather than treating automation alone as physical validation. The frozen 7.24C baseline entering 7.25 was `S4-7.24C-FINAL-L1-20260930-318659425b88`.

Useful records include [Map/route/recovery validation](flight-tracking/7.24c-map-route-recovery-validation.md), [terminal implementation](flight-tracking/7.24c-terminal-shutdown-implementation.md), [final T2 native validation](flight-tracking/7.24c-terminal-t2-native-20260930.md), and [Logbook closeout](flight-tracking/7.24c-logbook-correction-closeout.md).

### 7.25 — COMPLETE / GO

7.25 separated filed cruise, observed cruise, and actual telemetry. It added a recoverable observed-cruise state machine, sequential step-climb provenance, and TOD based on the currently observed level. It preserved ETA smoothing and anchors across a coherent restart while treating corrupt ETA state as optional. Program-health marker replacement gained bounded retry/diagnostics. Engine-completion wording was changed to describe operational confirmation rather than claiming every engine was mechanically stopped. See the [7.25 report](flight-tracking/checkpoint-7.25-report.md).

### 7.25A — CLOSED BY ENGINEERING ACCEPTANCE

DAL40 A350 endurance evidence exposed two presentation/tracking issues: a renderer-owned 2,048-point trail could lose early-flight geometry, and a naturally shallow step climb could fall below the old vertical-speed threshold before reaching the displacement threshold. The correction moved presentation history to a flight-scoped WPF owner with bounded sampling/compaction and separated climb intent from displacement/stable-level qualification.

Native DAL9929 evidence proved one live A350 integration: initial observed FL350, sequential FL350→FL370→FL390 provenance, immutable filed FL350, TOD from the latest observed FL390, pause/resume handling, 14,613 samples in one connection, a greater-than-2,048-observation flight represented as a bounded 216-point/two-segment presentation track, OOOI in order, generic two-engine compatibility, unsupported enhanced shutdown, guarded manual Block In, and one PIREP with no errors.

The preserved DAL40 replay proved 54,061 samples, FL350→FL370→FL390→FL410, the naturally shallow taper defect, and corrected TOD from FL410. Exact shallow-taper boundaries, more-than-five-minute hidden-workspace accumulation, and renderer recreation were proved by replay/automation; they were not all independently repeated inside the returned DAL9929 native package. Three low-risk opportunistic observations remain: a naturally shallow native climb preserving intent below 500 fpm before 750-foot displacement, return to Operations after more than six minutes showing the hidden interval, and V2→Classic→V2 reconstructing the complete trail. The status deliberately remains **CLOSED BY ENGINEERING ACCEPTANCE**, not “every native gate directly proven.” See the [7.25A report](flight-tracking/checkpoint-7.25a-report.md) and [engineering-acceptance decision](flight-tracking/checkpoint-7.25a-engineering-acceptance.md).

### 7.26 / 7.26B — PASS / CLOSED

7.26 audited release architecture, a 43.1 GiB working area, storage identity, generated outputs, and legacy material. 7.26B then created a verified external archive, reduced the active workspace, preserved compact hash-checked replay fixtures, consolidated the full 7.24C–7.25A implementation into Git, and established the Sierra 4 user-facing identity. It added rollback-safe legacy-profile migration, dual single-instance identity during compatibility, production retention/verified gzip, package replacement and extraction checks, production diagnostic exclusion, and a clean pre-UI freeze. No lifecycle authority or premium UI was introduced. See the [7.26 audit](checkpoint-7.26-release-architecture-storage-legacy-audit.md) and [7.26B implementation report](checkpoint-7.26b/implementation-report.md).

## 5. Flight-tracking guarantees

The following are validated architectural contracts:

- An accepted assignment is associated with an observed session deliberately; the assignment, operational flight ID, history ID, and connection/session observations are distinct identities.
- Telemetry is normalized before lifecycle use. Stale, duplicate, paused, slewing, invalid, reversed-time, discontinuous, and incompatible-aircraft input cannot silently manufacture continuity.
- OUT/OFF/ON/IN are ordered and exactly once for one operational flight. The coordinator/phase recorder owns milestones; UI, Map V2, ETA, SimBrief, and event wording do not.
- Recovery preserves accepted association, phase, OOOI, events, history identifiers, manual terminal confirmation, observation start, and coherent optional derived state. A reconnect continues the same session when continuity and identity checks pass. An application restart reconstructs the same active flight from its checkpoint.
- Persistent flight/history identity prevents a repeated flight number from merging distinct sessions. Flight number alone is never the unique key.
- Completed history is reconciled to the verified completed row even if a final checkpoint write failed after SQLite completion.
- PIREP submission is idempotent and tied to a completed flight row; one flight produces at most one `pirep_submitted` event.
- Route progress is monotonic along accepted route geometry while that geometry is reliable. It does not infer flight identity or completion. Destination completion requires authoritative lifecycle/destination checks; Complete can force presentation progress to 100% only after those checks pass.
- Pause/slew and invalid telemetry provide no qualification time for lifecycle, observed cruise, or terminal confirmation.
- Joined-in-progress and interrupted flights remain explicit. Earlier unobserved OOOI is not invented. Interrupted records do not masquerade as completed sessions.
- Same-flight-number operations remain isolated by operational/history/session identity, planned departure context, aircraft observation, and continuity boundaries.

**These contracts must not be casually rewritten during UI development.**

## 6. Map V2 architecture

Map V2 is an opt-in local renderer composed of WPF, WebView2, and local MapLibre GL JS assets. Classic map remains the fallback.

WPF owns the authoritative `FlightSessionSnapshot`, the `FlightTrackPresentationHistory`, map-host lifecycle, visibility, projection pump, renderer epoch, and camera/presentation state. `MapProjectionService` converts a snapshot plus the presentation track into a versioned serializable `MapSnapshot`: current aircraft marker, airports, planned route, completed/planned route portions, observed-track segments, reliability, lighting, and labels. `MapProjectionPump` is a latest-value worker with sequence, continuity, and visibility guards. `WebMapRenderer` posts JSON into the local page; the browser validates epoch/sequence and applies MapLibre sources/layers. Stale work, stale ACKs, and obsolete renderer generations are rejected.

The current aircraft marker comes from current valid telemetry. Planned route geometry comes from the accepted assignment and is split safely at the date line. The yellow remaining/completed plan split uses the coordinator’s authoritative route-progress fraction. The cyan flown route is presentation history derived from actual positions. Neither MapLibre geometry nor browser interaction can create OOOI, change phase, complete a flight, or submit a PIREP.

The earlier implementation kept a renderer/projection-owned trail capped at 2,048 points. Renderer recreation, long hidden workspaces, or long flights could discard the beginning of the display trail. The current correction observes the session in `MainWindow` even while the Operations workspace is hidden and supplies a flight-scoped presentation object to any renderer. It samples moving positions at 30-second/5-NM bounds, retains a live endpoint, hard-bounds materialized history at 4,096 points, compacts older interior points while keeping endpoints, and segments discontinuities caused by reconnect, invalidity, pause/slew, time gaps, implausible jumps, or session identity changes. Renderer recreation consumes the existing flight-scoped history instead of starting a new flight trail.

Visibility pauses projection/render delivery but not flight-scoped track observation. On return, the pump’s visibility generation prevents stale hidden work from publishing and a current snapshot reconstructs the route. Window minimization can retain and pause the same healthy renderer; navigation recreation is also safe because the operational owner is outside the renderer.

The 2,048 constant still exists inside the projection service as a bounded fallback and as the maximum accepted planned-route point count. It is not the owner or effective cap for the current flight-scoped observed trail. Do not move flown-track ownership back into the renderer during Hero Globe work.

## 7. Terminal shutdown and Block In

Generic mechanical engine classification answers whether normalized engine evidence looks running, starting, stopped, or unreliable. Terminal-shutdown intent answers the narrower operational question: has the pilot deliberately shut down at the destination with enough fresh aircraft-specific evidence to authorize IN? These are separate contracts.

### Fenix enhanced path

The enhanced path is an aircraft-adapter capability for reviewed Fenix A319/A320/A321 family configurations. Configuration recognition and runtime authority are separate. Each connection must freshly qualify finite binary master/cutoff values, combustion evidence, compatible two-engine coverage, and the expected cockpit binding graph. Qualification resets across relevant connection or identity changes.

At destination after ON, the policy accepts verified master/cutoff intent plus combustion transition and a stable terminal window. It can confirm Complete without waiting for N1/N2 spool-down. One engine failure, partial cutoff, stale/default/missing/contradictory data, changed identity, unsupported version/configuration, invalid telemetry, pause/slew, movement, or lost destination eligibility cannot grant enhanced authority.

### Generic and unsupported aircraft path

Unsupported aircraft use guarded manual Block In. The coordinator requires an accepted flight at the assigned destination, ON already recorded, on-ground and stationary evidence, parking brake, current valid telemetry, a stable interval, and protection against confirming while engines are still operating. The pilot then uses **Confirm Parked / Block In**; normal Complete/history closeout and the single PIREP path follow.

**iniBuilds A350 currently uses guarded manual Block In.** Fenix-specific enhanced authority must not be generalized to another aircraft without reviewed adapter semantics and native evidence. A future UI should state prominently: `AUTOMATIC SHUTDOWN CONFIRMATION NOT AVAILABLE` and `READY FOR PILOT BLOCK-IN CONFIRMATION` when those states apply.

## 8. Observed cruise and TOD

Sierra 4 maintains three truths:

1. **FILED / PLANNED CRUISE** is immutable dispatch provenance from the accepted plan.
2. **CURRENT OBSERVED CRUISE** is tracking-derived operational state qualified from telemetry.
3. **ACTUAL FLOWN TELEMETRY PROFILE** is the authoritative historical evidence and is never rewritten to match either plan or classifier.

A level matching the filed cruise qualifies after 30 valid observed seconds. A mismatched initial level requires 180 valid seconds and evidence of the preceding climb. Qualification uses finite current telemetry and allows no pause, slew, time reversal, or gap over three seconds to create stability. A restart restores valid accumulated state but grants zero credit for application downtime.

For a step climb, the state machine records climb intent separately from altitude displacement. Sustained climb intent can begin before the aircraft has moved 750 feet from the established level. The candidate must then reach the displacement threshold and establish a stable new level before the observed level changes. Sequential events preserve the previous and new observed levels. Returning toward the old level, invalidity, excessive gaps, or an expired unstable candidate cancels pending intent.

The DAL40 defect occurred because a naturally shallow A350 climb tapered below 500 fpm before reaching the old 750-foot displacement gate. A single combined predicate forgot that the climb had already clearly started. The corrected model retains bounded climb intent, then independently requires displacement and stable leveling. It does not lower the final evidence requirements.

TOD derives from descent after the **current observed cruise**, not blindly from the filed level. A later valid step level becomes the TOD reference. Go-around returns presentation/classification to climb while retaining prior cruise/TOD history; it does not erase actual telemetry or OOOI.

## 9. ETA architecture

ETA means predicted **Gate IN** in simulator UTC. Actual IN remains the authoritative historical value.

The arrival estimator uses route distance remaining, a finite exponentially smoothed airborne groundspeed, a five-minute taxi-in allowance, and schedule/actual-OUT anchors that blend toward live observation after departure. Predictions are minute-rounded and use two-minute hysteresis to avoid visual churn. Phase transitions replace an obsolete phase estimate, and predictions are kept in the future until actual IN exists.

`ArrivalEstimatorState` stores the last simulator sample, last supported phase, published ETA, airborne/taxi anchors, and smoothed speed. Recovery accepts it only when values are finite, time-bounded, phase-coherent, and coherent with the recovered authoritative flight. Invalid or legacy-missing state is reset and deterministically reconstructed from current authoritative data. Restart downtime does not manufacture movement or lifecycle events.

ETA is **never lifecycle authority**. It cannot authorize or alter assignment identity, phase, OUT/OFF/ON/IN, route progress, Complete, history, terminal shutdown, or PIREP. Corrupt ETA state cannot invalidate otherwise valid flight recovery or corrupt closeout.

## 10. Persistence and recovery

The canonical `Data` area contains assignment data, the active-flight recovery checkpoint, gate data, SQLite flight history, and PIREP authority. Recovery files are written through bounded atomic replacement. Diagnostic JSONL journals support analysis but are not the sole history authority.

Flight history assigns a durable unique ID when a tracked flight begins. Ordered events reference that ID. Completion updates the same row, and recovery carries live, pending-completed, and completed history identifiers so restart can reconcile the exact row without duplication. PIREP duplicate detection queries `pirep_submitted` for that completed flight ID.

Application restart restores one coherent active session: assignment/source, aircraft, phase, latest sample, route progress, OOOI, phase and tracking events, monitor state, observed-session identity, manual Block In state, arrival mismatch, history IDs, and optional estimator/observed-cruise state. Reconnect changes the connection generation and requires fresh transport/capability evidence while retaining the operational session when identity/continuity remain valid. Discontinuity, incompatible aircraft, position jumps, or simulator-clock breaks reset observation/session-dependent state rather than splice unrelated telemetry.

Completed sessions persist as Complete history and can reconcile a final recovery checkpoint. Interrupted sessions remain Interrupted and protect the newest unresolved diagnostic journal. Reusing a flight number does not reuse the old unique history/session ID.

Cross-store operations are defended by idempotency and reconciliation rather than a distributed transaction: SQLite completion may succeed before the JSON recovery replacement, and PIREP/history events may need a safe retry. The recovery record therefore retains the exact history identities and the next start checks the durable row. Do not replace this with display strings or flight-number matching.

## 11. Sierra 4 storage and runtime identity

The user-visible identity established by 7.26B is:

- Executable/assembly: `Sierra4.exe` / `Sierra4.dll`
- Profile: `%LOCALAPPDATA%\Sierra 4`
- Per-user install directory: `%LOCALAPPDATA%\Programs\Sierra 4`
- Start menu shortcut: `Sierra 4.lnk`
- Install ownership marker: `.sierra4`
- Uninstall key identity: `Sierra4`

The canonical profile separates `Data`, `Settings`, `Diagnostics`, `Cache`, and `State`. Program replacement does not own or delete the external profile. Internal `Alpha6Ops` namespaces and compatible filenames may remain; they are not user-facing package authority.

On first use, an existing `%LOCALAPPDATA%\Alpha6Designs\Alpha6OPS` profile is copied into a unique staging directory. Every copied file is SHA-256 checked before atomic promotion. `State\migration.json` marks success. Migration is idempotent: later starts use the completed canonical profile and do not recopy subsequent legacy changes. The legacy profile is deliberately preserved. Copy, verification, or destination-conflict failure leaves the legacy profile active and intact. During this compatibility window, Sierra 4 acquires both new and legacy mutex/event identities so two executable generations cannot write one logical profile concurrently.

Production startup applies retention only after storage initialization:

| Class | Current policy |
| --- | --- |
| Completed raw flight diagnostics | Verified atomic gzip; 14 days/recent-flight floor; 512 MiB cap |
| Interrupted diagnostics | Verified atomic gzip; 30 days/recent-incident floor; 1 GiB cap; newest unresolved protected |
| Routine logs | 14 days; 100 MiB cap |
| Crash reports | 90 days or 20 reports; 100 MiB cap |
| Map/WebView cache | 30-day, 500 MiB LRU cap |
| SimBrief cache | 90 days or latest 20; 100 MiB cap; active/recovery references protected |

Compression writes a temporary gzip, decompresses it, compares the source SHA-256, atomically promotes the gzip, and only then deletes the raw source. Flight history, PIREPs, active recovery, the newest unresolved journal, active OFP references, and user-chosen exports are outside cleanup authority. Retention rebuilds the diagnostic index from files that still exist.

`F:\Sierra4-Archive` is an external development archive. It is not inside the repository, not part of normal source/build/runtime requirements, and must not be copied into Git. The ignored `work/` and generated Sierra 4 package patterns likewise stay out of commits.

## 12. Known limitations

| Classification | Current limitation/status |
| --- | --- |
| **Known limitation** | The 7.26B installer/portable payload is built and extraction-tested, but was not automatically installed or exercised in a new native flight under the Sierra 4 package identity. Profile migration was tested with isolated roots, not the user’s live LocalAppData. |
| **Known limitation** | The installer is an unsigned per-user bootstrap built with the existing .NET Framework compiler. Clean-machine reputation/SmartScreen, ARM64, and a future signed maintained installer toolchain remain outside 7.26B. |
| **Known limitation** | Map V2 is opt-in and requires the separately installed WebView2 Evergreen Runtime. Classic fallback remains required. Some responsiveness/performance evidence is instrumented automation and cannot prove GPU presentation or every physical input path. |
| **Known limitation** | Automatic terminal shutdown is qualified only for the reviewed Fenix enhanced family path. The A350 and other unsupported aircraft require guarded manual Block In. Generic engine/manual readiness needs clearer future visual emphasis. |
| **Known limitation** | Retention runs on ordinary startup and does not compact flight-history or PIREP authority. User exports are intentionally never cleaned automatically. The legacy profile is intentionally retained as rollback evidence. |
| **Known limitation** | Internal `Alpha6Ops` namespaces/project names and some journal/database/crash prefixes remain for compatibility. |
| **Known limitation** | Production binaries deliberately omit diagnostic command harnesses. Use a development build for replay, probe, smoke, and map-check commands. |
| **Future feature** | Safe in-flight SimBrief refresh/re-sync with REV0 and reviewed revisions is not implemented. Current assignment replacement is not a safe in-flight revision operation. |
| **Future feature** | Premium UI, Hero Globe, command palette, alert command center, and the new design system have not started. |
| **Low-risk unproven observation** | Naturally shallow native climb intent below 500 fpm before 750-foot displacement; an Operations absence over six minutes retaining the hidden interval; and V2→Classic→V2 reconstructing the complete live trail were accepted from combined native plus replay/automation evidence, not all directly repeated in DAL9929. |

No open lifecycle/recovery defect was accepted at 7.26B. Optional derived recovery data can still be discarded when incoherent by design. A newly observed failure must be classified from evidence before changing the protected state machines.

The authoritative carry-forward list is [7.26B known limitations](checkpoint-7.26b/known-limitations.md).

## 13. SimBrief future revision contract

**NOT IMPLEMENTED.** Safe in-flight SimBrief refresh/re-sync is future work.

The intended architecture retains the first accepted dispatch as immutable **REV0** provenance, independent of SimBrief’s provider release number. A newly fetched OFP is a proposal, not an automatic replacement. The application must store revised route/dispatch data separately, summarize material differences, and obtain pilot confirmation where appropriate.

Applying a same-flight revision must preserve the active tracking session, operational flight/history/session IDs, accepted aircraft association, full telemetry and flown path, OUT/OFF/ON/IN, terminal evidence, event history, accumulated statistics, estimator context, recovery, and exactly-one PIREP identity. It must never relabel actual telemetry to match a plan. REV0, the accepted revision chain, proposal identity, hashes/timestamps, and reviewed differences remain available for the final report.

Current `AcceptAssignment` replacement semantics disconnect and reset recovery/identity, so they must not be reused for an in-flight revision. Future work needs an atomic append-and-current-pointer operation with safe failure/restart behavior, route-progress recomputation from the current position, and explicit identity review for material flight/airport/aircraft changes. See the [JST771 revision analysis and future design](flight-tracking/7.24c-jst771-simbrief-revision.md).

## 14. Current UI state

> [!CAUTION]
> **7.27 PREMIUM UI HAS NOT BEGUN.**

The current UI is not the final Sierra 4 visual design. Existing WPF layouts, dimensions, cards, colors, typography, and visual hierarchy are not permanent product requirements unless a specific operational interaction contract says otherwise.

The redesign must preserve persistent flight state; clear tracking/connection visibility; safe internal workspace switching; Map V2 visibility, scheduling, and renderer protections; PIREP review/submission; restart/reconnect recovery; guarded manual Block In; authoritative Logbook OOOI; and visible alerts/errors. Visual replacement cannot reset, duplicate, hide, or independently derive these operations.

## 15. Approved future Premium UI direction

The approved direction, recorded here without implementation, is a premium dark aviation-operations environment with a borderless/edgeless shell, persistent left navigation, top application shell, Exit Ops, and settings for fullscreen/windowed/resizable behavior. Workspaces should transition inside one shell using horizontal slide/fade/scale behavior rather than opening separate product windows.

Interaction has three levels: workspace switch, panel expansion, and deep overlay. Home is the primary operations screen. Its future interactive 3D Hero Globe shows the live airline/VA network, airports, routes, hubs, and active flights. Accepting a pilot flight transitions the globe toward that flight region while leaving the network visible and adding the pilot’s route/tracking.

The planned Home KPIs are Active Flights, Scheduled Today, On-Time Performance, Active Alerts, Fuel Burn Today, and CO2 Emissions Today. The direction also includes a contextual right-side panel, future command palette, alert command center, skeleton/loading states, keyboard shortcuts, and a clear Dispatch→Tracking visual transition.

This section is product direction only. It grants no authority to begin 7.27 or alter the validated foundation.

## 16. Design and development tooling for 7.27

- **Figma** is the authoritative visual/design-system source.
- **Codex** implements the approved design against the operational contracts.
- **GitHub** is source, version, pull-request, and merge authority.
- **Linear** tracks UI issues and the visual punch list.
- **Visual acceptance** requires explicit project-owner approval.

Automated tests are necessary for behavior, accessibility mechanics, rendering contracts, and regressions, but cannot approve the Premium UI’s visual result by themselves.

## 17. Things that must not be casually rewritten

The following protected areas require an explicit checkpoint scope, root-cause justification, regression coverage, preservation of established evidence, and appropriate native validation where physical behavior is involved:

- OOOI state machine and milestone ownership
- Flight/session/assignment/history identity
- Reconnect and application-restart recovery semantics
- Persistence/history ownership and cross-store reconciliation
- PIREP identity and exactly-once closeout
- Terminal-shutdown policy and Fenix adapter/capability architecture
- Guarded generic/manual Block In protections
- Observed-cruise and step-climb state machine
- Filed-versus-observed-versus-actual provenance
- TOD relationship to the current observed level
- ETA authority boundaries and recovery validation
- Map V2 latest-value scheduling, continuity, visibility, and epoch guards
- Flight-scoped flown-track history and bounded compaction
- Renderer lifecycle/restoration and Classic fallback
- Direct responsiveness acceptance model and its stated measurement boundaries
- Pilot Logbook authoritative OOOI resolver
- Production storage, gzip, retention, and protected-data exclusions
- Idempotent legacy-profile migration and dual single-instance protection

Premium UI implementation must consume these contracts rather than silently replace them.

## 18. Next planned checkpoints

Next is **7.27A — PREMIUM UI FOUNDATION / DESIGN SYSTEM / SHELL**. Planned follow-ons are **7.27B — HERO GLOBE / HOME**, **7.27C — NAVIGATION / TRANSITIONS / WORKSPACE ARCHITECTURE**, and **7.27D+ — OPERATIONAL WORKSPACE PREMIUM INTEGRATION**. Names may be refined when their scopes are approved.

None of these checkpoints has begun.

## 19. Receiving developer handoff

Use the published branch `codex/pre-premium-ui-7.26b` or the immutable annotated tag `sierra4-7.26b-pre-ui`. The tag identifies the documentation-only handoff commit; the frozen executable candidate remains bound to source commit `77e337c7c46911d0e86c576231131fd38cc5dfc5`.

From the receiving clone:

```powershell
git fetch origin codex/pre-premium-ui-7.26b --tags
git show --no-patch --decorate sierra4-7.26b-pre-ui
git status --short --branch
git switch <receiving-branch>
git merge --no-ff origin/codex/pre-premium-ui-7.26b
```

Replace `<receiving-branch>` with the developer’s actual integration branch. Do not merge while local changes are uncommitted. After the merge, require a clean status, then run:

```powershell
.\work\dotnet\dotnet.exe restore Alpha6Ops.slnx --configfile NuGet.Config
.\work\dotnet\dotnet.exe build Alpha6Ops.slnx -c Release --no-restore -nodeReuse:false
.\work\dotnet\dotnet.exe run --project tests/Alpha6Ops.Tests -c Release --no-build
```

For a release/architecture integration, also run the full `verify-checkpoint-7.25a.ps1` gate with valid .NET/Python/Node paths. Rebuilding the frozen 7.26B package is unnecessary unless the baseline itself is suspected; a new package from a later merge is a new candidate and must receive a new identity.

Read this file first, followed by the [7.26B handoff](checkpoint-7.26b/pre-premium-ui-handoff.md), [known limitations](checkpoint-7.26b/known-limitations.md), [7.25 report](flight-tracking/checkpoint-7.25-report.md), [7.25A engineering acceptance](flight-tracking/checkpoint-7.25a-engineering-acceptance.md), [Map V2 architecture](map-v2/architecture.md), [active-flight tracking](active-flight-tracking.md), and [desktop build guide](desktop.md).

Expected state after a clean merge is Sierra 4 v0.23.0 with the complete 7.24C–7.26B operational foundation and no 7.27 implementation. Generated packages, databases, `work/`, and `F:\Sierra4-Archive` must remain outside the commit.

Resolve ordinary text/document conflicts normally. For competing changes in OOOI, identity, recovery, persistence, PIREP, terminal shutdown, observed cruise/TOD, ETA authority, Map V2 scheduling/history, Logbook resolution, or storage/migration, stop and compare semantics and evidence. **Do not silently choose one side of a protected flight-tracking conflict. Flag it for human review, preserve both evidence trails, and create an explicit checkpoint scope before changing the contract.**
