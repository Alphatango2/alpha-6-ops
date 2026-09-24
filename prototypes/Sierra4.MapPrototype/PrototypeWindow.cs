using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Sierra4.MapPrototype;

// Isolated experiment: no project reference, production profile, recorder or simulator access.
internal sealed class PrototypeWindow : Window
{
    private const string Origin="https://sierra-prototype.invalid";
    private readonly WebView2 web=new();
    private readonly string directory;
    private readonly int cycle;
    private readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource complete=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Stopwatch startup=Stopwatch.StartNew();
    private readonly ProcessMetrics metrics=new();
    private readonly System.Windows.Threading.DispatcherTimer sampler=new(){Interval=TimeSpan.FromMilliseconds(500)};
    private readonly JsonSerializerOptions json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,Converters={new JsonStringEnumConverter()}};
    private CoreWebView2Environment? environment;
    private int browserPid;

    internal PrototypeWindow(string directory,int cycle)
    {
        this.directory=directory;this.cycle=cycle;
        Title="Sierra 4 · Map V2 architecture prototype · SYNTHETIC DATA ONLY";Width=1720;Height=980;
        Background=new SolidColorBrush(Color.FromRgb(9,12,15));
        var grid=new Grid();grid.RowDefinitions.Add(new(){Height=new GridLength(52)});grid.RowDefinitions.Add(new());
        grid.Children.Add(new TextBlock{Text="SIERRA 4  /  MAP V2 LAB     •     ISOLATED PROTOTYPE — SYNTHETIC TRAFFIC",Foreground=Brushes.Gold,FontFamily=new("Segoe UI"),FontSize=17,Margin=new(22,15,0,0)});
        Grid.SetRow(web,1);grid.Children.Add(web);Content=grid;
        Closed+=(_,_)=>{DisposeRenderer();if(cycle==0&&!complete.Task.IsCompleted)complete.TrySetCanceled();};
        sampler.Tick+=(_,_)=>{if(environment is not null)metrics.Sample(environment);};
    }
    internal async Task InitializeAsync()
    {
        environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(directory,"webview-profile"));
        await web.EnsureCoreWebView2Async(environment);
        var core=web.CoreWebView2;browserPid=(int)core.BrowserProcessId;
        core.Settings.AreHostObjectsAllowed=false;core.Settings.AreDefaultContextMenusEnabled=false;
        core.Settings.AreDevToolsEnabled=false;core.Settings.IsStatusBarEnabled=false;
        core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
        core.DownloadStarting+=(_,e)=>e.Cancel=true;
        core.NewWindowRequested+=(_,e)=>e.Handled=true;
        core.NavigationStarting+=(_,e)=>{if(!e.Uri.StartsWith(Origin+"/",StringComparison.Ordinal))e.Cancel=true;};
        core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
        core.WebResourceRequested+=(_,e)=>
        {
            if(!e.Request.Uri.StartsWith(Origin+"/",StringComparison.Ordinal)&&!e.Request.Uri.StartsWith("data:",StringComparison.Ordinal)&&!e.Request.Uri.StartsWith("blob:"+Origin+"/",StringComparison.Ordinal))
                e.Response=environment.CreateWebResourceResponse(new MemoryStream(),403,"Blocked","Content-Type: text/plain");
        };
        core.ProcessFailed+=(_,e)=>complete.TrySetException(new InvalidOperationException("Renderer failed: "+e.ProcessFailedKind));
        core.WebMessageReceived+=Message;
        core.SetVirtualHostNameToFolderMapping("sierra-prototype.invalid",Path.Combine(AppContext.BaseDirectory,"web"),CoreWebView2HostResourceAccessKind.DenyCors);
        core.Navigate(Origin+"/index.html");
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
        File.WriteAllText(Path.Combine(directory,$"startup-{cycle}.json"),JsonSerializer.Serialize(new{cycle,readyMilliseconds=startup.Elapsed.TotalMilliseconds,runtime=environment.BrowserVersionString,browserPid},json));
        sampler.Start();
        var start=new GeoPoint(151.1772,-33.9461);var end=new GeoPoint(-.4543,51.4700);var now=new DateTimeOffset(2026,9,24,12,0,0,TimeSpan.Zero);
        var snapshot=new MapSnapshot(1,cycle,1,now,
            [new("synthetic-001","S4P001",null,start,36000,315,450,MapEntitySource.Simulated,now,MapReliability.Current,MapAircraftStatus.Airborne,true)],
            [new("YSSY","YSSY","SYD",start,"Sydney",MapSeverity.Unknown,null,null,null,null),new("EGLL","EGLL","LHR",end,"London Heathrow",MapSeverity.Unknown,null,null,null,null)],
            [new("prototype-route","YSSY","EGLL",null,null,MapRouteKind.Planned,MapEntitySource.Simulated,Geography.GreatCircle(start,end),[],null,MapReliability.Current)],[],[]);
        core.PostWebMessageAsJson(JsonSerializer.Serialize(new{type="snapshot",snapshot},json));
    }
    private void Message(object? sender,CoreWebView2WebMessageReceivedEventArgs e)
    {
        if(!e.Source.StartsWith(Origin+"/",StringComparison.Ordinal)||e.WebMessageAsJson.Length>64000)return;
        using var doc=JsonDocument.Parse(e.WebMessageAsJson);var root=doc.RootElement;
        if(!root.TryGetProperty("type",out var type))return;
        switch(type.GetString())
        {
            case "ready": ready.TrySetResult();break;
            case "phase": metrics.Start(environment!);break;
            case "benchmark":
                var count=root.GetProperty("count").GetInt32();
                File.WriteAllText(Path.Combine(directory,$"benchmark-{count}.json"),JsonSerializer.Serialize(new{renderer=root.Clone(),process=metrics.Finish(environment!)},json));break;
            case "checks": File.WriteAllText(Path.Combine(directory,$"renderer-checks-{cycle}.json"),root.GetRawText());break;
            case "error": complete.TrySetException(new InvalidOperationException(root.GetProperty("message").GetString()));ready.TrySetException(new InvalidOperationException(root.GetRawText()));break;
            case "complete": complete.TrySetResult();break;
        }
    }
    internal async Task VerifyAsync(bool benchmark)
    {
        await web.CoreWebView2.ExecuteScriptAsync($"window.prototype.runDiagnostics({benchmark.ToString().ToLowerInvariant()})");
        await complete.Task.WaitAsync(TimeSpan.FromMinutes(3));
        using var stream=File.Create(Path.Combine(directory,$"prototype-{cycle}.png"));
        await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
        var info=new{os=Environment.OSVersion.ToString(),logicalProcessors=Environment.ProcessorCount,hostBitness=Environment.Is64BitProcess,windowWidth=ActualWidth,windowHeight=ActualHeight};
        File.WriteAllText(Path.Combine(directory,"environment.json"),JsonSerializer.Serialize(info,json));
    }
    internal void DisposeRenderer()
    {
        sampler.Stop();web.Dispose();environment=null;
    }
}

