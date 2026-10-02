using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Alpha6Ops.Desktop;

internal sealed record FenixShutdownCapability(string AircraftTitle, string PackageVersion, string IcaoType, string PackageName);

internal sealed class FenixShutdownCapabilities
{
    internal const string ReviewedVersion = "2.4.0.4720";
    private const string BasePackage = "fnx-aircraft-320";
    private const string ExpansionPackage = "fnx-aircraft-319-321";
    private const string Aircraft = "SimObjects/Airplanes/FNX_32X";
    private const string Attachments = Aircraft + "/attachments/fnx/";
    private const string Cockpit = Attachments + "Part_Interior_Cockpit";
    private const string MasterTemplate = "FNX32X_Interact_Switch_Toggle_Lift_Template";
    private readonly Dictionary<string, FenixShutdownCapability> capabilities = new(StringComparer.Ordinal);

    internal static FenixShutdownCapabilities Empty { get; } = new();
    internal FenixShutdownCapability? Find(string title) =>
        title is not null && capabilities.TryGetValue(title, out var capability) ? capability : null;

    // Configuration recognition only selects a candidate. Each connection must still
    // qualify both live master/combustion channels before it can report shutdown intent.
    internal static FenixShutdownCapabilities Read(string? userCfgPath)
    {
        if (string.IsNullOrWhiteSpace(userCfgPath)) return Empty;
        try
        {
            var settings = ReadText(userCfgPath).Split('\n').Select(line => line.Trim())
                .Where(line => line.StartsWith("InstalledPackagesPath ", StringComparison.Ordinal)).ToArray();
            if (settings.Length != 1) return Empty;
            var value = settings[0]["InstalledPackagesPath ".Length..].Trim();
            if (value.Length < 3 || value[0] != '"' || value[^1] != '"' || value[1..^1].Contains('"')) return Empty;
            var packages = value[1..^1];
            if (!Path.IsPathFullyQualified(packages)) return Empty;
            var community = Path.Combine(packages, "Community");
            var baseRoot = Within(community, BasePackage);
            if (!ValidManifest(baseRoot, "Fenix Airbus A320") || !SharedCockpit(baseRoot)) return Empty;

            var catalog = new FenixShutdownCapabilities();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var ambiguous = new HashSet<string>(StringComparer.Ordinal);
            catalog.ReadPackage(baseRoot, BasePackage, seen, ambiguous);
            var expansionPath = Path.Combine(community, ExpansionPackage);
            if (Directory.Exists(expansionPath))
            {
                try
                {
                    var expansionRoot = Within(community, ExpansionPackage);
                    if (ValidManifest(expansionRoot, "Fenix Airbus A319 & A321"))
                        catalog.ReadPackage(expansionRoot, ExpansionPackage, seen, ambiguous);
                }
                catch (Exception error) when (Unavailable(error)) { /* A320 does not require the expansion. */ }
            }
            foreach (var title in ambiguous) catalog.capabilities.Remove(title);
            return catalog;
        }
        catch (Exception error) when (Unavailable(error)) { return Empty; }
    }

