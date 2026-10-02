using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Alpha6Ops.Desktop.MapV2;

internal static partial class MapV2Checks
{
    // Real bridge and browser; bounded render-priority pressure isolates dispatch
    // ordering. This is not a physical mouse or native simulator measurement.
    private static async Task DeliveryUnderRenderPressure(string directory)
    {
        await WithRenderer(directory,"delivery",async renderer=>
        {
            var projection=new MapProjectionService();
            renderer.Offer(projection.Project(Fixture(),false,renderer.Epoch,1,At));
            await Until(()=>Convert.ToInt64(Read(renderer.Measurements,"acknowledged"))==1);
            var watch=Stopwatch.StartNew();var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            double acknowledgedDuringPressureAt=-1;
            renderer.Core.WebMessageReceived+=(_,e)=>
            {
                using var message=JsonDocument.Parse(e.WebMessageAsJson);
                if(message.RootElement.GetProperty("type").GetString()=="ack"&&message.RootElement.GetProperty("sequence").GetInt64()==2)
                    acknowledgedDuringPressureAt=watch.Elapsed.TotalMilliseconds;
            };
            int renderTurns=0;var probes=new List<object>();
            void RenderTurn()
            {
                renderTurns++;
                if(watch.Elapsed.TotalMilliseconds<750)
                    renderer.View.Dispatcher.BeginInvoke(DispatcherPriority.Render,new Action(RenderTurn));
                else finished.TrySetResult();
            }
            _=renderer.View.Dispatcher.BeginInvoke(DispatcherPriority.Render,new Action(RenderTurn));
            foreach(var priority in new[]{DispatcherPriority.Background,DispatcherPriority.Input,DispatcherPriority.Loaded,DispatcherPriority.Render,DispatcherPriority.DataBind,DispatcherPriority.Normal})
            {
                double posted=watch.Elapsed.TotalMilliseconds;
                _=renderer.View.Dispatcher.BeginInvoke(priority,new Action(()=>probes.Add(new{priority=priority.ToString(),waitMs=watch.Elapsed.TotalMilliseconds-posted})));
            }
            renderer.Offer(projection.Project(Fixture(),false,renderer.Epoch,2,At.AddSeconds(1)));
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Until(()=>Convert.ToInt64(Read(renderer.Measurements,"acknowledged"))==2);
            using var metrics=JsonDocument.Parse(JsonSerializer.Serialize(renderer.Measurements));
            double delivery=metrics.RootElement.GetProperty("dispatcherQueueMs").GetProperty("max").GetDouble();
            string browser=await renderer.Core.ExecuteScriptAsync("window.sierraMapDiagnostics.read()");
            using var state=JsonDocument.Parse(browser);
            Check(renderTurns>100,"Delivery: sustained render work was exercised without blocking a single callback");
            Check(delivery<250,"Delivery: current map data reaches WebView during sustained render work instead of waiting for render idle");
            Check(acknowledgedDuringPressureAt is >=0 and <750,"Delivery: newest snapshot is acknowledged before render pressure ends, including worker/serialization time");
            Check(state.RootElement.GetProperty("lastSequence").GetInt64()==2,"Delivery: browser receives the newest offered snapshot after render pressure");
            Write(directory,"delivery-pressure.json",new{renderTurns,pressureMs=750,probes,deliveryQueueMaxMs=delivery,acknowledgedDuringPressureAt,bridge=renderer.Measurements,browser=state.RootElement.Clone(),
                limitation="Deterministic dispatcher-order regression; not physical input latency or GPU frame latency. Existing heartbeat gate is unchanged and runs separately."});
        });
    }
}
