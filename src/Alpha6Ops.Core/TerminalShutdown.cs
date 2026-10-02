using System.Collections.Immutable;

namespace Alpha6Ops.Core;

public enum ShutdownIntentSupport { Limited, Enhanced }
public enum TerminalShutdownStatus { Unavailable, Operating, Confirmed, Invalid }
public sealed record TerminalEngineEvidence(int Index, bool? Cutoff, bool? Combustion, DateTimeOffset ReceivedUtc);
public sealed record TerminalShutdownEvidence(ShutdownIntentSupport Support, string Provider, string ConnectionId,
    int ExpectedEngineCount, ImmutableArray<TerminalEngineEvidence> Engines, DateTimeOffset SimulatorUtc,
    DateTimeOffset ReceivedUtc, bool Valid, bool Qualified, bool OperatingEvidence = false)
{
    public TerminalShutdownStatus Status(DateTimeOffset now, DateTimeOffset simulatorUtc)
    {
        if (!Valid || SimulatorUtc != simulatorUtc || string.IsNullOrWhiteSpace(ConnectionId)
            || !BlockInEligibilityPolicy.Fresh(ReceivedUtc, now)) return TerminalShutdownStatus.Invalid;
        if (Support == ShutdownIntentSupport.Limited)
            return OperatingEvidence ? TerminalShutdownStatus.Operating : TerminalShutdownStatus.Unavailable;
        if (!Qualified || ExpectedEngineCount is < 1 or > 4 || Engines.IsDefault || Engines.Length != ExpectedEngineCount
            || !Engines.Select(e => e.Index).Order().SequenceEqual(Enumerable.Range(1, ExpectedEngineCount))
            || Engines.Any(e => e.Cutoff is null || e.Combustion is null || !BlockInEligibilityPolicy.Fresh(e.ReceivedUtc, now)))
            return TerminalShutdownStatus.Invalid;
        if (Engines.Any(e => e.Cutoff == true && e.Combustion == true)) return TerminalShutdownStatus.Invalid;
        return OperatingEvidence || Engines.Any(e => e.Cutoff != true || e.Combustion != false)
            ? TerminalShutdownStatus.Operating : TerminalShutdownStatus.Confirmed;
    }
    public static TerminalShutdownEvidence Limited(Telemetry sample, DateTimeOffset receivedUtc, string connectionId) =>
        new(ShutdownIntentSupport.Limited, "unsupported", connectionId, 0, [], sample.At, receivedUtc, true, false,
            sample.EngineDataValid && sample.EnginesRunning || sample.EngineReadings?.Any(e => e.Combustion > 0 || e.Starter > 0) == true);
}

public sealed record BlockInContext(Telemetry Sample, FlightPhase Phase, bool HasOn, bool AtDestination,
    bool SessionValid, long Generation, DateTimeOffset ReceivedUtc, TerminalShutdownEvidence Shutdown);
public sealed record BlockInDecision(bool TerminalEligible, bool CanConfirm, bool PilotConfirmed, string Reason);

public sealed class BlockInEligibilityPolicy
{
    public static readonly TimeSpan Freshness = TimeSpan.FromSeconds(3);
    private DateTimeOffset? parkedSince, lastAt, lastReceived;
    private long? generation;
    private string? evidenceConnection;
    private bool pilotConfirmed;
    public BlockInDecision Observe(BlockInContext context, DateTimeOffset now)
    {
        var discontinuity = generation is not null && (generation != context.Generation || evidenceConnection != context.Shutdown.ConnectionId
            || lastAt is {} previous && (context.Sample.At <= previous || context.Sample.At - previous > TimeSpan.FromSeconds(15))
            || lastReceived is {} receipt && (context.ReceivedUtc < receipt || context.ReceivedUtc - receipt > Freshness));
        if (discontinuity) Reset();
        generation = context.Generation; evidenceConnection = context.Shutdown.ConnectionId; lastAt = context.Sample.At; lastReceived = context.ReceivedUtc;
        var status = context.Shutdown.Status(now, context.Sample.At);
        if (!Common(context, now) || status is TerminalShutdownStatus.Invalid or TerminalShutdownStatus.Operating || discontinuity)
        {
            parkedSince = null; pilotConfirmed = false;
            return new(false, false, false, discontinuity ? "Interrupted evidence" : status == TerminalShutdownStatus.Invalid ? "Invalid shutdown evidence" : "Terminal parking conditions not met");
        }
        parkedSince ??= context.Sample.At;
        var manual = status == TerminalShutdownStatus.Unavailable && context.Sample.At - parkedSince >= TimeSpan.FromSeconds(10);
        return new(status == TerminalShutdownStatus.Confirmed || manual && pilotConfirmed, manual && !pilotConfirmed, manual && pilotConfirmed,
            status == TerminalShutdownStatus.Confirmed ? "Verified terminal shutdown" : manual ? "Automatic shutdown intent unavailable; confirm Block In" : "Confirming destination parking");
    }
    public bool TryConfirm(BlockInContext context, DateTimeOffset now)
    {
        if (!Common(context, now) || generation != context.Generation || evidenceConnection != context.Shutdown.ConnectionId
            || lastAt != context.Sample.At || parkedSince is null || context.Sample.At - parkedSince < TimeSpan.FromSeconds(10)
            || context.Shutdown.Status(now, context.Sample.At) != TerminalShutdownStatus.Unavailable) return false;
        pilotConfirmed = true; return true;
    }
    private static bool Common(BlockInContext context, DateTimeOffset now) => context.SessionValid && context.HasOn
        && context.Phase == FlightPhase.TaxiIn && context.AtDestination && context.Sample.OnGround
        && double.IsFinite(context.Sample.GroundSpeedKnots) && context.Sample.GroundSpeedKnots is >= 0 and < .5
        && context.Sample.ParkingBrake && !context.Sample.Paused && !context.Sample.Slewing && Fresh(context.ReceivedUtc, now);
    public void Reset() { parkedSince = null; lastAt = null; lastReceived = null; generation = null; evidenceConnection = null; pilotConfirmed = false; }
    public static bool Fresh(DateTimeOffset received, DateTimeOffset now) => now >= received && now - received <= Freshness;
}
