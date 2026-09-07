using System;
using System.Collections.Generic;

namespace Alpha6Ops.Desktop;

internal sealed record AirlineBrand(string Icao,string Mark,string Name,string Background,string Foreground);

internal static class AirlineBranding
{
    private static readonly IReadOnlyDictionary<string,AirlineBrand> Brands=new Dictionary<string,AirlineBrand>(StringComparer.OrdinalIgnoreCase)
    {
        ["DAL"]=new("DAL","▲","DELTA AIR LINES","#D71920","#FFFFFF"),
        ["JBU"]=new("JBU","B6","JETBLUE","#003876","#56C5D0"),
        ["AAL"]=new("AAL","AA","AMERICAN AIRLINES","#0078D2","#FFFFFF"),
        ["UAL"]=new("UAL","UA","UNITED AIRLINES","#002244","#62B5E5"),
        ["SWA"]=new("SWA","♥","SOUTHWEST AIRLINES","#304CB2","#F9B612"),
        ["ASA"]=new("ASA","AS","ALASKA AIRLINES","#01426A","#8DC63F"),
        ["FFT"]=new("FFT","F9","FRONTIER AIRLINES","#007A33","#FFFFFF"),
        ["NKS"]=new("NKS","NK","SPIRIT AIRLINES","#FFE500","#121212"),
        ["A6"]=new("A6","A6","ALPHA 6 OPS","#FFDA00","#071019")
    };

    internal static AirlineBrand For(string? icao)
    {
        var code=(icao??"").Trim().ToUpperInvariant();
        if(Brands.TryGetValue(code,out var brand))return brand;
        var mark=code.Length==0?"—":code[..Math.Min(3,code.Length)];
        return new(code,mark,code.Length==0?"AIRLINE NOT IDENTIFIED":code+" AIRLINE","#243642","#FFDA00");
    }

    internal static string? FromFlightNumber(string? flightNumber)
    {
        var value=(flightNumber??"").Trim().ToUpperInvariant();var letters=0;
        while(letters<value.Length&&letters<3&&char.IsLetter(value[letters]))letters++;
        return letters>=2?value[..letters]:null;
    }
}
