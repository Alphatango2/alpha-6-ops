using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alpha6Ops.Desktop;

internal static class FenixShutdownCapabilityChecks
{
    private const string Aircraft = "SimObjects/Airplanes/FNX_32X";
    private const string Version = "2.4.0.4720";
    private static readonly string[] ExpectedTitles =
    [
        "FenixA320 CFM SL", "FenixA320 CFM WF", "FenixA320 IAE SL", "FenixA320 IAE WF",
        "FenixA319 CFM SL HD", "FenixA319 CFM SL SD", "FenixA319 CFM WF HD", "FenixA319 CFM WF SD",
        "FenixA319 IAE SL HD", "FenixA319 IAE SL SD", "FenixA319 IAE WF HD", "FenixA319 IAE WF SD",
        "FenixA321 CFM SL SC", "FenixA321 CFM SL TC", "FenixA321 CFM WF SC", "FenixA321 CFM WF TC",
        "FenixA321 IAE SL SC", "FenixA321 IAE SL TC", "FenixA321 IAE WF SC", "FenixA321 IAE WF TC"
    ];

    internal static void Run(string outputDirectory, Action<bool, string> Check)
    {
        var root = Path.Combine(outputDirectory, "capability-catalog-" + Guid.NewGuid().ToString("N"));
        string Fixture(string name, bool expansion = true) => CreateCatalog(Path.Combine(root, name), expansion);
        string Package(string configuration, string package = "fnx-aircraft-320") =>
            Path.Combine(Path.GetDirectoryName(configuration)!, "Packages", "Community", package);
        string Preset(string configuration, string preset = "FNX_320_IAE_WF", string file = "aircraft.cfg") =>
            Path.Combine(Package(configuration), Aircraft, "presets/fnx", preset, "config", file);
        string Attachment(string configuration, string part, string file) =>
            Path.Combine(Package(configuration), Aircraft, "attachments/fnx", part, file);
        void Replace(string file, string oldValue, string newValue) => File.WriteAllText(file, File.ReadAllText(file).Replace(oldValue, newValue, StringComparison.Ordinal));
        bool Missing(string configuration, string title = "FenixA320 IAE WF") => FenixShutdownCapabilities.Read(configuration).Find(title) is null;

        var complete = Fixture("complete");
        var catalog = FenixShutdownCapabilities.Read(complete);
        foreach (var title in ExpectedTitles)
        {
            var capability = catalog.Find(title);
            var expectedType = title.Substring(5, 4);
            var expectedPackage = expectedType == "A320" ? "fnx-aircraft-320" : "fnx-aircraft-319-321";
            Check(capability is not null && capability.AircraftTitle == title && capability.PackageVersion == Version
                && capability.IcaoType == expectedType && capability.PackageName == expectedPackage,
                "catalog resolves shipped twin-engine capability: " + title);
        }
        Check(catalog.Find("Airbus A220-300") is null && catalog.Find("FenixA320 IAE WF unofficial") is null,
            "unlisted aircraft and title-prefix lookalikes remain unavailable");
        Check(catalog.Find("fenixa320 iae wf") is null, "runtime title must match an installed preset exactly");
        var baseOnly = Fixture("base-only", false);
        Check(!Missing(baseOnly), "A320 capability does not require the optional A319/A321 expansion");
        Check(Missing(baseOnly, "FenixA321 IAE WF SC"), "absent expansion cannot provide an A321 capability");
        Check(FenixShutdownCapabilities.Read(null).Find("FenixA320 IAE WF") is null
            && FenixShutdownCapabilities.Empty.Find("FenixA320 IAE WF") is null
            && Missing(Path.Combine(root, "absent.opt")), "absent configuration produces an empty catalog");

        var missingManifest = Fixture("missing-manifest");
        File.Delete(Path.Combine(Package(missingManifest), "manifest.json"));
        Check(Missing(missingManifest) && Missing(missingManifest, "FenixA321 IAE WF SC"), "base manifest is required for base and shared-cockpit expansion capabilities");
        var mismatch = Fixture("expansion-version");
        Replace(Path.Combine(Package(mismatch, "fnx-aircraft-319-321"), "manifest.json"), Version, "2.4.0.9999");
        Check(!Missing(mismatch) && Missing(mismatch, "FenixA321 IAE WF SC"), "unreviewed expansion version does not disable valid base A320 or qualify expansion");
        var futureBase = Fixture("future-base");
        Replace(Path.Combine(Package(futureBase), "manifest.json"), Version, "9.0.0.0");
        Check(Missing(futureBase) && Missing(futureBase, "FenixA319 CFM WF SD"), "unknown future base version cannot inherit the reviewed switch contract");
        var provider = Fixture("provider");
        Replace(Path.Combine(Package(provider), "manifest.json"), "Fenix Simulations", "Unrelated Vendor");
        Check(Missing(provider), "package naming alone does not identify the Fenix provider");
        var malformed = Fixture("malformed-manifest");
        File.WriteAllText(Path.Combine(Package(malformed), "manifest.json"), "{broken");
        Check(Missing(malformed), "malformed manifest fails closed without escaping the optional resolver");
        var presetProvider = Fixture("preset-provider");
        Replace(Preset(presetProvider), "FenixSim", "OtherSim");
        Check(Missing(presetProvider) && !Missing(presetProvider, "FenixA320 CFM WF"), "untrusted preset is isolated from other valid candidates");
        var wrongType = Fixture("wrong-family");
        Replace(Attachment(wrongType, "Part_Exterior_Fuselage_A320", "config/aircraft.cfg"), "A320", "A220");
        Check(Missing(wrongType), "a Fenix-looking title cannot override the actual airframe type");
        var wrongEngineCount = Fixture("wrong-engine-count");
        Replace(Attachment(wrongEngineCount, "Part_Exterior_Fuselage_A320", "config/aircraft.cfg"), "icao_engine_count = 2", "icao_engine_count = 4");
        Check(Missing(wrongEngineCount), "a four-engine definition cannot claim the two-channel contract");
        var wrongEngineType = Fixture("wrong-engine-type");
        Replace(Attachment(wrongEngineType, "Function_Exterior_A320_IAE_WF", "config/engines.cfg"), "engine_type = 1", "engine_type = 0");
        Check(Missing(wrongEngineType), "non-jet engine configuration cannot claim the Fenix jet contract");
        var extraEngine = Fixture("extra-engine");
        File.AppendAllText(Attachment(extraEngine, "Function_Exterior_A320_IAE_WF", "config/engines.cfg"), "\nengine.2 = 6,0,-4\n");
        Check(Missing(extraEngine), "engine definitions must cover exactly the two expected channels");
        var turbineTuning = Fixture("repeated-unrelated-tuning");
        File.AppendAllText(Attachment(turbineTuning, "Function_Exterior_A320_IAE_WF", "config/engines.cfg"),
            "\n[TURBINEENGINEDATA]\noil_press_tuning_constant = 1.0\noil_press_tuning_constant = 1.0\n");
        Check(!Missing(turbineTuning), "repeated installed turbine tuning does not obscure valid engine identity");
        var noCockpit = Fixture("missing-cockpit");
        File.Delete(Attachment(noCockpit, "Part_Interior_Cockpit", "model/Cockpit.xml"));
        Check(Missing(noCockpit), "missing shared cockpit provenance cannot qualify a candidate");
        var foreignCockpit = Fixture("foreign-cockpit");
        Replace(Attachment(foreignCockpit, "Function_Interior_A320", "config/attached_objects.cfg"), "Part_Interior_Cockpit", "Part_Interior_Other");
        Check(Missing(foreignCockpit), "different cockpit attachment cannot inherit the master-switch contract");
        var missingMaster = Fixture("missing-master-binding");
        Replace(Attachment(missingMaster, "Part_Interior_Cockpit", "model/Cockpit_Behavior.xml"), "S_ENG_MASTER_2", "S_OTHER_2");
        Check(Missing(missingMaster), "both indexed master bindings must be present in the shared cockpit");
        var duplicate = Fixture("duplicate-title");
        Replace(Preset(duplicate, "FNX_320_CFM_WF"), "FenixA320 CFM WF", "FenixA320 IAE WF");
        Check(Missing(duplicate), "two installed presets claiming the same title are ambiguous");
        var duplicateField = Fixture("duplicate-field");
        File.AppendAllText(Preset(duplicateField), "\ntitle = FenixA320 IAE WF\n");
        Check(Missing(duplicateField), "duplicate identity fields are rejected instead of last-value-wins parsing");
        var escape = Fixture("attachment-escape");
        Replace(Preset(escape, file: "attached_objects.cfg"), Aircraft + "/attachments/fnx/Function_Exterior_A320_IAE_WF", "../../outside");
        Check(Missing(escape), "attachment traversal cannot escape the bounded Fenix package roots");
        var renamed = Fixture("catalog-title");
        Replace(Preset(renamed), "FenixA320 IAE WF", "Installed Fenix Test Variant");
        Check(!Missing(renamed, "Installed Fenix Test Variant") && Missing(renamed), "supported identity comes from proven configuration rather than a hardcoded title list");
        var multipleRoots = Fixture("ambiguous-configuration");
        File.AppendAllText(multipleRoots, "\n" + File.ReadAllText(multipleRoots));
        Check(Missing(multipleRoots), "ambiguous installed-package roots cannot select a catalog");
    }

