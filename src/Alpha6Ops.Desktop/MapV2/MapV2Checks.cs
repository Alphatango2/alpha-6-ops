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
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop.MapV2;

// Explicit diagnostic entry point only. Never supplies normal Operations data.
internal static partial class MapV2Checks
{
    private static readonly List<object> checks=[];
    private static int failures;
    private static readonly DateTimeOffset At=new(2026,9,24,2,30,0,TimeSpan.Zero);
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){WriteIndented=true};
    private static void Check(bool passed,string name)
    {
        checks.Add(new{passed,name});if(!passed)failures++;
    }
    internal static async Task<int> RunAsync(string directory,bool contentionOnly=false,bool deliveryOnly=false)
    {
        Directory.CreateDirectory(directory);var windows=new List<Window>();
        try
        {
            MapModelChecks.Run(Check);
            await PumpChecks(Check);
            await RoutedInputChecks(directory);
            Check(WebMapRenderer.TrustedOrigin(WebMapRenderer.Origin+"/index.html")&&!WebMapRenderer.TrustedOrigin("https://sierra-map.invalid.evil/")&&!WebMapRenderer.TrustedOrigin("http://sierra-map.invalid/")&&!WebMapRenderer.TrustedOrigin("https://sierra-map.invalid:8443/"),"Bridge origin rejects lookalikes, insecure and unexpected ports");
            var main=new MainWindow(Path.Combine(directory,"session"));windows.Add(main);
            main.Width=1920;main.Height=1080;main.Show();main.ShowDashboard();
            // Freeze only the diagnostic presentation clock so it cannot overwrite our explicitly labeled visual fixture.
            ((DispatcherTimer)FlightSessionDiagnosticAccess.Get(main,"dashboardClock")!).Stop();
            if(deliveryOnly)
            {
                await DeliveryUnderRenderPressure(directory);
                return failures==0?0:1;
            }
            if(contentionOnly)
            {
                if(Environment.GetEnvironmentVariable("S4_HEARTBEAT_CONTEXTS")=="1")
                    await WithRenderer(directory,"heartbeat-context",r=>HeartbeatContextChecks.RunAsync(main,r,directory));
                await DeliveryUnderRenderPressure(directory);
                await WithRenderer(directory,"contention",r=>MapContentionChecks.RunAsync(main,r,directory,Check));
                return failures==0?0:1;
            }
            await FirstPaintChecks(directory);
            await RestoredPaintChecks(directory);
            await FirstPaintLifecycleChecks(main,directory);
            var host=main.OperationsView.MapHost;host.DiagnosticProfile=Path.Combine(directory,"browser");
            var state=Fixture();
            void Present()=>main.OperationsView.Present(OperationsOverviewQuery.Project(state,false,[],null,false,DateTimeOffset.UtcNow));
            Present();host.CompositionHost=false;host.SetRenderer(true);await host.WaitReadyAsync();
            await Until(()=>host.V2?.Metrics.ReadyMs>0);
            var standard=host.V2!;
            await Task.Delay(900);
            DashboardSmokeTest.Capture(main,Path.Combine(directory,"standard-host.png"));
            Write(directory,"standard-host.json",new{readyMs=standard.Metrics.ReadyMs,isHwndHost=standard.View is System.Windows.Interop.HwndHost,airspace="Native HWND cannot participate in WPF sibling clipping/overlay composition"});
            Check(standard.View is System.Windows.Interop.HwndHost,"Standard host evaluated in actual Operations shell");
            host.SetRenderer(false);host.CompositionHost=true;host.SetRenderer(true);Present();await host.WaitReadyAsync();
            var renderer=host.V2??throw new InvalidOperationException("Composition map unavailable");
            await Until(()=>Convert.ToInt64(Read(renderer.Measurements,"acknowledged"))>0);
            Check(renderer.View is not System.Windows.Interop.HwndHost,"Composition host participates in WPF visual tree");
            await Task.Delay(800);
            await Visuals(main,state,directory);
            await host.WaitReadyAsync();renderer=host.V2!;
            await BridgeChecks(renderer,directory);
            await BrowserPressForwarding(renderer,directory);
            await Lifecycle(main,state,directory);
            await FailureCases(main,directory);
            host.SetRenderer(false);
            await Benchmark(directory);
            await DeliveryUnderRenderPressure(directory);
            await WithRenderer(directory,"contention",async r=>await MapContentionChecks.RunAsync(main,r,directory,Check));
        }
        catch(Exception error){Check(false,error.ToString());}
        finally
        {
            foreach(var window in windows)
            {
                if(window is MainWindow main){var h=main.OperationsView.MapHost;Write(directory,"host-lifetime.json",new{h.UseV2,h.IsLoaded,h.IsVisible,h.RecreationCount,h.RestartCount,h.LastFailure});h.SetRenderer(false);main.ExitApplication(directory);}
                else window.Close();
            }
            Write(directory,"summary.json",new{passed=failures==0,executed=checks.Count,failed=failures,checks});
        }
        return failures==0?0:1;
    }
    private static object? Read(object value,string property)=>value.GetType().GetProperty(property)!.GetValue(value);
    // Routed WPF input, not CDP: removing containment must let ScrollViewer's
    // left-button handler run again. Physical installed validation remains separate.
    private static async Task RoutedInputChecks(string directory)
    {
        var rows=new List<object>();
        for(var cycle=0;cycle<3;cycle++)
        {
            await using var renderer=new WebMapRenderer(1,Path.Combine(directory,"unused-input-profile"));
            var outside=new Border();var panel=new StackPanel();panel.Children.Add(renderer.View);panel.Children.Add(outside);
            var parent=new InputParent{Content=panel};
            var left=new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseDownEvent};
            renderer.View.RaiseEvent(left);
            Check(parent.LeftPresses==0&&left.Handled,$"Map left press cannot trigger parent focus (view {cycle+1})");
            foreach(var button in new[]{MouseButton.Right,MouseButton.Middle})
            {
                var down=new MouseButtonEventArgs(Mouse.PrimaryDevice,0,button){RoutedEvent=UIElement.MouseDownEvent};
                renderer.View.RaiseEvent(down);
                Check(!down.Handled,$"Map {button} press routing remains unchanged (view {cycle+1})");
            }
            renderer.View.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,120){RoutedEvent=UIElement.MouseWheelEvent});
            Check(parent.Wheels==1,$"Map wheel routing remains unchanged (view {cycle+1})");
            parent.LeftPresses=0;parent.Wheels=0;
            outside.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseDownEvent});
            outside.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,-120){RoutedEvent=UIElement.MouseWheelEvent});
            Check(parent.LeftPresses==1&&parent.Wheels==1,$"Outside-map parent focus and wheel remain reachable (view {cycle+1})");
            rows.Add(new{cycle,leftHandled=left.Handled,parent.LeftPresses,parent.Wheels});
            parent.Content=null;panel.Children.Clear();
        }
        Write(directory,"routed-input.json",new{rows,scope="Programmatically raised WPF events through actual renderer views; not physical mouse/DPI certification"});
    }
    private sealed class InputParent:ScrollViewer
    {
        internal int LeftPresses,Wheels;
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){LeftPresses++;base.OnMouseLeftButtonDown(e);}
        protected override void OnMouseWheel(MouseWheelEventArgs e){Wheels++;base.OnMouseWheel(e);}
    }
    internal static async Task<bool> BrowserPressForwarding(WebMapRenderer renderer,string directory)
    {
        var view=renderer.View;var initialPoint=Mouse.GetPosition(view);
        try
        {
        SyntheticMapPointer.Move(view,new Point(view.ActualWidth/2,view.ActualHeight/2));
        var point=Mouse.GetPosition(view);var origin=view.PointToScreen(new(0,0));
        var inside=point.X>=0&&point.X<view.ActualWidth&&point.Y>=0&&point.Y<view.ActualHeight;
        Check(inside,"Synthetic WPF pointer is inside the map before move/press");
        if(!inside)throw new InvalidOperationException("Synthetic pointer report failed; input result would be invalid");
        view.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=UIElement.MouseMoveEvent});
        var before=new{initialPoint=new{x=initialPoint.X,y=initialPoint.Y},point=new{x=point.X,y=point.Y},origin=new{x=origin.X,y=origin.Y},view.ActualWidth,view.ActualHeight,view.IsVisible,view.IsKeyboardFocusWithin,view.Opacity,renderer.Paused,nativeSuspended=renderer.Core.IsSuspended,windowActive=Window.GetWindow(view)?.IsActive};
        await renderer.Core.ExecuteScriptAsync("window.__s4BoundaryPress=null;document.addEventListener('mousedown',e=>window.__s4BoundaryPress={button:e.button,x:e.clientX,y:e.clientY,trusted:e.isTrusted,at:performance.now()},{capture:true,once:true});");
        renderer.View.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseDownEvent});
        var wait=Stopwatch.StartNew();JsonElement result;
        do
        {
            using var read=JsonDocument.Parse(await renderer.Core.ExecuteScriptAsync("window.__s4BoundaryPress"));result=read.RootElement.Clone();
            if(result.ValueKind==JsonValueKind.Object)break;
            await Task.Delay(20);
        }while(wait.ElapsedMilliseconds<1500);
        var passed=result.ValueKind==JsonValueKind.Object&&result.GetProperty("button").GetInt32()==0;
        Check(passed,"Contained WPF left press still reaches initialized browser");
        Check(passed&&result.GetProperty("trusted").GetBoolean()&&result.GetProperty("x").GetDouble()>=0&&result.GetProperty("x").GetDouble()<view.ActualWidth
            &&result.GetProperty("y").GetDouble()>=0&&result.GetProperty("y").GetDouble()<view.ActualHeight,"Forwarded WPF press is trusted and inside the browser viewport");
        var browser=await renderer.Core.ExecuteScriptAsync("({hidden:document.hidden,hasFocus:document.hasFocus(),width:innerWidth,height:innerHeight,event:window.__s4BoundaryPress})");
        Write(directory,"browser-press-forwarding.json",new{passed,before,elapsedMs=wait.Elapsed.TotalMilliseconds,result,browser=JsonDocument.Parse(browser).RootElement.Clone(),scope="Coordinate-bearing WPF input-report move followed by routed down/up; original 1500ms deadline; no global cursor movement, SDK state seeding or CDP mouse injection"});
        return passed;
        }
        finally
        {
            view.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseUpEvent});
            SyntheticMapPointer.Move(view,initialPoint);
        }
    }
    internal static async Task PumpChecks(Action<bool,string> check)
    {
        var collector=new SnapshotCollector();var state=Fixture();var metrics=new MapBridgeMetrics();Exception? error=null;
        await using var pump=new MapProjectionPump(collector,1,metrics,e=>error=e);
        pump.Offer(state,false);await Until(()=>collector.Last is not null);
        var first=collector.Last!;
        pump.Offer(state with{Telemetry=state.Telemetry! with{At=At.AddSeconds(1),Paused=true}},false);
        pump.Offer(state with{Telemetry=state.Telemetry! with{At=At.AddSeconds(2),LatitudeDegrees=-35.6001}},false);
        await Until(()=>collector.Last!.Sequence>first.Sequence);
        check(error is null&&collector.Last!.Aircraft[0].InterpolationAllowed==false&&collector.Last.Routes.All(r=>r.Kind!=MapRouteKind.ObservedTrack),"Coalesced pause/resume cannot create false observed continuity");
        for(var i=3;i<=4;i++)
        {
            var prior=collector.Last!.Sequence;
            pump.Offer(state with{Telemetry=state.Telemetry! with{At=At.AddSeconds(i),LatitudeDegrees=-35.6-i*.0001}},false);
            await Until(()=>collector.Last!.Sequence>prior);
        }
        var visible=collector.Last;
        var priorSegment=visible!.Routes.Single(r=>r.Kind==MapRouteKind.ObservedTrack).Segments.Single();
        pump.SetVisible(false);
        for(var i=5;i<20;i++)pump.Offer(state with{Telemetry=state.Telemetry! with{At=At.AddSeconds(i),LatitudeDegrees=-35.6-i*.0001}},false);
        await Task.Delay(300);
        check(ReferenceEquals(collector.Last,visible),"Hidden map pump does not project telemetry");
        pump.SetVisible(true);pump.Offer(state with{Telemetry=state.Telemetry! with{At=At.AddSeconds(20),LatitudeDegrees=-35.602}},false);
        await Until(()=>collector.Last!.Sequence>visible!.Sequence);
        check(collector.Last!.Aircraft[0].Position.Latitude==-35.602&&!collector.Last.Aircraft[0].InterpolationAllowed,"Resumed pump preserves sequence and begins fresh observation continuity");
        check(collector.Last.Routes.SingleOrDefault(r=>r.Kind==MapRouteKind.ObservedTrack)?.Segments.Single().SequenceEqual(priorSegment)==true,"Resumed pump preserves the previously observed trail without joining the hidden interval");
        var resumedSequence=collector.Last.Sequence;
        pump.Offer(state with{Telemetry=state.Telemetry! with{At=At.AddSeconds(21),LatitudeDegrees=-35.6021}},false);
        await Until(()=>collector.Last!.Sequence>resumedSequence);
        var segments=collector.Last!.Routes.Single(r=>r.Kind==MapRouteKind.ObservedTrack).Segments;
        check(segments.Length==2&&segments[0].SequenceEqual(priorSegment)&&segments[1].Length==2&&segments[1][0].Latitude==-35.602&&segments[1][1].Latitude==-35.6021,"Post-return observations form a separate segment after the retained trail");
        // Exercise the publication boundary with a projection that began before
        // hide/show but completes afterward. Its sequence is newer, so the
        // renderer's normal sequence check cannot distinguish this stale work.
        var accepted=collector.Last;
        var staleProjection=accepted with{Sequence=accepted.Sequence+1};
        var fields=BindingFlags.Instance|BindingFlags.NonPublic;
        var inputContinuity=(long)typeof(MapProjectionPump).GetField("continuity",fields)!.GetValue(pump)!;
        var inputVisibility=(long)typeof(MapProjectionPump).GetField("visibilityContinuity",fields)!.GetValue(pump)!;
        pump.SetVisible(false);pump.SetVisible(true);
        typeof(MapProjectionPump).GetMethod("PublishIfCurrent",fields)!.Invoke(pump,[collector,staleProjection,inputContinuity,inputVisibility]);
        check(ReferenceEquals(accepted,collector.Last),"Projection completed after hide and return cannot publish into the new visibility interval");
    }
    private sealed class SnapshotCollector:IMapRenderer
    {
        internal MapSnapshot? Last;
        public FrameworkElement View{get;}=new Border();
        public event Action<MapSelection?>? SelectionChanged{add{}remove{}}
        public event Action<MapCameraState>? CameraChanged{add{}remove{}}
        public event Action<MapRendererStatus>? StatusChanged{add{}remove{}}
        public Task InitializeAsync(CancellationToken token)=>Task.CompletedTask;
        public void Offer(MapSnapshot snapshot)=>Volatile.Write(ref Last,snapshot);
        public void Present(MapPresentationState state){}
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private static void Write(string directory,string name,object value)=>File.WriteAllText(Path.Combine(directory,name),JsonSerializer.Serialize(value,Json));
    private static async Task Until(Func<bool> predicate,int milliseconds=15000)
    {
        var timer=Stopwatch.StartNew();while(!predicate()){if(timer.ElapsedMilliseconds>milliseconds)throw new TimeoutException("Map diagnostic condition timed out");await Task.Delay(25);}
    }
    internal static FlightSessionSnapshot Fixture()
    {
        var telemetry=new Telemetry(At,false,430,false,true,LatitudeDegrees:-35.6,LongitudeDegrees:150.1,AltitudeFeet:34000,HeadingDegrees:195);
        var plan=new ActiveFlightPlan("JST721","VH-VGF","YSSY","YMHB",At.AddMinutes(-25),At.AddMinutes(87),"Map diagnostic fixture",DepartureGate:"54",ArrivalGate:"3",AircraftType:"A320",
            RoutePoints:[new("YSSY",-33.94,151.18,"Departure"),new("OTKED",-37,149),new("SUNOB",-40,148),new("YMHB",-42.84,147.51,"Arrival")]);
        return new(){Assignment=plan,Connection=SimulatorConnectionState.Connected,ConnectionGeneration=1,Connected=true,ConnectionActive=true,
            Reading=new("Airbus A320",telemetry),Telemetry=telemetry,Aircraft="Airbus A320",SimulatorUtc=At,DataCurrent=true,
            CanRenderAssignment=true,CanPresentCandidate=true,Association=new(true,"VERIFIED",[]),ScheduleTrusted=true,
            Phase=FlightPhase.Airborne,RouteProgress=.23,RouteReliable=true,EstimatedArrival=plan.PlannedArrivalUtc.AddMinutes(2)};
    }
    private static async Task Visuals(MainWindow main,FlightSessionSnapshot state,string directory)
    {
        var host=main.OperationsView.MapHost;
        main.HeroFlightText.Text="JST721";main.OriginCodeText.Text="YSSY";main.DestinationCodeText.Text="YMHB";
        main.HeroTimingText.Text="Diagnostic fixture · current production view";main.AircraftText.Text="Airbus A320 · VH-VGF";
        main.OriginCityText.Text="SYDNEY KINGSFORD SMITH INTERNATIONAL AIRPORT";main.DestinationCityText.Text="HOBART INTERNATIONAL AIRPORT";
        main.OperationsView.HeroAircraftTypeText.Text="A320";main.OperationsView.TrackerModeText.Text="DIAGNOSTIC FIXTURE";
        main.OperationsView.HeroDepartureText.Text="02:05";main.OperationsView.HeroArrivalText.Text="03:59";
        main.OperationsView.HeroDepartureGateText.Text="54";main.OperationsView.HeroArrivalGateText.Text="3";
        main.OperationsView.DashboardFlightsGrid.ItemsSource=Array.Empty<object>();
        foreach(var size in new[]{(1920,1080),(2560,1440),(1366,768)})
        {
            main.Width=size.Item1;main.Height=size.Item2;main.UpdateLayout();await Task.Delay(600);
            DashboardSmokeTest.Capture(main,Path.Combine(directory,$"operations-{size.Item1}x{size.Item2}.png"));
            Check(host.ActualHeight>=220&&host.ActualWidth>300,$"Map viewport retained at {size.Item1}×{size.Item2}");
            if(size.Item1==1366){host.BringIntoView();await Task.Delay(100);DashboardSmokeTest.Capture(main,Path.Combine(directory,"operations-1366x768-map-scrolled.png"));main.OperationsView.Scroller.ScrollToTop();}
        }
        main.Width=1920;main.Height=1080;
        foreach(var scale in new[]{1.5,2.0})
        {
            main.OperationsView.LayoutTransform=new ScaleTransform(scale,scale);main.UpdateLayout();await Task.Delay(600);
            DashboardSmokeTest.Capture(main,Path.Combine(directory,$"operations-scale-{scale*100:0}.png"));
            Check(host.ActualWidth>250,$"Composition remains sized at {scale*100:0}% layout scaling");
            host.BringIntoView();await Task.Delay(100);DashboardSmokeTest.Capture(main,Path.Combine(directory,$"operations-scale-{scale*100:0}-map-scrolled.png"));main.OperationsView.Scroller.ScrollToTop();
        }
        main.OperationsView.LayoutTransform=Transform.Identity;main.UpdateLayout();await Task.Delay(400);
        main.Shell.OpenSearch();await Task.Delay(300);DashboardSmokeTest.Capture(main,Path.Combine(directory,"shell-search-overlay.png"));main.Shell.CloseOverlays();
        main.Shell.ShowUtilities();await Task.Delay(300);DashboardSmokeTest.Capture(main,Path.Combine(directory,"shell-utilities.png"));main.Shell.CloseOverlays();
        var retained=host.V2;
        main.WindowState=WindowState.Minimized;await Task.Delay(250);
        Check(ReferenceEquals(retained,host.V2)&&host.V2?.Paused==true,"Minimized window retains a paused renderer");
        main.RestoreWindow();await host.WaitReadyAsync();await Task.Delay(500);
        Check(host.V2 is not null,"Map survives minimize/restore without simulator interaction");
        Write(directory,"visual-host.json",new{host="WebView2CompositionControl",dpiMethod="WPF LayoutTransform rendering at 150%/200%; no manual OS monitor change",manualInputOrScreenReaderCertified=false});
    }
    private static async Task BridgeChecks(WebMapRenderer renderer,string directory)
    {
        var before=renderer.Metrics.Rejected;
        foreach(var pair in new[]{("https://evil.invalid/","{}"),(WebMapRenderer.Origin+"/index.html","{"),(WebMapRenderer.Origin+"/index.html",$"{{\"type\":\"ack\",\"schemaVersion\":1,\"epoch\":{renderer.Epoch-1},\"generation\":1,\"sequence\":1}}")})
            renderer.ReceiveMessage(pair.Item1,pair.Item2);
        Check(renderer.Metrics.Rejected==before+3,"Origin, malformed JSON and stale browser epoch rejected");
        var read=await renderer.Core.ExecuteScriptAsync("window.sierraMapDiagnostics.read()");
        File.WriteAllText(Path.Combine(directory,"initial-renderer.json"),read);
        var checksJson=await renderer.Core.ExecuteScriptAsync("({controls:[...document.querySelectorAll('button')].every(b=>!!b.textContent.trim()||!!b.getAttribute('aria-label')),text:!!document.querySelector('[aria-live]'),canvas:!!document.querySelector('canvas')})");
        using var doc=JsonDocument.Parse(checksJson);
        Check(doc.RootElement.GetProperty("controls").GetBoolean()&&doc.RootElement.GetProperty("text").GetBoolean(),"Map controls are named and selection has non-canvas live text");
        Check(doc.RootElement.GetProperty("canvas").GetBoolean(),"GPU map canvas exists in production renderer");
        renderer.View.Focus();
        await renderer.Core.ExecuteScriptAsync("window.diagnosticKeys=[];document.addEventListener('keydown',e=>window.diagnosticKeys.push({key:e.key,target:e.target.id,trusted:e.isTrusted}));document.getElementById('layers').focus()");
        await renderer.Core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyDown\",\"key\":\"Enter\",\"code\":\"Enter\",\"text\":\"\\r\",\"unmodifiedText\":\"\\r\",\"windowsVirtualKeyCode\":13}");
        await renderer.Core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyUp\",\"key\":\"Enter\",\"code\":\"Enter\",\"windowsVirtualKeyCode\":13}");
        Check(await renderer.Core.ExecuteScriptAsync("!document.getElementById('layer-panel').hidden")=="true","Browser keyboard Enter opens the named Layers control");
        File.WriteAllText(Path.Combine(directory,"keyboard.json"),await renderer.Core.ExecuteScriptAsync("({keys:window.diagnosticKeys,focused:document.activeElement.id,hasFocus:document.hasFocus(),layersOpen:!document.getElementById('layer-panel').hidden})"));
        await renderer.Core.ExecuteScriptAsync("document.getElementById('reduced-motion').click()");
        Check(await renderer.Core.ExecuteScriptAsync("document.getElementById('reduced-motion').checked")=="true","Reduced-motion control changes presentation preference");
        foreach(var selection in new[]{new MapSelection(MapEntityKind.Airport,"YSSY"),new MapSelection(MapEntityKind.Route,"planned-route")})
        {
            renderer.Present(new(new(new(150,-38),4,0,0,null),selection,Enum.GetValues<MapLayer>().ToImmutableHashSet(),true,DateTimeOffset.UtcNow){RestoreCamera=true});
            await Task.Delay(70);
            var restored=await renderer.Core.ExecuteScriptAsync("window.sierraMapDiagnostics.read()");using var result=JsonDocument.Parse(restored);
            Check(result.RootElement.GetProperty("selectionFilters").GetProperty(selection.Kind==MapEntityKind.Airport?"airport":"route")[2].GetString()==selection.Id,$"{selection.Kind} selection restores its highlight filter");
        }
        renderer.View.Focus();
        await renderer.Core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyDown\",\"key\":\"Escape\",\"code\":\"Escape\",\"windowsVirtualKeyCode\":27}");
        await Task.Delay(70);
        Check(!renderer.View.IsKeyboardFocusWithin,"Escape bypass moves focus out of the map into WPF content");
    }
    private static long diagnosticEpoch=100000;
    private static async Task WithRenderer(string directory,string name,Func<WebMapRenderer,Task> action)
    {
        await using var renderer=new WebMapRenderer(++diagnosticEpoch,Path.Combine(directory,"browser-"+name));
        var window=new Window{Title="Sierra Map diagnostic · "+name,Width=1150,Height=650,Content=renderer.View,ShowInTaskbar=false};
        window.Show();
        try{await renderer.InitializeAsync(CancellationToken.None);await action(renderer);}
        finally{window.Content=null;window.Close();}
    }
    private static async Task Benchmark(string directory)
    {
        var results=new List<object>();
        foreach(var count in new[]{1,25,100,500,1000,2000,5000})
        {
            await WithRenderer(directory,"load-"+count,async renderer=>
            {
            long sequence=0;
            var sampler=new ProcessSampler(renderer.Environment!);var timer=Stopwatch.StartNew();var frames=0;
            while(timer.Elapsed<TimeSpan.FromSeconds(4))
            {
                renderer.Offer(Load(renderer.Epoch,++sequence,count,timer.Elapsed.TotalSeconds));frames++;
                sampler.Sample();await Task.Delay(200);
            }
            await Until(()=>Convert.ToInt64(typeof(WebMapRenderer).GetField("lastSequence",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(renderer))==sequence);sampler.Sample();
            var js=await renderer.Core.ExecuteScriptAsync("window.sierraMapDiagnostics.read()");
            results.Add(new{count,offered=frames,seconds=timer.Elapsed.TotalSeconds,bridge=renderer.Measurements,process=sampler.Read(),renderer=JsonDocument.Parse(js).RootElement.Clone()});
            using var rendered=JsonDocument.Parse(js);
            Check(rendered.RootElement.GetProperty("counts").GetProperty("aircraft").GetInt32()==count&&rendered.RootElement.GetProperty("lastSequence").GetInt64()==sequence,$"Production bridge processes {count} aircraft with routes/trails/labels");
            });
        }
        Write(directory,"benchmark.json",results);
    }
    internal static MapSnapshot Load(long generation,long sequence,int count,double elapsed)
    {
        var now=DateTimeOffset.UtcNow;
        var aircraft=Enumerable.Range(0,count).Select(i=>new MapAircraft("load-"+i,"SYN"+i,null,new(((i*137.5+elapsed*.03)%340)-170,((i*47.33)%120)-60),35000,(i*37)%360,450,MapEntitySource.Simulated,now,MapReliability.Current,MapAircraftStatus.Airborne,i==0,1,1,true)).ToImmutableArray();
        var routes=Enumerable.Range(0,Math.Min(count,25)).Select(i=>new MapRoute("route-"+i,null,null,null,null,MapRouteKind.Planned,MapEntitySource.Simulated,
            MapGeography.GreatCircle(aircraft[i].Position,new(Math.Clamp(aircraft[i].Position.Longitude+15,-180,180),aircraft[i].Position.Latitude+10)),[],null,MapReliability.Current)).ToImmutableArray();
        var trails=Enumerable.Range(0,Math.Min(count,25)).Select(i=>new MapRoute("trail-"+i,null,null,null,null,MapRouteKind.ObservedTrack,MapEntitySource.Simulated,
            [Enumerable.Range(0,64).Select(p=>new GeoPoint(aircraft[i].Position.Longitude-p*.003,aircraft[i].Position.Latitude)).ToImmutableArray()],[],now,MapReliability.Current));
        return new(1,generation,sequence,now,aircraft,[],routes.AddRange(trails),[],[]){SunVector=MapSolar.SunVector(now)};
    }
    private static async Task Lifecycle(MainWindow main,FlightSessionSnapshot state,string directory)
    {
        var host=main.OperationsView.MapHost;var owner=FlightSessionDiagnosticAccess.Owner(main);
        var baseline=JsonSerializer.Serialize(owner.Snapshot,MapSnapshotValidator.JsonOptions);
        var memory=new List<object>();var watch=Stopwatch.StartNew();var epochs=new HashSet<long>();
        for(int i=0;i<12;i++)
        {
            main.ShowDashboard();if(!host.UseV2)host.SetRenderer(true);
            main.OperationsView.Present(OperationsOverviewQuery.Project(state,false,[],null,false,DateTimeOffset.UtcNow));
            await host.WaitReadyAsync();var r=host.V2!;epochs.Add(r.Epoch);
            await r.Core.ExecuteScriptAsync("document.getElementById('follow').click();document.getElementById('follow').click();");
            main.Width=i%2==0?1366:1920;main.UpdateLayout();await Task.Delay(200);
            memory.Add(new{cycle=i,readyMs=r.Metrics.ReadyMs,hostWorkingSetMiB=Process.GetCurrentProcess().WorkingSet64/1048576d,browserProcesses=r.Environment!.GetProcessInfos().Count});
            main.ShowDispatch();await Task.Delay(100);
            Check(ReferenceEquals(host.V2,r)&&r.Paused,$"Hidden map retains one paused renderer in lifecycle cycle {i+1}");
            // Malformed late messages cannot revive or reveal a hidden view.
            r.ReceiveMessage(WebMapRenderer.Origin+"/index.html","{\"schemaVersion\":1,\"type\":\"selection\"}");
            Check(r.View.Opacity==0,$"Late message cannot expose hidden map in lifecycle cycle {i+1}");
        }
        Check(epochs.Count==1,"Twelve workspace returns retain one bridge epoch");
        Check(baseline==JsonSerializer.Serialize(owner.Snapshot,MapSnapshotValidator.JsonOptions),"Map navigation/recreation leaves authoritative flight snapshot unchanged");
        main.ShowDashboard();host.SetRenderer(true);
        main.OperationsView.Present(OperationsOverviewQuery.Project(state,false,[],null,false,DateTimeOffset.UtcNow));await host.WaitReadyAsync();
        var pids=host.V2!.Environment!.GetProcessInfos().Select(p=>p.ProcessId).ToArray();host.SetRenderer(false);
        await Task.Delay(2500);
        var remaining=pids.Where(ProcessAlive).ToArray();
        Check(remaining.Length==0,"Owned WebView processes exit after final map disposal");
        Write(directory,"lifecycle.json",new{seconds=watch.Elapsed.TotalSeconds,cycles=12,memory,remainingBrowserPids=remaining,recreations=host.RecreationCount,limitation="Bounded lifecycle sampling, not proof of leak freedom; shared browser pages can affect working-set totals."});
    }
    private static bool ProcessAlive(int pid){try{using var p=Process.GetProcessById(pid);return !p.HasExited;}catch(ArgumentException){return false;}}
    private static async Task FailureCases(MainWindow main,string directory)
    {
        var host=main.OperationsView.MapHost;var baseline=FlightSessionDiagnosticAccess.Owner(main).Snapshot;
        var outcomes=new List<object>();
        async Task ExpectFallback(string name,Action configure,Func<WebMapRenderer,Task>? trigger=null)
        {
            host.SetRenderer(false);configure();host.SetRenderer(true);
            if(trigger is not null)
            {
                await host.WaitReadyAsync();await trigger(host.V2!).WaitAsync(TimeSpan.FromSeconds(4));
                await Until(()=>host.RestartCount==1&&host.V2?.Ready.IsCompletedSuccessfully==true);
                await trigger(host.V2!).WaitAsync(TimeSpan.FromSeconds(4));
            }
            await Until(()=>!host.UseV2,30000);
            Check(host.RestartCount==1&&host.V2 is null,$"{name}: one restart then classic map fallback");
            outcomes.Add(new{name,host.RestartCount,host.LastFailure});Write(directory,"failures-progress.json",outcomes);
            host.DiagnosticAssets=null;host.DiagnosticRuntimeVersion=null;host.DiagnosticRendererFactory=null;
        }
        await ExpectFallback("Renderer construction failure",()=>host.DiagnosticRendererFactory=()=>throw new InvalidOperationException("Injected constructor failure"));
        await ExpectFallback("Missing runtime (injected detection)",()=>host.DiagnosticRuntimeVersion=()=>"");
        await ExpectFallback("Missing local assets",()=>host.DiagnosticAssets=Path.Combine(directory,"no-assets"));
        await ExpectFallback("WebGL context loss",()=>{},async r=>{await r.Core.ExecuteScriptAsync("const c=document.querySelector('canvas');c.getContext('webgl2').getExtension('WEBGL_lose_context').loseContext()");});
        await ExpectFallback("Renderer process crash",()=>{},async r=>{try{await r.Core.CallDevToolsProtocolMethodAsync("Page.crash","{}").WaitAsync(TimeSpan.FromSeconds(2));}catch(System.Runtime.InteropServices.COMException){}catch(InvalidOperationException){}catch(TimeoutException){}});
        await ExpectFallback("Invalid map payload",()=>{},r=>{r.Offer(Load(r.Epoch,900000,1,0) with{Aircraft=[new("invalid",null,null,new(0,0),null,double.NaN,null,MapEntitySource.Unknown,DateTimeOffset.UtcNow,MapReliability.Unknown,MapAircraftStatus.Unknown)]});return Task.CompletedTask;});
        // A deliberate missing handshake tests the actual ten-second readiness deadline.
        var incomplete=Path.Combine(directory,"timeout-assets");Directory.CreateDirectory(incomplete);
        foreach(var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory,"MapV2","Web"),"*",SearchOption.AllDirectories))
        {var target=Path.Combine(incomplete,Path.GetRelativePath(Path.Combine(AppContext.BaseDirectory,"MapV2","Web"),file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target,true);}
        File.WriteAllText(Path.Combine(incomplete,"map.mjs"),"// Deliberately no readiness handshake in diagnostic fixture.");
        await ExpectFallback("Initialization timeout",()=>host.DiagnosticAssets=incomplete);
        File.WriteAllText(Path.Combine(incomplete,"map.mjs"),"window.chrome.webview.addEventListener('message',e=>{if(e.data.type==='initialize')window.chrome.webview.postMessage({type:'error',schemaVersion:1,epoch:e.data.epoch,reason:'Injected JavaScript initialization failure'})});window.chrome.webview.postMessage({type:'ready',schemaVersion:1});");
        await ExpectFallback("JavaScript initialization failure",()=>host.DiagnosticAssets=incomplete);
        Check(ReferenceEquals(baseline,FlightSessionDiagnosticAccess.Owner(main).Snapshot),"Renderer failures never publish or reconnect the flight session");
        Write(directory,"failures.json",outcomes);
    }

    private sealed class ProcessSampler
    {
        private readonly Microsoft.Web.WebView2.Core.CoreWebView2Environment environment;
        private readonly Dictionary<int,double> first=new(),last=new();private long peak;private readonly Stopwatch timer=Stopwatch.StartNew();
        internal ProcessSampler(Microsoft.Web.WebView2.Core.CoreWebView2Environment environment){this.environment=environment;Sample();}
        internal void Sample()
        {
            long total=0;
            foreach(var pid in environment.GetProcessInfos().Select(p=>p.ProcessId).Append(Environment.ProcessId).Distinct())
                try{using var process=Process.GetProcessById(pid);if(!first.ContainsKey(pid))first[pid]=process.TotalProcessorTime.TotalMilliseconds;last[pid]=process.TotalProcessorTime.TotalMilliseconds;total+=process.WorkingSet64;}
                catch(ArgumentException){}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
            peak=Math.Max(peak,total);
        }
        internal object Read()=>new{cpuPercentMachine=last.Sum(p=>p.Value-first[p.Key])/timer.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100,peakSummedWorkingSetMiB=peak/1048576d,logicalProcessors=Environment.ProcessorCount,workingSetMayDoubleCountSharedPages=true};
    }
}
