using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Alpha6Ops.Desktop.MapV2;

// Only reached by the explicit isolated diagnostic CLI. No normal runtime call site.
internal static partial class MapPerformanceChecks
{
    private static async Task Responsiveness(MainWindow main,FlightSessionCoordinator owner,Source source,Resources resources,Timings timings,string run,double minutes,bool light,bool probeEnabled)
    {
        if(minutes<1||minutes*2!=Math.Floor(minutes*2))throw new ArgumentException("Use complete 30-second windows, at least one minute.");
        await EnsureVisibleMap(main);
        var host=main.OperationsView.MapHost;
        var renderer=host.V2!;
        var epoch=renderer.Epoch;
        long generation=owner.Snapshot.ConnectionGeneration;
        const double warmup=15, hz=1.1;
        double duration=warmup+minutes*60;
        int expected=(int)Math.Ceiling(duration*hz);
        var watch=Stopwatch.StartNew();
        var emissions=new List<object>();var consumed=new Dictionary<DateTimeOffset,double>();var records=new List<object>();
        var heartbeatRaw=new List<object>();var queueRaw=new List<object>();var queue=new MapMeasurement();
        var probeState=new ResponsivenessProbeState();bool visibleThroughout=true;
        using var cancellation=new CancellationTokenSource();
        using var probeCancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
        void Observed(object? sender,EventArgs args){if(owner.Snapshot.Telemetry is {} s)consumed.TryAdd(s.At,watch.Elapsed.TotalMilliseconds);visibleThroughout&=main.IsVisible&&host.IsVisible&&host.V2?.Epoch==epoch&&host.UseV2;}
        owner.SnapshotChanged+=Observed;
        timings.OnHeartbeat=ms=>heartbeatRaw.Add(new{seconds=watch.Elapsed.TotalSeconds,ms});
        // Producer never waits for presentation. The only coalescing here is the
        // diagnostic queue probe, bounded to one outstanding probe, not flight input.
        var producer=Task.Run(async()=>
        {
            for(int i=0;i<expected;i++)
            {
                double due=i*1000/hz,delay=due-watch.Elapsed.TotalMilliseconds;
                if(delay>0)await Task.Delay(TimeSpan.FromMilliseconds(delay),cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                var at=Start.AddSeconds(i);
                emissions.Add(new{index=i,at,dueMs=due,emittedMs=watch.Elapsed.TotalMilliseconds});
                source.Emit(Cruise(at,i*.0001));
            }
        },cancellation.Token);
        var probes=probeEnabled?Task.Run(async()=>
        {
            while(!probeCancellation.IsCancellationRequested)
            {
                await Task.Delay(100,probeCancellation.Token);
                var started=probeState.TryStart(watch);if(started is null)continue;
                double posted=started.Value;
                _=main.Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>
                {
                    double ms=watch.Elapsed.TotalMilliseconds-posted;
                    queue.Add(ms);queueRaw.Add(new{seconds=watch.Elapsed.TotalSeconds,ms});
                    probeState.Complete();
                }));
            }
        },probeCancellation.Token):Task.CompletedTask;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(warmup));
            resources.Sample(renderer,"warmup-end");
            timings.Reset();resources.ResetCpu();queue=new();heartbeatRaw.Clear();queueRaw.Clear();probeState.ResetSkipped();
            double start=watch.Elapsed.TotalSeconds,nextResource=start;
            var previousBridge=Element(renderer.Measurements);
            for(int index=0;index<(int)(minutes*2);index++)
            {
                double end=start+30;
                if(!probeEnabled)
                {
                    // Truly passive arm: no synthetic 50ms Normal-priority polling
                    // wakeups and no extra Input probe. Production telemetry/clock,
                    // heartbeat observation and durable saves all remain active.
                    resources.Sample(renderer,"passive");
                    await Task.Delay(TimeSpan.FromSeconds(30));
                    if(producer.IsFaulted)await producer;
                }
                while(probeEnabled&&watch.Elapsed.TotalSeconds<end)
                {
                    if(producer.IsFaulted)await producer;
                    visibleThroughout&=main.IsVisible&&host.IsVisible&&host.V2?.Epoch==epoch&&host.UseV2;
                    if(watch.Elapsed.TotalSeconds>=nextResource)
                    {
                        resources.Sample(renderer,light?"light":"full");
                        nextResource+=light?30:5;
                    }
                    await Task.Delay(50);
                }
                // Include unfinished callback ages before yielding. A Normal-priority
                // continuation can run before an overdue Input tick/probe; resetting
                // then would erase a stall straddling the window boundary.
                var boundary=new{heartbeatPendingMs=timings.PendingHeartbeatMs,queuePendingMs=probeState.PendingAge(watch)};
                if(index==(int)(minutes*2)-1)
                {
                    probeCancellation.Cancel();
                    try{await probes;}catch(OperationCanceledException){}
                }
                await main.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Input);
                if(index==(int)(minutes*2)-1)await Until(()=>!probeState.Pending);
                var bridge=Element(renderer.Measurements);
                double ended=watch.Elapsed.TotalSeconds;
                records.Add(new{index,startSeconds=start,endSeconds=ended,seconds=ended-start,epoch,visibleThroughout,
                    view=new{width=main.ActualWidth,height=main.ActualHeight,compositionHost=host.CompositionHost},
                    timing=timings.Read(),heartbeatSamples=heartbeatRaw.ToArray(),dispatcherProbeMs=queue.Read(),queueSamples=queueRaw.ToArray(),probeSkipped=probeState.Skipped,boundary,
                    resources=resources.Summary(),bridge,
                    mapDelta=new{produced=bridge.GetProperty("produced").GetInt64()-previousBridge.GetProperty("produced").GetInt64(),
                        acknowledged=bridge.GetProperty("acknowledged").GetInt64()-previousBridge.GetProperty("acknowledged").GetInt64(),
                        coalesced=bridge.GetProperty("coalesced").GetInt64()-previousBridge.GetProperty("coalesced").GetInt64()}});
                previousBridge=bridge;
                // Serialize outside measurement windows; do not attribute file-output
                // cost to the application. Resource sampling remains inside each window.
                Write(run,"responsiveness-progress.json",new{light,records});
                timings.Reset();resources.ResetCpu();queue=new();heartbeatRaw.Clear();queueRaw.Clear();probeState.ResetSkipped();
                start=watch.Elapsed.TotalSeconds;
            }
            await producer.WaitAsync(TimeSpan.FromSeconds(10));
            await Until(()=>consumed.Count==expected,5000);
            var recovered=FlightRecoveryStore.Load(Plan,System.IO.Path.Combine(run,"session"));
            Check(consumed.Count==expected&&owner.ReadTimeline()?.Snapshots.Count==expected,"Responsiveness trial retains every independent authoritative input");
            Check(recovered?.LastSimulatorUtc==Start.AddSeconds(expected-1),"Responsiveness trial recovery includes final input");
            Check(source.Starts==1&&owner.Snapshot.ConnectionGeneration==generation,"Responsiveness trial never reconnects simulator source");
            Check(visibleThroughout&&host.V2?.Epoch==epoch&&host.LastFailure is null,"Responsiveness trial stays visible with one healthy renderer epoch");
            Check(((DispatcherTimer)FlightSessionDiagnosticAccess.Get(main,"dashboardClock")!).IsEnabled,"Responsiveness trial retains production dashboard clock");
            Check(records.Count==(int)(minutes*2),"Responsiveness trial flushes every requested complete observation window");
            Check(Number(resources.Summary(),"samplingErrors")==0,"Responsiveness trial resource observations have no sampling errors");
            Write(run,"responsiveness.json",new{schema=2,light,probeEnabled,pollIntervalMs=probeEnabled?50:0,requestedMinutes=minutes,warmupSeconds=warmup,targetHz=hz,expected,processed=consumed.Count,
                sourceStarts=source.Starts,visibleThroughout,epoch,seconds=watch.Elapsed.TotalSeconds,records,
                limitations="Bridge percentiles are cumulative within the one renderer epoch; independent Input queue probe and other timings reset per window. This is a visible current-flight cruise trial, not a replacement for full-flight lifecycle coverage."});
            Write(run,"responsiveness-feed.json",new{emissions,consumed=consumed.Select(p=>new{at=p.Key,processedMs=p.Value})});
        }
        finally
        {
            cancellation.Cancel();owner.SnapshotChanged-=Observed;timings.OnHeartbeat=null;
            try{await producer;}catch(OperationCanceledException){}
            try{await probes;}catch(OperationCanceledException){}
            await Until(()=>!probeState.Pending);
        }
    }

    private sealed class ResponsivenessProbeState
    {
        private readonly object sync=new();private bool pending;private double posted;private int skipped;
        internal double? TryStart(Stopwatch watch){lock(sync){if(pending){skipped++;return null;}pending=true;posted=watch.Elapsed.TotalMilliseconds;return posted;}}
        internal void Complete(){lock(sync)pending=false;}
        internal bool Pending{get{lock(sync)return pending;}}
        internal int Skipped{get{lock(sync)return skipped;}}
        internal void ResetSkipped(){lock(sync)skipped=0;}
        internal double PendingAge(Stopwatch watch){lock(sync)return pending?watch.Elapsed.TotalMilliseconds-posted:0;}
    }

    private static async Task ResponsivenessBoundaryContract(MainWindow main,Timings timings)
    {
        var watch=Stopwatch.StartNew();var state=new ResponsivenessProbeState();double completed=0;
        timings.Reset();
        // Keep this UI turn active until the real Input callback is queued, then
        // deliberately block across the synthetic end-of-window boundary.
        Task.Run(()=>
        {
            double posted=state.TryStart(watch)!.Value;
            _=main.Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>{completed=watch.Elapsed.TotalMilliseconds-posted;state.Complete();}));
        }).Wait(TimeSpan.FromSeconds(2));
        Thread.Sleep(600);
        double pendingBeat=timings.PendingHeartbeatMs,pendingQueue=state.PendingAge(watch);
        Check(Element(timings.Read()).GetProperty("heartbeatMs").GetProperty("max").GetDouble()<500&&pendingBeat>=550,"Boundary captures a >500ms UI stall before overdue heartbeat callback executes");
        Check(completed==0&&pendingQueue>=550,"Boundary captures a >500ms pending Input probe before its callback executes");
        await main.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Input);
        await Until(()=>!state.Pending);
        Check(completed>=550,"Final Input probe completion is drained before evidence closes");
    }
}
