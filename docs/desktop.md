# Windows desktop preview 0.2

The WPF pilot application calls Core directly and embeds the existing JSONL sample as a resource. It makes no HTTP requests, starts no server and uses no browser component. The web app remains available separately for future OCC/management use. Simple and Advanced views show the same deterministic rotation; milestones arrive over roughly eight seconds so the tray behavior can be demonstrated.

## Running and packaging

Run in development from the root:

```powershell
dotnet restore Alpha6Ops.slnx --configfile NuGet.Config
dotnet run --project src/Alpha6Ops.Desktop
```

The repository NuGet.Config intentionally clears package sources because there are no external .NET package dependencies. Windows Desktop targeting packs must be installed with the SDK. Add an explicitly reviewed source when later introducing NuGet packages.

To recreate both the portable ZIP and preview setup with the current Windows SDK/runtime installation:

```powershell
./packaging/build-desktop.ps1 -DotNetRoot ./work/dotnet -RuntimeVersion 10.0.11
```

Supply another local .NET 10 SDK root as needed. The build script uses the Windows .NET Framework x64 C# compiler for the small setup program. It builds the WPF app, bundles the local .NET and Windows Desktop runtime folders with their license notices, builds a small uninstaller, compresses the portable package, and embeds that payload in the setup executable. Outputs are in `outputs/` and generated binaries are ignored by Git.

This is a framework-dependent publish with a private runtime, not the SDK's single-file/self-contained publish mode. `AppHostDotNetSearch=AppRelative` and `AppHostRelativeDotNet=runtime` make the executable load only the adjacent runtime. This is a supported [.NET deployment configuration](https://learn.microsoft.com/en-us/dotnet/core/deploying/#configure-net-install-search-behavior). Runtime resolution from the packaged folder was verified. Keep all package files together. Future runtime updates require rebuilding the package; use a fresh output directory when changing runtime versions.

## Installation behavior

The unsigned preview setup extracts for the current Windows user under `%LOCALAPPDATA%/Alpha6Designs/Alpha6OPSPreview`, adds a Start menu shortcut and an uninstall registration under the current-user registry hive. It does not request administrator elevation, configure startup at login, install a service, or open firewall ports. Existing previews must be uninstalled first. The setup uses .NET Framework already supplied by current Windows installations.

Extraction validates archive destinations and uses a staging directory before moving into the fixed install location. The uninstaller requires the product marker, asks the user to confirm, refuses a running installed app, and refuses directory junctions before removing the product folder. It runs a small temporary copy to avoid the Windows executable lock; that temporary helper remains in the Windows temp folder. There is no update/repair workflow, release signing or publisher reputation. Move to a maintained, signed installer toolchain and validate upgrades before distributing publicly.

The portable ZIP skips registry and shortcut changes. Its included Uninstall.exe is for installed copies only; remove a portable copy by exiting and deleting the folder. Uninstall.exe refuses deletion without the installed product marker.

## Verification

Verified here:
- Release build: zero warnings/errors, including desktop project.
- Existing domain regression executable: all 22 checks passed.
- Actual WPF app startup, embedded replay, close-to-tray while replay continues, downstream delays, restore, reset and orderly exit.
- Setup executable payload extraction into a new workspace test directory.
- Launch of that extracted application with the same WPF smoke checks; runtimeDirectory resolved inside the extracted package.
- WPF-rendered preview inspected; corrected yellow-button text contrast.

The diagnostic command `Alpha6OPS.exe --smoke-test <output-directory>` runs the actual WPF lifecycle and writes JSON plus a PNG; it exits nonzero on failure. The setup command `Alpha6OPS-Setup-0.2.exe --extract-test <new-directory>` verifies its actual payload extraction without registering an installation.

Not verified: interactive installer shortcut/registry/uninstall round-trip, clean Windows PC without development tools, SmartScreen/publisher reputation, ARM64, multi-monitor/tray settings, full accessibility, live MSFS, persistence or voice. The installer has been built and its payload exercised, but the per-user installation flow has not been run on this account. Further network/tool installation was blocked by the session's automatic approval policy; packaging used available local components.

## Next integration

Implement the SimConnect provider and aircraft/assignment checks behind `ISimulatorTelemetry`; replace the embedded replay with an explicit source selector. Add durable flight checkpoints and offline upload before calling this a live flight client. Single-instance activation, preferences, custom approved icon assets, signing, updates and clean-PC installation tests are subsequent desktop work. The tray currently uses the standard Windows application icon because no original Alpha 6 logo asset was supplied.
