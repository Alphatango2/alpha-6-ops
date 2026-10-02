# 7.24B — connected Operations layout oscillation

Status: **native validation HOLD / physical stability FAIL**. No reconnect cycling or real-flight validation is permitted while this issue remains unresolved. Classic remains default; Map V2 remains opt-in.

## Preserved installed observation

Build: `S4-7.24B-VISUAL-R2A-20260927-87fa792a7c29`, source `87fa792a7c2924b9c6684a47ad76a6d6c053591b`. The previous eight physical input checks and day/night first-frame retest passed. Those results remain separate from this new native failure.

The pilot reports continuous whole-Operations hitches/layout jumps at approximately half-second intervals while parked and connected to real MSFS telemetry. Scrollbars appear and disappear extremely quickly across the workspace. Disconnecting immediately stops the effect; disconnected Operations is stable. This is authoritative physical failure evidence even without a captured frame or exception.

The one requested parked A/B comparison returned: **“Classic also flickers while connected.”** Therefore CompositionControl/Map V2 is not required to trigger the failure. The investigation below establishes a shared Operations presentation cause.

Display previously reported: 3440×1440 at 100% scaling. No new display change reported.

## Entry evidence

`work/checkpoint-7.24b-layout-oscillation/entry-20260927-013949/` preserves git status/diff, installed R2A verification (514 matching payload files), the source comparison (223 files unchanged from R2A), profile inventory, and timestamped copies/hashes of the then-current status and two journal/report pairs. Files copied while the application was active are explicitly live snapshots and may be incomplete. Do not overwrite them with later finalized logs.

The original frozen package, INPUT R1, VISUAL R2, VISUAL R2A, and their earlier installed observations are retained unchanged.

## Investigation scope

Use an isolated test profile and captured parked telemetry before requesting further physical observations. Trace actual WPF measure/arrange effects: DesiredSize, ActualWidth/Height, ScrollViewer extent/viewport and computed scrollbar visibility, ItemsSource changes, map host geometry, RuntimeChanged activities, snapshot cadence and Dispatcher layout work. Keep transient property transitions as well as settled LayoutUpdated samples.

Do not infer the cause from a low frame rate alone. No scrollbar hiding, arbitrary product dimensions, telemetry throttling, suppressed authoritative updates, operational changes, renderer-default changes or native simulator launches. Any correction must follow measured reproduction and a regression check.

## Measured reproduction and cause

A separate diagnostic runner loaded the hash-identical frozen R2A Desktop and Core assemblies, an isolated profile, and 32 parked samples from the preserved native journal. The existing diagnostic connection source delivered these through the normal coordinator callback/publication path; no native simulator was opened or connected by the runner. The same experiment ran with Classic and Map V2.

The recorder observes the actual WPF visual tree: DesiredSize, ActualWidth/Height, RenderSize, measure/arrange validity, ScrollViewer extent/viewport/offset and computed scrollbar visibility. Dependency-property callbacks retain transient scrollbar changes; SizeChanged and LayoutUpdated retain both intermediate and settled geometry. RuntimeChanged ActivitySource intervals, incoming sample numbers, published snapshot counts, ItemsSource changes and Dispatcher layout/render/timer operations share a monotonic clock. Logging is buffered in memory; no forced UpdateLayout or CompositionTarget.Rendering observer is used. Each run requested 3440×1440 at 100%; Windows constrained the actual window to its 3440×1400 work area.

Every sample rebuilt `DashboardFlightsGrid.ItemsSource`, even though its complete row data was unchanged. The DataGrid then briefly displayed its automatic horizontal scrollbar: height **76 → 93 → 76**. Its transient extent reached **2022** against a **2016** viewport. The lower Flights/Activity layout moved **235 → 248 → 235**. These changes followed RuntimeChanged and the ItemsSource reset; they were not map-host resizing. Both map implementations reproduced the same sequence.

| Recorded-telemetry run | Samples / snapshots | Steady scrollbar transitions | Steady size events | Map-host size events |
|---|---:|---:|---:|---:|
| Frozen R2A, Classic | 32 / 36 | 58 | 290 | 0 |
| Frozen R2A, Map V2 | 32 / 36 | 58 | 290 | 0 |
| Corrected, Classic | 32 / 36 | 0 | 0 | 0 |
| Corrected, Map V2 | 32 / 36 | 0 | 0 | 0 |

“Steady” excludes the first three samples, leaving 29 repeated updates; startup may legitimately change panels. Disconnected baselines have no scrollbar transitions. The baseline also has the scrollbar pair on all 32 offered samples. The captured journal's median recorded interval is approximately 1.0003 seconds. The diagnostic intentionally offers the preserved samples every 500 ms plus dispatch overhead, stressing presentation at the pilot's estimated cadence; this is not a measurement of the native callback interval. This instrumentation reproduces the mechanism in the real WPF UI, but is not an in-process trace of the original native session.

