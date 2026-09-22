# v0.18.1 — Flight Lab retirement

Local maintenance update to checkpoint 7.13. No GitHub publication.

- Removed the Flight Lab solution project, source implementation, named-pipe protocol and OPS client, dedicated Lab tests, settings card and connection button.
- OPS connects directly to MSFS through SimConnect; the Lab-specific identity bypass and connection branches are gone.
- New installer/portable payloads contain only OPS and its required dependencies. The .NET desktop runtime is shared and remains necessary for OPS.
- Successful upgrade replaces the older installation, removing its FlightLab subfolder, and removes the old Start menu shortcut. A running legacy Lab must be closed before upgrading so its files can be replaced safely.
- Historical Lab records remain readable, labeled LEGACY TEST FLIGHT. Pilot logs/settings live outside the replaced installation and are preserved.
- The recorded-flight replay and core/WPF test suites remain independent of Lab.

The old source icons and ignored historical development/build artifacts are not packaged. Automatic approval review rejected filesystem deletion of the old source folder/icon files with the generic reason “blocked by policy”; source-project deletion and removal from all active build references succeeded.

Update using `outputs/Alpha6OPS-Setup-0.18.1.exe`, or extract `outputs/Alpha6OPS-Desktop-0.18.1-win-x64.zip` into a fresh folder for portable use. No installed application is automatically updated by creating these packages.

## Verification

- 99 core checks passed.
- New ZIP contains zero Flight Lab/Alpha6FlightLab/A6FL entries.
- Isolated installer upgrade from an extracted v0.18.0 payload (which included Lab) to v0.18.1 removed the Lab folder, updated the ownership marker and DLL version, and preserved an external pilot-data sentinel. This did not modify the real installed app or Start menu.
- The upgraded package passed the desktop/WPF and shared-progress/ETA integration suites.
- All five recorded-flight replay checks passed on the upgraded executable, covering 47,142 telemetry samples.
- SHA256 hashes are saved beside the package in `outputs/Alpha6OPS-0.18.1-SHA256.txt`.
