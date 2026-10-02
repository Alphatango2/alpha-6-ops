namespace Alpha6Ops.Core;

public sealed record ArrivalEstimatorState(DateTimeOffset? LastSample, FlightPhase? LastPhase, DateTimeOffset? Published,
    DateTimeOffset? AirborneSince, DateTimeOffset? TaxiSince, double? SmoothedSpeedKnots);

/// <summary>Published gate-IN estimate in simulator UTC. Assumes five minutes taxi-in.</summary>
public sealed class ArrivalEstimator
{
    private DateTimeOffset? lastSample, published, airborneSince, taxiSince;
    private FlightPhase? lastPhase;
    private double? speed;
    public void Reset() { lastSample = published = airborneSince = taxiSince = null; lastPhase = null; speed = null; }
    public ArrivalEstimatorState CaptureState()=>new(lastSample,lastPhase,published,airborneSince,taxiSince,speed);
    public bool TryRestore(ArrivalEstimatorState? state)
    {
        if(state is null){Reset();return true;}
        if(!Valid(state))return false;
        lastSample=state.LastSample;lastPhase=state.LastPhase;published=state.Published;
        airborneSince=state.AirborneSince;taxiSince=state.TaxiSince;speed=state.SmoothedSpeedKnots;
        return true;
    }
    private static bool Valid(ArrivalEstimatorState state)
    {
        if(state.LastSample is null)return state==new ArrivalEstimatorState(null,null,null,null,null,null);
        var last=state.LastSample.Value;
        if(last==default||state.LastPhase is not (FlightPhase.Airborne or FlightPhase.TaxiIn))return false;
        if(state.Published is{} published&&(published<=last||published-last>TimeSpan.FromHours(36)))return false;
        if(state.SmoothedSpeedKnots is{} speed&&(!double.IsFinite(speed)||speed<60||speed>1500))return false;
        if(state.AirborneSince is{} airborne&&(airborne>last||last-airborne>TimeSpan.FromHours(36)))return false;
        if(state.TaxiSince is{} taxi&&(taxi>last||last-taxi>TimeSpan.FromHours(24)))return false;
        return state.LastPhase switch
        {
            FlightPhase.Airborne=>state.AirborneSince is not null&&state.TaxiSince is null&&state.SmoothedSpeedKnots is not null,
            FlightPhase.TaxiIn=>state.TaxiSince is not null,
            _=>false
        };
    }
    public DateTimeOffset? Observe(Telemetry sample, FlightPhase phase, double? remainingNm, DateTimeOffset? actualIn,
        DateTimeOffset? plannedIn = null, DateTimeOffset? plannedOut = null, DateTimeOffset? actualOut = null)
    {
        if (actualIn is not null) return actualIn;
        if (sample.Paused || sample.Slewing) return published > sample.At ? published : null;
        if (phase != FlightPhase.Airborne && phase != FlightPhase.TaxiIn) return null;
        // Withdraw stale estimates before the duplicate-sample shortcut: route
        // confidence can change while several views render the same telemetry.
        if (phase == FlightPhase.Airborne && (remainingNm is null || !double.IsFinite(remainingNm.Value) || remainingNm < 0 || sample.GroundSpeedKnots < 60))
        {
            published = null;
            return null;
        }
        if (lastSample == sample.At && lastPhase == phase) return published;
        if (lastSample is { } previous && (sample.At < previous || sample.At - previous > TimeSpan.FromMinutes(2))) Reset();
        var phaseChanged = lastPhase != phase;
        lastPhase = phase;
        lastSample = sample.At;
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
            var observed = sample.At.ToUniversalTime().AddMinutes(5 + remainingNm!.Value / Math.Max(100, speed!.Value) * 60);
            raw = observed;
            // A valid observed OUT anchors the plan to simulator time, including
            // intentionally shifted flights. Late connections use live data only.
            if (plannedIn is { } pin && plannedOut is { } pout && actualOut is { } aout && pin > pout)
            {
                var baseline = aout + (pin - pout);
                var weight = Math.Clamp((sample.At - airborneSince.Value).TotalMinutes / 20, 0, 1);
                raw = baseline.AddTicks((long)((raw - baseline).Ticks * weight));
                if (raw <= sample.At) raw = observed;
            }
        }
        raw = raw.ToUniversalTime();
        raw = new DateTimeOffset(raw.Year, raw.Month, raw.Day, raw.Hour, raw.Minute, 0, TimeSpan.Zero).AddMinutes(raw.Second >= 30 ? 1 : 0);
        // Rounding and anti-churn must never turn a prediction into a past actual.
        // Phase transitions also replace the old navigation/taxi prediction even
        // when the change is smaller than the normal two-minute threshold.
        if (raw <= sample.At) raw = new DateTimeOffset(sample.At.UtcDateTime.Year, sample.At.UtcDateTime.Month,
            sample.At.UtcDateTime.Day, sample.At.UtcDateTime.Hour, sample.At.UtcDateTime.Minute, 0, TimeSpan.Zero).AddMinutes(1);
        if (published is null || published <= sample.At || phaseChanged || Math.Abs((raw - published.Value).TotalMinutes) >= 2) published = raw;
        return published;
    }
}
