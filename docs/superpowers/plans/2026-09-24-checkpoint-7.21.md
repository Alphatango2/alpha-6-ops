# Checkpoint 7.21 — Map V2 architecture and isolated prototype plan

> Execute inline with executing-plans; independent research and final review use focused agents. Preserve evidence and decisions here for interruption recovery.

**Goal:** Select a defensible map architecture using current official evidence and a measured, non-production WPF prototype.

**Architecture:** Provider-neutral immutable map records and an IMapRenderer contract are demonstrated only in a standalone prototype. The production OperationsMapHost, FlightRouteMap and FlightSessionCoordinator remain untouched. The prototype uses a local-only page and synthetic aircraft, with existing real Natural Earth geography.

**Stack:** .NET 10 WPF; candidate WebView2 + MapLibre GL JS, subject to comparison; no production dependency change.

**Spec:** `docs/superpowers/specs/2026-09-24-checkpoint-7.21.md` (user supplied).

## Constraints

- Stop at architecture/selection/prototype; no production Map V2 or world engine.
- Renderer owns presentation only; typed sources distinguish Observed/Inferred/Simulated/Replay/Unknown.
- No tokens, provider accounts, remote production tiles or fabricated airline state.
- Required production regression: 1,871 prior assertions, nine journals, 91,366 samples; report native skips honestly.
- Current official project status supersedes remembered capabilities; distinguish measurements from predictions.

## Tasks and validation

- [x] Research candidate renderers and provider terms. Root covers JS/WebView2; two independent researchers cover alternatives and geographic/solar sources. Save direct official links and caveats in `docs/map-v2/`.
- [x] Create proof-only contracts in `prototypes/Sierra4.MapPrototype/MapContracts.cs`: immutable collections, typed sources/routes/layers/selection/camera. Host has no Core reference. Document future integration separately.
- [x] Build local-only WPF prototype with pinned MapLibre/WebView2 versions. `prepare.ps1` stages pinned assets plus existing Natural Earth and Sierra tokens into ignored `web/vendor`/generated files; no CDN at runtime. Prototype command `--verify <directory>` writes checks, screenshots and measurements and exits.
- [x] Verify geometry/contract semantics before rendering. Tests assert endpoint preservation, dateline segments without world-spanning edges, finite polar geometry, invalid-coordinate rejection and detached immutable entity storage. Demonstrate expected failure before implementation.
- [x] Demonstrate dark geography, two airports, representative great circle, heading, dynamic update, selection, pan/zoom/home/follow and disposal/recreation. Use map query hit testing and actual WebView capture, not generated artwork.
- [x] Measure bounded synthetic batches of 1/25/100/500/1000/5000 aircraft. Report update cadence, frame interval distribution, process-group CPU/working set, startup and disposal. Keep results explicitly diagnostic and hardware/workload specific. Do not extrapolate to production world simulation.
- [x] Write architecture, candidate matrix, source ledger, provider/security/failure plan, layer/solar/interpolation design and exact 7.22 scope. Separate demonstrated features from future requirements.
- [x] Run full production migration suite using existing private replay manifest; verify production diff is empty. Review isolated prototype/report, correct material issues, commit locally, provide full completion report. No production installation or push.

## Review focus

1. Browser initialization/render failure cannot block flight processing: prototype isolated, future bridge latest-only and bounded.
2. Synthetic aircraft must never reach production/runtime: no production reference or hook.
3. Invalid/out-of-order data and dateline/polar routes must not fabricate observed continuity: geometry checks and explicit draft contracts.
4. Browser origin/messages and assets must be restricted: local origin, size/type checks, no host objects/navigation/downloads/permissions.
5. Benchmark must measure real WebView rendering, not merely JS array construction: animation frames, render events, feature counts and screenshots; report visibility/hardware limitations.

## Execution ledger

- Initial state: clean 7.20 at `3b51b5c`, except preexisting untracked 7.11 bundle; left untouched. Created `codex/checkpoint-7.21`.
- Ruling: supplied checkpoint already authorizes renderer investigation and isolated prototype. Proceed with those concrete tasks without another approval gate; no production integration decision is implied.
- Ruling: keep draft contracts in the prototype, outside the production solution, so architecture can be evaluated without prematurely committing the application to a map API. Cost: moving the reviewed contracts to a production assembly is explicit 7.22 work.
- Research: current-source checks found actual native C# and community WPF options; these are documented rather than incorrectly labeled unavailable. ArcGIS licensing and current OS floor are qualified. MapLibre JS selected for staged integration, without an unmeasured comparative-speed claim.
- Verification-first geometry: `work/checkpoint-7.21/contracts-red.txt` records the expected NotImplemented failure; implemented endpoint/dateline/polar/invalid/immutable checks subsequently pass.
- Preparation/build: production NuGet feeds are intentionally cleared. Used explicit NuGet source only for isolated prototype restore; production configuration is unchanged. Pinned archive integrity and package lock retained.
- Prototype corrections: generated solar image fetch needed same-origin/data CSP permission; whole-world image needed two hemisphere quads. Picking needed stable IDs in feature properties. Independent review found stale Follow text and partial CPU sampling; fixed both. Subsequent diagnostic showed longitude clamped at 142.56 while target was 151.11 with wrapping disabled: Follow enables wrapping, World restores single-world mode. The distance assertion was also corrected to pass LngLat rather than an array.
- Accepted prototype run: `work/checkpoint-7.21/probe-accepted`, 56/56 assertions, three rendered views recreated/disposed, 1/25/100/500/1000/5000 aircraft stages. Browser PID 2236 absent after exit. Actual captures and compact synthetic results committed under `docs/map-v2/evidence`; all earlier probes remain local and are excluded from final counts.
- Final production run: `work/checkpoint-7.18a/20260924-144343-24bc24bf`. Solution, normal package and harness pass; 1,871/1,871 checks, zero failures, two native unavailable; nine replays and exactly 91,366 raw/processed samples. Existing dead PID 23988 recognized as terminated; activation passed. Journal/profile hashes unchanged.
- Review and scope: focused independent review found no material isolation blocker; root reviewed research, fixes, screenshots and complete report. No existing tracked production file changed. Full 26-item report is `docs/checkpoint-7.21.md`. Local commit contains only docs/prototype; preexisting 7.11 bundle excluded. Stop after 7.21.
