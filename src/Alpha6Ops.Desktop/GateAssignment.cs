using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Alpha6Ops.Desktop;

internal sealed record GateAssignment(string? DepartureGate, string? ArrivalGate, string Source, string Confidence);

internal static partial class GateAssignmentResolver
{
    // Curated pilot convenience suggestions. They are not airport or airline operational assignments.
    private static readonly IReadOnlyDictionary<string,string[]> Catalog = new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["DAL:KATL"]=["A17","A24","B18","B26","T2"], ["DAL:KDTW"]=["A38","A45","A55","A66"],
        ["DAL:KMSP"]=["F3","F8","G4","G12"], ["DAL:KSLC"]=["A21","A29","A35","B5"],
        ["DAL:KJFK"]=["B31","B35","B39","B43"], ["DAL:KLAX"]=["21","24","26B","28"],
        ["DAL:KLGA"]=["81","84","89","92"], ["DAL:KBOS"]=["A5","A9","A14","A18"],
        ["DAL:KSEA"]=["A4","A8","A12","B5"], ["DAL:KORD"]=["E7","E11","E15","F4"],
        ["JBU:KJFK"]=["5","7","12","18","22"], ["JBU:KBOS"]=["C8","C14","C20","C27"],
        ["JBU:KLAX"]=["52A","54A","56","59"], ["JBU:KFLL"]=["F3","F5","F9","F11"],
        ["AAL:KDFW"]=["A21","A28","C12","C24"], ["AAL:KCLT"]=["B6","B12","C9","C15"],
        ["UAL:KORD"]=["B8","B14","C17","C25"], ["UAL:KDEN"]=["B24","B32","B40","B48"],
        ["SWA:KDAL"]=["9","11","13","15"], ["SWA:KMDW"]=["B6","B12","B18","B24"]
    };

    internal static GateAssignment Resolve(string airline, string flight, string origin, string destination, DateTimeOffset departure, string notes)
    {
        var departureGate=Match(DepartureGatePattern(),notes);var arrivalGate=Match(ArrivalGatePattern(),notes);
        var hasExplicitDeparture=departureGate is not null;var hasExplicitArrival=arrivalGate is not null;
        if(hasExplicitDeparture || hasExplicitArrival)
        {
            departureGate??=Suggest(airline,origin,flight,departure);
            arrivalGate??=Suggest(airline,destination,flight,departure);
            return new(departureGate,arrivalGate,"SimBrief dispatch notes"+(!hasExplicitDeparture||!hasExplicitArrival?" / catalog suggestion":""),hasExplicitDeparture&&hasExplicitArrival?"High":"Mixed");
        }
        departureGate=Suggest(airline,origin,flight,departure);arrivalGate=Suggest(airline,destination,flight,departure);
        return new(departureGate,arrivalGate,departureGate is null&&arrivalGate is null?"Unassigned":"Airline gate catalog suggestion",departureGate is null&&arrivalGate is null?"None":"Suggested");
    }

    private static string? Match(Regex pattern,string notes) => pattern.Match(notes) is {Success:true} match ? Normalize(match.Groups[1].Value) : null;
    private static string? Suggest(string airline,string airport,string flight,DateTimeOffset departure)
    {
        if(!Catalog.TryGetValue($"{airline}:{airport}",out var gates)||gates.Length==0)return null;
        var seed=$"{flight}|{departure:yyyyMMdd}|{airport}";var hash=17;
        foreach(var character in seed)hash=unchecked(hash*31+character);
        return gates[(hash&int.MaxValue)%gates.Length];
    }
    internal static string? Normalize(string? value)
    {
        var gate=(value??"").Trim().ToUpperInvariant();
        return gate.Length is >=1 and <=6 && gate.All(c=>char.IsLetterOrDigit(c)) ? gate : null;
    }

    [GeneratedRegex(@"\b(?:DEP(?:ARTURE)?\s*GATE|GATE\s*DEP(?:ARTURE)?)\s*[:#=\-]?\s*([A-Z]?\d{1,3}[A-Z]?)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex DepartureGatePattern();
    [GeneratedRegex(@"\b(?:ARR(?:IVAL)?\s*GATE|GATE\s*ARR(?:IVAL)?)\s*[:#=\-]?\s*([A-Z]?\d{1,3}[A-Z]?)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex ArrivalGatePattern();
}
