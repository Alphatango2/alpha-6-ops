# v0.18.2 — SimConnect connection hotfix

The v0.18.1 installer removed Flight Lab correctly but did not include the local SDK-path configuration required by the native SimConnect adapter. Live connection therefore failed before calling `SimConnect_Open`, with `DllNotFoundException` and zero telemetry samples.

v0.18.2 resolves `SimConnect.dll` in this order:

1. `ALPHA6_SIMCONNECT_DLL`, when it names an existing absolute file.
2. `SimConnect.dll` beside Alpha6OPS.
3. `simconnect-sdk-path.txt` beside Alpha6OPS, when it names an existing absolute file.
4. Installed SDK layouts named `SimConnect SDK/lib/SimConnect.dll` or `MSFS 2024 SDK/SimConnect SDK/lib/SimConnect.dll` on ready drives.

The local package also writes the verified SDK path `M:\SimConnect SDK\lib\SimConnect.dll` into `simconnect-sdk-path.txt`. An invalid or stale configured path no longer prevents discovery of another valid SDK installation.

Flight Lab remains removed. The installer, UI and recorded-flight work from v0.18.1 are retained.

Validation requires the packaged `--simconnect-probe` to load the native DLL and reach `SimConnect_Open`. When MSFS is not running, error `0x80004005` is the expected proof that native loading succeeded. When a flight is loaded, the probe must receive live samples.

## Validation result

- The extracted v0.18.2 package resolved `M:\SimConnect SDK\lib\SimConnect.dll`, connected to the running simulator as `SunRise`, and received three live aircraft samples with no probe failure.
- An isolated v0.18.1-to-v0.18.2 upgrade produced marker/version 0.18.2 and retained the verified SDK configuration.
- Packaged desktop smoke and shared tracking integration passed.
- All five recorded flights passed again, covering 47,142 telemetry samples.
- Package hashes are saved in `outputs/Alpha6OPS-0.18.2-SHA256.txt`.