internal sealed class ProcessMetrics
{
    private readonly Dictionary<int,double> baseline=new();
    private readonly Dictionary<int,double> latest=new();
    private readonly Stopwatch clock=new();
    private long peakWorkingSet;
    internal void Start(CoreWebView2Environment environment){baseline.Clear();latest.Clear();peakWorkingSet=0;clock.Restart();Sample(environment);}
    internal void Sample(CoreWebView2Environment environment)
    {
        long working=0;
        foreach(var id in environment.GetProcessInfos().Select(p=>p.ProcessId).Append(Environment.ProcessId).Distinct())
        {
            try { using var p=Process.GetProcessById(id);var cpu=p.TotalProcessorTime.TotalMilliseconds;if(!baseline.ContainsKey(id))baseline[id]=cpu;latest[id]=cpu;working+=p.WorkingSet64; }
            catch(ArgumentException){} catch(InvalidOperationException){} catch(System.ComponentModel.Win32Exception){}
        }
        peakWorkingSet=Math.Max(working,peakWorkingSet);
    }
    internal object Finish(CoreWebView2Environment environment)
    {
        Sample(environment);var cpu=latest.Sum(p=>p.Value-baseline.GetValueOrDefault(p.Key));
        return new{approxCpuMilliseconds=cpu,elapsedMilliseconds=clock.Elapsed.TotalMilliseconds,approxCpuPercentMachine=100*cpu/clock.Elapsed.TotalMilliseconds/Environment.ProcessorCount,peakSummedWorkingSetMiB=peakWorkingSet/1048576d,processCount=latest.Count,scope="Host plus isolated WebView process group; summed working set may count shared pages more than once; 500ms samples"};
    }
}
