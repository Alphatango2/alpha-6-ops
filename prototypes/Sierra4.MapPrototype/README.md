# Sierra 4 Map V2 architecture prototype — 7.21

Isolated experiment, **not a production map replacement**. No references to Alpha6Ops.Core/Desktop or pilot profile data. It is deliberately absent from the production solution and installer. All aircraft/motion and scenario timestamps are synthetic; airport locations and coastlines are geographic examples.

## Build and run

From the repository root, use .NET 10 on Windows with the WebView2 Evergreen Runtime installed. Existing prepared workstation uses `work/dotnet/dotnet.exe`. This experiment does not install a runtime.

```powershell
./prototypes/Sierra4.MapPrototype/prepare.ps1
./work/dotnet/dotnet.exe restore prototypes/Sierra4.MapPrototype --source https://api.nuget.org/v3/index.json --locked-mode
./work/dotnet/dotnet.exe build prototypes/Sierra4.MapPrototype -c Release --no-restore
./prototypes/Sierra4.MapPrototype/bin/Release/net10.0-windows/Sierra4.MapPrototype.exe --verify work/checkpoint-7.21/my-run
```

The repository's production NuGet configuration intentionally clears remote feeds; the explicit restore source applies only to this isolated project. Preparation downloads pinned MapLibre 6.11.2, checks its recorded SHA-512, retains its complete license and copies existing Natural Earth land and generated Sierra palette. All runtime content is local. A restored package lock pins Microsoft.Web.WebView2 1.0.4191.47. Generated assets and binaries are ignored; do not commit the browser profile, private logs or vendor bundle.

For an interactive test, omit `--verify`. Close its window to exit. `--contracts <output-file>` runs only the eight geometry/immutable-record checks. The verifier runs approximately one minute, creates/disposes three independent WPF map views and exits with 0 on success, 1 on failure. Do not run other CPU/GPU workloads during measurements.

## Demonstrated versus proposed

Demonstrated: .NET 10 WPF browser hosting; locally styled geographic map; two airports; host-generated great-circle geometry; heading symbols; synthetic dynamic interpolation; camera movement; feature picking; route visibility; approximate physical day/night mask; actual WebView screenshots; recreation; bounded 1–5,000 entity measurements.

Draft only: the full `IMapRenderer` adapter, validated latest-snapshot mailbox, production projection, genuine observed/replay data, robust source/generation resets, offline vector-tile packages, renderer crash recovery, accessibility certification and low-end hardware budgets. These are design/7.22 requirements. The prototype's initial typed snapshot crosses WPF→JS; subsequent benchmark updates are generated inside the renderer. Results do not measure recurring C# serialization/IPC or the production flight Dispatcher under map load.

## Reading evidence

- `contracts.json`: geometry/immutable collection checks.
- `renderer-checks-N.json`: actual renderer checks for each of three recreated views.
- `startup-N.json`: elapsed construction-to-ready, runtime version and isolated browser process ID.
- `benchmark-N.json`: six-second camera/aircraft workload, counts, visible feature count, frame timing/render events, submitted source updates, detected WebGL backend and sampled host/browser process CPU/working set.
- `prototype-N.png`: WebView's real rendered content after each cycle; outer WPF title/header is not included in CapturePreview.
- `complete.json` or `failure.txt`: completion status.

Frame callbacks/render events do not establish GPU completion/present latency or a production FPS guarantee. Source updates target 30Hz but actual counts are reported. Process CPU is the sum of sampled process CPU deltas divided by elapsed time and logical CPU count, with samples at the phase boundaries and every 500ms. A process that starts/exits between samples can be missed. Summed working sets may double-count shared pages. One short run on a high-end GPU does not establish memory-leak freedom or scale on integrated graphics. All benchmark aircraft use one batched symbol layer, no per-aircraft DOM/WPF controls, and no aircraft labels/trails/weather/remote tiles.

## Notices and safety boundary

- MapLibre GL JS: BSD-3-Clause with bundled third-party terms. `prepare.ps1` copies the exact release's complete `LICENSE.txt` into `web/vendor`.
- WebView2 SDK/runtime: Microsoft license; see package LICENSE and [runtime distribution guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution).
- Geography: existing Natural Earth 110m public-domain data; voluntary credit retained. This is not navigational airport/chart data.
- Day/night: locally implemented [NOAA approximate equations](https://gml.noaa.gov/grad/solcalc/solareqns.PDF), not copied NREL SPA code. A geometric visual approximation, not certified solar ephemerides.
- Styling: palette generated from existing SierraDesignTokens; OS Segoe UI font is referenced, not redistributed.

The page uses a restricted local HTTPS virtual host and CSP, no host objects or provider keys, and blocks external navigation/resources, popups, downloads and permissions. Map data remains synthetic, renderer interactions are presentation only, and the prototype never writes to the pilot profile.

See [architecture and exact proposed 7.22 scope](../../docs/map-v2/architecture.md), [renderer research](../../docs/map-v2/renderer-alternatives.md) and [data/solar research](../../docs/map-v2/data-sources-and-solar.md).
