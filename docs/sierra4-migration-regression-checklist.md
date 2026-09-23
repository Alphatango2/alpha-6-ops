# Sierra 4 UI Migration Regression Checklist

Run after every migration checkpoint. This checklist protects the current flight behavior; it does not authorize the next checkpoint.

## Reproduce the automated baseline

Close OPS first (the runner checks activation and copies local history). Do not start or connect the simulator for this automated run. Supply a private JSON array containing the absolute paths of all nine original journals; do not commit journals or the manifest.

```powershell
./scripts/verify-migration-baseline.ps1 -ReplayManifest .work/replay-7.17-manifest.json
```

The script accepts `-DotNetRoot`, `-ProfileDirectory` and `-PythonPath` for another workstation. The manifest location above is specific to this prepared workspace. Use the configured .NET 10 SDK, not `dotnet test`: Core checks are an executable. The external migration harness is built separately and is not shipped with OPS.

The runner currently targets the v0.22.0 package names used by `packaging/build-desktop.ps1`. When a later checkpoint changes the release version, update those paths together. Preserve the frozen compatibility fixtures and original replay expectations.

Evidence is written to a fresh `work/checkpoint-7.18a/<timestamp-id>/` directory. Read `summary.json`, `migration/migration-checks.json`, `migration/replay-coverage.json`, individual Desktop reports, and build logs. Missing reports, missing journals, harness errors and mismatched coverage fail the run. Exit **0** means no failed automated checks (read remaining skips); **1** means recorded regression failures; **2** means an incomplete run. Known migration failures are not converted to passes.

## Required checks

- [ ] Production solution Release build: zero errors; review every warning.
- [ ] Normal production package build succeeds; test the packaged runtime. Do not install over the active pilot installation merely to run this checklist.
- [ ] All Core checks pass (7.18A baseline: 122).
- [ ] Desktop, dashboard/header/responsive/monitor, refresh, live identity, diagnostic index, monitor lifecycle, tracking integration and single-instance activation reports all pass.
- [ ] Explicitly exercise `includeLargeWorkload=true`; the default Desktop suite omits the 1,000-file indexing workload.
- [ ] Existing fleet-import checks pass using a real Python runtime.
- [ ] Nine original flights pass: JST221, QFA124, JST786, QLK1579, JST721, JST724, JST760, QLK1274 and ANG2.
- [ ] Per-flight SHA-256 and independently counted telemetry match the reports: **91,366 raw and processed samples**, with zero omitted pre-assignment samples in this baseline.
- [ ] Recreated/unloaded views and repeated workspace switches preserve assignment, session/connection owner, actuals, events and progress.
- [ ] Refreshing the same sample leaves estimator/tracker state, event/history state and recovery bytes unchanged.
- [ ] A first render cannot consume an unprocessed estimator sample or change operational state; repeat-render idempotence alone is insufficient.
- [ ] Samples advance recorder snapshots, geographic progress and saved telemetry in every workspace; hidden-window milestones also reach history and recovery.
- [ ] Old queued callbacks cannot enter a replacement connection; current callbacks still work.
- [ ] Assignment acceptance persists independently of presentation delays.
- [ ] PIREP submission is durable, idempotent and independent of presentation delays; closeout clears the assignment/recovery independently of animation completion.
- [ ] Renamed phase/event labels do not change PIREP eligibility or history creation. Keep typed operational meaning separate from future wording changes.
- [ ] Frozen assignment/recovery/gate/history fixtures remain readable, including absent newer fields and named NaN values.
- [ ] Copies of existing pilot history and gates remain readable; source profile hashes remain unchanged. Missing profile inputs must be reported as unavailable, not counted as successful compatibility validation.
- [ ] Review all failed/skipped checks and any change in the reported assertion count. A larger count alone is not evidence of broader independent coverage.

## Native/manual release gates

The automated harness injects telemetry below SimConnect. These remain explicitly skipped until tested with the simulator:

- [ ] Connect once, repeatedly switch/recreate workspaces, hide/restore the app, and confirm one native connection/worker continues with no event duplication or session reset.
- [ ] Rapidly disconnect/reconnect and exit during retry; confirm old callbacks are rejected and native handles/workers are disposed. Verify a real A32x/Boeing/A220 sensor sequence when the affected integration changes.

For UI/map checkpoints also inspect actual display sizes/DPI, long airport names, unavailable/stale values, keyboard focus, map date-line/high-latitude routes, observed-vs-inferred trails and long-flight rendering cost. These are release gates for those changes, not automated passes claimed by 7.18A.

## Baseline exceptions

See [checkpoint 7.18A](checkpoint-7.18a.md). Its seven failed migration contracts expose existing coupling issues. Keep them visible until a separately approved checkpoint fixes them. Do not weaken a check, rename a failure to a skip, or remove a recorded journal to obtain a green result.
