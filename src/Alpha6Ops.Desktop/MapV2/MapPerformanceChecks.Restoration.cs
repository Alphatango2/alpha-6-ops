using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Alpha6Ops.Desktop.MapV2;

internal static partial class MapPerformanceChecks
{
    // Explicit diagnostic only. Each browser/session lives in this run directory.
    // The baseline exercises production navigation before a disposable trial.
    private static async Task Restoration(MainWindow main,FlightSessionCoordinator owner,Source source,Resources resources,Timings timings,string run,bool verify)
    {
        var host=main.OperationsView.MapHost;
        source.Emit(Cruise(Start,0));await Until(()=>owner.Snapshot.SimulatorUtc==Start);
        host.SetRenderer(false);
        var cold=Stopwatch.StartNew();await EnsureVisibleMap(main);
        await Until(()=>host.V2?.View.Opacity==1);
        var coldMs=cold.Elapsed.TotalMilliseconds;
        var rows=new List<object>();var initial=host.V2!;var initialEpoch=initial.Epoch;
        if(verify)
        {
            initial.Present(new(new(new(149,-36),5,18,0,null),new(MapEntityKind.Aircraft,"current-aircraft"),Enum.GetValues<MapLayer>().Where(x=>x!=MapLayer.Labels).ToImmutableHashSet(),true,DateTimeOffset.UtcNow){RestoreCamera=true});
            await Task.Delay(200);
        }
        resources.Sample(initial,"restoration-cold");
        var same=0;var navigation=new Action[]{main.ShowDispatch,main.ShowFlightTracking,main.ShowPilotLogbook};
        for(var cycle=0;cycle<(verify?18:6);cycle++)
        {
            var before=host.V2!;var beforeEpoch=before.Epoch;
            var beforeBrowser=await ReadBrowser(before);
            navigation[cycle%navigation.Length]();
            await Task.Delay(250);
            var retained=ReferenceEquals(before,host.V2);
            var produced=before.Metrics.Produced;
            source.Emit(Cruise(Start.AddSeconds(cycle+1),(cycle+1)*.001));
            await Until(()=>owner.Snapshot.SimulatorUtc==Start.AddSeconds(cycle+1));
            if(verify)
            {
                await Task.Delay(250);
                Check(retained&&before.Paused&&before.Core.IsSuspended,$"Navigation {cycle+1}: retained hidden browser reaches native suspension");
                Check(before.Metrics.Produced==produced,$"Navigation {cycle+1}: hidden telemetry produces no visual snapshots");
                var current=RestorationSnapshot(before);
                if(current is not null)before.ReceiveMessage(WebMapRenderer.Origin+"/index.html",JsonSerializer.Serialize(new{type="ack",schemaVersion=1,epoch=before.Epoch,generation=before.Epoch,sequence=current.Sequence}));
                Check(before.View.Opacity==0,$"Navigation {cycle+1}: late browser ACK cannot reveal hidden map");
            }
            if(cycle==0)
            {
                timings.Reset();resources.ResetCpu();
                using var cpu=new RestorationCpu(host.V2);
                for(var sample=0;sample<20;sample++){resources.Sample(host.V2,"restoration-hidden");await Task.Delay(250);}
                Write(run,"restoration-hidden-resources.json",new{resources=resources.Summary(),cpu=cpu.Read(),timings=timings.Read(),retained});
            }
            var restore=Stopwatch.StartNew();await EnsureVisibleMap(main);
            await Until(()=>host.V2?.View.Opacity==1);
            var correctVisibleMs=restore.Elapsed.TotalMilliseconds;
            var after=host.V2!;var browser=await ReadBrowser(after);
            if(verify)
            {
                await Until(()=>RestorationSnapshot(after)?.Aircraft.FirstOrDefault()?.ObservedAt==Start.AddSeconds(cycle+1));
                browser=await ReadBrowser(after);
                var snapshot=RestorationSnapshot(after)!;
                Check(snapshot.Aircraft.Single().Position.Latitude==-35.6-(cycle+1)*.001,$"Navigation {cycle+1}: latest hidden telemetry reaches restored aircraft");
                var observed=snapshot.Routes.SingleOrDefault(r=>r.Kind==MapRouteKind.ObservedTrack);
                var aircraft=snapshot.Aircraft.Single().Position;
                Check(observed is not null&&observed.Segments.Any(s=>s.Length>=2)&&observed.Segments[^1][^1]==aircraft,
                    $"Navigation {cycle+1}: hidden telemetry extends the flight-scoped trail through the latest aircraft position");
                Check(browser.GetProperty("camera").GetRawText()==beforeBrowser.GetProperty("camera").GetRawText()&&browser.GetProperty("selection").GetRawText()==beforeBrowser.GetProperty("selection").GetRawText(),$"Navigation {cycle+1}: camera and selection survive");
                Check(await after.Core.ExecuteScriptAsync("document.querySelector('[data-layer=Labels]').checked===false&&document.getElementById('reduced-motion').checked") == "true",$"Navigation {cycle+1}: layer and reduced-motion preferences survive");
                Check(after.View.Opacity==1&&browser.GetProperty("solarSignature").GetString()==string.Join(',',snapshot.SunVector.Select(v=>v.ToString("F3",System.Globalization.CultureInfo.InvariantCulture))),$"Navigation {cycle+1}: visible frame uses current snapshot solar state");
            }
            if(ReferenceEquals(before,after)&&beforeEpoch==after.Epoch)same++;
            resources.Sample(after,"restoration-return-"+cycle);
            rows.Add(new{cycle,target=cycle%3==0?"Dispatch":cycle%3==1?"Flights":"Logbook",retainedWhileHidden=retained,sameRenderer=ReferenceEquals(before,after),beforeEpoch,afterEpoch=after.Epoch,correctVisibleMs,restoreMs=restore.Elapsed.TotalMilliseconds,browser,resources=resources.Summary()});
            Write(run,"restoration-navigation.json",new{coldMs,rows,sourceStarts=source.Starts,host.RecreationCount});
        }
        Check(same==rows.Count,"Workspace navigation retains the same renderer and bridge epoch");
        Check(source.Starts==1,"Map navigation does not reconnect the authoritative simulator source");
        if(verify)
        {
            var retained=host.V2!;var recreated=host.RecreationCount;
            for(var i=0;i<12;i++){main.ShowDispatch();main.ShowDashboard();}
            await host.WaitReadyAsync();await Until(()=>host.V2?.View.Opacity==1);
            Check(ReferenceEquals(retained,host.V2)&&host.RecreationCount==recreated,"Rapid hide/show requests cannot create duplicate renderers or leave stale suspension");
            var browserBlock=retained.Core.ExecuteScriptAsync("(()=>{const start=performance.now();while(performance.now()-start<1000){};return 'controlled lifecycle ACK delay'})()");
            source.Emit(Cruise(Start.AddSeconds(30),.030));
            await Until(()=>typeof(WebMapRenderer).GetField("applying",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(retained) is not null);
            main.ShowDispatch();main.ShowDashboard();
            await browserBlock.WaitAsync(TimeSpan.FromSeconds(5));
            await host.WaitReadyAsync();await Until(()=>host.V2?.View.Opacity==1&&RestorationSnapshot(host.V2)?.Aircraft.FirstOrDefault()?.ObservedAt==Start.AddSeconds(30));
            Check(ReferenceEquals(retained,host.V2)&&host.LastFailure is null,"Hide and return with a delayed in-flight browser ACK cancels obsolete work without restart or timeout");
            main.WindowState=WindowState.Minimized;await Task.Delay(250);
            Check(ReferenceEquals(retained,host.V2)&&retained.Paused,"Minimize retains the same paused map");
            main.WindowState=WindowState.Normal;await EnsureVisibleMap(main);await Until(()=>host.V2?.View.Opacity==1);
            Check(ReferenceEquals(retained,host.V2)&&!retained.Paused,"Restore resumes the same map after minimization");
            var expiry=(System.Windows.Threading.DispatcherTimer)typeof(OperationsMapHost).GetField("dormantExpiry",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
            var retention=expiry.Interval;expiry.Interval=TimeSpan.FromMilliseconds(150);
            main.ShowDispatch();await Until(()=>host.V2 is null);expiry.Interval=retention;
            await EnsureVisibleMap(main);await Until(()=>host.V2?.View.Opacity==1);
            Check(host.V2!.Epoch!=retained.Epoch,"Dormant expiry releases the renderer and a later return creates a fresh epoch");
            var expired=host.V2!;host.SetRenderer(false);
            Check(host.V2 is null&&!host.UseV2,"Classic selection explicitly releases retained WebView ownership");
            host.SetRenderer(true);await host.WaitReadyAsync();await Until(()=>host.V2?.View.Opacity==1);
            Check(host.V2!.Epoch!=expired.Epoch,"Explicit Classic to V2 switch initializes a fresh renderer");
        }
        if(!verify)
        {
            host.SetRenderer(false);await Until(()=>resources.LiveBrowserIds().Length==0);
            await RetainedHiddenTrial(run,resources,timings);
        }
        Write(run,"restoration-result.json",new{mode=verify?"production-retained":"baseline-destroy-and-diagnostic-trial",coldMs,returns=rows.Count,sameRendererReturns=same,initialEpoch,sourceStarts=source.Starts,host.RecreationCount,scope="Actual isolated WPF/WebView2 and injected synthetic readings; no physical mouse or native simulator certification"});
    }

    private static MapSnapshot? RestorationSnapshot(WebMapRenderer renderer)=>(MapSnapshot?)typeof(WebMapRenderer).GetField("applied",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(renderer);

    private static async Task RetainedHiddenTrial(string run,Resources resources,Timings timings)
    {
        await using var renderer=new WebMapRenderer(900724,Path.Combine(run,"trial-browser"));
        var window=new Window{Title="Sierra retained-hidden lifecycle trial",Width=1200,Height=700,Content=renderer.View,ShowInTaskbar=false};
        window.Show();
        try
        {
            await renderer.InitializeAsync(CancellationToken.None);
            renderer.Offer(RichLoad(renderer.Epoch,1,1,0));await Until(()=>renderer.View.Opacity==1);
            // Pause scheduled visual callbacks in this trial document only. No
            // production API or host policy is changed before its measurement.
            await renderer.Core.ExecuteScriptAsync("(()=>{const raf=window.requestAnimationFrame.bind(window);window.__retentionTrial={paused:false,pending:[]};window.requestAnimationFrame=callback=>{const p=window.__retentionTrial;if(p.paused){p.pending.push(callback);return 0;}return raf(callback);};window.__retentionTrial.resume=()=>{const p=window.__retentionTrial;p.paused=false;for(const callback of p.pending.splice(0))raf(callback);};})()");
            await Task.Delay(1000);
            var before=await ReadBrowser(renderer);
            await renderer.Core.ExecuteScriptAsync("window.__retentionTrial.paused=true");window.Hide();await Task.Delay(250);
            var suspendSucceeded=await renderer.Core.TrySuspendAsync();
            timings.Reset();resources.ResetCpu();
            using var cpu=new RestorationCpu(renderer);
            for(var sample=0;sample<20;sample++){resources.Sample(renderer,"trial-retained-hidden");await Task.Delay(250);}
            var hiddenResources=resources.Summary();var hiddenTimings=timings.Read();var hiddenCpu=cpu.Read();
            var suspended=renderer.Core.IsSuspended;
            renderer.Core.Resume();window.Show();var restore=Stopwatch.StartNew();
            await renderer.Core.ExecuteScriptAsync("window.__retentionTrial.resume()");
            renderer.Offer(RichLoad(renderer.Epoch,2,1,.1));await Until(()=>ReadBrowserSequence(renderer)==2);
            var after=await ReadBrowser(renderer);
            Write(run,"retained-hidden-trial.json",new{hiddenSeconds=5,suspendSucceeded,suspended,hiddenResources,hiddenCpu,hiddenTimings,restoreMs=restore.Elapsed.TotalMilliseconds,before,after,scope="Disposable actual WebView trial; requestAnimationFrame pause injected only into trial document plus real CoreWebView2.TrySuspendAsync; retained process/working-set cost is measured, not assumed free"});
            Check(renderer.Epoch==900724&&ReadBrowserSequence(renderer)==2,"Diagnostic retained-hidden trial keeps epoch and accepts latest snapshot");
        }
        finally{window.Content=null;window.Close();}
    }

    private sealed class RestorationCpu:IDisposable
    {
        private readonly List<(Process Process,double Initial,bool Browser)> processes=[];
        private readonly Stopwatch elapsed=Stopwatch.StartNew();
        internal RestorationCpu(WebMapRenderer? renderer)
        {
            foreach(var pid in (renderer?.Environment?.GetProcessInfos().Select(p=>p.ProcessId)??[]).Append(Environment.ProcessId).Distinct())
            {
                var process=Process.GetProcessById(pid);
                _=process.Handle; // Hold original identity throughout the bounded sample.
                processes.Add((process,process.TotalProcessorTime.TotalMilliseconds,pid!=Environment.ProcessId));
            }
        }
        internal object Read()
        {
            double browser=0,host=0;
            foreach(var item in processes)
            {
                if(item.Process.HasExited)continue;
                var delta=item.Process.TotalProcessorTime.TotalMilliseconds-item.Initial;
                if(item.Browser)browser+=delta;else host+=delta;
            }
            return new{wallMs=elapsed.Elapsed.TotalMilliseconds,browserCpuMs=browser,hostCpuMs=host,browserPercentOfMachine=browser/elapsed.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100,hostPercentOfMachine=host/elapsed.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100,logicalProcessors=Environment.ProcessorCount};
        }
        public void Dispose(){foreach(var item in processes)item.Process.Dispose();}
    }
}