    private void ReadPackage(string root, string package, HashSet<string> seen, HashSet<string> ambiguous)
    {
        var presets = Within(root, Aircraft + "/presets/fnx");
        var directories = Directory.EnumerateDirectories(presets).Take(65).ToArray();
        if (directories.Length > 64) return;
        foreach (var directory in directories)
        {
            try
            {
                var config = Within(root, Path.GetRelativePath(root, directory) + "/config");
                var aircraft = ReadIni(Within(config, "aircraft.cfg"));
                var title = Field(aircraft, "fltsim.0", "title");
                if (string.IsNullOrWhiteSpace(title) || title.Length > 255) continue;
                if (!seen.Add(title)) ambiguous.Add(title);
                if (Field(aircraft, "fltsim.0", "ui_createdby") != "FenixSim"
                    || aircraft.Keys.Count(section => section.StartsWith("fltsim.", StringComparison.OrdinalIgnoreCase)) != 1) continue;

                var attachments = ReadIni(Within(config, "attached_objects.cfg"));
                var exterior = AttachmentRoot(root, Field(attachments, "sim_attachment.0", "attachment_root"));
                var interior = AttachmentRoot(root, Field(attachments, "sim_attachment.1", "attachment_root"));
                if (Field(attachments, "sim_attachment.0", "attachment_file") != "model/Exterior.xml"
                    || Field(attachments, "sim_attachment.1", "attachment_file") != "model/Interior.xml") continue;
                var inheritance = ReadIni(Within(exterior, "attachment.cfg"));
                var fuselage = AttachmentRoot(root, Field(inheritance, "Inherit", "base"));
                var general = ReadIni(Within(fuselage, "config/aircraft.cfg"));
                var type = Field(general, "GENERAL", "icao_type_designator");
                if (type is not ("A319" or "A320" or "A321") || (package == BasePackage) != (type == "A320")
                    || Path.GetFileName(fuselage) != "Part_Exterior_Fuselage_" + type
                    || Field(general, "GENERAL", "icao_manufacturer") != "AIRBUS"
                    || Field(general, "GENERAL", "icao_engine_type") != "Jet"
                    || Field(general, "GENERAL", "icao_engine_count") != "2"
                    || Field(general, "GENERAL", "icao_generic") != "0") continue;
                var engineData = ReadIni(Within(exterior, "config/engines.cfg"), "GENERALENGINEDATA");
                if (Field(engineData, "GENERALENGINEDATA", "engine_type") != "1"
                    || !engineData["GENERALENGINEDATA"].Keys.Where(key => key.StartsWith("engine.", StringComparison.OrdinalIgnoreCase))
                        .Order(StringComparer.OrdinalIgnoreCase).SequenceEqual(new[] { "engine.0", "engine.1" }, StringComparer.OrdinalIgnoreCase)) continue;
                var model = Field(ReadIni(Within(exterior, "config/aircraft.cfg")), "GENERAL", "icao_model").Split(' ');
                if (model.Length != 3 || model[0] != "A-" + type[1..] || model[1] is not ("IAE" or "CFM") || model[2] is not ("WF" or "SL")) continue;
                var cockpit = ReadIni(Within(interior, "config/attached_objects.cfg"));
                if (Field(cockpit, "sim_attachment.0", "attachment_root").Replace('\\', '/') != Cockpit
                    || Field(cockpit, "sim_attachment.0", "attachment_file") != "model/Cockpit.xml") continue;
                capabilities[title] = new(title, ReviewedVersion, type, package);
            }
            catch (Exception error) when (Unavailable(error)) { /* A bad preset cannot promote a candidate. */ }
        }
    }

    private static bool ValidManifest(string root, string expectedTitle)
    {
        using var document = JsonDocument.Parse(ReadText(Within(root, "manifest.json")));
        var manifest = document.RootElement;
        if (manifest.ValueKind != JsonValueKind.Object || manifest.EnumerateObject().GroupBy(property => property.Name).Any(group => group.Count() != 1)) return false;
        string? Text(string name) => manifest.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() : null;
        return Text("package_version") == ReviewedVersion && Text("content_type") == "AIRCRAFT"
            && Text("creator") == "Fenix Simulations" && Text("manufacturer") == "Fenix Simulations" && Text("title") == expectedTitle;
    }

