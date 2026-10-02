using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Alpha6Ops.Desktop.MapV2;

internal static partial class MapPerformanceChecks
{
    // Explicit diagnostics only. CDP exercises Chromium input/rendering, not the
    // CompositionControl's physical pointer route. The two must not be conflated.
    private static async Task Interaction(MainWindow main, FlightSessionCoordinator owner, Source source,
        Resources resources, Timings timings, string run, string mode, double seconds)
    {
        if (mode is not ("interaction-baseline" or "interaction-contract" or "interaction-native" or "interaction-normal"))
            throw new ArgumentException("Unknown interaction diagnostic");
        if (seconds < 10 || seconds > 180) throw new ArgumentException("Interaction window must be between ten and 180 seconds");
        var host = main.OperationsView.MapHost;
        host.CompositionHost = mode != "interaction-native";
        await EnsureVisibleMap(main);
        var renderer = host.V2!;
        resources.Sample(renderer,"interaction-start");
        var dashboard = (DispatcherTimer)FlightSessionDiagnosticAccess.Get(main,"dashboardClock")!;
        using var trace = new DispatcherSchedulingTrace(main,dashboard,resources.LiveBrowserIds());
        timings.OnHeartbeat = ms => trace.Mark("heartbeat",value:ms);
        var arrivals = new ConcurrentDictionary<DateTimeOffset,double>();
        var consumed = new Dictionary<DateTimeOffset,double>();
        var input = new ConcurrentQueue<object>();
        var latency = new MapMeasurement();
        var queue = new MapMeasurement();
        var cdp = new MapMeasurement();
        var ipc = new Dictionary<string,int>();
        void Observed(object? sender, EventArgs args)
        {
            if (owner.Snapshot.Telemetry is {} telemetry && arrivals.TryGetValue(telemetry.At,out var at) && consumed.TryAdd(telemetry.At,trace.Now))
            { latency.Add(trace.Now-at); trace.Mark("telemetry-consumed",telemetry.At.ToString("O"),trace.Now-at); }
        }
        owner.SnapshotChanged += Observed;
        renderer.Core.WebMessageReceived += (_,e) =>
        {
            using var json=JsonDocument.Parse(e.WebMessageAsJson);
            var type=json.RootElement.GetProperty("type").GetString()??"unknown";
            ipc[type]=ipc.GetValueOrDefault(type)+1;trace.Mark("browser-message",type);
        };
        // Injected observer is removed with this isolated renderer. RAF records
        // include long intervals (the production diagnostic ring excludes >300ms).
        await renderer.Core.ExecuteScriptAsync("""
        (()=>{
          const d={frames:[],pointers:[],longTasks:[],sourceCalls:[],messages:[],stopped:false,previous:0,dropped:0};
          window.interactionTrace=d;
          const add=(a,v)=>{if(a.length<60000)a.push(v);else d.dropped++};
          const frame=t=>{if(d.stopped)return;if(d.previous)add(d.frames,{t,ms:t-d.previous});d.previous=t;requestAnimationFrame(frame)};
          requestAnimationFrame(frame);
          for(const name of ['pointerdown','pointermove','pointerup'])document.addEventListener(name,e=>add(d.pointers,{t:performance.now(),type:name,x:e.clientX,y:e.clientY,buttons:e.buttons}),true);
          new PerformanceObserver(list=>{for(const e of list.getEntries())add(d.longTasks,{t:e.startTime,ms:e.duration})}).observe({entryTypes:['longtask']});
          chrome.webview.addEventListener('message',e=>add(d.messages,{t:performance.now(),type:e.data.type,sequence:e.data.snapshot?.sequence}));
          import('./vendor/maplibre-gl.mjs').then(m=>{
            const original=m.GeoJSONSource.prototype.setData;
            m.GeoJSONSource.prototype.setData=function(...args){const t=performance.now();try{return original.apply(this,args)}finally{add(d.sourceCalls,{t,ms:performance.now()-t,id:this.id})}};
            const render=m.Map.prototype._render;
            if(render)m.Map.prototype._render=function(...args){const t=performance.now();try{return render.apply(this,args)}finally{add(d.sourceCalls,{t,ms:performance.now()-t,id:'map-render'})}};
          });
          return {origin:performance.timeOrigin,now:performance.now()};
        })()
        """);
        await Task.Delay(1000);
        using var nativeTrace=mode=="interaction-contract"?new NativeMapInputTrace(main,System.IO.Path.Combine(run,"native-observer-contract")):null;
        timings.Reset();resources.ResetCpu();
        double begin=trace.Now;
        trace.Mark("interaction-begin");
        using var cancellation=new CancellationTokenSource();
        int emitted=0;
        double injectedInputDelay=0;
        var producer=Task.Run(async()=>
        {
            var watch=Stopwatch.StartNew();
            for(int i=0;i<(int)Math.Ceiling(seconds);i++)
            {
                var delay=i*1000-watch.Elapsed.TotalMilliseconds;
                if(delay>0)await Task.Delay(TimeSpan.FromMilliseconds(delay),cancellation.Token).ConfigureAwait(false);
                var at=Start.AddSeconds(i);arrivals[at]=trace.Now;
                trace.Mark("telemetry-arrived",at.ToString("O"));Interlocked.Increment(ref emitted);
                source.Emit(Cruise(at,i*.0001));
            }
        });
        var drag=Task.Run(async()=>
        {
            int step=0;
            while(trace.Now-begin<seconds*1000)
            {
                double scheduled=trace.Now;
                string type=step==0?"mousePressed":"mouseMoved";
                double x=400+150*Math.Sin(step*.04),y=160+50*Math.Sin(step*.025);
                await main.Dispatcher.InvokeAsync(async()=>
                {
                    double entered=trace.Now;queue.Add(entered-scheduled);
                    trace.Mark("pointer-dispatch",type,entered-scheduled,step);
                    await renderer.Core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type,x,y,button="left",buttons=1,clickCount=1}));
                    double finished=trace.Now;cdp.Add(finished-entered);
                    input.Enqueue(new{step,scheduled,entered,finished,x,y,type});
                },mode=="interaction-normal"?DispatcherPriority.Normal:DispatcherPriority.Input).Task.Unwrap();
                step++;
                await Task.Delay(16,cancellation.Token).ConfigureAwait(false);
            }
        });
        try
        {
            if(mode=="interaction-contract")
            {
                await Task.Delay(2850);
                double sentinelPosted=trace.Now;
                var sentinel=main.Dispatcher.InvokeAsync(()=>
                {injectedInputDelay=trace.Now-sentinelPosted;trace.Mark("injected-input-sentinel",value:injectedInputDelay);},DispatcherPriority.Input);
                trace.Mark("injection-begin","ui-650ms");
                Thread.Sleep(650);trace.Mark("injection-end","ui-650ms");await sentinel;
                await Task.Delay(2000);trace.Mark("injection-begin","js-650ms");
                await renderer.Core.ExecuteScriptAsync("(()=>{const start=performance.now();while(performance.now()-start<650){};return true})()");
                trace.Mark("injection-end","js-650ms");
            }
            await Task.WhenAll(producer,drag).WaitAsync(TimeSpan.FromSeconds(seconds+20));
            await Pointer(renderer,"mouseReleased",400,160);
            await Until(()=>consumed.Count==emitted);
            await Task.Delay(300);
            trace.Mark("interaction-end");
            var browser=JsonDocument.Parse(await renderer.Core.ExecuteScriptAsync("(()=>{const d=window.interactionTrace;d.stopped=true;return {origin:performance.timeOrigin,now:performance.now(),dropped:d.dropped,frames:d.frames,pointers:d.pointers,longTasks:d.longTasks,sourceCalls:d.sourceCalls,messages:d.messages,state:window.sierraMapDiagnostics.read()}})()")).RootElement.Clone();
            var frameMax=browser.GetProperty("frames").EnumerateArray().Select(e=>e.GetProperty("ms").GetDouble()).DefaultIfEmpty().Max();
            var sourceCalls=browser.GetProperty("sourceCalls").GetArrayLength();
            var inputRows=Element(input.ToArray()).EnumerateArray().ToArray();
            var fullQueue=InteractionDistribution(inputRows.Select(p=>p.GetProperty("entered").GetDouble()-p.GetProperty("scheduled").GetDouble()));
            var fullCdp=InteractionDistribution(inputRows.Select(p=>p.GetProperty("finished").GetDouble()-p.GetProperty("entered").GetDouble()));
            Check(consumed.Count==emitted && owner.ReadTimeline()?.Snapshots.Count==emitted,"Interaction preserves every authoritative telemetry sample");
            Check(source.Starts==1 && host.V2==renderer && host.LastFailure is null,"Sustained drag never reconnects, recreates or falls back");
            Check(browser.GetProperty("pointers").GetArrayLength()>50,"Browser receives sustained injected pointer input");
            Check(sourceCalls>0,"Interaction observer measures actual GeoJSON/render work");
            Check(browser.GetProperty("dropped").GetInt32()==0 && trace.Dropped==0,"Interaction trace covers its entire window without dropped events");
            if(mode=="interaction-contract")
            {
                Check(injectedInputDelay>=500,"Injected UI stall is detected in queued Input-priority work");
                Check(frameMax>=500,"Injected JS stall is detected in unfiltered browser frame intervals");
                nativeTrace!.Dispose();
                using var nativeEvidence=JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(run,"native-observer-contract","early-exit-events.json")));
                var nativeEvents=nativeEvidence.RootElement.GetProperty("events").EnumerateArray().ToArray();
                Check(nativeEvents.Count(e=>e.GetProperty("Kind").GetString()=="native-telemetry-arrival")==emitted &&
                    nativeEvents.Count(e=>e.GetProperty("Kind").GetString()=="native-telemetry-processed")==emitted,
                    "Opt-in native-path observer records arrival and processed timestamps for every real connection callback");
                Check(NativeMapInputTrace.TelemetryObserver is null,"Disposing opt-in trace removes its telemetry observer");
            }
            else
            {
                Check(Number(fullQueue,"max")<500,"Sustained injected drag dispatcher queue remains below 500ms");
                Check(Number(fullCdp,"max")<500,"Sustained injected drag browser round trip remains below 500ms");
                Check(frameMax<500,"Sustained injected drag browser frame interval remains below 500ms");
            }
            resources.Sample(renderer,"interaction-end");
            Write(run,"interaction.json",new{mode,seconds,emitted,consumed=consumed.Count,injectedInputDelayMs=injectedInputDelay,inputQueueMs=fullQueue,cdpMs=fullCdp,telemetryProcessingDelayMs=latency.Read(),frameMaxMs=frameMax,bridge=renderer.Measurements,timings=timings.Read(),resources=resources.Summary(),ipc,
                limitations="Synthetic 1Hz producer and CDP pointer injection; observer changes scheduling. No physical pointer/native-MSFS pass. Browser and host clocks have separate origins; sequences and explicit markers correlate them.",input=input.ToArray(),arrivals,consumption=consumed,browser});
            Write(run,"interaction-trace.json",await trace.FinishAsync());
        }
        finally
        {
            cancellation.Cancel();owner.SnapshotChanged-=Observed;timings.OnHeartbeat=null;
            try{await Task.WhenAll(producer,drag);}catch(OperationCanceledException){}
        }
    }
    private static object InteractionDistribution(IEnumerable<double> values)
    {
        var sorted=values.Order().ToArray();
        return new{count=sorted.Length,mean=sorted.Length==0?0:sorted.Average(),p50=sorted.Length==0?0:sorted[(int)((sorted.Length-1)*.5)],
            p95=sorted.Length==0?0:sorted[(int)((sorted.Length-1)*.95)],p99=sorted.Length==0?0:sorted[(int)((sorted.Length-1)*.99)],max=sorted.LastOrDefault()};
    }
}
