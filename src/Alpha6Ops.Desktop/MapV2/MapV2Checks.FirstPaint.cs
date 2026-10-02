using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Alpha6Ops.Desktop.MapV2;

internal static partial class MapV2Checks
{
    // Test the real renderer, image decoder and MapLibre event order. A fixed
    // barrier is injected only into the image dependency, never production code.
    private const string ObserveFirstPaint = """
        (async()=>{
          const {Map,ImageSource}=await import('./vendor/maplibre-gl.mjs');
          window.firstPaintProbe={events:[],loads:0,installed:false};
          const imageGate=new Promise(resolve=>window.firstPaintProbe.release=resolve);
          const load=ImageSource.prototype.load;
          ImageSource.prototype.load=async function(...args){
            if(this.id==='night'){
              window.firstPaintProbe.loads++;this._loaded=false;
              await imageGate;
            }
            return load.apply(this,args);
          };
          const fire=Map.prototype.fire;
          Map.prototype.fire=function(event,...args){
            const type=typeof event==='string'?event:event.type;
            if(type==='render'||type==='idle'){
              const d=window.sierraMapDiagnostics.read();
              window.firstPaintProbe.events.push({type,sequence:d.lastSequence,solar:d.solarSignature,nightLoaded:this.getSource('night')?.loaded()===true});
            }
            return fire.call(this,event,...args);
          };
          window.firstPaintProbe.installed=true;
        })();
        """;

    private static async Task FirstPaintChecks(string directory)
    {
        var records=new List<object>();
        for(var cycle=0;cycle<3;cycle++)
        {
            await using var renderer=new WebMapRenderer(++diagnosticEpoch,Path.Combine(directory,"first-paint-browser"));
            var window=new Window{Title="Sierra first-paint diagnostic",Width=1000,Height=600,Content=renderer.View,ShowInTaskbar=false};
            var readyCount=0;Task<string>? atReady=null;
            renderer.StatusChanged+=s=>{if(s.State==MapRendererState.Ready){readyCount++;atReady=renderer.Core.ExecuteScriptAsync("({map:window.sierraMapDiagnostics.read(),probe:window.firstPaintProbe??null})");}};
            window.Show();
            try
            {
                if(cycle==2)renderer.Present(new(new(new(0,20),.6,0,0,null),null,Enum.GetValues<MapLayer>().Where(x=>x!=MapLayer.DayNight).ToImmutableHashSet(),true,DateTimeOffset.UtcNow));
                await renderer.InitializeAsync(CancellationToken.None);
                var initial=await renderer.Core.ExecuteScriptAsync("window.sierraMapDiagnostics.read()");
                Check(renderer.View.Opacity==0&&readyCount==0,$"First paint {cycle+1}: initialized bridge stays concealed until current data");
                using(var state=JsonDocument.Parse(initial))Check(state.RootElement.GetProperty("solarSignature").GetString()=="",$"First paint {cycle+1}: no placeholder sun is installed");
                await renderer.Core.ExecuteScriptAsync(ObserveFirstPaint);
                var probeDeadline=DateTime.UtcNow.AddSeconds(5);
                while(await renderer.Core.ExecuteScriptAsync("window.firstPaintProbe?.installed===true")!="true")
                {if(DateTime.UtcNow>probeDeadline)throw new TimeoutException("First-paint probe did not initialize");await Task.Delay(20);}
                var snapshot=new MapSnapshot(1,renderer.Epoch,1,DateTimeOffset.UtcNow,[],[],[],[],[]){SunVector=[0,1,0]};
                renderer.Offer(snapshot);
                var loadDeadline=DateTime.UtcNow.AddSeconds(5);
                while(await renderer.Core.ExecuteScriptAsync("window.firstPaintProbe.loads>0")!="true"&&readyCount==0)
                {if(DateTime.UtcNow>loadDeadline)throw new TimeoutException("First-paint image did not begin loading");await Task.Delay(20);}
                var duringLoad=await renderer.Core.ExecuteScriptAsync("({loads:window.firstPaintProbe.loads,map:window.sierraMapDiagnostics.read()})");
                Write(directory,$"first-paint-timing-{cycle+1}.json",new{duringLoad,opacity=renderer.View.Opacity,readyCount});
                using(var state=JsonDocument.Parse(duringLoad))Check(state.RootElement.GetProperty("loads").GetInt32()>0&&renderer.View.Opacity==0&&readyCount==0,$"First paint {cycle+1}: slow night image cannot expose an incomplete frame");
                await renderer.Core.ExecuteScriptAsync("window.firstPaintProbe.release()");
                await Until(()=>readyCount>0&&Convert.ToInt64(Read(renderer.Measurements,"acknowledged"))>=1);
                var revealed=await atReady!;
                using(var state=JsonDocument.Parse(revealed))
                {
                    var root=state.RootElement;var probe=root.GetProperty("probe");
                    Check(root.GetProperty("map").GetProperty("lastSequence").GetInt64()==1&&root.GetProperty("map").GetProperty("solarSignature").GetString()=="0.000,1.000,0.000"&&probe.ValueKind==JsonValueKind.Object&&probe.GetProperty("events").EnumerateArray().Any(e=>e.GetProperty("type").GetString()=="idle"&&e.GetProperty("nightLoaded").GetBoolean()&&e.GetProperty("sequence").GetInt64()==1),$"First paint {cycle+1}: visible readiness follows current lighting's completed frame");
                }
                renderer.Offer(snapshot with{Sequence=2,SunVector=[-1,0,0]});
                await Until(()=>Convert.ToInt64(Read(renderer.Measurements,"acknowledged"))>=2);
                Check(renderer.View.Opacity==1&&readyCount==1,$"First paint {cycle+1}: subsequent snapshots retain normal visible streaming");
                records.Add(new{cycle,initial=JsonDocument.Parse(initial).RootElement.Clone(),atReady=JsonDocument.Parse(revealed).RootElement.Clone(),readyCount});
            }
            finally{window.Content=null;window.Close();}
        }
        Write(directory,"first-paint.json",new{records,scope="Real isolated WebView/MapLibre with deliberately delayed image decoding; not installed physical validation"});
    }

