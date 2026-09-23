using System;
using System.Text.RegularExpressions;

namespace Alpha6Ops.Desktop;

// Simulator TITLE values describe a product installation, not an aircraft identity.
// Keep that raw value in diagnostics while presenting a stable manufacturer/type name.
internal static partial class AircraftIdentity
{
    internal static string CanonicalType(string? dispatchType, string? simulatorTitle = null)
    {
        var source = string.IsNullOrWhiteSpace(dispatchType) ? simulatorTitle : dispatchType;
        if (string.IsNullOrWhiteSpace(source)) return "AIRCRAFT TYPE UNAVAILABLE";
        var value = source.Trim().ToUpperInvariant();
        if (Has(value,"A220-300","A223")) return "Airbus A220-300";
        if (Has(value,"A220-100","A221")) return "Airbus A220-100";
        if (Has(value,"A320NEO","A20N")) return "Airbus A320neo";
        if (Has(value,"A321NEO","A21N")) return "Airbus A321neo";
        if (Has(value,"A319")) return "Airbus A319";
        if (Has(value,"A321")) return "Airbus A321";
        if (Has(value,"A320")||value.Contains("FENIX",StringComparison.Ordinal)) return "Airbus A320";
        if (Has(value,"737-800","B738")) return "Boeing 737-800";
        if (Has(value,"737-900","B739")) return "Boeing 737-900";
        if (Has(value,"737 MAX 8","B38M")) return "Boeing 737 MAX 8";
        if (Has(value,"777-300","B77W")) return "Boeing 777-300ER";
        if (Has(value,"787-10","B78X")) return "Boeing 787-10";
        if (Has(value,"787-9","B789")) return "Boeing 787-9";
        if (Has(value,"A350-1000","A35K")) return "Airbus A350-1000";
        if (Has(value,"A350-900","A359")) return "Airbus A350-900";
        if (Has(value,"A380","A388")) return "Airbus A380-800";
        if (Has(value,"E175","E75L","E75S")) return "Embraer E175";
        if (Has(value,"CRJ900","CRJ-900","CRJ9")) return "Bombardier CRJ900";
        return Cleanup().Replace(source.Trim(), " ").Trim();
    }

    internal static string TelemetryProvider(string? simulatorTitle)
    {
        var value=simulatorTitle??"";
        if(value.Contains("FENIX",StringComparison.OrdinalIgnoreCase))return "Fenix";
        if(value.Contains("PMDG",StringComparison.OrdinalIgnoreCase))return "PMDG";
        if(value.Contains("INIBUILDS",StringComparison.OrdinalIgnoreCase)||value.Contains("INI ",StringComparison.OrdinalIgnoreCase))return "iniBuilds";
        return "Simulator";
    }

    private static bool Has(string value,params string[] tokens)=>Array.Exists(tokens,token=>value.Contains(token,StringComparison.OrdinalIgnoreCase));
    [GeneratedRegex(@"\s+")]
    private static partial Regex Cleanup();
}
