using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal static class AirportCatalog
{
    private static readonly Lazy<AirportReference[]> Airports = new(Load);
    private static readonly Lazy<Dictionary<string, AirportReference>> Index = new(() => Airports.Value
        .GroupBy(a => a.Ident, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase));
    internal static AirportReference? Find(string? ident) => ident is not null && Index.Value.TryGetValue(ident, out var airport) ? airport : null;
    internal static NearbyAirport? Nearest(GeoPosition? position, IEnumerable<AirportReference>? simulatorAirports = null)
    {
        if (position is not { IsValid: true }) return null;
        // Prefer simulator scenery when present; the offline reference supplies coverage if unavailable.
        var candidates = simulatorAirports?.ToArray();
        var source = candidates is { Length: > 0 } ? candidates : Airports.Value;
        AirportReference? best = null; var distance = 200d;
        foreach (var airport in source)
        {
            if (Math.Abs(airport.Position.Latitude - position.Latitude) > 4) continue;
            var next = position.DistanceNm(airport.Position);
            if (next < distance) { distance = next; best = airport; }
        }
        return best is null ? null : new(best, distance);
    }
    private static AirportReference[] Load()
    {
        try
        {
            using var resource = typeof(AirportCatalog).Assembly.GetManifestResourceStream("airports.tsv.gz");
            if (resource is null) return [];
            using var gzip = new GZipStream(resource, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            var airports = new List<AirportReference>();
            while (reader.ReadLine() is { } line)
            {
                var parts = line.Split('\t');
                if (parts.Length != 5 || !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                    || !double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) continue;
                var position = new GeoPosition(lat, lon);
                if (position.IsValid) airports.Add(new(parts[0], parts[1], parts[2], position, "OurAirports offline reference"));
            }
            return airports.ToArray();
        }
        catch (Exception e) when (e is IOException or InvalidDataException) { return []; }
    }
}
