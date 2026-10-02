namespace Alpha6Ops.Core;

// Geographical route estimate, not OUT/OFF/ON/IN lifecycle completion. Remaining
// distance is never less than the direct distance to the filed endpoint. Fraction
// may decrease during vectors/overshoots; unreliable values are last-known only.
public sealed record RouteProgress(double Fraction, double RemainingNm, double CrossTrackNm, bool IsReliable = true, DateTimeOffset? At = null);

/// <summary>Distance-weighted route projection, independent of any view or rendering mesh.</summary>
public sealed class RouteProgressTracker
{
    private GeoPosition[] route = [];
    private double[] lengths = [];
    private double total, along;
    private GeoPosition? previous;
    private DateTimeOffset? lastAt;
    public RouteProgress? Current { get; private set; }

    public void SetRoute(IEnumerable<GeoPosition> points)
    {
        var next = points.Where(p => p.IsValid).ToArray();
        if (route.SequenceEqual(next)) return;
        route = next;
        lengths = route.Zip(route.Skip(1), (a, b) => a.DistanceNm(b)).ToArray();
        total = lengths.Sum();
        Reset();
    }

    public void Reset() { previous = null; lastAt = null; Current = null; along = 0; }

    public RouteProgress? Observe(GeoPosition position, DateTimeOffset at, bool suspended = false)
    {
        if (suspended || !position.IsValid || total < .01) return Current;
        if (lastAt is { } last && at <= last) return Current;
        var continuous = previous is not null && lastAt is { } time && at - time < TimeSpan.FromMinutes(2);
        var moved = previous?.DistanceNm(position) ?? 0;
        // A continuity corridor prevents jumping across a loop/intersection. Its width is
        // geographic, never a fixed number of tessellation points or route waypoints.
        var allowance = Math.Max(5, moved * 3);
        var bestScore = double.PositiveInfinity;
        var bestAlong = along;
        var bestCross = double.PositiveInfinity;
        var accumulated = 0d;
        for (var i = 0; i < lengths.Length; i++)
        {
            var length = lengths[i];
            if (length < .00001) continue;
            var a = route[i]; var b = route[i + 1];
            var angular = a.DistanceNm(position) / 3440.065;
            var bearingDifference = Bearing(a, position) - Bearing(a, b);
            var projected = Math.Atan2(Math.Sin(angular) * Math.Cos(bearingDifference), Math.Cos(angular)) * 3440.065;
            var segmentAlong = Math.Clamp(projected, 0, length);
            var cross = projected < 0 ? a.DistanceNm(position) : projected > length ? b.DistanceNm(position)
                : Math.Abs(Math.Asin(Math.Clamp(Math.Sin(angular) * Math.Sin(bearingDifference), -1, 1))) * 3440.065;
            var candidate = accumulated + segmentAlong;
            // Continuity breaks geographic ties at loops; it must not outweigh a
            // clearly closer segment forever after a different departure or vector.
            var score = cross + (continuous ? Math.Min(5, Math.Max(0, Math.Abs(candidate - along) - allowance) * 2) : 0);
            if (score < bestScore) { bestScore = score; bestAlong = candidate; bestCross = cross; }
            accumulated += length;
        }
        // Keep the last reliable route position during excursions. Rejoining or reconnecting
        // can reacquire a later segment; the aircraft marker always uses actual coordinates.
        if (bestCross <= 25) along = continuous ? Math.Max(along, Math.Min(bestAlong, along + allowance)) : bestAlong;
        previous = position; lastAt = at;
        var reliable = bestCross <= 25 && Math.Abs(bestAlong - along) <= allowance;
        // A projected endpoint is not evidence that the aircraft reached it. Keep
        // the continuity cursor, but include the real distance still to fly after
        // vectors, a reversed arrival or a go-around. Only actual endpoint geometry
        // yields 100%; a coordinator may separately close a confirmed Complete leg.
        var remaining = reliable ? Math.Max(total - along, position.DistanceNm(route[^1])) : Current?.RemainingNm ?? total;
        var fraction = reliable ? Math.Clamp(1 - remaining / total, 0, 1) : Current?.Fraction ?? 0;
        Current = new(fraction, remaining, bestCross, reliable, at);
        return Current;
    }

    private static double Bearing(GeoPosition a, GeoPosition b)
    {
        const double rad = Math.PI / 180;
        var latitude = a.Latitude * rad; var other = b.Latitude * rad; var delta = (b.Longitude - a.Longitude) * rad;
        return Math.Atan2(Math.Sin(delta) * Math.Cos(other), Math.Cos(latitude) * Math.Sin(other) - Math.Sin(latitude) * Math.Cos(other) * Math.Cos(delta));
    }
}