    private static bool SharedCockpit(string root)
    {
        var model = ReadXml(Within(root, Cockpit + "/model/Cockpit.xml"));
        if (model.Root?.Name != "ModelInfo" || !model.Descendants("IncludeBase").Any(node => (string?)node.Attribute("RelativeFile") == "Cockpit_Behavior.xml")) return false;
        var behavior = ReadXml(Within(root, Cockpit + "/model/Cockpit_Behavior.xml"));
        if (behavior.Root?.Name != "ModelBehaviors" || !behavior.Descendants("Include").Any(node => (string?)node.Attribute("Path") == "fnx32x\\Templates.xml")) return false;
        for (var index = 1; index <= 2; index++)
        {
            var bindings = behavior.Descendants("UseTemplate").Where(node => (string?)node.Element("VAR_NAME") == $"S_ENG_MASTER_{index}").ToArray();
            if (bindings.Length != 1 || (string?)bindings[0].Attribute("Name") != MasterTemplate
                || (string?)bindings[0].Element("ANIM_NAME") != $"Throttle_Engine_{index}_Master_Switch") return false;
        }
        var templates = ReadXml(Within(root, "ModelBehaviorDefs/fnx32x/Templates.xml"));
        if (!templates.Descendants("Include").Any(node => (string?)node.Attribute("RelativeFile") == "Templates\\Interactions.xml")) return false;
        var interactions = ReadXml(Within(root, "ModelBehaviorDefs/fnx32x/Templates/Interactions.xml"));
        var lifts = interactions.Descendants("Template").Where(node => (string?)node.Attribute("Name") == MasterTemplate).ToArray();
        return lifts.Length == 1 && lifts[0].Descendants("Animation").Descendants("Code").Any(node => node.Value.Contains("(L:#VAR_NAME#)", StringComparison.Ordinal))
            && lifts[0].Descendants("CallbackCode").Descendants("Code").Any(node => node.Value.Contains("(L:#VAR_NAME#) ! (>L:#VAR_NAME#)", StringComparison.Ordinal));
    }

    private static string AttachmentRoot(string root, string relative)
    {
        relative = relative.Replace('\\', '/');
        if (!relative.StartsWith(Attachments, StringComparison.Ordinal)) throw new InvalidDataException("Unrecognized Fenix attachment root.");
        return Within(root, relative);
    }

    private static string Within(string root, string relative)
    {
        var parts = relative.Replace('\\', '/').Split('/');
        if (Path.IsPathRooted(relative) || parts.Any(part => part is "" or "." or "..")) throw new InvalidDataException("Invalid Fenix package path.");
        var current = Path.GetFullPath(root);
        if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Ambiguous Fenix package link.");
        foreach (var part in parts)
        {
            current = Path.Combine(current, part);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Ambiguous Fenix package link.");
        }
        return current;
    }

    private static string ReadText(string path)
    {
        if (new FileInfo(path).Length > 1_000_000) throw new InvalidDataException("Fenix configuration exceeds the catalog limit.");
        return File.ReadAllText(path);
    }

    private static XDocument ReadXml(string path)
    {
        using var text = new StringReader(ReadText(path));
        using var reader = XmlReader.Create(text, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1_000_000 });
        return XDocument.Load(reader);
    }

    private static Dictionary<string, Dictionary<string, string>> ReadIni(string path, string? selectedSection = null)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? current = null;
        foreach (var raw in ReadText(path).Split('\n'))
        {
            var line = raw.Split(';', 2)[0].Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var section = line[1..^1].Trim();
                if (selectedSection is not null && !string.Equals(section, selectedSection, StringComparison.OrdinalIgnoreCase))
                { current = null; continue; }
                current = new(StringComparer.OrdinalIgnoreCase);
                if (!sections.TryAdd(section, current)) throw new InvalidDataException("Duplicate Fenix section.");
                continue;
            }
            if (selectedSection is not null && current is null) continue;
            var equal = line.IndexOf('=');
            if (current is null || equal <= 0) throw new InvalidDataException("Malformed Fenix configuration.");
            var key = line[..equal].Trim();
            var value = line[(equal + 1)..].Trim().Trim('"');
            if (!current.TryAdd(key, value)) throw new InvalidDataException("Duplicate Fenix field.");
        }
        return sections;
    }

    private static string Field(Dictionary<string, Dictionary<string, string>> ini, string section, string key) =>
        ini.TryGetValue(section, out var values) && values.TryGetValue(key, out var value) ? value : "";

    private static bool Unavailable(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException
        or XmlException or ArgumentException or NotSupportedException or InvalidOperationException;
}
