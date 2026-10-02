using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Alpha6Ops.Desktop.MapV2;

internal static partial class MapV2Checks
{
    private static async Task RestoredPaintChecks(string directory)
    {
        await using var renderer=new WebMapRenderer(++diagnosticEpoch,Path.Combine(directory,"restored-paint-browser"));
        var window=new Window{Title="Sierra restored-paint diagnostic",Width=1000,Height=600,Content=renderer.View,ShowInTaskbar=false};
        var readyCount=0;Task<string>? revealed=null;
        renderer.StatusChanged+=s=>{if(s.State==MapRendererState.Ready){readyCount++;revealed=renderer.Core.ExecuteScriptAsync("({map:window.sierraMapDiagnostics.read(),probe:window.firstPaintProbe??null})");}};
        window.Show();
        try
        {
            await renderer.InitializeAsync(CancellationToken.None);
            var snapshot=new MapSnapshot(1,renderer.Epoch,1,DateTimeOffset.UtcNow,[],[],[],[],[]){SunVector=[0,1,0]};
            renderer.Offer(snapshot);await Until(()=>readyCount==1);
            await renderer.SetVisibleAsync(false);
            Check(renderer.Paused&&renderer.Core.IsSuspended&&renderer.View.Opacity==0,"Retained map is natively suspended and concealed while hidden");
            await renderer.SetVisibleAsync(true);
            await renderer.Core.ExecuteScriptAsync(ObserveFirstPaint);
            var installDeadline=DateTime.UtcNow.AddSeconds(5);
            while(await renderer.Core.ExecuteScriptAsync("window.firstPaintProbe?.installed===true")!="true")
            {if(DateTime.UtcNow>installDeadline)throw new TimeoutException("Restored-paint probe did not initialize");await Task.Delay(20);}
            renderer.Offer(snapshot with{Sequence=2,SunVector=[-1,0,0]});
            var deadline=DateTime.UtcNow.AddSeconds(5);
            while(await renderer.Core.ExecuteScriptAsync("window.firstPaintProbe.loads>0")!="true")
            {if(DateTime.UtcNow>deadline)throw new TimeoutException("Restored solar image did not begin loading");await Task.Delay(20);}
            renderer.ReceiveMessage(WebMapRenderer.Origin+"/index.html",$"{{\"type\":\"ack\",\"schemaVersion\":1,\"epoch\":{renderer.Epoch},\"generation\":{renderer.Epoch},\"sequence\":1}}");
            Check(renderer.View.Opacity==0&&readyCount==1,"Returned map stays concealed through delayed current solar loading and stale ACK");
            await renderer.Core.ExecuteScriptAsync("window.firstPaintProbe.release()");
            await Until(()=>readyCount==2);
            using var result=JsonDocument.Parse(await revealed!);
            var data=result.RootElement;
            Check(data.GetProperty("map").GetProperty("lastSequence").GetInt64()==2&&data.GetProperty("map").GetProperty("solarSignature").GetString()=="-1.000,0.000,0.000","Returned map reveals latest snapshot and current solar state");
            var completed=false;
            foreach(var e in data.GetProperty("probe").GetProperty("events").EnumerateArray())
                completed|=e.GetProperty("type").GetString()=="idle"&&e.GetProperty("nightLoaded").GetBoolean()&&e.GetProperty("sequence").GetInt64()==2;
            Check(completed&&renderer.View.Opacity==1,"Returned map readiness follows the completed current-lighting frame");
            Write(directory,"restored-paint.json",new{readyCount,atReveal=data.Clone(),scope="Actual retained WebView/MapLibre with delayed image load and injected obsolete sequence ACK; no physical input certification"});
        }
        finally{window.Content=null;window.Close();}

    }
}
