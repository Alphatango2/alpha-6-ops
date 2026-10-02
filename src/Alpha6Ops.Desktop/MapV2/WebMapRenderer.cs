using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Alpha6Ops.Desktop.MapV2;

// The only production component that knows about WebView2 and its message protocol.
internal sealed class WebMapRenderer : IMapRenderer
{
    internal const string Origin="https://sierra-map.invalid";
    private readonly IWebView2 browser;
    private readonly string assets,profile;
    private readonly Func<string> runtimeVersion;
    private readonly CancellationTokenSource lifetime=new();
    private readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object ackLock=new();
    private readonly long epoch;
    private LatestMapMailbox<MapSnapshot>? mailbox;
    private TaskCompletionSource? acknowledgment;
    private MapSnapshot? applying,applied;
    private long lastSequence;
    private long visibilityVersion,applyingVisibilityVersion;
    private Task visibilityTransition=Task.CompletedTask;
    private volatile bool paused;
    private bool disposed,failed,visibleReady;
    private MapPresentationState? presentation;
    internal CoreWebView2Environment? Environment { get; private set; }
    internal CoreWebView2 Core=>browser.CoreWebView2;
    internal MapBridgeMetrics Metrics { get; }=new();
    internal long Epoch=>epoch;
    internal object Measurements=>Metrics.Read(mailbox);
    internal Task Ready=>ready.Task;
    internal bool Paused=>paused;
    public FrameworkElement View { get; }
    public event Action<MapSelection?>? SelectionChanged;
    public event Action<MapCameraState>? CameraChanged;
    public event Action<MapRendererStatus>? StatusChanged;
    internal event Action<ImmutableHashSet<MapLayer>,bool>? PreferencesChanged;
    private static readonly HashSet<string> ResourcePaths=new(StringComparer.Ordinal)
    {"/index.html","/map.mjs","/map-math.mjs","/map.css","/theme.json","/land.geojson",
        "/vendor/maplibre-gl.mjs","/vendor/maplibre-gl-shared.mjs","/vendor/maplibre-gl-worker.mjs"};

