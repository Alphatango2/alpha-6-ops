using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal sealed record ShutdownSignal(double Value, DateTimeOffset ReceivedUtc, bool Valid = true);
internal sealed record ShutdownIntentFrame(string Aircraft, double? EngineCount, DateTimeOffset SimulatorUtc,
    DateTimeOffset ReceivedUtc, string ConnectionId, IReadOnlyDictionary<string, ShutdownSignal> Signals);
internal interface IShutdownIntentAdapter
{
    TerminalShutdownEvidence Observe(ShutdownIntentFrame frame);
}
internal static class ShutdownIntentAdapters
{
    internal const string ValidatedVersion = "2.4.0.4720";
    internal static IShutdownIntentAdapter? Create(string aircraft, FenixShutdownCapabilities? capabilities) =>
        capabilities?.Find(aircraft) is { PackageVersion: ValidatedVersion } candidate
            && candidate.AircraftTitle == aircraft && candidate.IcaoType is "A319" or "A320" or "A321"
            ? new FenixFamilyAdapter(candidate) : null;

    private sealed class FenixFamilyAdapter(FenixShutdownCapability capability) : IShutdownIntentAdapter
    {
        private readonly bool[] qualified = new bool[2];
        private string? connection;
        public TerminalShutdownEvidence Observe(ShutdownIntentFrame frame)
        {
            var covered = frame.Aircraft == capability.AircraftTitle && frame.EngineCount == 2;
            if (connection != frame.ConnectionId || !covered)
            { Array.Clear(qualified); connection = frame.ConnectionId; }
            bool? Read(string name, out DateTimeOffset received)
            {
                received = DateTimeOffset.MinValue;
                if (!frame.Signals.TryGetValue(name, out var signal) || !signal.Valid
                    || !BlockInEligibilityPolicy.Fresh(signal.ReceivedUtc, frame.ReceivedUtc) || signal.Value is not (0 or 1)) return null;
                received = signal.ReceivedUtc; return signal.Value == 1;
            }
            var engines = ImmutableArray.CreateBuilder<TerminalEngineEvidence>();
            var operating = false;
            for (var i = 1; i <= 2; i++)
            {
                var masterOn = Read($"L:S_ENG_MASTER_{i}", out var masterAt);
                var combustion = Read($"GENERAL ENG COMBUSTION:{i}", out var combustionAt);
                if (covered && masterOn == true && combustion == true) qualified[i - 1] = true;
                operating |= Read($"GENERAL ENG STARTER:{i}", out _) == true;
                engines.Add(new(i, masterOn is null ? null : !masterOn.Value, combustion, masterAt < combustionAt ? masterAt : combustionAt));
            }
            return new(ShutdownIntentSupport.Enhanced, $"fenix-a320-family/{capability.PackageVersion}/v2", frame.ConnectionId, 2, engines.ToImmutable(),
                frame.SimulatorUtc, frame.ReceivedUtc, covered,
                qualified.All(value => value), operating);
        }
    }
}
