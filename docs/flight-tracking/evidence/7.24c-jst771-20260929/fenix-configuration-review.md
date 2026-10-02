# Fenix capability evidence for the JST771 review — 29 September 2026

This report records the initial read-only configuration and source review. During that phase, no production code, tests, installed package, user profile, flight record or simulator state was changed; writes were confined to this evidence folder. The collector did not open SimConnect. Source hashes describe the source snapshot when collection ran. The subsequently authorized catalog implementation and its verification are documented separately in `implementation-and-verification.md`; the findings below retain their original evidentiary scope.

**Finding:** `FenixA320 IAE WF` is a real installed Fenix preset. Its difference from the previously captured `FenixA321 IAE WF SC` is expected variant selection, not corrupt identity. There is direct installed configuration evidence for a shared master-switch contract across all 20 installed A319/A320/A321 presets. That supports a bounded family candidate resolver. Native shutdown behavior has only been demonstrated for the previously captured A321 IAE title/version; file discovery alone cannot qualify runtime shutdown authority for the other variants.

## Evidence outputs

- `installed-variants.json`: 20 exact preset titles, their package and exterior/interior/fuselage identities, engine configuration, and common cockpit resolution.
- `package-manifests.json` and `package-discovery.json`: package provenance and the single relevant UserCfg setting. The full user configuration was not copied.
- `source-hashes.json`: 149 input paths and SHA-256 hashes, including production source snapshot and relevant aircraft definitions.
- `shared-master-binding-excerpts.json`: line-numbered excerpts of the shared control and interaction definitions, with original hashes.
- `config-fixtures/`: 141 unchanged, non-sensitive package metadata/configuration files in their original package-relative layout, suitable for resolver fixture tests. No simulator connection or installation is required to read these fixtures.
- `fixture-manifest.json`: source and fixture hashes. All 141 pairs match.
- `collect-config-evidence.ps1`: reproducible collector, restricted to the two named package roots, relevant source files and this output folder.

The collector verified 20 different titles, one shared cockpit behavior path, and two jet engines in every preset's inherited/exterior configuration. It is an evidence extractor, not proposed production parsing code; a production resolver needs bounded paths and defensive parsing.

## Exact installed provenance chain

The configuration source is `C:\Users\DanniBaLs\AppData\Roaming\Microsoft Flight Simulator 2024\UserCfg.opt`, line 276:

```text
InstalledPackagesPath "M:\Users\DanniBaLs\App Data\Roaming\Microsoft Flight Simulator 2024\Packages"
```

In the paths below, `COMMUNITY` means the absolute directory `M:\Users\DanniBaLs\App Data\Roaming\Microsoft Flight Simulator 2024\Packages\Community`. `AIRCRAFT` means the package-relative path `SimObjects\Airplanes\FNX_32X`.

Both `COMMUNITY\fnx-aircraft-320\manifest.json` and `COMMUNITY\fnx-aircraft-319-321\manifest.json` identify `creator` and `manufacturer` as `Fenix Simulations`, `content_type` as `AIRCRAFT`, and `package_version` as `2.4.0.4720`. Their titles are respectively `Fenix Airbus A320` and `Fenix Airbus A319 & A321`. Both manifest dependency arrays are empty; the shared cockpit requirement comes from the attachment graph, not the manifest dependency field.

For each immediate directory below `AIRCRAFT\presets\fnx`, resolve the following chain:

| Step | File and fields | What it establishes |
| --- | --- | --- |
| 1 | `config\aircraft.cfg`, `[fltsim.0]`: `title`, `ui_createdby`, `ui_type` | Exact simulator title and preset identity. All 20 set `ui_createdby = "FenixSim"`. |
| 2 | `config\attached_objects.cfg`, `[sim_attachment.0]` and `[sim_attachment.1]`: `attachment_root`, `attachment_file` | Exterior and interior component roots. These files also set `always_execute_associate_js = 1` and `always_execute_model_behavior = 1` for those attachments. |
| 3 | Exterior `attachment.cfg`, `[Inherit]`: `base` | Resolves to `AIRCRAFT\attachments\fnx\Part_Exterior_Fuselage_A319`, `...A320` or `...A321`. |
| 4 | Inherited fuselage `config\aircraft.cfg`, `[GENERAL]` | `icao_type_designator` is A319/A320/A321, `icao_manufacturer = "AIRBUS"`, `icao_engine_type = "Jet"`, `icao_engine_count = 2`, `icao_generic = 0`. |
| 5 | Exterior `config\aircraft.cfg`, `[GENERAL]`: `icao_model`; exterior `config\engines.cfg`, `[GENERALENGINEDATA]` | Engine/wing model identity and `engine_type = 1`, exactly `engine.0` and `engine.1`. The preset's own `engines.cfg`/`systems.cfg` only request `[MODULAR_MERGE] auto = true`; reading those files in isolation would miss the engine definition. |
| 6 | Interior `config\attached_objects.cfg`, `[sim_attachment.0]` | Every interior refers to `AIRCRAFT\attachments\fnx\Part_Interior_Cockpit`, `attachment_file = "model/Cockpit.xml"`. |
| 7 | Shared cockpit `model\Cockpit.xml`, `Behaviors/IncludeBase` | Refers to `Cockpit_Behavior.xml` in the same directory. This shared component exists in the A320 base package. |
| 8 | `Cockpit_Behavior.xml`, line 2 and lines 3717–3724 | Includes `fnx32x\Templates.xml`; distinct Engine 1 and Engine 2 master animations bind to `S_ENG_MASTER_1` and `S_ENG_MASTER_2` via `FNX32X_Interact_Switch_Toggle_Lift_Template`. The master bindings have no family/engine/wing conditional around them. |
| 9 | `ModelBehaviorDefs\fnx32x\Templates.xml`, line 4; `Templates\Interactions.xml`, lines 153–186 | Resolves the shared lift-toggle template. It reads `L:#VAR_NAME#` for animation and toggles that same LVar on the cockpit interaction. |