The four final snapshots compare exactly after excluding **only `LastJournal`**, which necessarily identifies a different isolated test log. All other typed flight, telemetry, event, assignment, connection-generation and presentation fields match. The recorded source and failed installed result remain unchanged.

## Narrow correction

`MainWindow.RefreshFlightsTable` now retains the existing source and selection when **complete immutable `DashboardFlightRow` values are equal in order**. This removes the repeated destructive table reset. Changes to any typed value, membership or order still bind normally and retain selection by key. It does not compare display text, suppress snapshots or change operational decisions.

The duplicate higher-level presentation calls were not refactored. No scrollbar policy, product dimensions, update cadence, coordinator, simulator integration, map renderer, solar/first-paint logic, input routing or map contract changed. All 14 map assets and the Core binary remain identical to R2A.

Seven checks were added to the existing Desktop smoke suite: no transient scrollbar/height oscillation for equal rows; source/selection retention; genuine data/status replacement; same-text typed data updates; empty watchlist filtering; returning to all flights; clearing rows/selection. The pre-correction test failed the first two (16 scrollbar and 16 size transitions over eight refreshes). The correction passes all seven. These tests drain the Dispatcher and restore prior rows, selected row, overview, filter and dimensions. An independent read-only review found no blocking issue; its test-cleanup suggestion was addressed before freeze.

## Separate corrected validation build

- Build: **`S4-7.24B-LAYOUT-R3-20260927-666f495dce7e`**, application version **0.22.0**.
- Frozen source: `666f495dce7efb3ccd70b99939501144d91ab606`; source parent is the exact R2A snapshot. Current working branch/index were preserved.
- Source scope: `MainWindow.Dashboard.cs`, `OperationsSmokeTest.cs`, new `OperationsLayoutChecks.cs`, and the installer/readme revision labels. No other source differences from R2A.
- Installer SHA-256: `19FA4133006BFE69A6975F03467E8608630D1E140D9AFDB087FE8CAF55FB4490`.
- ZIP SHA-256: `AB94D2013983F0EE9672DDE22BA6B34498E8D4A6F6D351CE7DA891B24968E36D`.

Original, INPUT R1, VISUAL R2 and VISUAL R2A artifacts are preserved separately. This corrected build is not installed automatically.

## Final frozen-build verification

| Suite | Executed | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Core/Desktop/migration/compatibility/fleet/replay baseline | 1,878 | 1,878 | 0 | 2 |
| Map V2 input, first paint, lifecycle, fallback and contention | 162 | 162 | 0 | 0 |
| Map mailbox | 7 | 7 | 0 | 0 |
| Browser/map math | 7 | 7 | 0 | 0 |
| Input, scaling and lifecycle contracts | 22 | 22 | 0 | 0 |
| Recovery persistence | 21 | 21 | 0 | 0 |
| **Total** | **2,097** | **2,097** | **0** | **2** |

The two skipped checks are native connection lifecycle and native reconnect. All **9/9 replays** passed with **91,366/91,366** telemetry samples, independently matched to unchanged input journals. The seven new layout checks are included once in the baseline; development red/green runs and the four recorded-telemetry comparisons are not added to this total. Recovery completed 5,000 durable replacements. Production solution and required harness builds passed; the production solution reported zero warnings/errors. No dependency changed.

All **514 payload files** match across frozen portable files, ZIP and installer extraction. Extraction performed no installation. The **224 source inputs**, exact-byte source ZIP and Git bundle verify. The original and R1/R2/R2A payloads and package hashes still match, with all existing handoff companions preserved. The diagnostic report guard found **35 expected injected reports and zero unexpected reports** under the unchanged prior policy.

After Sierra 4 was closed normally, a hash-verified backup preserved **224 profile/history/settings files**, excluding regenerable MapCache. Those files remained unchanged through verification. All **514 installed files still match R2A**; R3 has not been installed. At final preflight no Alpha6OPS process was running; MSFS remained parked per the user's workflow. No native connection or reconnect was made by this work. No renderer override exists at process/user/machine scope.

Compact evidence is archived in `docs/map-v2/evidence/7.24b-layout-r3/`; full logs, buffered layout traces and the isolated diagnostic runner remain under `work/checkpoint-7.24b-layout-oscillation/`. The compact evidence includes a ZIP of the four traces and their runner source, the preserved failing regression, final checks, package/source manifests and original entry identity.

**GO for the normal LAYOUT R3 upgrade and installed identity/data verification only.** Follow [the staged retest sheet](checkpoint-7.24b-layout-r3-retest.md). The native R2A physical result remains **FAIL**, and R3 physical stability is **PENDING**. Automated reproduction demonstrates the corrected mechanism; it does not certify the installed user-visible result. Reconnect cycles, real-flight validation and 7.24B completion remain HOLD until the correction-specific physical gate is reviewed. Earlier composition-scheduling concerns are not erased by this table-layout correction.
