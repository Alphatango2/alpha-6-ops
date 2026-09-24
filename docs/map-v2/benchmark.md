# Map V2 isolated benchmark — 7.21

Final accepted run: `work/checkpoint-7.21/probe-accepted`, 24 September 2026. [Committed machine-readable evidence](evidence/prototype-results.json) contains all measurements and checks. Only MapLibre GL JS/WebView2 was benchmarked; no comparative speed ranking is established.

## Workload and environment

MapLibre GL JS 6.11.2, WebView2 SDK 1.0.4191.47, installed Evergreen runtime 153.0.4234.48, .NET 10 WPF x64. Windows 10 build 19045, 16 logical CPUs, NVIDIA RTX 5070 Ti through ANGLE/D3D11. WPF window 1720×980; captured WebView content 1704×889. Hardware refresh produced approximately 240 animation callbacks/second, independently of the approximately 30Hz source-update target.

Six sequential stages, each with 900ms warm-up and six seconds of scripted pan/zoom plus moving aircraft. All entities are explicitly synthetic. Two airport markers, one host-generated geographic route, existing offline Natural Earth 110m land, one batched aircraft symbol layer. No per-aircraft DOM/WPF controls, aircraft labels, trails, weather, remote tiles or world engine. The physical night layer is verified after the timed stages; these measurements do not include its active rendering cost.

Initial immutable snapshot crosses WPF→JS once. Subsequent synthetic 2Hz endpoint generation and delayed interpolation occur inside JS, with full GeoJSON `setData` targeted at 30Hz. This measures rendering/source-update feasibility, **not** recurring .NET serialization, bridge saturation or production Dispatcher contention.

## Measured results

| Submitted aircraft | Queried visible IDs at end | Source updates/s | Animation interval p95 / max (ms) | Approx. machine CPU | Peak summed process working sets (MiB) |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 1 | 27.50 | 4.3 / 4.3 | 8.83% | 577.1 |
| 25 | 23 | 27.50 | 4.3 / 4.3 | 7.99% | 610.2 |
| 100 | 91 | 27.67 | 4.3 / 4.3 | 8.26% | 593.9 |
| 500 | 472 | 27.50 | 4.3 / 4.3 | 5.13% | 624.5 |
| 1,000 | 948 | 27.66 | 4.3 / 4.3 | 9.75% | 640.9 |
| 5,000 | 4,742 | 27.49 | 8.3 / 12.6 | 12.44% | 718.3 |

No measured animation interval exceeded 50ms in the timed stages. All six stages verified unique rendered feature IDs, rather than merely counting generated arrays. Not all submitted aircraft are on screen. Icons deliberately overlap at high density; this is not a production decluttering strategy.

**Interpretation:** the chosen approach is feasible for further integration testing. Source refresh remained around 27.5Hz; p95 animation intervals increased at 5,000. Memory/process cost is substantial even for one aircraft. Six-second CPU observations are noisy and non-monotonic; the 500-aircraft value does not establish a scaling improvement. Full-source replacement, repeated GeoJSON preparation and browser/GPU resources are likely optimization targets, not profiled causal findings.

Animation callbacks and render events are not GPU-completion timing or measured input-to-display latency. Do not advertise these as guaranteed FPS. CPU sums sampled process CPU deltas divided by elapsed time and 16 logical processors; boundary samples plus 500ms sampling can miss short-lived processes. Summed working sets can double-count shared pages. GPU utilization and dedicated VRAM were not measured. No low-end/integrated GPU, remote desktop, thermal soak or simultaneous MSFS workload was tested.

## Startup, lifecycle and checks

Construction-to-local-page-ready: **977.7ms**, then **448.9ms** and **427.1ms** for recreated views. These are warm-machine observations, not clean-install cold-start guarantees. Each view passed initialization, geography, interaction, motion and lighting checks, captured its actual WebView output, and disposed. The three views shared a browser process; the recorded browser PID was absent after host exit. Three successful recreations do not prove long-term leak freedom or correct crash recovery.

Eight geometry/contract checks plus **20 + 14 + 14 renderer checks = 56 passed**, zero failed in the accepted run. Preliminary failures are retained locally and excluded from totals. They exposed a missing geometry implementation, generated-image CSP blocking, string feature-ID picking, stale Follow text and world-edge Follow clamping. All were corrected in the isolated prototype. The distance assertion also needed the API's `LngLat` object rather than a raw array. Final checks execute the same picking and button handlers as interaction.

Actual screenshots: [world view after benchmark](evidence/prototype-world.png), [freshly recreated view](evidence/prototype-recreated.png). Near-black ocean, charcoal land, restrained yellow route/selection and geographic night shading were visually reviewed. Coarse coastlines, limited labels and a flat map are intentional prototype limitations. The synthetic selected aircraft is slightly separated from Sydney after the benchmark; the route is representative, not its observed track. WebView capture excludes the native WPF title/header.

## Production acceptance still required

Use a representative lower-end Windows machine and realistic labels/trails/route density; include real snapshot projection/serialization, slow or stalled renderer, hidden/minimized views, DPI/monitor changes, context loss, disconnected startup and repeated lifecycle soak. Set explicit latency/memory/startup budgets. Measure flight Dispatcher delay and event/history completeness under that load. Compare another candidate only if the selected host fails a requirement or comparative performance becomes a decision criterion.