For **JST771's A320 IAE WF**, the concrete chain is entirely in `fnx-aircraft-320`:

```text
AIRCRAFT\presets\fnx\FNX_320_IAE_WF\config\aircraft.cfg
  title = FenixA320 IAE WF
AIRCRAFT\presets\fnx\FNX_320_IAE_WF\config\attached_objects.cfg
  exterior -> AIRCRAFT\attachments\fnx\Function_Exterior_A320_IAE_WF
  interior -> AIRCRAFT\attachments\fnx\Function_Interior_A320
exterior\attachment.cfg [Inherit]
  base -> AIRCRAFT\attachments\fnx\Part_Exterior_Fuselage_A320
exterior\config\aircraft.cfg [GENERAL]
  icao_model = A-320 IAE WF
interior\config\attached_objects.cfg [sim_attachment.0]
  root -> AIRCRAFT\attachments\fnx\Part_Interior_Cockpit
  file -> model/Cockpit.xml -> Cockpit_Behavior.xml
```

**A320 does not need the A319/A321 expansion for this contract.** The four A320 presets, their exterior/interior configuration and shared cockpit are all in the base package. The expansion supplies its 16 preset/exterior/interior variants, which reference the base package's same cockpit. Therefore `ReadFenixPackageVersion` requiring both manifests and an equal version for every aircraft is unnecessarily restrictive for an A320-only installation. A resolver should validate only the package roots required by the selected candidate's dependency graph; for the observed expansion version, that is base plus expansion. This review did not uninstall anything to simulate an A320-only installation.

## Family coverage actually established

| Family | Installed exact title pattern (braces enumerate observed values, not a proposed regex) | Count | Interior function roots |
| --- | --- | --- | --- |
| A320 | `FenixA320 {CFM,IAE} {SL,WF}` | 4 | `Function_Interior_A320` |
| A319 | `FenixA319 {CFM,IAE} {SL,WF} {HD,SD}` | 8 | `Function_Interior_A319_HD`, `Function_Interior_A319_SD` |
| A321 | `FenixA321 {CFM,IAE} {SL,WF} {SC,TC}` | 8 | `Function_Interior_A321_SingleClass`, `Function_Interior_A321_TwoClass` |

All 20 configurations share the same cockpit master binding and are defined as twin jets. The exterior engine configuration differs among variants; there is no basis to claim identical spool-down or fuel-flow behavior for CFM and IAE. Searching readable CFG/XML/JS content in the two packages found the two master variables in the cockpit controls and sound bindings, but no implementation mapping from those controls to `GENERAL ENG COMBUSTION`. The proprietary runtime may implement that mapping outside the inspected text. This review does not claim to have inspected that runtime or captured any new native behavior.

The earlier parked native report, `docs\flight-tracking\7.24c-fenix-shutdown-native-20260928.md`, demonstrated independent 1-to-0 master transitions and subsequent combustion changes on `FenixA321 IAE WF SC`, package `2.4.0.4720`. Its shared cockpit hash equals the currently installed file. That is supporting evidence for the shared switch contract; it is not a native matrix of all 20 variants, failure modes or reconnect behavior. The XML tooltips describe actions and must not be used to reverse the observed 1=ON, 0=OFF polarity.

Fenix's [hardware integration guide](https://support.fenixsim.com/hc/en-us/articles/12466468901135-Example-of-How-to-Bind-Switches-Knobs-and-Buttons-on-FenixSim-Aircraft-to-External-Hardware) specifically directs readers to this installed cockpit XML and its `VAR_NAME` entries when locating aircraft controls. The [MSFS aircraft.cfg reference](https://docs.flightsimulator.com/msfs2024/retail/content-configuration/cfg-files/aircraft.cfg/) describes `title` as the SimObject variation identifier. Those sources support using the actual installed graph rather than guessing capability from a title substring.

