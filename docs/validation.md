# Foundation validation — 2 September 2026

Desktop follow-up: native WPF preview, private runtime and setup executable are now built. Release build, the 22 domain checks, setup payload extraction and six WPF lifecycle checks passed. See [desktop validation](desktop.md) for exact coverage and remaining installation tests. The original foundation results below describe the preceding web/API slice.

Verified on Windows x64 with workspace-local .NET SDK 10.0.400, Node 24.19.0, pnpm 11.19.0, React 19.2.8, TypeScript 5.9.3 and Vite 7.3.6.

| Check | Result |
| --- | --- |
| Build `Alpha6Ops.slnx` | Passed, zero warnings/errors |
| Run `tests/Alpha6Ops.Tests` | 22 checks passed |
| Pilot JSONL replay | Four expected milestones, exit 0, downstream delays +30/+5 minutes |
| Pilot `--simconnect` | Explicit not-implemented message, expected exit 2 |
| API replay endpoint | Complete phase and expected downstream delays |
| Unknown tenant demo route | HTTP 404 |
| Frozen frontend dependency install | Passed; esbuild installation script explicitly allowed |
| TypeScript check and Vite production build | Passed |
| Browser Simple view | Original A601 10:00Z; replay changes next leg to A602 12:20Z +30 min |
| Browser OCC view | Actual A601, projected A602 +30 / A603 +5 minutes |
| Browser Reset and Advanced view | Original on-time table restored; Advanced heading and view rendered |
| Desktop visual inspection | Full-page screenshot inspected; no visible clipping or overlap |

Tests cover phase debounce, duplicates/out-of-order inputs, pause recovery, midnight, bounce/go-around, telemetry gaps, stable block-in, terminal completion, invalid speed, invalid turnaround, missing departure actual, station continuity, impossible actual turnaround, early arrival and schedule recovery.

Not verified: real MSFS 2024 or SDK binary compatibility, Windows packaging, persistence/restart recovery, real authentication/roles, mobile viewport, automated accessibility audit, load, cloud deployment, voice, or other planned subsystems. The source guard rejects production hosting, but the production failure path was not separately exercised. The read-only tenant route check does not prove security isolation for a future authenticated service.

Environment notes: system `dotnet` had runtimes but no SDK; installed SDK under ignored `work/dotnet`. Downloads/builds needed approved access beyond the restricted shell. The package registry initially failed certificate verification; enabling Node's Windows system trust store (`NODE_USE_SYSTEM_CA=1`) resolved it without disabling TLS validation. Build output, local dependencies and SDK files are ignored by Git. No remote, commit, publication or deployment was created.
