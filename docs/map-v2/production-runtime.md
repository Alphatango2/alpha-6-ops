# Operations Map V2: runtime, rollback and verification

Checkpoint 7.22 integrates Map V2 into Operations as an **opt-in**. Flight Tracking continues to use its existing map. The completion report and rollout decision are in [checkpoint 7.22](../checkpoint-7.22.md).

## Use and rollback

Select **Use Map V2** beneath the Operations map. **Use classic map** returns immediately to the retained renderer. The choice lasts for the application session. For a developer launch, `SIERRA4_MAP_V2=1` opts in at startup; remove that environment setting to return to the default. No existing settings, assignment, recovery or history schema changes are required.

The renderer uses local geography and application state. It requires no map-provider account, key, tile download or Internet access for its content. It does not supply airline-world traffic, live airport status, weather, roads, city lights or terminal detail.

## Runtime and package

The existing **Microsoft Edge WebView2 Evergreen Runtime** is detected through `CoreWebView2Environment.GetAvailableBrowserVersionString`. The adapter catches detection/creation failures. It tries one bounded restart and then falls back to the classic map. Operations and the flight session remain available.

WebView2 SDK **1.0.4191.47** is pinned in the Desktop project and package lock. MapLibre GL JS **6.11.2** is bundled locally; its publisher archive SHA-512 and asset SHA-256 values are recorded in `MapV2/Web/assets-manifest.json`. Licenses and attribution are distributed with those assets. The Desktop Windows target is `net10.0-windows10.0.17763.0`, which supplies the WinRT bindings needed by the composition host.

This checkpoint does **not** silently install an external runtime, package an Evergreen bootstrapper, bundle a Fixed Version browser, or change application/profile branding. Installer prerequisite handling is deferred. A user without the runtime can keep the classic renderer or install Evergreen explicitly using [Microsoft's WebView2 distribution guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution). Evergreen servicing is separate from the pinned application SDK; the local runtime is not version-frozen by this package.

Normal browser cache resides beneath the existing application profile in `MapCache`. Renderer failures produce bounded, nonfatal reason records in `MapDiagnostics`. They contain no telemetry payload or provider credentials. Diagnostics use isolated profiles. Cache cleanup is not performed on user flight data.

## Repeatable checks

Run these sequentially from the repository root with the application's normal SDK available:

```powershell
& scripts/verify-migration-baseline.ps1 -ReplayManifest .work/replay-7.17-manifest.json
& scripts/verify-map-v2.ps1 -ApplicationPath (Join-Path $PWD outputs/Alpha6OPS-Desktop-0.22.0/Alpha6OPS.exe)
```

The first command builds/packages and verifies the production baseline, including nine recorded-flight journals and exactly 91,366 samples. It does not install the package. The second verifies pinned assets, mailbox/browser checks and the actual packaged WPF/WebView map. Both preserve raw evidence under `work`. Run UI suites sequentially so focus, startup and process checks are meaningful. A live OPS session must be closed first; do not interrupt an active flight for testing.

`scripts/prepare-map-v2.ps1 -VerifyOnly` checks local asset hashes without downloading. Omit that switch only when deliberately regenerating assets from the pinned publisher archive and existing repository geography/tokens.

The map runner fails on failed assertions **or** an unexpected diagnostic crash report. Do not discard a file-save error simply because map/flight assertions passed. Earlier 7.22 runs exposed intermittent access-denied file replacement in existing flight recovery, also reproduced outside the application; see the checkpoint report. Intentional renderer-failure records belong in `MapDiagnostics`, not the unexpected-report directory.

## Remaining manual gates

Test real mouse/touch input, OS monitor-DPI transitions, screen-reader use, real simulator reconnect and extended flight sessions on representative machines. Automated layout scaling, synthetic callbacks and short lifecycle checks do not certify those cases. Retain opt-in until remaining rollout gates have evidence.