## Runtime signal meaning and limits

The [SimConnect data-definition documentation](https://docs.flightsimulator.com/msfs2024/html/6_Programming_APIs/SimConnect/API_Reference/Events_And_Data/SimConnect_AddToDataDefinition.htm) supports FLOAT64 LVar access, and explains that these LVars are global/shared. An LVar name is not an authenticated provider identifier. The SDK's [RPN documentation](https://docs.flightsimulator.com/msfs2024/html/6_Programming_APIs/Reverse_Polish_Notation.htm) and [JavaScript SimVar documentation](https://docs.flightsimulator.com/msfs2024/html/6_Programming_APIs/JavaScript/Simvar.htm) document creation/default-zero semantics for previously undefined local variables. Neither successful registration nor a stream of fresh zero values establishes that the aircraft publishes a working control channel. This is a documented risk, not a claim that undefined-variable behavior was tested in the simulator during this review.

`GENERAL ENG COMBUSTION:n` is an engine state channel; `GENERAL ENG STARTER:n` describes starter state. Their SDK definitions do not make either one pilot cutoff intent, and the public reference does not guarantee a particular add-on's fidelity. See the [engine variable reference](https://docs.flightsimulator.com/msfs2024/html/6_Programming_APIs/SimVars/Aircraft_SimVars/Aircraft_Engine_Variables.htm).

The current source has useful safeguards:

- Master reads are independent requests 301/302, so optional-channel failures do not tear down primary telemetry.
- Exceptions, malformed packets, missing values, nonfinite values, values outside 0/1, and stale values are not treated as OFF.
- Each engine must previously have master=1 and combustion=1 in the adapter's connection before qualification becomes true.
- Shutdown needs both fresh master OFF readings and absent combustion for all expected engines; active starters are an operating veto. Expected runtime engine count must equal 2.
- The terminal policy separately requires the completed flight's landing, TaxiIn, destination, stationary/on-ground/brake conditions, live coherent time, and no pause/slew before completion.

Limits to carry forward explicitly:

- The adapter's qualified bits are sticky within a connection and reset on title/connection changes. A same-title reload or a new flight with the same title is not itself a reset signal at this boundary. A simulator load/session-generation boundary should invalidate candidate identity and prior qualification. Existing terminal-policy reset is not the same as clearing adapter qualification.
- Both channels reading ON while both engines combust defeats a missing channel stuck at zero. It does not, on its own, prove channel independence or continued provider ownership after the provider stops. The earlier A321 capture and distinct config bindings establish mapping evidence; new per-flight asymmetric observations would provide stronger runtime independence evidence. Simultaneous zero readings must not be portrayed as such proof.
- Master packet receipt times are retained. Primary combustion is current to the telemetry packet; optional starter/count reads are allowed up to three seconds old and are currently rewrapped with the primary receipt timestamp. A diagnostics record should preserve the actual source ages if timing fidelity matters.
- The current client has no LVar-exists flag and no active-package/VFS attestation. An installed manifest plus matching TITLE is bounded identity evidence, not proof that the same disk files are the active simulator mount. Duplicate/ambiguous title definitions or unresolved roots must not be treated as support.
- The SDK documents `AircraftLoaded` system state/events as a flight-dynamics filename. It can help invalidate qualification on aircraft loads, but its exact returned path for this modular Fenix package has not been observed here and should not be asserted to be an `aircraft.cfg` path. See [RequestSystemState](https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/api-reference/general/simconnect_requestsystemstate/) and [SubscribeToSystemEvent](https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/api-reference/general/simconnect_subscribetosystemevent/).

## What the existing S4 journal cannot prove

`TerminalSensors.Register` requests both master LVars even for titles without an adapter. However, `Observe` takes the Limited/unsupported branch before forwarding those stored master readings. `FlightSessionCoordinator.Tracking.cs` journals the derived `terminalShutdown` object plus mechanical readings, not the raw master requests, receipts, registration outcomes or numeric master values.

Consequently an unsupported JST771 journal cannot establish whether either LVar existed, returned valid values, was ON during operation, transitioned OFF at shutdown, was stale, or triggered a request exception. It cannot reconstruct actual master-switch times or retroactively replay enhanced authority. Absence of recorded master channels is a recording/selection limitation, not evidence that the user failed to shut down the engines. `Provider = unsupported` means the current exact title/version factory selected no adapter; it is not a finding that Fenix A320 lacks the capability. The parent agent's independent journal timeline remains the source for JST771 timing/count conclusions.

## Smallest safe implementation proposal

1. Replace the single hardcoded title factory with one Fenix two-engine master/combustion contract plus a bounded resolver over the two known package roots. Read exact titles from discovered presets and establish the chain above. Verify creator/type/version and the reviewed shared binding revision, unique resolution, family/engine count and required package roots. Keep title matching exact against this discovered catalog; do not use `StartsWith("Fenix")`, arbitrary community traversal or an inline list of 20 title exceptions. Read/rebuild this small catalog outside the per-frame path and invalidate it when its identity changes.
2. Treat a successful catalog match as **candidate recognized**, independently from **runtime qualified**. The installed 2.4.0.4720 contract is the reviewed revision; other versions or altered/unresolved binding graphs remain unavailable pending a reviewed contract. Package version alone must not produce Enhanced authority. Conversely, do not require an unrelated expansion for an A320 candidate.
3. Keep the existing per-engine fresh binary master plus combustion qualification and all terminal-policy guards. Grant shutdown authority only after both required channels have independently earned the current connection's ON+combustion baseline, and currently provide fresh OFF+no-combustion data. Reset prior qualification on relevant aircraft/session/load/reconnect boundaries. Never infer ON qualification, OFF intent or support from missing/default-zero values. Do not replace cutoff intent with the generic fuel valve, fuel-flow thresholds or model naming.
4. Record candidate identity and rejection reason, required package versions, shared contract revision, raw master/combustion values and their real receipt ages, qualification bits, and connection/load generation before the adapter decision. The unsupported path must retain this diagnostic evidence too. This makes the next natural flight/capture reviewable without changing past records or injecting reconstructed evidence into the journal.

For the narrowly scoped resolver correction, the existing runtime authority may be preserved rather than changing the completion policy. State the coverage precisely: family configuration recognition is verified offline, and runtime qualification continues to gate each connection. Do not label the whole family natively validated. A targeted natural startup/shutdown capture on the affected A320 can establish its observed independence/transition ordering; equivalent new evidence for CFM is still absent. A title-only replay cannot close those native-evidence gaps.

Useful fixture cases are: A320-only package graph; all 20 installed variants; absent/mismatched required expansion/base; duplicate titles; wrong creator/model/engine count; missing or escaping attachment path; wrong cockpit contract; runtime all-zero/missing/stale/nonbinary channels; wrong engine count; reconnect and title/load change. These are suggested checks for the parent implementer; no production tests were edited or run by this review.

## Selected source fingerprints

Full absolute paths and all 149 hashes are in `source-hashes.json`. The following are relative to `COMMUNITY` except the source-code row:

| Source | SHA-256 |
| --- | --- |
| `fnx-aircraft-320\manifest.json` | `66D82D714B1377F2B266F98062021A206326DE6F65D896E09EA49A47502F23A4` |
| `fnx-aircraft-319-321\manifest.json` | `EED6312C94DE423431081FF060EFCBD6563471C43B417F27E2C61A573AB77FED` |
| `fnx-aircraft-320\AIRCRAFT\presets\fnx\FNX_320_IAE_WF\config\aircraft.cfg` (AIRCRAFT expands as above) | `00F3E283CFF48B9ECFBA84C8AFDC64065FFB950E4FD0C0CA6BC82C68A69F7C8F` |
| `fnx-aircraft-319-321\AIRCRAFT\presets\fnx\FNX_321_IAE_WF_SC\config\aircraft.cfg` | `8C4029DE5C059151E95E3AC1DCD2B7B47C03AF610817E62F83955318358C88A6` |
| `fnx-aircraft-320\AIRCRAFT\attachments\fnx\Part_Interior_Cockpit\model\Cockpit_Behavior.xml` | `3EC0C065680A204BACAB4B16462B01CDE1442A20706321D7CC84A3FF4C3B321E` |
| `fnx-aircraft-320\ModelBehaviorDefs\fnx32x\Templates\Interactions.xml` | `72502389D262983341CE8D1C7C884C37479E789FD74B38084D514CABC6400D13` |
| `fnx-aircraft-320\AIRCRAFT\attachments\fnx\Function_Interior_A320\config\attached_objects.cfg` | `1932F0364D3DD5B74B04432C744A6FA944765EB8B453F20C47516414B8E763B8` |
| `fnx-aircraft-320\AIRCRAFT\attachments\fnx\Part_Exterior_Fuselage_A320\config\aircraft.cfg` | `CFE7CCBC1DB8A8BA895AA853F4F504EF7D5FE7731C1122D5AB985F9674701178` |
| Repository `src\Alpha6Ops.Desktop\ShutdownIntentAdapters.cs` at collection | `6C13F1F1C302FFDBCB060A8D0A2ECE674D99D71C10CAB3D1F819742853666B30` |
