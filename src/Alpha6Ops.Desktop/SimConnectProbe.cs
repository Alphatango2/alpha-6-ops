using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha6Ops.Desktop;
internal static class SimConnectProbe
{
    internal static async Task RunAsync(string output)
    {
        var status = new List<string>();
        var samples = new List<LiveReading>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string? failure = null;
        try { await SimConnectSource.RunAsync(status.Add, r => { samples.Add(r); if (samples.Count >= 3) timeout.Cancel(); }, timeout.Token); }
        catch (Exception e) { failure = e.GetBaseException().Message; }
        File.WriteAllText(output, JsonSerializer.Serialize(new { status, sampleCount = samples.Count, samples, failure }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
