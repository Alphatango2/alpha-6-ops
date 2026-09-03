using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha6Ops.Desktop;

internal record SimBriefImport(ActiveFlightPlan Plan, DateTimeOffset GeneratedUtc, string AircraftType,
    string Route, int? CruiseAltitudeFeet, double? RampFuel, string FuelUnits, bool FromCache);

internal static class SimBriefImporter
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static string CacheDirectory => Path.Combine(CrashReporter.RootDirectory, "SimBrief");
    private static string CachePath => Path.Combine(CacheDirectory, "latest-ofp.json");
    private static string UsernamePath => Path.Combine(CacheDirectory, "username.txt");

    internal static string LoadUsername()
    {
        try { return File.Exists(UsernamePath) ? File.ReadAllText(UsernamePath).Trim() : ""; }
        catch (IOException) { return ""; }
    }

    internal static async Task<SimBriefImport> ImportAsync(string username, CancellationToken token = default)
    {
        username = username.Trim();
        if (username.Length is < 2 or > 80) throw new ArgumentException("Enter your Navigraph Alias or SimBrief username.");
        Directory.CreateDirectory(CacheDirectory);
        try
        {
            var endpoint = "https://www.simbrief.com/api/xml.fetcher.php?username=" + Uri.EscapeDataString(username) + "&json=1";
            using var response = await Client.GetAsync(endpoint, token);
            var json = await response.Content.ReadAsStringAsync(token);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"SimBrief returned {(int)response.StatusCode}. Check the username and generate a flight plan first.");
            var imported = Parse(json, username, false);
            var temporary = CachePath + ".tmp";
            File.WriteAllText(temporary, json); File.Move(temporary, CachePath, true);
            File.WriteAllText(UsernamePath, username);
            return imported;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            if (File.Exists(CachePath) && LoadUsername().Equals(username, StringComparison.OrdinalIgnoreCase)) return Parse(File.ReadAllText(CachePath), username, true);
            throw new IOException("Could not reach SimBrief and no offline briefing is cached.", error);
        }
    }

    internal static SimBriefImport Parse(string json, string username, bool fromCache)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        string Text(string section, string name)
        {
            if (!root.TryGetProperty(section, out var group) || !group.TryGetProperty(name, out var value)) return "";
            return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
        }
        static long Epoch(string value, string name) => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && result > 0 ? result : throw new InvalidDataException("SimBrief did not provide " + name + ".");
        var airline = Text("general", "icao_airline").Trim().ToUpperInvariant();
        var number = Text("general", "flight_number").Trim().ToUpperInvariant();
        var flight = (airline + number).Trim();
        var origin = Text("origin", "icao_code").Trim().ToUpperInvariant();
        var destination = Text("destination", "icao_code").Trim().ToUpperInvariant();
        var departure = DateTimeOffset.FromUnixTimeSeconds(Epoch(Text("times", "sched_out"), "scheduled departure"));
        var arrivalValue = Text("times", "est_in");
        if (string.IsNullOrWhiteSpace(arrivalValue)) arrivalValue = Text("times", "sched_in");
        var arrival = DateTimeOffset.FromUnixTimeSeconds(Epoch(arrivalValue, "estimated arrival"));
        var generated = DateTimeOffset.FromUnixTimeSeconds(Epoch(Text("params", "time_generated"), "briefing generation time"));
        if (flight.Length < 2 || origin.Length != 4 || destination.Length != 4 || arrival <= departure)
            throw new InvalidDataException("The latest SimBrief briefing has incomplete flight identification or timing data.");
        int? altitude = int.TryParse(Text("general", "initial_altitude"), out var altitudeValue) ? altitudeValue : null;
        double? fuel = double.TryParse(Text("fuel", "plan_ramp"), NumberStyles.Float, CultureInfo.InvariantCulture, out var fuelValue) ? fuelValue : null;
        var plan = new ActiveFlightPlan(flight, Text("aircraft", "reg").Trim().ToUpperInvariant(), origin, destination, departure, arrival,
            "SimBrief", username, generated);
        return new(plan, generated, Text("aircraft", "icao_code"), Text("general", "route"), altitude, fuel, Text("params", "units").ToUpperInvariant(), fromCache);
    }
}
