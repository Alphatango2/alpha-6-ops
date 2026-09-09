using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha6Ops.Desktop;
internal static class SimConnectProbe
{
    internal static async Task RunAsync(string output, bool launchSimulator = false)
    {
        var status = new List<string>();
        var samples = new List<LiveReading>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(launchSimulator ? 45 : 10));
        string? failure = null;
        try
        {
            if (launchSimulator) status.Add(SimulatorLauncher.LaunchIfNeeded() ? "MSFS launch requested through Windows" : "MSFS already running; no duplicate launch");
            do
            {
                try
                {
                    await SimConnectSource.RunAsync(status.Add, r => { if (samples.Count < 30) samples.Add(r); if (samples.Count >= 3 && r.Evidence?.Position is not null && r.Evidence.SimulationRunning == true) timeout.Cancel(); }, timeout.Token);
                    break;
                }
                catch (IOException) when (launchSimulator && !timeout.IsCancellationRequested) { await Task.Delay(1000, timeout.Token); }
            } while (!timeout.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
        catch (Exception e) { failure = e.GetBaseException().Message; }
        if (samples.Count == 0) failure ??= "No aircraft telemetry received before the probe deadline.";
        File.WriteAllText(output, JsonSerializer.Serialize(new { status, sampleCount = samples.Count, samples, failure }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
