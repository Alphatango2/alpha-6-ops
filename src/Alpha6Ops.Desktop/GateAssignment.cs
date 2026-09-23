using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Alpha6Ops.Desktop;

internal sealed record GateAssignment(string? DepartureGate, string? ArrivalGate, string Source, string Confidence);

internal static partial class GateAssignmentResolver
{
    internal static GateAssignment Resolve(string airline, string flight, string origin, string destination, DateTimeOffset departure, string notes)
    {
        var departureGate=Match(DepartureGatePattern(),notes);var arrivalGate=Match(ArrivalGatePattern(),notes);
        var supplied=departureGate is not null||arrivalGate is not null;
        return new(departureGate,arrivalGate,supplied?"SimBrief dispatch notes":"Unassigned",supplied?"High":"None");
    }

    private static string? Match(Regex pattern,string notes) => pattern.Match(notes) is {Success:true} match ? Normalize(match.Groups[1].Value) : null;
    internal static string? Normalize(string? value)
    {
        var gate=(value??"").Trim().ToUpperInvariant();
        if(gate is "NA" or "N/A")return null;
        return gate.Length is >=1 and <=6 && gate.All(c=>char.IsLetterOrDigit(c)) ? gate : null;
    }

    [GeneratedRegex(@"\b(?:DEP(?:ARTURE)?\s*GATE|GATE\s*DEP(?:ARTURE)?)\s*[:#=\-]?\s*([A-Z]?\d{1,3}[A-Z]?)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex DepartureGatePattern();
    [GeneratedRegex(@"\b(?:ARR(?:IVAL)?\s*GATE|GATE\s*ARR(?:IVAL)?)\s*[:#=\-]?\s*([A-Z]?\d{1,3}[A-Z]?)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex ArrivalGatePattern();
}
