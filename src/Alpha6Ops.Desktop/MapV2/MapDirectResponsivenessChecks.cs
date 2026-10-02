using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Alpha6Ops.Desktop.MapV2;

// Explicit CLI diagnostics only. No production scheduler, renderer or simulator
// setting changes. Observers/controlled faults belong to this isolated document.
internal static partial class MapPerformanceChecks
{
    private static async Task<Task> DirectContracts(MainWindow main,FlightSessionCoordinator owner,Source source,Resources resources,string run,Task connection)
    {
        var checksByGroup=new Dictionary<string,List<bool>>();var records=new List<object>();
        void C(string group,bool passed,string message)
        { if(!checksByGroup.TryGetValue(group,out var list))checksByGroup[group]=list=[];list.Add(passed);Check(passed,"Direct "+group+": "+message); }
        var clock=(DispatcherTimer)FlightSessionDiagnosticAccess.Get(main,"dashboardClock")!;
        using var trace=new DispatcherSchedulingTrace(main,clock,[]);
        var emissions=new ConcurrentDictionary<DateTimeOffset,double>();
        var ingestions=new ConcurrentDictionary<DateTimeOffset,double>();
        var processed=new ConcurrentDictionary<DateTimeOffset,double>();
        var sampleStages=new ConcurrentDictionary<DateTimeOffset,string>();
        var processingOrder=new ConcurrentQueue<DateTimeOffset>();
        var readyBeforeNext=new ConcurrentQueue<bool>();
        int index=0;long generation=owner.Snapshot.ConnectionGeneration;
        var previousObserver=NativeMapInputTrace.TelemetryObserver;
        if(previousObserver is not null)throw new InvalidOperationException("Another telemetry observer is active");
        NativeMapInputTrace.TelemetryObserver=(kind,at,ticks)=>
        {
            if(kind=="arrival")ingestions.TryAdd(at,trace.Now);
            else if(kind=="processed"&&processed.TryAdd(at,trace.Now))processingOrder.Enqueue(at);
            trace.Mark("direct-telemetry-"+kind,at.ToString("O"));
        };
        Task Feed(string stage,int count)=>Task.Run(async()=>
        {
            DateTimeOffset? prior=null;var watch=Stopwatch.StartNew();
            for(int j=0;j<count;j++)
            {
                var delay=j*1000-watch.Elapsed.TotalMilliseconds;
                if(delay>0)await Task.Delay(TimeSpan.FromMilliseconds(delay)).ConfigureAwait(false);
                if(prior is {} previous)readyBeforeNext.Enqueue(processed.ContainsKey(previous));
                int n=Interlocked.Increment(ref index);var at=Start.AddSeconds(n);
                emissions[at]=trace.Now;sampleStages[at]=stage;trace.Mark("direct-emission",stage,n);
                source.Emit(Cruise(at,n*.0001));prior=at;
            }
        });
        async Task Drain()=>await Until(()=>processed.Count==emissions.Count);
        var dashboardRows=new List<object>();var dashboardTicks=0;
        EventHandler tick=(_,_)=>
        {
            dashboardTicks++;var now=DateTimeOffset.UtcNow;
            var text=main.ClockText.Text;
            bool current=text==now.ToString("HH:mm:ss 'Z'",CultureInfo.InvariantCulture)||text==now.AddSeconds(-1).ToString("HH:mm:ss 'Z'",CultureInfo.InvariantCulture);
            bool latest=main.OperationsView.Overview?.MapState?.Telemetry?.At==owner.Snapshot.Telemetry?.At;
            dashboardRows.Add(new{at=trace.Now,utc=now,text,current,latest,telemetry=owner.Snapshot.SimulatorUtc});
            trace.Mark("direct-dashboard-tick",text);
            C("dashboard",current&&latest,"real timer renders current time and newest Operations state");
        };
        clock.Tick+=tick;
        try
        {
            await Feed("warmup",1);await Drain();
            await EnsureVisibleMap(main);var host=main.OperationsView.MapHost;var renderer=host.V2!;
            resources.Sample(renderer,"direct-start");
            await Until(()=>renderer.View.Opacity==1);
            await renderer.Core.ExecuteScriptAsync("(()=>{const c=document.getElementById('reduced-motion');if(!c.checked)c.click()})()");
            await renderer.Core.ExecuteScriptAsync(DirectBrowserProbe);
            var install=Stopwatch.StartNew();
            while(await renderer.Core.ExecuteScriptAsync("!!window.directProbe")!="true")
            {if(install.Elapsed.TotalSeconds>5)throw new TimeoutException("Direct browser observer did not install");await Task.Delay(20);}
            C("map",true,"receipt, source and actual MapLibre render observers installed");
            var acknowledgments=new Dictionary<long,double>();
            var selections=new List<string>();
            renderer.Core.WebMessageReceived+=(_,e)=>
            {
                using var d=JsonDocument.Parse(e.WebMessageAsJson);
                if(d.RootElement.GetProperty("type").GetString()=="ack")
                    acknowledgments[d.RootElement.GetProperty("sequence").GetInt64()]=trace.Now;
                if(d.RootElement.GetProperty("type").GetString()=="selection"&&d.RootElement.GetProperty("kind").GetString()=="Aircraft")
                    selections.Add(d.RootElement.GetProperty("id").GetString()!);
            };
            await Feed("normal",4);await Drain();
            var inputFeed=Feed("interaction",8);
            var inputBefore=processed.Count;
            // Reproduce the old stale SDK-coordinate precondition on the real
            // WPF/browser route; it must fail this endpoint, never count as a pass.
            var bad=await DirectPress(renderer,false);records.Add(new{kind="out-of-bounds-negative",bad});
            C("input",!bad.Delivered,"cached out-of-bounds press is rejected by input contract");
            var late=await DirectPress(renderer,true,true);records.Add(new{kind="late-response-negative",late});
            C("input",late.Delivered&&!PressAccepted(late),"a trusted press handled after the original deadline is rejected, not converted to a pass");
            var good=await DirectPress(renderer,true);records.Add(new{kind="trusted-wpf-positive",good});
            C("input",PressAccepted(good),"coordinate-bearing WPF press reaches trusted in-bounds browser handler within original deadline");
            var initial=(await ReadBrowser(renderer)).GetProperty("camera").GetProperty("longitude").GetDouble();
            var dragTimes=new List<double>();
            await renderer.Core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent","{\"type\":\"mouseMoved\",\"x\":400,\"y\":180}");
            await Pointer(renderer,"mousePressed",400,180);
            for(int j=1;j<=12;j++)
            {
                var watch=Stopwatch.StartNew();
                await renderer.Core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type="mouseMoved",x=400+j*5,y=180+j*2,button="left",buttons=1}));
                dragTimes.Add(watch.Elapsed.TotalMilliseconds);await Task.Delay(35);
            }
            await Pointer(renderer,"mouseReleased",460,204);await Task.Delay(200);
            var moved=(await ReadBrowser(renderer)).GetProperty("camera").GetProperty("longitude").GetDouble()!=initial;
            C("input",moved,"browser pointer drag changes camera while telemetry runs");
            records.Add(new{kind="drag",cameraChanged=moved,cdpRoundTripMs=dragTimes});
            await renderer.Core.ExecuteScriptAsync("document.getElementById('focus').click()");await Task.Delay(700);
            var points=(await ReadBrowser(renderer)).GetProperty("points").EnumerateArray().Where(p=>p.GetProperty("kind").GetString()=="Aircraft").ToArray();
            bool selected=false;double selectionMs=0;
            foreach(var point in points)
            {
                var watch=Stopwatch.StartNew();var x=point.GetProperty("x").GetDouble();var y=point.GetProperty("y").GetDouble();
                int selectionBefore=selections.Count;string expected=point.GetProperty("id").GetString()!;
                await Pointer(renderer,"mousePressed",x,y);await Pointer(renderer,"mouseReleased",x,y);
                await Task.Delay(100);var selection=(await ReadBrowser(renderer)).GetProperty("selection");
                selected=SelectionAccepted(selection,expected,selections.Skip(selectionBefore).Contains(expected));
                C("input",!SelectionAccepted(selection,expected,false),"existing selected aircraft without a new click event cannot pass");
                selectionMs=watch.Elapsed.TotalMilliseconds;if(selected)break;
            }
            C("input",selected,"click selects observed aircraft through browser hit testing");
            records.Add(new{kind="selection",selected,selectionObservationMs=selectionMs,selections});
            await inputFeed;await Drain();
            C("liveness",processed.Count>inputBefore,"authoritative work progresses during actual input exercise");

            long epoch=renderer.Epoch;int starts=source.Starts;
            var navigationFeed=Feed("navigation",4);var restore=Stopwatch.StartNew();
            main.ShowDispatch();await Task.Delay(1300);main.ShowDashboard();await host.WaitReadyAsync();
            await Until(()=>host.V2?.View.Opacity==1);
            records.Add(new{kind="navigation",elapsedMs=restore.Elapsed.TotalMilliseconds});
            await navigationFeed;await Drain();
            C("restoration",host.V2==renderer&&renderer.Epoch==epoch&&!renderer.Paused&&source.Starts==starts,"navigation retains one renderer/source and resumes usable current map");
            C("telemetry",owner.Snapshot.ConnectionGeneration==generation,"navigation does not reconnect authoritative source");
            owner.DisconnectSimulator();await connection.WaitAsync(TimeSpan.FromSeconds(5));
            connection=owner.ConnectSimulatorAsync();await Until(()=>source.Starts==starts+1&&owner.Snapshot.Connection==SimulatorConnectionState.Connected);
            await Feed("reconnected",4);await Drain();
            C("telemetry",source.Starts==starts+1&&owner.Snapshot.Assignment?.FlightNumber==Plan.FlightNumber,"single explicit reconnect preserves assignment and resumes processing");

            // Delay the real Background timer with bounded short Render work.
            // Normal runtime updates still execute. Do not change timer priority,
            // interval or invoke a substitute refresh implementation.
            int beforeTicks=dashboardTicks;await Until(()=>dashboardTicks>beforeTicks);
            var pressure=Stopwatch.StartNew();var pressureDone=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int pressureTicks=dashboardTicks;
            double pressureBeganMs=trace.Now;trace.Mark("direct-dashboard-pressure-begin");
            void RenderTurn(){if(pressure.ElapsedMilliseconds<1500)main.Dispatcher.BeginInvoke(DispatcherPriority.Render,new Action(RenderTurn));else pressureDone.TrySetResult();}
            _=main.Dispatcher.BeginInvoke(DispatcherPriority.Render,new Action(RenderTurn));
            var dashboardFeed=Feed("dashboard-pressure",3);
            await pressureDone.Task;
            double pressureEndedMs=trace.Now;trace.Mark("direct-dashboard-pressure-end");
            bool deferred=DashboardDeferred(pressureTicks,dashboardTicks,pressure.Elapsed.TotalMilliseconds,clock.Interval.TotalMilliseconds);
            C("dashboard",deferred,"real Background timer becomes due during Render pressure without refreshing until pressure ends");
            C("dashboard",!DashboardDeferred(10,11,1500,1000)&&!DashboardDeferred(10,10,999,1000),"a serviced timer or pressure shorter than its interval cannot claim delay coverage");
            records.Add(new{kind="dashboard-pressure",pressureBeganMs,pressureEndedMs,pressureTicks,ticksAtEnd=dashboardTicks,intervalMs=clock.Interval.TotalMilliseconds,deferred});
            await dashboardFeed;await Drain();
            await Until(()=>dashboardTicks>pressureTicks);
            var stable=owner.Snapshot;var timelineBefore=owner.ReadTimeline()?.Snapshots.Count;
            int refreshes=dashboardTicks;await Until(()=>dashboardTicks>=refreshes+2);
            C("dashboard",ReferenceEquals(stable,owner.Snapshot)&&timelineBefore==owner.ReadTimeline()?.Snapshots.Count,"periodic refresh alone never mutates flight snapshot or records telemetry");
            C("dashboard",main.OperationsView.Overview?.MapState?.Telemetry?.At==stable.Telemetry?.At,"delayed refresh samples latest state instead of replaying an old capture");

            var latest=owner.Snapshot.Telemetry!;
            await Task.Delay(500);var final=JsonDocument.Parse(await renderer.Core.ExecuteScriptAsync("window.directProbe.read()")).RootElement.Clone();
            var receipts=final.GetProperty("receipts").EnumerateArray().ToArray();
            var frames=final.GetProperty("frames").EnumerateArray().ToArray();
            string latestAt=latest.At.ToString("O");
            var currentReceipts=receipts.Where(x=>DateTimeOffset.TryParse(x.GetProperty("observedAt").GetString(),out var at)&&at==latest.At).ToArray();
            bool currentAck=currentReceipts.Any(x=>acknowledgments.ContainsKey(x.GetProperty("sequence").GetInt64()));
            bool correctFrame=frames.Any(x=>x.GetProperty("loaded").GetBoolean()&&x.GetProperty("nightLoaded").GetBoolean()
                &&currentReceipts.Any(r=>r.GetProperty("sequence").GetInt64()==x.GetProperty("sequence").GetInt64()&&r.GetProperty("solar").GetString()==x.GetProperty("solar").GetString())
                &&x.GetProperty("renderedIds").EnumerateArray().Any(id=>id.GetString()=="current-aircraft")&&x.GetProperty("aircraft").EnumerateArray().Any(a=>
                Math.Abs(a.GetProperty("latitude").GetDouble()-latest.LatitudeDegrees)<.000001&&Math.Abs(a.GetProperty("longitude").GetDouble()-latest.LongitudeDegrees)<.000001));
            C("map",currentAck,"latest authoritative aircraft timestamp reaches browser receipt and matching sequence acknowledgment");
            C("map",correctFrame&&renderer.View.Opacity==1,"loaded MapLibre render matches current sequence, geometry, rendered aircraft and loaded solar state");
            C("map",final.GetProperty("dropped").GetInt32()==0,"browser observation remains uncensored");
            C("liveness",receipts.Length>1&&frames.Length>1&&processed.Count==emissions.Count,"input, browser and authoritative endpoints all make meaningful progress");
            C("telemetry",readyBeforeNext.Count>0&&readyBeforeNext.All(v=>v),"each independently paced sample is processed before the next one-second source emission");
            C("integrity",emissions.Keys.Order().SequenceEqual(ingestions.Keys.Order())&&emissions.Keys.Order().SequenceEqual(processed.Keys.Order()),"emission, ingestion and processing cover exactly the same samples");
            C("integrity",processingOrder.SequenceEqual(emissions.Keys.Order()),"authoritative processing preserves exact source order without duplicates");
            C("integrity",owner.ReadTimeline()?.Snapshots.Count==emissions.Count,"recorder retains every sample through navigation and reconnect");
            C("restoration",host.RestartCount==0&&host.LastFailure is null,"no unexpected restart, fallback or renderer error");
            var telemetry=emissions.Keys.Order().Select(at=>new{at,stage=sampleStages[at],emittedMs=emissions[at],ingestedMs=ingestions.GetValueOrDefault(at,double.NaN),processedMs=processed.GetValueOrDefault(at,double.NaN),delayMs=processed.GetValueOrDefault(at,double.NaN)-emissions[at]}).ToArray();
            Write(run,"direct-observations.json",new{records,telemetry,dashboardRows,acknowledgments,browser=final,latestAt,bridge=renderer.Measurements,
                boundaries="WPF to trusted browser press; CDP to pointer/camera/selection; source callback to authoritative processed hook; MapLibre loaded render/source data, not GPU pixels. No 20ms probe timer in this mode. Reduced motion isolates source-position freshness."});
            return connection;
        }
        finally
        {
            clock.Tick-=tick;NativeMapInputTrace.TelemetryObserver=previousObserver;
            Write(run,"partial-observations.json",new{records,dashboardRows,emissions,ingestions,processed,processingOrder});
            Write(run,"direct-contracts.json",checksByGroup.ToDictionary(p=>p.Key,p=>new{passed=p.Value.All(v=>v),checks=p.Value.Count,boundary=p.Key switch{
                "input"=>"WPF/browser handling and injected camera/selection response; not physical pixels",
                "map"=>"Receipt/ACK and loaded MapLibre source/render; not GPU presentation",
                "telemetry"=>"Independent emission through connection ingestion to authoritative completion",
                "dashboard"=>"Real timer callback/current WPF text and latest Operations overview",
                "restoration"=>"Retained renderer usable after navigation; supplemental full resource suite required",
                "integrity"=>"Exact emitted/ingested/processed/recorded identity and order",
                _=>"Progress of actual input, browser and authoritative work"}}));
            Write(run,"direct-trace.json",await trace.FinishAsync());
        }
    }

    private const int InputCorrectnessDeadlineMs=1500;
    private sealed record PressResult(bool Delivered,bool Trusted,bool InBounds,double ElapsedMs);
    private static bool PressAccepted(PressResult p)=>p.Delivered&&p.Trusted&&p.InBounds&&p.ElapsedMs<=InputCorrectnessDeadlineMs;
    private static bool SelectionAccepted(JsonElement selection,string expected,bool freshEvent)=>freshEvent&&selection.ValueKind==JsonValueKind.Object&&selection.GetProperty("kind").GetString()=="Aircraft"&&selection.GetProperty("id").GetString()==expected;
    private static bool DashboardDeferred(int before,int after,double pressureMs,double intervalMs)=>before==after&&pressureMs>=intervalMs;
    private static async Task<PressResult> DirectPress(WebMapRenderer renderer,bool inside,bool delayedBrowser=false)
    {
        var view=renderer.View;var old=Mouse.GetPosition(view);
        try
        {
            SyntheticMapPointer.Move(view,inside?new(view.ActualWidth/2,view.ActualHeight/2):new(view.ActualWidth+100,view.ActualHeight+100));
            view.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=UIElement.MouseMoveEvent});
            await renderer.Core.ExecuteScriptAsync("window.directPress=null;document.addEventListener('mousedown',e=>{"+
                (delayedBrowser?$"const begin=performance.now();while(performance.now()-begin<{InputCorrectnessDeadlineMs+100}){{}};":"")+
                "window.directPress={trusted:e.isTrusted,x:e.clientX,y:e.clientY}},{capture:true,once:true})");
            var watch=Stopwatch.StartNew();view.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseDownEvent});
            JsonElement result;
            do{result=JsonDocument.Parse(await renderer.Core.ExecuteScriptAsync("window.directPress")).RootElement.Clone();if(result.ValueKind==JsonValueKind.Object)break;await Task.Delay(20);}while(watch.ElapsedMilliseconds<InputCorrectnessDeadlineMs);
            bool delivered=result.ValueKind==JsonValueKind.Object;
            return new(delivered,delivered&&result.GetProperty("trusted").GetBoolean(),delivered&&result.GetProperty("x").GetDouble()>=0&&result.GetProperty("x").GetDouble()<view.ActualWidth&&result.GetProperty("y").GetDouble()>=0&&result.GetProperty("y").GetDouble()<view.ActualHeight,watch.Elapsed.TotalMilliseconds);
        }
        finally{view.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseUpEvent});SyntheticMapPointer.Move(view,old);}
    }

    private const string DirectBrowserProbe="""
    (async()=>{
      const {Map}=await import('./vendor/maplibre-gl.mjs');
      const p={receipts:[],frames:[],pointers:[],dropped:0};
      const add=(a,v)=>{if(a.length<20000)a.push(v);else p.dropped++};
      chrome.webview.addEventListener('message',e=>{if(e.data.type==='snapshot')add(p.receipts,{t:performance.now(),sequence:e.data.snapshot.sequence,observedAt:e.data.snapshot.aircraft[0]?.observedAt??null,solar:e.data.snapshot.sunVector.map(v=>v.toFixed(3)).join(',')})});
      for(const type of ['pointerdown','pointermove','pointerup'])document.addEventListener(type,e=>add(p.pointers,{t:performance.now(),type,x:e.clientX,y:e.clientY,trusted:e.isTrusted}),true);
      const fire=Map.prototype.fire;
      Map.prototype.fire=function(e,...args){const result=fire.call(this,e,...args);const type=typeof e==='string'?e:e.type;
        if(type==='render'||type==='idle'){const state=window.sierraMapDiagnostics.read();const data=this.getSource('aircraft')?._data?.geojson;
          add(p.frames,{t:performance.now(),type,loaded:this.loaded(),sequence:state.lastSequence,solar:state.solarSignature,nightLoaded:this.getSource('night')?.loaded()===true,
            renderedIds:this.queryRenderedFeatures({layers:['aircraft']}).map(f=>f.properties.id),
            aircraft:(data?.features??[]).map(f=>({id:f.properties.id,longitude:f.geometry.coordinates[0],latitude:f.geometry.coordinates[1]}))});}
        return result;};
      window.directProbe={read:()=>({...p,state:window.sierraMapDiagnostics.read()})};return true;
    })()
    """;
}
