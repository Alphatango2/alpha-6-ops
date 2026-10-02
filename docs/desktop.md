# Sierra 4 Windows desktop

Sierra 4 v0.23.0 is a native WPF application. It calls the Core projects directly, starts no local server, and uses no browser component for the main shell. Map V2 uses the installed Microsoft Edge WebView2 Evergreen Runtime when the user enables it; the classic map remains the fallback.

## Running in development

From the repository root:

```powershell
./work/dotnet/dotnet.exe restore Alpha6Ops.slnx --configfile NuGet.Config
./work/dotnet/dotnet.exe run --project src/Alpha6Ops.Desktop
```

The development executable is `src/Alpha6Ops.Desktop/bin/Debug/net10.0-windows/Sierra4.exe`. Development builds include the command-driven verification harnesses used by the checkpoint gates.

## Building the production package

```powershell
./packaging/build-desktop.ps1 -DotNetRoot ./work/dotnet -RuntimeVersion 10.0.11
```

The build creates:

- `outputs/Sierra4-Desktop-0.23.0/`
- `outputs/Sierra4-Desktop-0.23.0-win-x64.zip`
- `outputs/Sierra4-Setup-0.23.0.exe`

The payload carries a private .NET 10 runtime. Production publishing sets `IncludeDiagnosticHarnesses=false`, so smoke, replay, probe, and map-check command entry points are absent from the shipped assembly. `scripts/test-production-package.ps1` verifies that split and confirms identical publish, ZIP, installer-extraction, and replacement payloads.

## Installation and profile identity

The unsigned per-user installer targets `%LOCALAPPDATA%\Programs\Sierra 4`, owns the directory with `.sierra4`, creates `Sierra 4.lnk`, and registers `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\Sierra4`. It requests no elevation and installs no service or firewall rule. The portable ZIP makes no registry or shortcut changes.

Product data is separate from program files. `%LOCALAPPDATA%\Sierra 4` is the canonical profile, divided into `Data`, `Settings`, `Diagnostics`, `Cache`, and `State`. On first use, an existing `%LOCALAPPDATA%\Alpha6Designs\Alpha6OPS` profile is copied through a verified staging directory and atomically promoted. The legacy profile remains intact. If migration cannot complete safely, the application continues with the legacy layout.

Sierra 4 acquires both the current and legacy single-instance identities during the compatibility window. This prevents the old and new executables from writing the same logical profile concurrently.

## Retention

Production startup applies bounded retention after storage initialization. Completed and interrupted raw diagnostic journals are compressed with verified atomic gzip; routine logs, crash reports, Map/WebView cache, and SimBrief cache have age/count/size limits. Flight history, PIREPs, active recovery, the newest unresolved journal, active SimBrief references, and user-chosen exports are protected from automatic cleanup.

## Current verification limits

The production package is built and extraction-tested but is not automatically installed. The migration suite uses isolated profile roots and does not touch the user's live LocalAppData profile. The installer remains unsigned. Clean-machine, SmartScreen reputation, ARM64, and a new native simulator flight under the Sierra 4 package identity remain outside checkpoint 7.26B.

See the [7.26B handoff](checkpoint-7.26b/pre-premium-ui-handoff.md) for the architecture boundary and the [known limitations](checkpoint-7.26b/known-limitations.md) for the full carry-forward list.