    private static async Task FirstPaintLifecycleChecks(MainWindow main,string directory)
    {
        var host=main.OperationsView.MapHost;
        host.SetRenderer(false);host.DiagnosticProfile=Path.Combine(directory,"first-paint-navigation-browser");
        var owner=FlightSessionDiagnosticAccess.Owner(main);
        var before=JsonSerializer.Serialize(owner.Snapshot,MapSnapshotValidator.JsonOptions);
        var records=new List<object>();var seen=new HashSet<long>();
        var initialHidden=new Dictionary<long,bool>();var readAtReady=new Dictionary<long,Task<string>>();
        void Observe()
        {
            if(host.V2 is not {} renderer||!seen.Add(renderer.Epoch))return;
            initialHidden[renderer.Epoch]=renderer.View.Opacity==0;
            renderer.StatusChanged+=s=>{if(s.State==MapRendererState.Ready)readAtReady[renderer.Epoch]=renderer.Core.ExecuteScriptAsync("window.sierraMapDiagnostics.read()");};
        }
        host.LayoutRequested+=Observe;
        try
        {
            // The genuine empty/disconnected initial state also needs current lighting.
            main.OperationsView.Present(OperationsOverviewQuery.Project(owner.Snapshot,false,[],null,false,DateTimeOffset.UtcNow));
            foreach(var path in new[]{"initial Operations", "tracking return 1", "tracking return 2", "tracking return 3", "tracking return 4", "minimize restore", "classic to V2", "unload reload", "recreate before first data"})
            {
                var previous=host.V2;
                if(previous is not null)readAtReady.Remove(previous.Epoch);
                if(path.StartsWith("tracking"))
                {
                    main.ShowFlightTracking();await Until(()=>host.V2?.Paused==true);
                    Check(ReferenceEquals(previous,host.V2)&&host.V2!.View.Opacity==0,$"{path}: hidden retained view remains concealed");
                    main.ShowDashboard();
                }
                else if(path=="minimize restore"){main.WindowState=WindowState.Minimized;await Until(()=>host.V2?.Paused==true);main.Show();main.WindowState=WindowState.Normal;}
                else if(path=="unload reload")
                {
                    var parent=main.LegacyWorkspaceContent;var index=parent.Children.IndexOf(main.OperationsView);
                    parent.Children.Remove(main.OperationsView);await Until(()=>host.V2 is null);
                    parent.Children.Insert(index,main.OperationsView);
                }
                else {host.SetRenderer(false);host.SetRenderer(true);if(path=="recreate before first data"){host.SetRenderer(false);host.SetRenderer(true);}}
                await host.WaitReadyAsync();await Until(()=>host.V2?.Ready.IsCompletedSuccessfully==true);var renderer=host.V2!;
                await Until(()=>readAtReady.ContainsKey(renderer.Epoch));
                using var data=JsonDocument.Parse(await readAtReady[renderer.Epoch]);
                Check(initialHidden[renderer.Epoch]&&data.RootElement.GetProperty("lastSequence").GetInt64()>0&&data.RootElement.GetProperty("solarSignature").GetString() is not("" or "1.000,0.000,0.000"),$"{path}: visible renderer becomes ready with supplied lighting");
                if(path.StartsWith("tracking")||path=="minimize restore")Check(ReferenceEquals(previous,renderer),$"{path}: correct-lighting return retains renderer identity");
                records.Add(new{path,renderer.Epoch,initialHidden=initialHidden[renderer.Epoch],atReady=data.RootElement.Clone()});
                Write(directory,"first-paint-lifecycle.json",records);
            }
            Check(before==JsonSerializer.Serialize(owner.Snapshot,MapSnapshotValidator.JsonOptions),"First-paint lifecycle does not mutate authoritative flight state");
            Write(directory,"first-paint-lifecycle.json",records);
        }
        finally{host.LayoutRequested-=Observe;host.SetRenderer(false);}
    }
}
