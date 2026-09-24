using System;
using System.Collections.Immutable;

namespace Alpha6Ops.Desktop.MapV2;

// NOAA fractional-year approximation, government-published equations:
// https://gml.noaa.gov/grad/solcalc/solareqns.PDF . Visualization, not navigation.
internal static class MapSolar
{
    internal static ImmutableArray<double> SunVector(DateTimeOffset utc)
    {
        utc = utc.ToUniversalTime();
        double minutes = utc.TimeOfDay.TotalMinutes;
        double year = DateTime.IsLeapYear(utc.Year) ? 366 : 365;
        double g = 2 * Math.PI / year * (utc.DayOfYear - 1 + (minutes / 60 - 12) / 24);
        double equation = 229.18 * (0.000075 + 0.001868 * Math.Cos(g) - 0.032077 * Math.Sin(g)
            - 0.014615 * Math.Cos(2 * g) - 0.040849 * Math.Sin(2 * g));
        double declination = 0.006918 - 0.399912 * Math.Cos(g) + 0.070257 * Math.Sin(g)
            - 0.006758 * Math.Cos(2 * g) + 0.000907 * Math.Sin(2 * g)
            - 0.002697 * Math.Cos(3 * g) + 0.00148 * Math.Sin(3 * g);
        double longitude = (180 - (minutes + equation) / 4) * Math.PI / 180;
        return [Math.Cos(declination) * Math.Cos(longitude),
            Math.Cos(declination) * Math.Sin(longitude), Math.Sin(declination)];
    }
}
