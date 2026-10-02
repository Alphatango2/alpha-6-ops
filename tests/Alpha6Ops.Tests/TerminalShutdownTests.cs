using System.Collections.Immutable;
using Alpha6Ops.Core;

internal static class TerminalShutdownTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-28T21:15:00Z");
    private static BlockInContext Context(int second, int engines = 2, bool cutoff = true, bool combustion = false)
    {
        var now = Start.AddSeconds(second);
        var sample = new Telemetry(now, true, 0, true, false, EngineDataValid: false);
        var evidence = new TerminalShutdownEvidence(ShutdownIntentSupport.Enhanced, "validated test adapter", "connection-1", engines,
            Enumerable.Range(1, engines).Select(i => new TerminalEngineEvidence(i, cutoff, combustion, now)).ToImmutableArray(), now, now, true, true);
        return new(sample, FlightPhase.TaxiIn, true, true, true, 1, now, evidence);
    }
    internal static void Run(Action<bool, string> check)
    {
        var c = Context(0);
        check(new BlockInEligibilityPolicy().Observe(c, Start).TerminalEligible, "terminal: verified intent allows completion while mechanical state remains invalid/spooling");
        check(new BlockInEligibilityPolicy().Observe(Context(0, 4), Start).TerminalEligible, "terminal: four-engine verified coverage");
        var detector = new PhaseDetector(initialPhase: FlightPhase.TaxiIn);
        detector.Observe(c.Sample with { TerminalBlockInEligible = true });
        check(detector.Observe(c.Sample with { At = Start.AddSeconds(3), TerminalBlockInEligible = true })?.Phase == FlightPhase.Complete,
            "phase completion consumes explicit terminal authority while mechanical engines spool");
        var blocked = new PhaseDetector(initialPhase: FlightPhase.TaxiIn);
        blocked.Observe(c.Sample with { EngineDataValid = true, TerminalBlockInEligible = false, PilotConfirmedBlockIn = true });
        check(blocked.Observe(c.Sample with { At = Start.AddSeconds(3), EngineDataValid = true, TerminalBlockInEligible = false, PilotConfirmedBlockIn = true }) is null,
            "new policy denial cannot be bypassed by legacy mechanical or pilot flags");
        var negative = new (string Name, Func<BlockInContext, BlockInContext> Change)[] {
            ("partial shutdown / single-engine taxi", x => x with { Shutdown = x.Shutdown with { Engines = x.Shutdown.Engines.SetItem(1, new(2, false, true, Start)) } }),
            ("cutoff with contradictory combustion", x => x with { Shutdown = x.Shutdown with { Engines = x.Shutdown.Engines.SetItem(0, new(1, true, true, Start)) } }),
            ("combustion loss without intent / failure / starvation", x => x with { Shutdown = x.Shutdown with { Engines = x.Shutdown.Engines.Select(e => e with { Cutoff = false }).ToImmutableArray() } }),
            ("missing coverage", x => x with { Shutdown = x.Shutdown with { Engines = [x.Shutdown.Engines[0]] } }),
            ("duplicate index", x => x with { Shutdown = x.Shutdown with { Engines = [x.Shutdown.Engines[0], x.Shutdown.Engines[0]] } }),
            ("missing fourth engine", x => x with { Shutdown = x.Shutdown with { ExpectedEngineCount = 4 } }),
            ("invalid signal", x => x with { Shutdown = x.Shutdown with { Engines = x.Shutdown.Engines.SetItem(0, new(1, null, false, Start)) } }),
            ("stale engine signal", x => x with { Shutdown = x.Shutdown with { Engines = x.Shutdown.Engines.SetItem(0, new(1, true, false, Start.AddSeconds(-4))) } }),
            ("unqualified startup zero", x => x with { Shutdown = x.Shutdown with { Qualified = false } }),
            ("invalid evidence", x => x with { Shutdown = x.Shutdown with { Valid = false } }),
            ("wrong destination", x => x with { AtDestination = false }),
            ("missing ON", x => x with { HasOn = false }),
            ("wrong session", x => x with { SessionValid = false }),
            ("moving", x => x with { Sample = x.Sample with { GroundSpeedKnots = .5 } }),
            ("invalid speed", x => x with { Sample = x.Sample with { GroundSpeedKnots = double.NaN } }),
            ("brake release", x => x with { Sample = x.Sample with { ParkingBrake = false } }),
            ("go around", x => x with { Sample = x.Sample with { OnGround = false } }),
            ("wrong phase", x => x with { Phase = FlightPhase.AtGate }),
            ("pause", x => x with { Sample = x.Sample with { Paused = true } }),
            ("slew", x => x with { Sample = x.Sample with { Slewing = true } }),
            ("stale base telemetry", x => x with { ReceivedUtc = Start.AddSeconds(-4) }),
        };
        foreach (var (name, change) in negative)
        {
            var policy = new BlockInEligibilityPolicy(); var bad = change(c);
            check(!policy.Observe(bad, Start).TerminalEligible && !policy.TryConfirm(bad, Start), "terminal rejects " + name);
        }
        BlockInContext Limited(int second) { var x = Context(second); return x with { Shutdown = TerminalShutdownEvidence.Limited(x.Sample, x.ReceivedUtc, "limited-1") }; }
        var manual = new BlockInEligibilityPolicy();
        for (int i = 0; i < 10; i++) check(!manual.Observe(Limited(i), Start.AddSeconds(i)).CanConfirm, "manual parked interval not yet satisfied " + i);
        var eligible = Limited(10);
        check(manual.Observe(eligible, eligible.ReceivedUtc).CanConfirm, "unsupported aircraft offers manual after ten stable seconds");
        check(!manual.TryConfirm(eligible with { AtDestination = false }, eligible.ReceivedUtc), "direct manual command cannot bypass destination");
        check(!manual.TryConfirm(eligible, eligible.ReceivedUtc.AddSeconds(4)), "direct manual command cannot reuse stale UI eligibility");
        check(manual.TryConfirm(eligible, eligible.ReceivedUtc), "manual confirmation accepts complete current predicate");
        check(manual.Observe(Limited(11), Start.AddSeconds(11)).TerminalEligible, "manual confirmation permits terminal candidate");
        manual.Observe(Limited(12) with { Sample = Limited(12).Sample with { ParkingBrake = false } }, Start.AddSeconds(12));
        check(!manual.Observe(Limited(13), Start.AddSeconds(13)).TerminalEligible, "brake release clears manual authority");
        var running = Limited(14) with { Shutdown = Limited(14).Shutdown with { OperatingEvidence = true } };
        check(!manual.Observe(running, Start.AddSeconds(14)).CanConfirm, "unsupported running engines cannot be manually bypassed");
        manual.Reset();
        check(!manual.Observe(Limited(30), Start.AddSeconds(30)).CanConfirm, "restart/disconnect resets parked interval");
        check(!manual.Observe(Limited(31) with { Generation = 2 }, Start.AddSeconds(31)).CanConfirm, "new connection cannot reuse old manual interval");
        var mechanical = new EngineEvidenceTracker();
        mechanical.Observe([new(1, 1, 1000, 35, 75, 0, 11000)], Start);
        mechanical.Observe([new(1, 1, 1000, 35, 75, 0, 11000)], Start.AddSeconds(2));
        mechanical.Observe([new(1, 0, 900, 30, 70, 0, 10500)], Start.AddSeconds(3));
        var state = mechanical.Observe([new(1, 0, 500, 20, 60, 0, 10000)], Start.AddSeconds(5));
        check(state[0].Condition == EngineCondition.Stopping, "mechanical classifier retains stopping with residual spool/fuel flow");
    }
}
