using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal static class SimulatorFlightPlan
{
    // Read only the path explicitly reported by the running simulator. Never scan for a stale PLN.
    internal static (SimulatorRoute? Route, string Status) Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return (null, "No simulator flight plan");
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) ||
            !new[] { ".pln", ".xml" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            return (null, "Simulator plan path unavailable");
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Parse(stream);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or XmlException or ArgumentException or NotSupportedException)
        { return (null, "Simulator plan unreadable; using live position"); }
    }
    internal static (SimulatorRoute? Route, string Status) Parse(Stream stream)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2_000_000 });
        var doc = XDocument.Load(reader);
        var plan = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "FlightPlan.FlightPlan");
        string? Value(string name) => plan?.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim().ToUpperInvariant();
        var origin = Value("DepartureID"); var destination = Value("DestinationID");
        return FlightIdentity.IsAirportId(origin) && FlightIdentity.IsAirportId(destination)
            ? (new(origin!, destination!), "MSFS active flight plan") : (null, "Simulator plan has incomplete airports");
    }
}
