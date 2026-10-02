using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Alpha6Ops.Desktop.MapV2;

// Characterization only, gated by explicit diagnostic entry and environment flag.
// Does not replace or amend MapContentionChecks' existing acceptance assertion.
internal static class HeartbeatContextChecks
{
    internal static async Task RunAsync(MainWindow main,WebMapRenderer renderer,string directory)
    {
        string[] order=Environment.GetEnvironmentVariable("S4_HEARTBEAT_CONTEXT_ORDER")=="normal-first"
            ?["normal","idle"]:["idle","normal"];
        long sequence=0;
        foreach(string condition in order)
        {
            string path=Path.Combine(directory,"context-"+condition);Directory.CreateDirectory(path);
            var dashboard=(DispatcherTimer)FlightSessionDiagnosticAccess.Get(main,"dashboardClock")!;
            using var trace=Environment.GetEnvironmentVariable("S4_CONTENTION_TRACE")=="1"?new DispatcherSchedulingTrace(main,dashboard,[]):null;
            var native=trace is null?null:new HeartbeatNativeObserver(trace,renderer);
            var intervals=new List<double>();var clock=Stopwatch.StartNew();var tick=Stopwatch.StartNew();
            var heartbeat=new DispatcherTimer(DispatcherPriority.Background,main.Dispatcher){Interval=TimeSpan.FromMilliseconds(20)};
            void Tick(object? sender,EventArgs e){intervals.Add(tick.Elapsed.TotalMilliseconds);trace?.Mark("heartbeat",value:intervals[^1]);tick.Restart();}
            heartbeat.Tick+=Tick;
            try
            {
                heartbeat.Start();trace?.Mark("context-start",condition);
                if(condition=="normal")
                {
                    // Worker cadence cannot be delayed by a UI-paced producer.
                    await Task.Run(async()=>{for(int i=0;i<30;i++){renderer.Offer(MapV2Checks.Load(renderer.Epoch,++sequence,1,i));trace?.Mark("map-offered",value:i);await Task.Delay(1000).ConfigureAwait(false);}});
                }
                else await Task.Delay(30000);
                double pending=tick.Elapsed.TotalMilliseconds;
                trace?.Mark("pending-heartbeat-at-stop",condition,pending);
                heartbeat.Stop();
                File.WriteAllText(Path.Combine(path,"timings.json"),JsonSerializer.Serialize(new{
                    condition,priority="Background",intervalMs=20,elapsedMs=clock.Elapsed.TotalMilliseconds,
                    heartbeatIntervalsMs=intervals,pendingHeartbeatMs=pending,
                    mainWidth=main.ActualWidth,mainHeight=main.ActualHeight,mapWidth=renderer.View.ActualWidth,mapHeight=renderer.View.ActualHeight,
                    dashboardClock=dashboard.IsEnabled,mapMetrics=renderer.Measurements,
                    limitation="Synthetic map-only context, not authoritative flight input or physical interaction; pending tail is retained separately."},new JsonSerializerOptions{WriteIndented=true}));
            }
            finally
            {
                heartbeat.Stop();heartbeat.Tick-=Tick;native?.Dispose();
                if(trace is not null)File.WriteAllText(Path.Combine(path,"scheduling-trace.json"),JsonSerializer.Serialize(await trace.FinishAsync()));
            }
        }
    }
}