    // Minimal synthetic equivalents of the installed modular Fenix graph, without proprietary assets.
    internal static string CreateCatalog(string directory, bool includeExpansion = true)
    {
        directory = Path.GetFullPath(directory);
        var packages = Path.Combine(directory, "Packages");
        var basePackage = Path.Combine(packages, "Community", "fnx-aircraft-320");
        void Write(string path, string value) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, value); }
        string Part(string package, string name, string file) => Path.Combine(package, Aircraft, "attachments/fnx", name, file);
        var configuration = Path.Combine(directory, "UserCfg.opt");
        Write(configuration, $"InstalledPackagesPath \"{packages}\"\n");
        foreach (var type in includeExpansion ? new[] { "A320", "A319", "A321" } : new[] { "A320" })
        {
            var package = type == "A320" ? basePackage : Path.Combine(packages, "Community", "fnx-aircraft-319-321");
            Write(Path.Combine(package, "manifest.json"), JsonSerializer.Serialize(new { content_type = "AIRCRAFT", title = type == "A320" ? "Fenix Airbus A320" : "Fenix Airbus A319 & A321", creator = "Fenix Simulations", manufacturer = "Fenix Simulations", package_version = Version, dependencies = Array.Empty<string>() }));
            Write(Part(package, "Part_Exterior_Fuselage_" + type, "config/aircraft.cfg"), $"[GENERAL]\nicao_type_designator = \"{type}\"\nicao_manufacturer = \"AIRBUS\"\nicao_engine_type = \"Jet\"\nicao_engine_count = 2\nicao_generic = 0\n");
            foreach (var engine in new[] { "CFM", "IAE" })
            foreach (var wing in new[] { "SL", "WF" })
            {
                var exterior = $"Function_Exterior_{type}_{engine}_{wing}";
                Write(Part(package, exterior, "attachment.cfg"), $"[Inherit]\nbase = \"{Aircraft}/attachments/fnx/Part_Exterior_Fuselage_{type}\"\n");
                Write(Part(package, exterior, "config/aircraft.cfg"), $"[GENERAL]\nicao_model = \"A-{type[1..]} {engine} {wing}\"\n");
                Write(Part(package, exterior, "config/engines.cfg"), "[GENERALENGINEDATA]\nengine_type = 1\nengine.0 = 6,-19.348571,-4\nengine.1 = 6,19.348571,-4\n");
                foreach (var cabin in type switch { "A319" => new[] { "HD", "SD" }, "A321" => new[] { "SC", "TC" }, _ => new[] { "" } })
                {
                    var suffix = cabin == "" ? "" : "_" + cabin;
                    var titleSuffix = cabin == "" ? "" : " " + cabin;
                    var preset = $"FNX_{type[1..]}_{engine}_{wing}{suffix}";
                    var interior = type switch { "A319" => "Function_Interior_A319_" + cabin, "A321" => "Function_Interior_A321_" + (cabin == "SC" ? "SingleClass" : "TwoClass"), _ => "Function_Interior_A320" };
                    var config = Path.Combine(package, Aircraft, "presets/fnx", preset, "config");
                    Write(Path.Combine(config, "aircraft.cfg"), $"[fltsim.0]\ntitle = \"Fenix{type} {engine} {wing}{titleSuffix}\"\nui_createdby = \"FenixSim\"\nui_type = \"{type} | {engine} | {wing}\"\n");
                    Write(Path.Combine(config, "attached_objects.cfg"), $"[sim_attachment.0]\nattachment_root = \"{Aircraft}/attachments/fnx/{exterior}\"\nattachment_file = \"model/Exterior.xml\"\n[sim_attachment.1]\nattachment_root = \"{Aircraft}/attachments/fnx/{interior}\"\nattachment_file = \"model/Interior.xml\"\n");
                    Write(Path.Combine(config, "engines.cfg"), "[MODULAR_MERGE]\nauto = true\n");
                    Write(Path.Combine(config, "systems.cfg"), "[MODULAR_MERGE]\nauto = true\n");
                    Write(Part(package, interior, "config/attached_objects.cfg"), $"[sim_attachment.0]\nattachment_root = \"{Aircraft}/attachments/fnx/Part_Interior_Cockpit\"\nattachment_file = \"model/Cockpit.xml\"\n");
                }
            }
        }
        Write(Part(basePackage, "Part_Interior_Cockpit", "model/Cockpit.xml"), "<ModelInfo><Behaviors><IncludeBase RelativeFile=\"Cockpit_Behavior.xml\" /></Behaviors></ModelInfo>");
        Write(Part(basePackage, "Part_Interior_Cockpit", "model/Cockpit_Behavior.xml"), """
            <ModelBehaviors>
              <Include Path="fnx32x\Templates.xml" />
              <Component ID="Throttle">
                <UseTemplate Name="FNX32X_Interact_Switch_Toggle_Lift_Template"><ANIM_NAME>Throttle_Engine_1_Master_Switch</ANIM_NAME><VAR_NAME>S_ENG_MASTER_1</VAR_NAME></UseTemplate>
                <UseTemplate Name="FNX32X_Interact_Switch_Toggle_Lift_Template"><ANIM_NAME>Throttle_Engine_2_Master_Switch</ANIM_NAME><VAR_NAME>S_ENG_MASTER_2</VAR_NAME></UseTemplate>
              </Component>
            </ModelBehaviors>
            """);
        Write(Path.Combine(basePackage, "ModelBehaviorDefs/fnx32x/Templates.xml"), "<ModelBehaviors><Include RelativeFile=\"Templates\\Interactions.xml\" /></ModelBehaviors>");
        Write(Path.Combine(basePackage, "ModelBehaviorDefs/fnx32x/Templates/Interactions.xml"), """
            <ModelBehaviors><Template Name="FNX32X_Interact_Switch_Toggle_Lift_Template">
              <Component><Animation><Parameter><Code>(L:#VAR_NAME#) #ANIM_LENGTH# *</Code></Parameter></Animation>
              <MouseRect><CallbackCode><Code>(M:Event) 'LeftSingle' scmi 0 == if{ (L:#VAR_NAME#) ! (&gt;L:#VAR_NAME#) }</Code></CallbackCode></MouseRect></Component>
            </Template></ModelBehaviors>
            """);
        return configuration;
    }
}
