using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop.MapV2;

// Explicit CLI diagnostics only. Synthetic input and browser automation never run
// from normal application startup. All persistent state is rooted in the run directory.
internal static partial class MapPerformanceChecks
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
    private static readonly List<object> checks = [];
    private static int failures;
    private static void Check(bool condition, string name) { checks.Add(new { passed = condition, name }); if (!condition) failures++; }
    private static void Write(string root, string name, object value) => File.WriteAllText(Path.Combine(root, name), JsonSerializer.Serialize(value, Json));
    private static async Task Until(Func<bool> test, int timeout = 15000)
    {
        var watch = Stopwatch.StartNew();
        while (!test()) { if (watch.ElapsedMilliseconds > timeout) throw new TimeoutException("Performance diagnostic condition timed out"); await Task.Delay(20); }
    }
    private static JsonElement Element(object data) => JsonSerializer.SerializeToElement(data, Json);
    private static double Number(object data, string property) => Element(data).GetProperty(property).GetDouble();
    private static readonly DateTimeOffset Start = new(2026, 9, 24, 2, 0, 0, TimeSpan.Zero);
    private static ActiveFlightPlan Plan => new("PERF724", "VH-MAP", "YSSY", "YMHB", Start.AddSeconds(30), Start.AddSeconds(3940),
        RoutePoints: [new("YSSY", -33.946, 151.177111), new("YMHB", -42.836111, 147.510278)], AircraftType: "A320");
    private static IReadOnlyList<LiveReading> Samples => MapContentionChecks.FlightSamples(Start, new(-33.946,151.177111), new(-42.836111,147.510278),1);

    internal static async Task<int> RunAsync(string directory, string mode, double minutes)
    {
        var run = Path.GetFullPath(directory); Directory.CreateDirectory(run);
        MainWindow? main = null;
        var lifecycle=new List<object>();
        try
        {
            if ((!mode.StartsWith("restoration-",StringComparison.Ordinal)&&!mode.StartsWith("interaction-",StringComparison.Ordinal)&&!mode.StartsWith("scheduling-",StringComparison.Ordinal)&&mode is not ("direct-contracts" or "profile" or "verify" or "soak" or "probe" or "contracts" or "responsiveness-full" or "responsiveness-light" or "responsiveness-passive" or "responsiveness-boundary")) || !double.IsFinite(minutes) || minutes < 0 || minutes > 90) throw new ArgumentException("Invalid performance diagnostic mode/duration");
            var source = new Source();
            using var owner = new FlightSessionCoordinator(Path.Combine(run,"session"), true, new WpfFlightRuntimeDispatcher(Application.Current.Dispatcher), source);
            main = new MainWindow(owner, Path.Combine(run,"session")) { Width = 1920, Height = 1080 };
            void ViewState(string reason)=>lifecycle.Add(new{utc=DateTimeOffset.UtcNow,reason,windowState=main.WindowState.ToString(),visible=main.IsVisible,operationsVisible=main.OperationsView.IsVisible,width=main.ActualWidth,height=main.ActualHeight});
            main.StateChanged+=(_,_)=>ViewState("window-state");main.IsVisibleChanged+=(_,_)=>ViewState("window-visibility");main.OperationsView.IsVisibleChanged+=(_,_)=>ViewState("workspace-visibility");
            main.Show(); main.ShowDashboard();
            if(mode=="scheduling-legacy"||(mode!="direct-contracts"&&mode!="soak"&&!mode.StartsWith("interaction-",StringComparison.Ordinal)&&!mode.StartsWith("responsiveness-",StringComparison.Ordinal)&&!mode.StartsWith("scheduling-",StringComparison.Ordinal)))((DispatcherTimer)FlightSessionDiagnosticAccess.Get(main,"dashboardClock")!).Stop();
            var host = main.OperationsView.MapHost; host.DiagnosticProfile = Path.Combine(run,"browser");
            using var timings = new Timings(main);
            using var resources = new Resources();
            await ProcessOwnershipContract();
            owner.AcceptAssignment(Plan);
            var connection = owner.ConnectSimulatorAsync(); await source.Ready.Task;
            if (mode=="direct-contracts") { timings.Dispose(); connection=await DirectContracts(main,owner,source,resources,run,connection); }
            else if (mode=="restoration-pump") await MapV2Checks.PumpChecks(Check);
            else if(mode=="restoration-input")
            {
                await EnsureVisibleMap(main);await Until(()=>host.V2?.View.Opacity==1);
                Check(await MapV2Checks.BrowserPressForwarding(host.V2!,run),"Contained WPF left press still reaches initialized browser");
            }
            else if (mode is "restoration-baseline" or "restoration-verify") await Restoration(main,owner,source,resources,timings,run,mode=="restoration-verify");
            else if (mode.StartsWith("interaction-",StringComparison.Ordinal)) await Interaction(main,owner,source,resources,timings,run,mode,minutes*60);
            else if (mode.StartsWith("scheduling-",StringComparison.Ordinal)&&mode!="scheduling-legacy") await Scheduling(main,owner,source,resources,timings,run,mode,minutes*60);
            else if (mode=="responsiveness-boundary") await ResponsivenessBoundaryContract(main,timings);
            else if (mode.StartsWith("responsiveness-",StringComparison.Ordinal)) await Responsiveness(main,owner,source,resources,timings,run,minutes,mode!="responsiveness-full",mode!="responsiveness-passive");
            else if (mode == "soak") await Soak(main, owner, source, resources, timings, run, minutes);
            else if(mode=="contracts")
            {
                PresentationContracts(main);
                source.Emit(Cruise(Start,0));await Until(()=>owner.Snapshot.SimulatorUtc==Start);
                await WindowRestoration(main,resources);
                await ReducedMotion(main,run);
                await InputAndScaling(main,resources,run);
                resources.Sample(host.V2,"contracts");
            }
            else if(mode=="probe")
            {
                PresentationContracts(main);
                var probes=new List<object>();
                foreach(var stage in new[]{"map-v2-divider-1","map-v2-divider-2","map-v2-divider-4"})
                    probes.Add(await Profile(main,owner,source,resources,timings,run,stage));
                Write(run,"profiles.json",probes);
            }
            else
            {
                PresentationContracts(main);
                var profiles = new List<object>();
                foreach (var stage in new[] { "runtime-only", "classic", "map-v2", "map-v2-detached-cards", "map-v2-realtime", "map-v2-realtime-light-sampler", "map-v2-realtime-native-host", "map-v2-realtime-light-sampler-repeat" })
                {
                    profiles.Add(await Profile(main, owner, source, resources, timings, run, stage,mode=="scheduling-legacy"));
                    Write(run,"profiles-progress.json",profiles);
                }
                Write(run,"profiles.json",profiles);
                await Loads(main, resources, timings, run);
                await InputAndScaling(main,resources,run);
                await WindowRestoration(main,resources);
                await ReducedMotion(main,run);
            }
            owner.DisconnectSimulator(); await connection.WaitAsync(TimeSpan.FromSeconds(5));
            resources.Sample(host.V2,"before-final-disposal");
            host.SetRenderer(false);var disposal=Stopwatch.StartNew();
            while(resources.LiveBrowserIds().Length>0&&disposal.Elapsed.TotalSeconds<10)await Task.Delay(100);
            resources.Sample(null,"disposed");
            var remaining = resources.LiveBrowserIds();
            Check(remaining.Length == 0,"No owned browser processes after final renderer disposal");
            Write(run,"resource-samples.json",resources.Rows);
            Write(run,"final-resources.json",new { remainingBrowserPids=remaining, disposalMs=disposal.Elapsed.TotalMilliseconds, hostMiB=Process.GetCurrentProcess().WorkingSet64/1048576d });
        }
        catch(Exception error) { Check(false,error.ToString()); }
        finally
        {
            Write(run,"summary.json",new { passed=failures==0, executed=checks.Count, failed=failures, mode, requestedMinutes=minutes, checks });
            Write(run,"view-lifecycle.json",lifecycle);
            main?.ExitApplication(run);
        }
        return failures==0?0:1;
    }

    private static EventHandler RuntimeHandler(MainWindow main) => (EventHandler)typeof(MainWindow).GetMethod("RuntimeChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.CreateDelegate(typeof(EventHandler),main);
    private static async Task EnsureVisibleMap(MainWindow main)
    {
        main.Show();if(main.WindowState==WindowState.Minimized)main.WindowState=WindowState.Normal;
        main.ShowDashboard();
        var host=main.OperationsView.MapHost;
        if(!host.UseV2)host.SetRenderer(true);
        await host.WaitReadyAsync();
        await Until(()=>host.V2?.Ready.IsCompletedSuccessfully==true&&host.IsVisible);
    }
    private static async Task WindowRestoration(MainWindow main,Resources resources)
    {
        await EnsureVisibleMap(main);var host=main.OperationsView.MapHost;var epoch=host.V2!.Epoch;
        resources.Sample(host.V2,"before-window-hide");
        main.Hide();await Task.Delay(200);
        Check(host.V2?.Epoch==epoch&&host.V2.Paused,"Hidden diagnostic window retains a paused renderer");
        await EnsureVisibleMap(main);
        resources.Sample(host.V2,"restored-window");
        Check(main.IsVisible&&host.V2!.Epoch==epoch&&!host.V2.Paused,"Diagnostic window restoration resumes the retained renderer");
    }
    private static async Task ReducedMotion(MainWindow main,string run)
    {
        await EnsureVisibleMap(main);var renderer=main.OperationsView.MapHost.V2!;
        await renderer.Core.ExecuteScriptAsync("(()=>{const c=document.getElementById('reduced-motion');if(!c.checked)c.click()})()");
        long sequence=ReadBrowserSequence(renderer)+1000;
        renderer.Offer(RichLoad(renderer.Epoch,++sequence,1,0));await Until(()=>ReadBrowserSequence(renderer)>=sequence);
        await Task.Delay(100);
        renderer.Offer(RichLoad(renderer.Epoch,++sequence,1,.1));await Until(()=>ReadBrowserSequence(renderer)>=sequence);
        await Task.Delay(100);var before=(await ReadBrowser(renderer)).GetProperty("sourceUpdates").GetInt32();
        await Task.Delay(600);var after=(await ReadBrowser(renderer)).GetProperty("sourceUpdates").GetInt32();
        Write(run,"reduced-motion.json",new{before,after,extraUpdates=after-before,observationMilliseconds=600});
        Check(before==after,"Reduced motion stops redundant interpolation source updates after a snapshot");
    }
    private static void PresentationContracts(MainWindow main)
    {
        var view=main.OperationsView;
        var first=OperationsOverviewQuery.Project(MapV2Checks.Fixture(),false,[],null,false,Start);
        view.Present(first);main.UpdateLayout();
        var items=view.Metrics.ItemsSource;var container=view.Metrics.ItemContainerGenerator.ContainerFromIndex(0);
        view.Present(first with{Metrics=first.Metrics.ToArray(),Airports=first.Airports.ToArray()});main.UpdateLayout();
        Check(ReferenceEquals(items,view.Metrics.ItemsSource)&&ReferenceEquals(container,view.Metrics.ItemContainerGenerator.ContainerFromIndex(0)),"Unchanged Operations metric values retain existing visual containers");
        view.Present(first with{Metrics=first.Metrics.Select((m,i)=>i==0?m with{Value="changed"}:m).ToArray()});
        Check(((OperationsMetric)view.Metrics.Items[0]).Value=="changed","Changed Operations metric values still update");
    }
    private static async Task<object> Profile(MainWindow main, FlightSessionCoordinator owner, Source source, Resources resources, Timings timings, string run, string stage,bool traceScheduling=false)
    {
        var host=main.OperationsView.MapHost;
        // Diagnostic ablation only; native WebView airspace is not a production replacement.
        host.CompositionHost=!stage.EndsWith("native-host",StringComparison.Ordinal);
        host.SetRenderer(stage.StartsWith("map-v2",StringComparison.Ordinal));
        if(host.UseV2) await host.WaitReadyAsync();
        if(stage.Contains("-divider-",StringComparison.Ordinal))
        {
            var property=host.V2!.View.GetType().GetProperty("FpsDivider");
            Write(run,"composition-properties.json",new{fpsDividerSupported=property is not null,properties=host.V2.View.GetType().GetProperties().Select(p=>p.Name).ToArray()});
            if(property is null&&stage!="map-v2-divider-1")return new{stage,unavailable="Installed stable SDK does not expose documented FpsDivider"};
            property?.SetValue(host.V2.View,int.Parse(stage[^1..]));
        }
        bool detached=stage is "runtime-only" or "map-v2-detached-cards";
        var handler=RuntimeHandler(main); if(detached)owner.SnapshotChanged-=handler;
        EventHandler? mapOnly=null;
        if(stage=="map-v2-detached-cards") { mapOnly=(_,_)=>main.OperationsView.MapHost.Present(OperationsOverviewQuery.Project(owner.Snapshot,false,[],null,false,DateTimeOffset.UtcNow)); owner.SnapshotChanged+=mapOnly; }
        var before=owner.ReadTimeline()?.Snapshots.Count??0;
        bool visibleThroughout=true;
        timings.Reset(); resources.ResetCpu(); var watch=Stopwatch.StartNew();
        if(traceScheduling)resources.Sample(host.V2,"trace-profile-initial");
        using var trace=traceScheduling?new DispatcherSchedulingTrace(main,(DispatcherTimer)FlightSessionDiagnosticAccess.Get(main,"dashboardClock")!,resources.LiveBrowserIds()):null;
        if(trace is not null){timings.OnHeartbeat=ms=>trace.Mark("heartbeat",value:ms);trace.Mark("measurement-begin",stage);}
        try
        {
            for(int i=0;i<160;i++)
            {
                var at=Start.AddSeconds((before+i)*2);
                source.Emit(Cruise(at,(before+i)*.00001));
                await Until(()=>owner.Snapshot.SimulatorUtc==at,3000);
                visibleThroughout&=main.IsVisible&&host.IsVisible&&host.UseV2==stage.StartsWith("map-v2",StringComparison.Ordinal)&&(!host.UseV2||host.V2?.Ready.IsCompletedSuccessfully==true);
                if(!stage.Contains("light-sampler",StringComparison.Ordinal)&&!stage.EndsWith("native-host",StringComparison.Ordinal)||i%5==0)resources.Sample(host.V2,stage);
                await Task.Delay(stage.StartsWith("map-v2-realtime",StringComparison.Ordinal)?1000:40);
            }
            await Task.Delay(300);
            var result=new{stage,seconds=watch.Elapsed.TotalSeconds,authoritativeSamples=160,snapshotRate=160/watch.Elapsed.TotalSeconds,
                renderer=host.V2?.Measurements,timing=timings.Read(),resources=resources.Summary(),rendererStartupMs=host.V2?.Metrics.ReadyMs,
                view=new{visibleThroughout,windowState=main.WindowState.ToString(),width=main.ActualWidth,height=main.ActualHeight,host.CompositionHost}};
            Check((owner.ReadTimeline()?.Snapshots.Count??0)-before==160,stage+": every input sample retained");
            Check(visibleThroughout,stage+": expected workspace remains visible throughout measurement");
            return result;
        }
        finally
        {
            if(trace is not null)
            {
                trace.Mark("measurement-end",stage);trace.Mark("pending-heartbeat",value:timings.PendingHeartbeatMs);
                trace.Mark("drain-request");await main.Dispatcher.InvokeAsync(()=>trace.Mark("drain-executed"),DispatcherPriority.Input);trace.Mark("drain-resumed");
                timings.OnHeartbeat=null;var evidence=await trace.FinishAsync();
                Write(run,"scheduling-trace-"+stage+".json",new{conditions=new{stage,traceRevision=2,compositionHost=host.CompositionHost,renderObserver=false,feed="original UI-paced sequence",dashboardClock=false,traceDrops=trace.Dropped},trace=evidence});
                Check(trace.Dropped==0,stage+": scheduling trace retains all events");
            }
            host.CompositionHost=true;if(detached)owner.SnapshotChanged+=handler; if(mapOnly is not null)owner.SnapshotChanged-=mapOnly;
        }
    }

    private static LiveReading Cruise(DateTimeOffset at,double shift)
    {
        var s=new Telemetry(at,false,430,false,true,LatitudeDegrees:-35.6-shift,LongitudeDegrees:150.1,AltitudeFeet:34000,HeadingDegrees:195,
            IndicatedAirspeedKnots:270,VerticalSpeedFeetPerMinute:0,FuelTotalWeightPounds:10000,RunningEngineCount:2,RunningEngineMask:3,PressureAltitudeFeet:34000);
        return new("Airbus A320 diagnostic",s,new(new(s.LatitudeDegrees,s.LongitudeDegrees),"VH-MAP",new("YSSY","YMHB"),true),RouteProgress:.3);
    }

    private static async Task Loads(MainWindow main,Resources resources,Timings timings,string run)
    {
        var owner=FlightSessionDiagnosticAccess.Owner(main);var handler=RuntimeHandler(main);owner.SnapshotChanged-=handler;
        var results=new List<object>(); var host=main.OperationsView.MapHost;
        try
        {
            foreach(var count in new[]{1,25,100,500,1000,2000})
            {
                host.SetRenderer(false);host.SetRenderer(true);await host.WaitReadyAsync();var renderer=host.V2!;
                timings.Reset();resources.ResetCpu();var timer=Stopwatch.StartNew();long sequence=100000;int offers=0;
                while(timer.Elapsed<TimeSpan.FromSeconds(8))
                {
                    renderer.Offer(RichLoad(renderer.Epoch,++sequence,count,timer.Elapsed.TotalSeconds));offers++;
                    resources.Sample(renderer,"load-"+count);await Task.Delay(200);
                }
                await Until(()=>ReadBrowserSequence(renderer)>=sequence);
                var browser=await ReadBrowser(renderer);
                Check(browser.GetProperty("counts").GetProperty("aircraft").GetInt32()==count,$"Actual Operations bridge consumes {count} test-only aircraft");
                Check(browser.GetProperty("counts").GetProperty("airports").GetInt32()>0,$"{count}-aircraft load includes airport markers and labels");
                results.Add(new{count,seconds=timer.Elapsed.TotalSeconds,offers,bridge=renderer.Measurements,timing=timings.Read(),resources=resources.Summary(),browser});
                Write(run,"load-progress.json",results);
            }
            host.SetRenderer(false);host.SetRenderer(true);await host.WaitReadyAsync();var constrained=host.V2!;
            await constrained.Core.CallDevToolsProtocolMethodAsync("Emulation.setCPUThrottlingRate","{\"rate\":4}");
            timings.Reset();resources.ResetCpu();var clock=Stopwatch.StartNew();long seq=200000;
            for(int i=0;i<40;i++){constrained.Offer(RichLoad(constrained.Epoch,++seq,100,i*.2));resources.Sample(constrained,"cpu-throttle-4x");await Task.Delay(200);}
            await Until(()=>ReadBrowserSequence(constrained)>=seq);
            Write(run,"constrained.json",new{method="Chromium DevTools CPU throttling rate 4; not representative hardware certification",seconds=clock.Elapsed.TotalSeconds,bridge=constrained.Measurements,timing=timings.Read(),resources=resources.Summary(),browser=await ReadBrowser(constrained)});
            await constrained.Core.CallDevToolsProtocolMethodAsync("Emulation.setCPUThrottlingRate","{\"rate\":1}");
            Check(ReadBrowserSequence(constrained)>=seq,"Constrained browser completes latest visual snapshot");
            Write(run,"loads.json",results);
        }
        finally {owner.SnapshotChanged+=handler;}
    }
    private static long ReadBrowserSequence(WebMapRenderer renderer)=>(long)typeof(WebMapRenderer).GetField("lastSequence",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(renderer)!;
    private static async Task<JsonElement> ReadBrowser(WebMapRenderer renderer)=>JsonDocument.Parse(await renderer.Core.ExecuteScriptAsync("window.sierraMapDiagnostics.read()")).RootElement.Clone();
    private static MapSnapshot RichLoad(long epoch,long sequence,int count,double elapsed)
    {
        var model=MapV2Checks.Load(epoch,sequence,count,elapsed);
        var airports=model.Routes.Where(r=>r.Kind==MapRouteKind.Planned).SelectMany((r,i)=>new[]{
            new MapAirport("origin-"+i,"DEP"+i,null,r.Segments[0][0],"Diagnostic departure "+i,MapSeverity.Unknown,null,null,null,null,"origin"),
            new MapAirport("arrival-"+i,"ARR"+i,null,r.Segments[^1][^1],"Diagnostic arrival "+i,MapSeverity.Unknown,null,null,null,null,"destination")}).ToImmutableArray();
        return model with{Airports=airports};
    }

    private static async Task InputAndScaling(MainWindow main,Resources resources,string run)
    {
        // Initial profile startup honors the production maximized preference. Scaling
        // captures explicitly use a normal window so requested dimensions take effect.
        main.WindowState=WindowState.Normal;main.Width=1920;main.Height=1080;
        var host=main.OperationsView.MapHost;host.SetRenderer(false);host.SetRenderer(true);await host.WaitReadyAsync();
        resources.Sample(host.V2,"input-renderer");
        main.OperationsView.Present(OperationsOverviewQuery.Project(FlightSessionDiagnosticAccess.Owner(main).Snapshot,false,[],null,false,DateTimeOffset.UtcNow));
        await Task.Delay(1000);var renderer=host.V2!;
        var actions=new List<object>();
        foreach(var id in new[]{"zoom-in","zoom-out","follow","follow","home","focus","layers","layers"})
        {
            var command=Stopwatch.StartNew();
            await renderer.Core.ExecuteScriptAsync($"document.getElementById('{id}').click()");
            var commandRoundTripMs=command.Elapsed.TotalMilliseconds;
            await Task.Delay(400);
            actions.Add(new{id,commandRoundTripMs,settlingDelayMs=400,state=await ReadBrowser(renderer)});
        }
        await renderer.Core.ExecuteScriptAsync("(()=>{const c=document.getElementById('reduced-motion');if(!c.checked)c.click()})()");
        var reduced=JsonDocument.Parse(await renderer.Core.ExecuteScriptAsync("document.getElementById('reduced-motion').checked")).RootElement.GetBoolean();
        Check(reduced,"Reduced-motion preference accepts automated activation");
        await renderer.Core.ExecuteScriptAsync("document.getElementById('focus').click()");await Task.Delay(700);
        foreach(var kind in new[]{"Airport","Aircraft","Route"})
        {
            // Focus centers the aircraft, which can place a long route midpoint
            // outside this short map viewport. Zoom out before route hit testing.
            if(kind=="Route"){await renderer.Core.ExecuteScriptAsync("document.getElementById('zoom-out').click()");await Task.Delay(500);}
            var points=(await ReadBrowser(renderer)).GetProperty("points").EnumerateArray().Where(p=>p.GetProperty("kind").GetString()==kind).ToArray();
            bool selected=false;
            foreach(var point in points)
            {
                await Pointer(renderer,"mousePressed",point.GetProperty("x").GetDouble(),point.GetProperty("y").GetDouble());
                await Pointer(renderer,"mouseReleased",point.GetProperty("x").GetDouble(),point.GetProperty("y").GetDouble());await Task.Delay(150);
                var value=(await ReadBrowser(renderer)).GetProperty("selection");
                if(value.ValueKind==JsonValueKind.Object&&value.GetProperty("kind").GetString()==kind){selected=true;break;}
            }
            Check(selected,kind+" selected by injected pointer hit testing");
        }
        await renderer.Core.ExecuteScriptAsync("document.getElementById('follow').click()");await Task.Delay(300);
        Check((await ReadBrowser(renderer)).GetProperty("camera").GetProperty("followId").ValueKind==JsonValueKind.String,"Follow acquires current aircraft");
        await renderer.Core.ExecuteScriptAsync("document.getElementById('follow').click()");await Task.Delay(200);
        Check((await ReadBrowser(renderer)).GetProperty("camera").GetProperty("followId").ValueKind==JsonValueKind.Null,"Unfollow releases aircraft");
        var beforePan=(await ReadBrowser(renderer)).GetProperty("camera").GetProperty("longitude").GetDouble();
        // CDP input is injected browser input, never evidence of physical pointer/touch delivery.
        await renderer.Core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent","{\"type\":\"mouseMoved\",\"x\":350,\"y\":250}");
        await renderer.Core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent","{\"type\":\"mousePressed\",\"x\":350,\"y\":250,\"button\":\"left\",\"clickCount\":1}");
        await renderer.Core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent","{\"type\":\"mouseMoved\",\"x\":450,\"y\":290,\"button\":\"left\",\"buttons\":1}");
        await renderer.Core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent","{\"type\":\"mouseReleased\",\"x\":450,\"y\":290,\"button\":\"left\",\"clickCount\":1}");
        await Task.Delay(250);
        Check(beforePan!=(await ReadBrowser(renderer)).GetProperty("camera").GetProperty("longitude").GetDouble(),"Injected pointer drag pans the map");
        var beforeZoom=(await ReadBrowser(renderer)).GetProperty("camera").GetProperty("zoom").GetDouble();
        await renderer.Core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent","{\"type\":\"mouseWheel\",\"x\":400,\"y\":260,\"deltaX\":0,\"deltaY\":-120}");
        await Task.Delay(400);
        Check((await ReadBrowser(renderer)).GetProperty("camera").GetProperty("zoom").GetDouble()>beforeZoom,"Injected wheel changes map zoom");
        await renderer.Core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyDown\",\"key\":\"Tab\",\"windowsVirtualKeyCode\":9}");
        await renderer.Core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyUp\",\"key\":\"Tab\",\"windowsVirtualKeyCode\":9}");
        await renderer.Core.ExecuteScriptAsync("document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}))");
        Write(run,"input.json",new{actions,afterInjectedInput=await ReadBrowser(renderer),limits="DOM activation and injected CDP pointer/key events; physical hit testing/touch/focus across real monitors require 7.24B"});
        var scaling=new List<object>();
        foreach(var scale in new[]{1d,1.25,1.5,1.75,2})
        {
            main.OperationsView.LayoutTransform=new ScaleTransform(scale,scale);main.UpdateLayout();host.BringIntoView();await Task.Delay(600);
            DashboardSmokeTest.Capture(main,Path.Combine(run,$"scale-{scale*100:0}.png"));
            scaling.Add(new{scale,windowWidth=main.ActualWidth,windowHeight=main.ActualHeight,mapWidth=host.ActualWidth,mapHeight=host.ActualHeight,dpi=VisualTreeHelper.GetDpi(main),method="Controlled Operations LayoutTransform; not physical monitor DPI transition"});
            Check(host.ActualWidth>250&&host.ActualHeight>220,$"Map remains sized at {scale*100:0}% controlled WPF scaling");
        }
        main.OperationsView.LayoutTransform=Transform.Identity;main.OperationsView.Scroller.ScrollToTop();
        Write(run,"scaling.json",scaling);
    }
    private static Task<string> Pointer(WebMapRenderer renderer,string type,double x,double y)=>renderer.Core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type,x,y,button="left",clickCount=1}));

    private static async Task ProcessOwnershipContract()
    {
        using var child=Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"cmd.exe"),"/d /c pause")
        {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true})
            ??throw new InvalidOperationException("Could not start process ownership fixture");
        using var resources=new Resources();
        try
        {
            resources.Track(child.Id);
            Check(resources.LiveBrowserIds().SequenceEqual(new[]{child.Id}),"Owned original process handle reports live child");
            await child.StandardInput.WriteLineAsync();await child.StandardInput.FlushAsync();
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Check(resources.LiveBrowserIds().Length==0,"Original process handle reports termination after child exit");
            Check(Enumerable.Range(0,10).All(_=>resources.LiveBrowserIds().Length==0),"Repeated exit checks retain original identity without reopening stale process IDs");
        }
        finally{if(!child.HasExited)child.Kill();}
    }

    private static async Task Soak(MainWindow main,FlightSessionCoordinator owner,Source source,Resources resources,Timings timings,string run,double minutes)
    {
        var host=main.OperationsView.MapHost;host.SetRenderer(true);await host.WaitReadyAsync();
        resources.Sample(host.V2,"soak-start");
        var watch=Stopwatch.StartNew();var records=new List<object>();
        var samples=minutes>=10?Samples:MapContentionChecks.FlightSamples(Start,new(-33.946,151.177111),new(-42.836111,147.510278));
        int count=0,cycle=-1,hiddenOffers=0,bucket=0,resourceStep=-1;long firstGeneration=owner.Snapshot.ConnectionGeneration;
        int stalls=0,lockedSaves=0;int lockInterval=minutes>=10?500:100;Task<string>? stall=null;var epochs=new HashSet<long>();
        var perSample=TimeSpan.FromSeconds(Math.Max(.05,minutes*60/samples.Count));
        var received=new Dictionary<DateTimeOffset,double>();var emissions=new List<object>();var lockResults=new List<bool>();
        void Observed(object? sender,EventArgs args){if(owner.Snapshot.Telemetry is {} s&&received.TryAdd(s.At,watch.Elapsed.TotalMilliseconds))count++;}
        owner.SnapshotChanged+=Observed;
        using var production=new CancellationTokenSource();
        var producer=Task.Run(async()=>
        {
            for(int index=0;index<samples.Count;index++)
            {
                var due=index*perSample.TotalMilliseconds;
                var delay=due-watch.Elapsed.TotalMilliseconds;if(delay>0)await Task.Delay(TimeSpan.FromMilliseconds(delay),production.Token);
                production.Token.ThrowIfCancellationRequested();
                FileStream? held=null;TaskCompletionSource? retried=null;
                if(index>0&&index%lockInterval==0)
                {
                    var path=Path.Combine(run,"session","active-flight-state.json");
                    held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
                    using(var old=JsonDocument.Parse(held))lockResults.Add(old.RootElement.GetProperty("LastSimulatorUtc").GetDateTimeOffset()<samples[index].Telemetry.At);
                    retried=new(TaskCreationOptions.RunContinuationsAsynchronously);
                    var signal=retried;var locked=held;
                    timings.OnRetry=a=>{if((string?)a.GetTagItem("path")==path&&(string?)a.GetTagItem("stage")=="replace"){locked.Dispose();signal.TrySetResult();}};lockedSaves++;
                }
                try
                {
                    emissions.Add(new{index,at=samples[index].Telemetry.At,dueMs=due,emittedMs=watch.Elapsed.TotalMilliseconds});
                    source.Emit(samples[index]);
                    if(retried is not null){await retried.Task.WaitAsync(TimeSpan.FromSeconds(3),production.Token);lockResults.Add(true);}
                }
                finally{timings.OnRetry=null;held?.Dispose();}
            }
        },production.Token);
        timings.Reset();resources.ResetCpu();
        try
        {
        while(watch.Elapsed<TimeSpan.FromMinutes(minutes)||count<samples.Count)
        {
            if(producer.IsFaulted)await producer;
            if(watch.Elapsed>TotalDuration(minutes)+TimeSpan.FromSeconds(30))throw new TimeoutException("Independent soak input did not drain");
            var halfMinute=(int)(watch.Elapsed.TotalSeconds/30);
            if(halfMinute!=bucket)
            {
                records.Add(new{seconds=watch.Elapsed.TotalSeconds,count,interval=bucket%2==0?"transition":"steady",hidden=!host.IsVisible,recreations=host.RecreationCount,bridge=host.V2?.Measurements,timing=timings.Read(),resources=resources.Summary()});
                bucket=halfMinute;timings.Reset();resources.ResetCpu();
                Write(run,"soak-progress.json",new{seconds=watch.Elapsed.TotalSeconds,count,records});Write(run,"resource-samples.json",resources.Rows);
            }
            var step=(int)(watch.Elapsed.TotalSeconds/60);
            if(step!=cycle)
            {
                cycle=step;bool hidden=cycle%5==1;
                if(hidden){main.ShowFlightTracking();Check(host.V2?.Paused==true,"Soak navigation pauses retained map epoch "+cycle);}
                else
                {
                    // Return resumes the retained map; explicit renderer switches
                    // still exercise teardown and creation independently.
                    if(cycle%5==0)host.SetRenderer(true);
                    await EnsureVisibleMap(main);
                    var renderer=host.V2??throw new InvalidOperationException("Visible map did not retain its ready renderer");
                    epochs.Add(renderer.Epoch);
                    await renderer.Core.ExecuteScriptAsync($"document.getElementById('{(cycle%2==0?"follow":"home")}').click();document.getElementById('zoom-in').click();document.getElementById('zoom-out').click();document.getElementById('night').click()");
                    // Read-only presentation selection never changes the coordinator.
                    var state=await ReadBrowser(renderer);var target=state.GetProperty("sourceIDs").EnumerateArray().FirstOrDefault();
                    var selection=cycle%2==0?new MapSelection(MapEntityKind.Airport,cycle%4==0?"YSSY":"YMHB"):
                        target.ValueKind==JsonValueKind.Object?new MapSelection(MapEntityKind.Aircraft,target.GetProperty("id").GetString()!):null;
                    renderer.Present(new(new(new(150,-38),4,0,0,null),selection,Enum.GetValues<MapLayer>().ToImmutableHashSet(),false,DateTimeOffset.UtcNow));
                    if(cycle%5==3)
                    {
                        stall=renderer.Core.ExecuteScriptAsync("(()=>{const s=performance.now();while(performance.now()-s<1500){};return performance.now()-s})()");stalls++;
                    }
                    else await renderer.Core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent","{\"type\":\"mouseWheel\",\"x\":300,\"y\":250,\"deltaX\":0,\"deltaY\":120}");
                    if(cycle%5!=3)
                    {
                        await Pointer(renderer,"mousePressed",320,240);
                        await renderer.Core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent","{\"type\":\"mouseMoved\",\"x\":370,\"y\":260,\"button\":\"left\",\"buttons\":1}");
                        await Pointer(renderer,"mouseReleased",370,260);
                    }
                    if(cycle%10==4){main.WindowState=WindowState.Minimized;await Task.Delay(200);Check(ReferenceEquals(host.V2,renderer)&&renderer.Paused,"Soak minimize pauses the same map "+cycle);main.RestoreWindow();await host.WaitReadyAsync();}
                    main.Width=cycle%2==0?1920:1600;main.Height=cycle%2==0?1080:900;
                }
                resources.Sample(host.V2,hidden?"hidden":"visible");
            }
            if(stall?.IsCompleted==true){Check(double.Parse(await stall,System.Globalization.CultureInfo.InvariantCulture)>=1450,"Soak real JavaScript stall completed "+stalls);stall=null;}
            if((int)(watch.Elapsed.TotalSeconds/5)!=resourceStep){resourceStep=(int)(watch.Elapsed.TotalSeconds/5);resources.Sample(host.V2,host.IsVisible?"visible":"hidden");}
            if(!host.IsVisible&&host.V2 is not null)hiddenOffers++;
            await Task.Delay(100);
        }
        await producer;
        }
        finally{production.Cancel();owner.SnapshotChanged-=Observed;try{await producer;}catch(OperationCanceledException){} }
        var recovered=FlightRecoveryStore.Load(Plan,Path.Combine(run,"session"));
        var snapshot=owner.Snapshot;var timeline=owner.ReadTimeline();
        Check(count==samples.Count&&timeline?.Snapshots.Count==samples.Count,"Soak retains every authoritative telemetry sample");
        Check(recovered?.LastSimulatorUtc==samples[^1].Telemetry.At,"Soak final recovery contains final input");
        Check(snapshot.Phase==FlightPhase.Complete&&snapshot.PhaseEvents.Length==4&&snapshot.PirepEligible,"Soak reaches OUT/OFF/ON/IN and PIREP eligibility");
        Check(snapshot.ConnectionGeneration==firstGeneration&&source.Starts==1,"Soak lifecycle never reconnects simulator source");
        Check(hiddenOffers==0,"Soak hidden workspace holds no live renderer");
        Check(host.LastFailure is null&&host.UseV2,"Soak has no unexpected map restart or fallback");
        Check(lockedSaves>0&&lockResults.Count==lockedSaves*2&&lockResults.All(v=>v)&&lockedSaves==(samples.Count-1)/lockInterval,"Soak preserves prior recovery and observes actual transient retries under map load");
        Check(owner.SubmitPirep(),"Soak PIREP submission succeeds");
        using(var history=new FlightHistoryDatabase(Path.Combine(run,"session")))Check(history.ReadRecentFlights().Single().FinalPhase=="Complete"&&history.ReadFlightEvents(history.ReadRecentFlights().Single().Id).Count(e=>e.Kind=="pirep_submitted")==1,"Soak PIREP durable after database reopening");
        Write(run,"soak-feed.json",new{targetHz=1/perSample.TotalSeconds,emissions,consumed=received.Select(p=>new{at=p.Key,processedMs=p.Value})});
        Write(run,"soak.json",new{seconds=watch.Elapsed.TotalSeconds,count,expected=samples.Count,sourceStarts=source.Starts,hiddenRendererObservations=hiddenOffers,stalls,lockedSaves,distinctEpochs=epochs.Count,lastFailure=host.LastFailure,records,finalSnapshot=snapshot,timeline});
    }
    private static TimeSpan TotalDuration(double minutes)=>TimeSpan.FromMinutes(minutes);

    private sealed class Source:ISimulatorConnectionSource
    {
        private Action<LiveReading>? received;
        internal TaskCompletionSource Ready {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Starts;
        public bool IsRunning()=>true;
        public void Launch()=>throw new InvalidOperationException("Diagnostics must never launch MSFS");
        public async Task RunAsync(Action<string> opened,Action<LiveReading> reading,CancellationToken token)
        {Starts++;received=reading;opened("Diagnostic simulator source");Ready.TrySetResult();await Task.Delay(Timeout.Infinite,token);}
        internal void Emit(LiveReading reading)=>(received??throw new InvalidOperationException("Source not ready"))(reading);
    }

    private sealed class Timings:IDisposable
    {
        private readonly MainWindow window;private readonly ActivityListener listener;private readonly DispatcherTimer timer;
        private readonly Stopwatch beat=Stopwatch.StartNew();private MapMeasurement heartbeat=new(),save=new(),presentation=new();private long layouts;
        private Action<Activity>? onRetry;
        internal Action<double>? OnHeartbeat;
        internal Action<Activity>? OnRetry{get=>Volatile.Read(ref onRetry);set=>Volatile.Write(ref onRetry,value);}
        internal Timings(MainWindow window)
        {
            this.window=window;window.LayoutUpdated+=Layout;
            listener=new(){ShouldListenTo=s=>s.Name is "Alpha6Ops.Recovery" or "Alpha6Ops.Presentation",Sample=(ref ActivityCreationOptions<ActivityContext> _)=>ActivitySamplingResult.AllData,
                ActivityStarted=a=>{if(a.OperationName=="Retry")OnRetry?.Invoke(a);},
                ActivityStopped=a=>{if(a.OperationName=="Save")save.Add(a.Duration.TotalMilliseconds);else if(a.Source.Name=="Alpha6Ops.Presentation")presentation.Add(a.Duration.TotalMilliseconds);}};
            ActivitySource.AddActivityListener(listener);
            timer=new(DispatcherPriority.Input,window.Dispatcher){Interval=TimeSpan.FromMilliseconds(20)};timer.Tick+=Tick;timer.Start();
        }
        private void Layout(object? sender,EventArgs e)=>layouts++;
        private void Tick(object? sender,EventArgs e){double elapsed=beat.Elapsed.TotalMilliseconds;heartbeat.Add(elapsed);beat.Restart();OnHeartbeat?.Invoke(elapsed);}
        internal void Reset(){heartbeat=new();save=new();presentation=new();layouts=0;beat.Restart();}
        internal double PendingHeartbeatMs=>beat.Elapsed.TotalMilliseconds;
        internal object Read()=>new{heartbeatMs=heartbeat.Read(),pendingHeartbeatMs=PendingHeartbeatMs,recoverySaveMs=save.Read(),presentationMs=presentation.Read(),layoutEvents=layouts};
        public void Dispose(){timer.Stop();timer.Tick-=Tick;listener.Dispose();window.LayoutUpdated-=Layout;}
    }

    private sealed class Resources:IDisposable
    {
        // Keep the original synchronization handle through disposal. Reopening historical
        // PIDs can inspect an unrelated process or fail with AccessDenied after PID reuse.
        // These handles exist only in CLI diagnostics, and are closed at the end of the run.
        private readonly Dictionary<int,SafeProcessHandle> browsers=[];private readonly Dictionary<int,double> first=new(),last=new();private Stopwatch cpu=Stopwatch.StartNew();private MapMeasurement sampling=new();
        private int exitedBeforeTracking,samplingErrors;
        [DllImport("kernel32.dll",SetLastError=true)]
        private static extern SafeProcessHandle OpenProcess(uint access,bool inherit,int processId);
        [DllImport("kernel32.dll",SetLastError=true)]
        private static extern uint WaitForSingleObject(SafeProcessHandle handle,uint milliseconds);
        internal void Track(int pid)
        {
            if(browsers.ContainsKey(pid))return;
            var handle=OpenProcess(0x00100000,false,pid); // SYNCHRONIZE only; no mutation rights.
            if(handle.IsInvalid)
            {
                int error=Marshal.GetLastWin32Error();handle.Dispose();
                if(error==87){exitedBeforeTracking++;return;} // No process with that ID remains.
                throw new System.ComponentModel.Win32Exception(error,$"Cannot track owned process {pid}");
            }
            browsers.Add(pid,handle);
        }
        private static bool IsLive(SafeProcessHandle handle)=>WaitForSingleObject(handle,0) switch
        {
            0=>false,258=>true,
            _=>throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Cannot query original owned process termination")
        };
        internal List<object> Rows {get;}=[];
        internal void ResetCpu(){first.Clear();last.Clear();cpu.Restart();sampling=new();}
        internal void Sample(WebMapRenderer? renderer,string stage)
        {
            var sampleTime=Stopwatch.StartNew();
            foreach(var info in renderer?.Environment?.GetProcessInfos()??[])
                Track(info.ProcessId);
            long browserBytes=0,hostBytes=0;int processCount=0;
            foreach(var pid in LiveBrowserIds().Append(Environment.ProcessId))
                try
                {
                    using var p=Process.GetProcessById(pid);if(p.HasExited)continue;
                    if(pid==Environment.ProcessId)hostBytes=p.WorkingSet64;else{browserBytes+=p.WorkingSet64;processCount++;}
                    double value=p.TotalProcessorTime.TotalMilliseconds;if(!first.ContainsKey(pid))first[pid]=value;last[pid]=value;
                }
                catch(ArgumentException){samplingErrors++;}catch(InvalidOperationException){samplingErrors++;}catch(System.ComponentModel.Win32Exception){samplingErrors++;}
            Rows.Add(new{utc=DateTimeOffset.UtcNow,stage,browserProcesses=processCount,hostMiB=hostBytes/1048576d,browserMiB=browserBytes/1048576d,totalMiB=(hostBytes+browserBytes)/1048576d,cpuPercentMachine=Cpu()});
            sampling.Add(sampleTime.Elapsed.TotalMilliseconds);
        }
        private double Cpu()=>last.Sum(p=>p.Value-first[p.Key])/Math.Max(1,cpu.Elapsed.TotalMilliseconds)/Environment.ProcessorCount*100;
        internal object Summary()=>new{cpuPercentMachine=Cpu(),samplingMs=sampling.Read(),last=Rows.LastOrDefault(),logicalProcessors=Environment.ProcessorCount,workingSetsMayDoubleCountSharedPages=true,trackedProcessHandles=browsers.Count,exitedBeforeTracking,samplingErrors};
        internal int[] LiveBrowserIds()=>browsers.Where(pair=>IsLive(pair.Value)).Select(pair=>pair.Key).ToArray();
        public void Dispose(){foreach(var handle in browsers.Values)handle.Dispose();browsers.Clear();}
    }
}
