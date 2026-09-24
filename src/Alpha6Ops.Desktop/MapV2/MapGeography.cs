using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Alpha6Ops.Desktop.MapV2;

internal static class MapGeography
{
    private const double Radians = Math.PI / 180;

    internal static double Angle(GeoPoint a, GeoPoint b)
    {
        var u = Unit(a);
        var v = Unit(b);
        return Math.Acos(Math.Clamp(u.X * v.X + u.Y * v.Y + u.Z * v.Z, -1, 1));
    }

    private static (double X, double Y, double Z) Unit(GeoPoint p)
    {
        double lat = p.Latitude * Radians, lon = p.Longitude * Radians;
        return (Math.Cos(lat) * Math.Cos(lon), Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat));
    }

    internal static GeoPoint Interpolate(GeoPoint a, GeoPoint b, double fraction)
    {
        if (!double.IsFinite(fraction) || fraction is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(fraction));
        double angle = Angle(a, b);
        if (angle < 1e-8) return a;
        if (Math.PI - angle < 1e-6) throw new ArgumentException("Antipodal route requires an authoritative intermediate waypoint.");
        var u = Unit(a);
        var v = Unit(b);
        double left = Math.Sin((1 - fraction) * angle) / Math.Sin(angle);
        double right = Math.Sin(fraction * angle) / Math.Sin(angle);
        double x = left * u.X + right * v.X, y = left * u.Y + right * v.Y, z = left * u.Z + right * v.Z;
        return new(Math.Atan2(y, x) / Radians, Math.Atan2(z, Math.Sqrt(x * x + y * y)) / Radians);
    }

    internal static ImmutableArray<ImmutableArray<GeoPoint>> GreatCircle(GeoPoint a, GeoPoint b)
    {
        int count = Math.Max(1, (int)Math.Ceiling(Angle(a, b) / Radians));
        var points = new List<GeoPoint> { a };
        for (int i = 1; i < count; i++) points.Add(Interpolate(a, b, (double)i / count));
        points.Add(b);
        return SplitDateline(points);
    }

    internal static ImmutableArray<ImmutableArray<GeoPoint>> SplitDateline(IEnumerable<GeoPoint> input)
    {
        var result = ImmutableArray.CreateBuilder<ImmutableArray<GeoPoint>>();
        var segment = ImmutableArray.CreateBuilder<GeoPoint>();
        foreach (var point in input)
        {
            if (segment.Count > 0)
            {
                var previous = segment[^1];
                double difference = point.Longitude - previous.Longitude;
                if (Math.Abs(difference) > 180)
                {
                    double end = point.Longitude + (difference > 0 ? -360 : 360);
                    double edge = difference > 0 ? -180 : 180;
                    double fraction = (edge - previous.Longitude) / (end - previous.Longitude);
                    double latitude = previous.Latitude + fraction * (point.Latitude - previous.Latitude);
                    segment.Add(new(edge, latitude));
                    if (segment.Count >= 2) result.Add(segment.ToImmutable());
                    segment.Clear();
                    segment.Add(new(-edge, latitude));
                }
            }
            segment.Add(point);
        }
        if (segment.Count >= 2) result.Add(segment.ToImmutable());
        return result.ToImmutable();
    }
}