    internal WebMapRenderer(long epoch,string profile,bool composition=true,string? assetDirectory=null,Func<string>? runtimeVersion=null)
    {
        this.epoch=epoch;this.profile=profile;
        assets=assetDirectory??Path.Combine(AppContext.BaseDirectory,"MapV2","Web");
        this.runtimeVersion=runtimeVersion??(()=>CoreWebView2Environment.GetAvailableBrowserVersionString());
        if(composition)
        {
            var control=new GuardedCompositionControl();
            control.InitializationFault+=()=>control.Dispatcher.BeginInvoke(()=>Fail("composition-initialization-failed"));
            // The SDK forwards first; stop the parent ScrollViewer taking focus
            // and cancelling MapLibre's drag when this left press bubbles upward.
            control.AddHandler(UIElement.MouseDownEvent,new MouseButtonEventHandler((_,e)=>
            {
                if(e.ChangedButton==MouseButton.Left)e.Handled=true;
            }));
            browser=control;
        }
        else browser=new WebView2();
        View=(FrameworkElement)browser;View.Opacity=0;
        AutomationProperties.SetName(View,"Sierra flight map. Tab through controls or use Escape to leave the map.");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,lifetime.Token);
        linked.CancelAfter(TimeSpan.FromSeconds(10));var token=linked.Token;
        var watch=Stopwatch.StartNew();
        try
        {
            if(string.IsNullOrWhiteSpace(runtimeVersion()))throw new InvalidOperationException("WebView2 runtime unavailable");
            foreach(var file in new[]{"index.html","map.mjs","map-math.mjs","map.css","land.geojson","theme.json","vendor/maplibre-gl.mjs","vendor/maplibre-gl-worker.mjs","vendor/maplibre-gl-shared.mjs"})
                if(!File.Exists(Path.Combine(assets,file)))throw new FileNotFoundException("Bundled map resource unavailable");
            Environment=await CoreWebView2Environment.CreateAsync(null,profile).WaitAsync(token);
            await browser.EnsureCoreWebView2Async(Environment).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            Core.Settings.AreHostObjectsAllowed=false;Core.Settings.AreDevToolsEnabled=false;
            Core.Settings.AreDefaultContextMenusEnabled=false;Core.Settings.IsStatusBarEnabled=false;
            Core.Settings.AreBrowserAcceleratorKeysEnabled=false;
            Core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
            Core.DownloadStarting+=(_,e)=>e.Cancel=true;
            Core.NewWindowRequested+=(_,e)=>e.Handled=true;
            Core.NavigationStarting+=(_,e)=>{if(!TrustedOrigin(e.Uri))e.Cancel=true;};
            Core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
            Core.WebResourceRequested+=(_,e)=>
            {
                if(!(TrustedOrigin(e.Request.Uri)&&ResourcePaths.Contains(new Uri(e.Request.Uri).AbsolutePath))&&!e.Request.Uri.StartsWith("data:image/png;",StringComparison.Ordinal)&&!e.Request.Uri.StartsWith("blob:"+Origin+"/",StringComparison.Ordinal))
                    e.Response=Environment.CreateWebResourceResponse(new MemoryStream(),403,"Blocked","Content-Type: text/plain");
            };
            Core.ProcessFailed+=(_,_)=>Fail("process-failed");
            Core.WebMessageReceived+=Receive;
            Core.SetVirtualHostNameToFolderMapping("sierra-map.invalid",assets,CoreWebView2HostResourceAccessKind.DenyCors);
            Core.Navigate(Origin+"/index.html");
            await ready.Task.WaitAsync(token);
            Metrics.ReadyMs=watch.Elapsed.TotalMilliseconds;
            mailbox=new(ApplyAsync,_=>Fail("bridge-failed"),TimeSpan.Zero);
            // The bridge can accept data now. Visible readiness waits for the
            // first snapshot's completed browser frame, not this handshake.
        }
        catch(Exception) when(!lifetime.IsCancellationRequested)
        {
            Fail("initialization-failed");throw;
        }
    }

    internal static bool TrustedOrigin(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host=="sierra-map.invalid"&&uri.IsDefaultPort&&string.IsNullOrEmpty(uri.UserInfo);
    public void Offer(MapSnapshot snapshot)
    {
        if(!disposed&&!failed&&!paused)mailbox?.Offer(snapshot);
    }
    public void Present(MapPresentationState state)
    {
        View.Dispatcher.VerifyAccess();presentation=state;
        if(ready.Task.IsCompletedSuccessfully&&!disposed&&!failed&&!paused)
            Core.PostWebMessageAsJson(JsonSerializer.Serialize(new{type="presentation",schemaVersion=1,epoch,presentation=state},MapSnapshotValidator.JsonOptions));
    }
    internal Task SetVisibleAsync(bool visible)
    {
        View.Dispatcher.VerifyAccess();
        if(disposed||failed||paused==!visible)return visibilityTransition;
        paused=!visible;visibleReady=false;View.Opacity=0;
        var version=Interlocked.Increment(ref visibilityVersion);
        var stopped=Task.CompletedTask;
        if(!visible)
        {
            // Controller invisibility is required by TrySuspendAsync, including
            // a rapid hide/show while the previous bridge ACK is still pending.
            View.Visibility=Visibility.Collapsed;
            var old=mailbox;mailbox=null;
            if(old is not null)stopped=old.DisposeAsync().AsTask();
        }
        return visibilityTransition=ChangeVisibilityAsync(visibilityTransition,stopped,visible,version);
    }
    private async Task ChangeVisibilityAsync(Task preceding,Task stopped,bool visible,long version)
    {
        await preceding;await stopped;
        if(disposed||failed||version!=Interlocked.Read(ref visibilityVersion))return;
        lock(ackLock){applying=null;acknowledgment=null;}
        if(!visible)
        {
            Core.PostWebMessageAsJson(JsonSerializer.Serialize(new{type="visibility",schemaVersion=1,epoch,visible=false}));
            await Core.TrySuspendAsync();
        }
        else
        {
            Core.Resume();
            Core.PostWebMessageAsJson(JsonSerializer.Serialize(new{type="visibility",schemaVersion=1,epoch,visible=true}));
            mailbox=new(ApplyAsync,_=>Fail("bridge-failed"),TimeSpan.Zero);
            View.Visibility=Visibility.Visible;
        }
    }
    private async Task ApplyAsync(MapSnapshot snapshot,CancellationToken token)
    {
        if(snapshot.Generation!=epoch||snapshot.Sequence<=Interlocked.Read(ref lastSequence)){Interlocked.Increment(ref Metrics.Rejected);return;}
        var watch=Stopwatch.StartNew();
        var bytes=MapSnapshotValidator.SerializeValidated(snapshot);
        // Serialize the neutral snapshot once on the worker; envelope adds only fixed metadata.
        var payload="{\"type\":\"snapshot\",\"schemaVersion\":1,\"epoch\":"+epoch+",\"snapshot\":"+Encoding.UTF8.GetString(bytes)+"}";
        if(Encoding.UTF8.GetByteCount(payload)>MapSnapshotValidator.MaxPayloadBytes)throw new ArgumentException("Map envelope budget exceeded");
        Metrics.SerializationMs.Add(watch.Elapsed.TotalMilliseconds);
        Interlocked.Exchange(ref Metrics.PayloadBytes,Encoding.UTF8.GetByteCount(payload));
        var ack=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock(ackLock){acknowledgment=ack;applying=snapshot;applyingVisibilityVersion=Interlocked.Read(ref visibilityVersion);}
        var queued=Stopwatch.StartNew();var ackTime=Stopwatch.StartNew();var posted=new Stopwatch();
        await View.Dispatcher.InvokeAsync(()=>
        {
            Metrics.DispatcherQueueMs.Add(queued.Elapsed.TotalMilliseconds);
            if(disposed||failed||paused)throw new OperationCanceledException(token);
            posted.Start();var post=Stopwatch.StartNew();Core.PostWebMessageAsJson(payload);Metrics.PostMs.Add(post.Elapsed.TotalMilliseconds);
        // Current visual data must reach the browser during input/composition,
        // not wait for WPF's Background/input-idle scheduling. Only the short
        // post runs here: projection/serialization stay on the mailbox worker,
        // with one in-flight update and one newest pending value. Render shares
        // composition turns fairly and remains below authoritative Normal work.
        },DispatcherPriority.Render,token).Task.ConfigureAwait(false);
        await ack.Task.WaitAsync(TimeSpan.FromSeconds(3),token).ConfigureAwait(false);
        Metrics.AcknowledgmentMs.Add(ackTime.Elapsed.TotalMilliseconds);
        Metrics.PostToAcknowledgmentMs.Add(posted.Elapsed.TotalMilliseconds);
        Interlocked.Exchange(ref lastSequence,snapshot.Sequence);
        lock(ackLock){applied=snapshot;applying=null;acknowledgment=null;}
    }
    private void Receive(object? sender,CoreWebView2WebMessageReceivedEventArgs e)=>ReceiveMessage(e.Source,e.WebMessageAsJson);
    internal void ReceiveMessage(string source,string json)
    {
        if(disposed||failed)return;
        try
        {
            if(!TrustedOrigin(source)||json.Length>16384)throw new ArgumentException();
            using var document=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=12});var root=document.RootElement;
            if(root.GetProperty("schemaVersion").GetInt32()!=1)throw new ArgumentException();
            var type=root.GetProperty("type").GetString();
            if(type=="ready")
            {
                if(!ready.Task.IsCompleted)Core.PostWebMessageAsJson(JsonSerializer.Serialize(new{type="initialize",schemaVersion=1,epoch,presentation},MapSnapshotValidator.JsonOptions));
                return;
            }
            if(root.GetProperty("epoch").GetInt64()!=epoch)throw new ArgumentException();
            if(type=="initialized"){ready.TrySetResult();return;}
            if(type=="error")
            {
                var detail=root.TryGetProperty("reason",out var reason)&&reason.ValueKind==JsonValueKind.String?reason.GetString():null;
                Fail("renderer-error",detail is{Length:>160}?detail[..160]:detail);return;
            }
            if(type=="ack")
            {
                lock(ackLock)
                {
                    if(applying is null||root.GetProperty("generation").GetInt64()!=applying.Generation||root.GetProperty("sequence").GetInt64()!=applying.Sequence)throw new ArgumentException();
                    if(!visibleReady&&!paused&&applyingVisibilityVersion==Interlocked.Read(ref visibilityVersion))
                    {
                        visibleReady=true;View.Opacity=1;
                        StatusChanged?.Invoke(new(MapRendererState.Ready,"ready",null));
                    }
                    acknowledgment?.TrySetResult();
                }
                return;
            }
            if(type=="selection")
            {
                var selectedSequence=root.GetProperty("sequence").GetInt64();
                MapSnapshot? snapshot;lock(ackLock)snapshot=applying?.Sequence==selectedSequence?applying:applied?.Sequence==selectedSequence?applied:null;
                if(snapshot is null)throw new ArgumentException();
                var id=root.GetProperty("id").GetString();var kind=root.GetProperty("kind").GetString();
                if(string.IsNullOrEmpty(id)){SelectionChanged?.Invoke(null);return;}
                var valid=kind switch{"Aircraft"=>snapshot.Aircraft.Any(a=>a.Id==id),"Airport"=>snapshot.Airports.Any(a=>a.Id==id),"Route"=>snapshot.Routes.Any(a=>a.Id==id),_=>false};
                if(!valid)throw new ArgumentException();
                SelectionChanged?.Invoke(new(Enum.Parse<MapEntityKind>(kind!),id));return;
            }
            if(type=="camera")
            {
                var longitude=root.GetProperty("longitude").GetDouble();var latitude=root.GetProperty("latitude").GetDouble();
                var zoom=root.GetProperty("zoom").GetDouble();var bearing=root.GetProperty("bearing").GetDouble();
                if(!double.IsFinite(zoom)||zoom<0||zoom>18||!double.IsFinite(bearing))throw new ArgumentException();
                var followId=root.TryGetProperty("followId",out var follow)?follow.GetString():null;
                MapSnapshot? current;lock(ackLock)current=applying??applied;
                if(followId is not null&&current?.Aircraft.Any(a=>a.Id==followId)!=true)throw new ArgumentException();
                CameraChanged?.Invoke(new(new(longitude,latitude),zoom,bearing,0,followId));return;
            }
            if(type=="presentationPreferences")
            {
                var layers=root.GetProperty("enabledLayers");
                if(layers.GetArrayLength()>Enum.GetValues<MapLayer>().Length)throw new ArgumentException();
                var parsed=layers.EnumerateArray().Select(x=>Enum.Parse<MapLayer>(x.GetString()!)).ToImmutableHashSet();
                if(parsed.Any(x=>!Enum.IsDefined(x)))throw new ArgumentException();
                PreferencesChanged?.Invoke(parsed,root.GetProperty("reducedMotion").GetBoolean());return;
            }
            if(type=="bypass") { View.MoveFocus(new(System.Windows.Input.FocusNavigationDirection.Next));return; }
            throw new ArgumentException();
        }
        catch(Exception error) when(error is JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { Interlocked.Increment(ref Metrics.Rejected); }
    }
    private void Fail(string reason,string? detail=null)
    {
        if(!View.Dispatcher.CheckAccess()){View.Dispatcher.BeginInvoke(()=>Fail(reason,detail));return;}
        if(disposed||failed)return;failed=true;ready.TrySetException(new InvalidOperationException(reason));
        _=ready.Task.Exception; // Initialization can fail before anyone awaits readiness; observe its fault.
        StatusChanged?.Invoke(new(MapRendererState.Failed,reason,detail));
    }
    public async ValueTask DisposeAsync()
    {
        View.Dispatcher.VerifyAccess();if(disposed)return;disposed=true;lifetime.Cancel();
        if(mailbox is not null)await mailbox.DisposeAsync();
        try{await visibilityTransition;}catch(Exception){/* Disposal still owns the browser after a failed suspension. */}
        ((IDisposable)browser).Dispose();Environment=null;
        SelectionChanged=null;CameraChanged=null;StatusChanged=null;PreferencesChanged=null;
    }
    // Composition initializes graphics during WPF layout, outside InitializeAsync's awaited call.
    // Contain that failure at the map boundary rather than letting it terminate flight processing.
    private sealed class GuardedCompositionControl:WebView2CompositionControl
    {
        internal event Action? InitializationFault;
        private bool templateFailed;
        public override void OnApplyTemplate()
        {
            if(templateFailed)return;
            try{base.OnApplyTemplate();}
            catch(Exception){templateFailed=true;InitializationFault?.Invoke();}
        }
    }
}
