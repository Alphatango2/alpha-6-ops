using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop.MapV2;

internal static partial class MapPerformanceChecks
{
    private static async Task Scheduling(MainWindow main,FlightSessionCoordinator owner,Source source,Resources resources,Timings timings,string run,string mode,double seconds)
    {
        string arm=mode["scheduling-".Length..];
        var allowed=new[]{"baseline","render-hook","native","clock-off","ui-feed","ui-clock-off","native-ui-clock-off","quiescent","hidden","cards","activity","handler","status","probe","poll","heartbeat100","resources5","drain","contract"};
        if(!allowed.Contains(arm))throw new ArgumentException("Unknown scheduling arm");
        bool uiFeed=arm is "ui-feed" or "ui-clock-off" or "native-ui-clock-off";
        var host=main.OperationsView.MapHost;host.CompositionHost=!arm.StartsWith("native",StringComparison.Ordinal);
        var dashboard=(DispatcherTimer)FlightSessionDiagnosticAccess.Get(main,"dashboardClock")!;
        if(arm.Contains("clock-off",StringComparison.Ordinal))dashboard.Stop();
        var heartbeat=(DispatcherTimer)typeof(Timings).GetField("timer",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(timings)!;
        if(arm=="heartbeat100")heartbeat.Interval=TimeSpan.FromMilliseconds(100);
        await EnsureVisibleMap(main);var renderer=host.V2!;var epoch=renderer.Epoch;var generation=owner.Snapshot.ConnectionGeneration;
        resources.Sample(renderer,"initial");
        using var trace=new DispatcherSchedulingTrace(main,dashboard,resources.LiveBrowserIds(),arm=="render-hook");
        timings.OnHeartbeat=ms=>trace.Mark("heartbeat",value:ms);
        renderer.Core.WebMessageReceived+=(_,e)=>{using var json=System.Text.Json.JsonDocument.Parse(e.WebMessageAsJson);trace.Mark("web-message",json.RootElement.GetProperty("type").GetString()??"unknown");};
        renderer.StatusChanged+=s=>trace.Mark("map-status",s.ToString()??"");
        var conditions=new{arm,traceRevision=2,renderObserver=arm=="render-hook",compositionHost=host.CompositionHost,dashboardClock=dashboard.IsEnabled,feed=uiFeed?"ui-paced":"independent",targetHz=1,
            heartbeatMs=heartbeat.Interval.TotalMilliseconds,probeMs=arm is "probe" or "drain"?100:0,pollMs=arm is "poll" or "drain"?50:0,
            resourcesMs=arm=="resources5"?5000:30000,warmupSeconds=5,measuredSeconds=seconds,tailSeconds=6,
            finalDrain=arm=="drain",width=main.ActualWidth,height=main.ActualHeight,
            presentationAblation=arm is "cards" or "activity"?"Selected visual subtrees collapsed; model refresh retained":arm=="handler"?"RuntimeChanged detached; map-only snapshot presentation; clock unchanged":"none"};
        EventHandler handler=RuntimeHandler(main);EventHandler? mapOnly=null;
        object? savedPump=null,savedContext=null;
        var pumpField=typeof(OperationsMapHost).GetField("pump",BindingFlags.NonPublic|BindingFlags.Instance)!;
        var contextField=typeof(OperationsMapHost).GetField("ContextChanged",BindingFlags.NonPublic|BindingFlags.Instance)!;
        var consumed=new Dictionary<DateTimeOffset,double>();
        void Observed(object? sender,EventArgs e){if(owner.Snapshot.Telemetry is {} t)consumed.TryAdd(t.At,trace.Now);trace.Mark("runtime-observed");}
        owner.SnapshotChanged+=Observed;
        using var cancellation=new CancellationTokenSource();
        Task producer=Task.CompletedTask,probes=Task.CompletedTask;
        object? windowEvidence=null;
        int emitted=0;long probeId=0;int pendingProbe=0;
        try
        {
            if(arm=="contract")
            {
                await Task.Delay(100);
                trace.Mark("injection-begin","sleep600");Thread.Sleep(600);trace.Mark("injection-end","sleep600");
                trace.Mark("pending-heartbeat",value:timings.PendingHeartbeatMs);
                Check(timings.PendingHeartbeatMs>=550,"Scheduling trace detects >500ms pending heartbeat before callback");
                await main.Dispatcher.InvokeAsync(()=>trace.Mark("drain-executed"),DispatcherPriority.Input);
                await Task.Delay(100);
            }
            else
            {
                // All mutations below affect the isolated diagnostic view only.
                if(arm=="cards")main.OperationsView.Metrics.Visibility=Visibility.Collapsed;
                if(arm=="activity"){main.OperationsView.ActivityList.Visibility=Visibility.Collapsed;main.OperationsView.AlertsList.Visibility=Visibility.Collapsed;}
                if(arm=="handler")
                {
                    owner.SnapshotChanged-=handler;
                    mapOnly=(_,_)=>host.Present(OperationsOverviewQuery.Project(owner.Snapshot,false,[],null,false,DateTimeOffset.UtcNow));owner.SnapshotChanged+=mapOnly;
                }
                if(arm=="status"){savedContext=contextField.GetValue(host);contextField.SetValue(host,null);}
                if(arm=="quiescent")
                {
                    savedPump=pumpField.GetValue(host);pumpField.SetValue(host,null);
                    if(savedPump is MapProjectionPump pump)await pump.DisposeAsync();
                }
                if(arm=="hidden")
                {
                    host.Visibility=Visibility.Hidden;
                    Check(ReferenceEquals(renderer,host.V2)&&renderer.Paused,"Scheduling hide immediately retains and pauses the current renderer");
                }
                int count=(int)Math.Ceiling(5+seconds+(arm=="drain"?0:6));
                async Task Feed()
                {
                    var watch=Stopwatch.StartNew();
                    for(int i=0;i<count;i++)
                    {
                        if(uiFeed){if(i>0)await Task.Delay(1000,cancellation.Token);}
                        else {double delay=i*1000-watch.Elapsed.TotalMilliseconds;if(delay>0)await Task.Delay(TimeSpan.FromMilliseconds(delay),cancellation.Token).ConfigureAwait(false);}
                        var at=Start.AddSeconds(i);trace.Mark("feed-emitted",value:i);Interlocked.Increment(ref emitted);source.Emit(Cruise(at,i*.0001));
                    }
                    trace.Mark("producer-complete");
                }
                producer=uiFeed?Feed():Task.Run(Feed);
                if(arm is "probe" or "drain")probes=Task.Run(async()=>
                {
                    while(!cancellation.IsCancellationRequested)
                    {
                        await Task.Delay(100,cancellation.Token).ConfigureAwait(false);
                        if(Interlocked.Exchange(ref pendingProbe,1)!=0){trace.Mark("probe-skipped");continue;}
                        long id=Interlocked.Increment(ref probeId);double at=trace.Now;trace.Mark("probe-scheduled",id:id);
                        _=main.Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>{trace.Mark("probe-begin",id:id,value:trace.Now-at);Volatile.Write(ref pendingProbe,0);trace.Mark("probe-end",id:id);}));
                    }
                });
                await Task.Delay(5000);timings.Reset();resources.ResetCpu();trace.Mark("measurement-begin");
                var hiddenProduced=renderer.Metrics.Produced;
                double end=trace.Now+seconds*1000,nextResource=trace.Now;
                // No frequent synthetic wakeups except in their explicitly named arms.
                while(trace.Now<end)
                {
                    if(trace.Now>=nextResource){trace.Mark("resource-begin");resources.Sample(host.V2,arm);trace.Mark("resource-end");nextResource+=arm=="resources5"?5000:30000;}
                    await Task.Delay(arm is "poll" or "drain"?50:(int)Math.Min(arm=="resources5"?5000:30000,Math.Max(1,end-trace.Now)));
                }
                trace.Mark("measurement-end");
                windowEvidence=new{conditions,timing=timings.Read(),resources=resources.Summary(),bridge=renderer.Measurements};
                trace.Mark("tail-begin",arm=="drain"?"producers stopping":"normal continued operation");
                if(arm=="drain")
                {
                    await producer;trace.Mark("producer-awaited");
                    cancellation.Cancel();try{await probes;}catch(OperationCanceledException){}
                    trace.Mark("probe-producer-awaited");trace.Mark("pending-heartbeat",value:timings.PendingHeartbeatMs);
                    trace.Mark("drain-request");await main.Dispatcher.InvokeAsync(()=>trace.Mark("drain-executed"),DispatcherPriority.Input);trace.Mark("drain-resumed");
                }
                await Task.Delay(6000);trace.Mark("tail-end");
                await producer.WaitAsync(TimeSpan.FromSeconds(15));
                cancellation.Cancel();try{await probes;}catch(OperationCanceledException){}
                trace.Mark("pending-heartbeat",value:timings.PendingHeartbeatMs);
                trace.Mark("termination-drain-request");await main.Dispatcher.InvokeAsync(()=>trace.Mark("termination-drain-executed"),DispatcherPriority.Input);trace.Mark("termination-drain-resumed");
                await Until(()=>consumed.Count==emitted,5000);
                var recovered=FlightRecoveryStore.Load(Plan,System.IO.Path.Combine(run,"session"));
                Check(consumed.Count==emitted&&owner.ReadTimeline()?.Snapshots.Count==emitted,"Scheduling arm retains every authoritative input");
                Check(recovered?.LastSimulatorUtc==Start.AddSeconds(emitted-1),"Scheduling arm final recovery contains last input");
                Check(source.Starts==1&&owner.Snapshot.ConnectionGeneration==generation,"Scheduling ablation never reconnects simulator source");
                Check(host.LastFailure is null&&(arm=="hidden"?host.V2 is null||host.V2.Epoch==epoch&&host.V2.Paused&&host.V2.Core.IsSuspended:host.V2?.Epoch==epoch),"Scheduling host lifecycle matches arm without failure/recreation");
                if(arm=="hidden")Check(renderer.Metrics.Produced==hiddenProduced,"Scheduling hidden arm produces no visual snapshots throughout measurement");
                Write(run,"scheduling-integrity.json",new{emitted,consumed=consumed.Count,source.Starts,generation,phase=owner.Snapshot.Phase,epoch,host.LastFailure});
            }
            Check(trace.Dropped==0,"High-resolution trace has no dropped events");
            trace.Mark("trace-stop-request");
            var evidence=await trace.FinishAsync();
            if(windowEvidence is not null)Write(run,"scheduling-window.json",windowEvidence);
            Write(run,"scheduling-trace.json",new{conditions,trace=evidence});
        }
        finally
        {
            cancellation.Cancel();try{await producer;}catch(OperationCanceledException){}try{await probes;}catch(OperationCanceledException){}
            timings.OnHeartbeat=null;owner.SnapshotChanged-=Observed;
            if(mapOnly is not null){owner.SnapshotChanged-=mapOnly;owner.SnapshotChanged+=handler;}
            if(savedContext is not null)contextField.SetValue(host,savedContext);
            // A disposed diagnostic pump is intentionally not reattached. RunAsync closes this isolated view.
            dashboard.Start();heartbeat.Interval=TimeSpan.FromMilliseconds(20);
        }
    }
}
