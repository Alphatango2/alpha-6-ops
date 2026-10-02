using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Alpha6Ops.Desktop;

// Read-only presentation projection. The caller supplies events queried by the
// selected persistent history ID; a callsign is never an identity key here.
internal sealed record PilotLogbookOperationalTimes(DateTimeOffset? Out, DateTimeOffset? Off, DateTimeOffset? On, DateTimeOffset? In)
{
    internal string Display => $"OUT {Format(Out)}  •  OFF {Format(Off)}\nON {Format(On)}  •  IN {Format(In)}";
    private static string Format(DateTimeOffset? value) => value?.UtcDateTime.ToString("HH:mm:ss'Z'", CultureInfo.InvariantCulture) ?? "—";
    private static DateTimeOffset? Timestamp(string? value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) ? at : null;

    internal static PilotLogbookOperationalTimes Read(IReadOnlyList<FlightHistoryEventEntry> events)
    {
        var parsed = new List<(FlightHistoryEventEntry Entry, JsonElement Detail)>();
        foreach (var entry in events.OrderBy(e => e.Sequence))
        {
            if (entry.Kind is not ("pirep_submitted" or "phase" or "tracking_event")) continue;
            try
            {
                using var document = JsonDocument.Parse(entry.DetailJson);
                if (document.RootElement.ValueKind == JsonValueKind.Object) parsed.Add((entry, document.RootElement.Clone()));
            }
            catch (JsonException) { /* Historical malformed payloads supply no milestone authority. */ }
        }
        DateTimeOffset? Resolve(string field, string phase, string legacyTitle, bool last = false)
        {
            // A stored null/invalid actual is unknown, not permission to invent one
            // from a detail event. Only absent fields in old payloads use fallback.
            foreach (var item in parsed.Where(p => p.Entry.Kind == "pirep_submitted").Reverse())
                if (item.Detail.TryGetProperty(field, out var actual))
                    return actual.ValueKind == JsonValueKind.String ? Timestamp(actual.GetString()) : null;

            var milestones = parsed.Where(p => p.Entry.Kind == "phase" && Text(p.Detail, "phase") == phase)
                .Select(p => Timestamp(p.Entry.SimulatorUtc)).Where(at => at.HasValue).ToArray();
            if (milestones.Length > 0) return last ? milestones[^1] : milestones[0];

            // Pre-PIREP live history stored the milestone's tracking event only.
            // Exact historical names exclude takeoff roll, taxi, shutdown and
            // session-end descriptions. Never substitute recorded/computer UTC.
            var legacy = parsed.Where(p => p.Entry.Kind == "tracking_event" && Text(p.Detail, "Title") == legacyTitle)
                .Select(p => Timestamp(p.Entry.SimulatorUtc)).Where(at => at.HasValue).ToArray();
            return legacy.Length == 0 ? null : last ? legacy[^1] : legacy[0];
        }
        return new(Resolve("actualOut", "TaxiOut", "Block-out / taxi out"),
            Resolve("actualOff", "Airborne", "Liftoff"),
            Resolve("actualOn", "TaxiIn", "Touchdown", last: true),
            Resolve("actualIn", "Complete", "Block-in / flight complete", last: true));
    }
    private static string? Text(JsonElement detail, string name) =>
        detail.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
