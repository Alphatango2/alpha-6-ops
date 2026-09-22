namespace Alpha6Ops.Core;

/// <summary>Published gate-IN estimate in simulator UTC. Assumes five minutes taxi-in.</summary>
public sealed class ArrivalEstimator
{
    private DateTimeOffset? lastSample, published, airborneSince, taxiSince;
    private double? speed;
    public void Reset() { lastSample = published = airborneSince = taxiSince = null; speed = null; }
    public DateTimeOffset? Observe(Telemetry sample, FlightPhase phase, double? remainingNm, DateTimeOffset? actualIn,
        DateTimeOffset? plannedIn = null, DateTimeOffset? plannedOut = null, DateTimeOffset? actualOut = null)
    {
        if (actualIn is not null) return actualIn;
        if (sample.Paused || sample.Slewing) return published;
        if (phase != FlightPhase.Airborne && phase != FlightPhase.TaxiIn) return null;
        if (lastSample == sample.At) return published;
        if (lastSample is { } previous && (sample.At < previous || sample.At - previous > TimeSpan.FromMinutes(2))) Reset();
        lastSample = sample.At;
        if (phase == FlightPhase.Airborne && (remainingNm is null || !double.IsFinite(remainingNm.Value) || sample.GroundSpeedKnots < 60)) return null;
        if (phase == FlightPhase.Airborne) speed = speed is { } prior ? prior * .9 + sample.GroundSpeedKnots * .1 : sample.GroundSpeedKnots;
        DateTimeOffset raw;
        if (phase == FlightPhase.TaxiIn)
        {
            taxiSince ??= sample.At;
            raw = taxiSince.Value.AddMinutes(5);
            // Once the allowance expires, keep the estimate in the future without
            // restarting a full five-minute taxi allowance on every sample.
            if (raw <= sample.At) raw = sample.At.AddMinutes(1);
        }
        else
        {
            taxiSince = null;
            airborneSince ??= sample.At;
            raw = sample.At.ToUniversalTime().AddMinutes(5 + remainingNm!.Value / Math.Max(100, speed!.Value) * 60);
            // A valid observed OUT anchors the plan to simulator time, including
            // intentionally shifted flights. Late connections use live data only.
            if (plannedIn is { } pin && plannedOut is { } pout && actualOut is { } aout && pin > pout)
            {
                var baseline = aout + (pin - pout);
                var weight = Math.Clamp((sample.At - airborneSince.Value).TotalMinutes / 20, 0, 1);
                raw = baseline.AddTicks((long)((raw - baseline).Ticks * weight));
            }
        }
        raw = raw.ToUniversalTime();
        raw = new DateTimeOffset(raw.Year, raw.Month, raw.Day, raw.Hour, raw.Minute, 0, TimeSpan.Zero).AddMinutes(raw.Second >= 30 ? 1 : 0);
        if (published is null || Math.Abs((raw - published.Value).TotalMinutes) >= 2) published = raw;
        return published;
    }
}
