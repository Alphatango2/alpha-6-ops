using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal static partial class SimConnectSource
{
    // Optional reads are independently registered: an unsupported add-on channel
    // must never tear down primary telemetry or be converted into a default zero.
    internal sealed class TerminalSensors(FenixShutdownCapabilities? capabilities)
    {
        private readonly Dictionary<string, ShutdownSignal> masters = new();
        private readonly Dictionary<uint, string> sent = new();
        private readonly string connection = Guid.NewGuid().ToString("N");
        private string? aircraft;
        private IShutdownIntentAdapter? adapter;
        internal void Register(IntPtr handle)
        {
            for (uint engine = 1; engine <= 2; engine++)
            {
                var name = $"L:S_ENG_MASTER_{engine}"; var id = 300 + engine;
                if (AddDefinition(handle, id, name, "number", 4, 0, uint.MaxValue) < 0) continue;
                Remember(handle, name);
                Request(handle, id, id, 0, 4, 0, 0, 0, 0); Remember(handle, name);
            }
        }
        private void Remember(IntPtr handle, string name) { if (LastSent(handle, out var packet) >= 0) sent[packet] = name; }
        internal bool Dispatch(int kind, IntPtr data, uint length)
        {
            if (kind == 1 && length >= 24 && sent.TryGetValue((uint)Marshal.ReadInt32(data, 16), out var failed))
            { masters[failed] = new(double.NaN, DateTimeOffset.UtcNow, false); return true; }
            if (kind != 8 || length < 40) return false;
            var request = Marshal.ReadInt32(data, 12);
            if (request is not (301 or 302)) return false;
            var name = $"L:S_ENG_MASTER_{request - 300}";
            if (length != 48 || Marshal.ReadInt32(data, 20) != request || Marshal.ReadInt32(data, 36) != 1)
            { masters[name] = new(double.NaN, DateTimeOffset.UtcNow, false); return true; }
            var values = new double[1]; Marshal.Copy(IntPtr.Add(data, 40), values, 0, 1);
            masters[name] = new(values[0], DateTimeOffset.UtcNow, double.IsFinite(values[0])); return true;
        }
        internal TerminalShutdownEvidence Observe(string title, Telemetry sample, DateTimeOffset receivedUtc,
            double? engineCount, IReadOnlyList<double> combustion, IReadOnlyList<double?> starters)
        {
            if (aircraft != title)
            {
                if (aircraft is not null) masters.Clear();
                aircraft = title; adapter = ShutdownIntentAdapters.Create(title, capabilities);
            }
            if (adapter is null)
            {
                // Mechanical enrichment can lack engine-count coverage while the primary
                // packet still positively reports combustion. Never lose that veto.
                var count = engineCount is >= 1 and <= 4 && engineCount == Math.Truncate(engineCount.Value)
                    ? (int)engineCount.Value : combustion.Count;
                var active = combustion.Take(count).ToArray();
                var limited = TerminalShutdownEvidence.Limited(sample, receivedUtc, connection);
                return limited with
                {
                    Valid = count is >= 1 and <= 4 && active.Length == count && active.All(v => v is 0 or 1),
                    OperatingEvidence = limited.OperatingEvidence || active.Any(v => v > 0) || starters.Any(v => v > 0)
                };
            }
            var signals = new Dictionary<string, ShutdownSignal>(masters);
            for (var i = 1; i <= 2; i++)
            {
                if (combustion.Count >= i) signals[$"GENERAL ENG COMBUSTION:{i}"] = new(combustion[i - 1], receivedUtc);
                if (starters.Count >= i && starters[i - 1] is {} starter) signals[$"GENERAL ENG STARTER:{i}"] = new(starter, receivedUtc);
            }
            return adapter.Observe(new(title, engineCount, sample.At, receivedUtc, connection, signals));
        }
    }

}
