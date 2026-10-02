using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Alpha6Ops.Core;
using Alpha6Ops.Desktop.MapV2;

namespace Alpha6Ops.Desktop;

// Owns presentation lifetimes only. The coordinator is never referenced here.
internal sealed class OperationsMapHost : ContentControl
{
    // Remains false until every default-enablement gate is demonstrated.
    internal static bool DefaultV2Enabled => false;
    internal FlightRouteMap Renderer { get; private set; }=new();
    internal WebMapRenderer? V2 { get; private set; }
    internal int RecreationCount { get; private set; }
    internal int RestartCount { get; private set; }
    internal string? LastFailure { get; private set; }
    internal bool UseV2 { get; private set; }=DefaultV2Enabled||Environment.GetEnvironmentVariable("SIERRA4_MAP_V2")=="1";
    internal string? DiagnosticProfile { get; set; }
    internal bool CompositionHost { get; set; }=true;
    internal string? DiagnosticAssets { get; set; }
    internal Func<string>? DiagnosticRuntimeVersion { get; set; }
    internal Func<WebMapRenderer>? DiagnosticRendererFactory { get; set; }
    internal event Action? LayoutRequested;
    internal event Action<string>? ContextChanged;
    private readonly ContentControl viewport=new();
    private readonly TextBlock status=new(){FontSize=11,VerticalAlignment=VerticalAlignment.Center,Foreground=new SolidColorBrush(Color.FromRgb(142,153,162))};
    private readonly Button toggle=new(){FontSize=11,Padding=new Thickness(9,3,9,3),Margin=new Thickness(8,0,0,0),MinHeight=24};
    private string? sessionKey;
    private bool hadPosition;
    private FlightRoutePoint[] route=[];
    private Telemetry? lastTelemetry;
    private double? lastProgress;
    private OperationsOverview? latest;
    private MapProjectionPump? pump;
    private CancellationTokenSource? lifetime;
    private Task? activation;
    private Window? ownerWindow;
    private readonly DispatcherTimer dormantExpiry=new(){Interval=TimeSpan.FromMinutes(5)};
    private long serial;
    private static long nextEpoch;
    private MapPresentationState viewState=new(new(new(15,20),1.1,0,0,null),null,
        Enum.GetValues<MapLayer>().ToImmutableHashSet(),!SystemParameters.ClientAreaAnimation,DateTimeOffset.UtcNow);

    public OperationsMapHost()
    {
        var root=new Grid{ClipToBounds=true,Background=new SolidColorBrush(Color.FromRgb(9,12,15))};
        root.RowDefinitions.Add(new());root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        viewport.Content=Renderer;root.Children.Add(viewport);
        var footer=new DockPanel{Margin=new Thickness(12,4,12,4)};
        DockPanel.SetDock(toggle,Dock.Right);footer.Children.Add(toggle);footer.Children.Add(status);
        Grid.SetRow(footer,1);root.Children.Add(footer);Content=root;
        AutomationProperties.SetName(toggle,"Switch Operations map renderer");
        toggle.Click+=(_,_)=>SetRenderer(!UseV2);
        dormantExpiry.Tick+=(_,_)=>{dormantExpiry.Stop();if(!PresentationVisible)DisposeRenderer();};
        Loaded+=(_,_)=>{ownerWindow=Window.GetWindow(this);if(ownerWindow is not null)ownerWindow.StateChanged+=WindowStateChanged;Activate();};
        IsVisibleChanged+=(_,_)=>{if(IsVisible)Activate();else EndVisibleTrail();};
        Unloaded+=(_,_)=>{if(ownerWindow is not null)ownerWindow.StateChanged-=WindowStateChanged;ownerWindow=null;DisposeRenderer();};UpdateFooter();
    }
    private void WindowStateChanged(object? sender,EventArgs e)
    {if(ownerWindow?.WindowState==WindowState.Minimized)EndVisibleTrail();else Activate();}
    internal void SetRenderer(bool v2)
    {
        UseV2=v2;RestartCount=0;DisposeRenderer();
        if(!v2&&latest is {} overview)PresentLegacy(overview);
        UpdateFooter();Activate();
    }
    private void UpdateFooter(string? message=null)
    {
        LayoutRequested?.Invoke();
        toggle.Content=UseV2?"Use classic map":"Use Map V2";
        status.Text=message??(UseV2?"Offline geography · Map V2":"Classic map · Map V2 available");
    }
    private void Activate()
    {
        if(!UseV2||!PresentationVisible)return;
        dormantExpiry.Stop();
        if(V2 is {} retained)
        {
            if(retained.Paused)activation=ResumeAsync(retained,serial);
            return;
        }
        if(activation is {IsCompleted:false})return;
        activation=ActivateAsync();
    }
    private bool PresentationVisible=>IsLoaded&&IsVisible&&ownerWindow?.WindowState!=WindowState.Minimized;
    private async Task ResumeAsync(WebMapRenderer renderer,long ticket)
    {
        try
        {
            await renderer.SetVisibleAsync(true);
            if(ticket!=serial||!PresentationVisible||renderer.Paused)return;
            pump?.SetVisible(true);
            if(latest?.MapState is {} state)pump?.Offer(state,latest.MapTrack,latest.MapDemo);
            UpdateContext();
        }
        catch(Exception error){LastFailure=error.ToString();if(ticket==serial)await RecoverAsync(ticket);}
    }
    private async Task ActivateAsync()
    {
        var ticket=++serial;var tokenSource=lifetime=new();var epoch=Interlocked.Increment(ref nextEpoch);
        var profile=DiagnosticProfile??Path.Combine(CrashReporter.DiagnosticMode?CrashReporter.RootDirectory:ProductStorage.Current.Cache,"MapCache");
        try
        {
        var renderer=DiagnosticRendererFactory?.Invoke()??new WebMapRenderer(epoch,profile,CompositionHost,DiagnosticAssets,DiagnosticRuntimeVersion);
        V2=renderer;RecreationCount++;viewport.Content=renderer.View;UpdateFooter("Opening offline map…");
        renderer.SelectionChanged+=selection=>{if(ticket==serial)viewState=viewState with{Selection=selection};};
        renderer.CameraChanged+=camera=>{if(ticket==serial)viewState=viewState with{Camera=camera,RestoreCamera=true};};
        renderer.PreferencesChanged+=(layers,reduced)=>{if(ticket==serial)viewState=viewState with{EnabledLayers=layers,ReducedMotion=reduced};};
        renderer.StatusChanged+=state=>
        {
            if(ticket!=serial)return;
            if(state.State==MapRendererState.Failed){LastFailure=state.ReasonCode+": "+state.Detail;_=RecoverAsync(ticket);}
            else if(state.State==MapRendererState.Ready){UpdateFooter();UpdateContext();}
        };
            await renderer.InitializeAsync(tokenSource.Token);
            if(ticket!=serial)return;
            renderer.Present(viewState);
            pump=new(renderer,epoch,renderer.Metrics,error=>Dispatcher.BeginInvoke(()=>{if(ticket==serial)_=RecoverAsync(ticket);}));
            if(latest?.MapState is {} state)pump.Offer(state,latest.MapTrack,latest.MapDemo);
            UpdateContext();
        }
        catch(Exception error) { LastFailure=error.ToString();if(ticket==serial)await RecoverAsync(ticket); }
    }
    private async Task RecoverAsync(long ticket)
    {
        if(ticket!=serial)return;
        CrashReporter.Write("map_renderer",null,new{reason="presentation-renderer-unavailable",restartCount=RestartCount},Path.Combine(CrashReporter.DiagnosticMode?CrashReporter.RootDirectory:ProductStorage.Current.Diagnostics,"MapDiagnostics"));
        DisposeRenderer();
        if(UseV2&&RestartCount<1)
        {
            RestartCount++;
            UpdateFooter("Map restarting…");await Task.Delay(250);
            if(UseV2&&IsVisible&&V2 is null){activation=null;Activate();}
        }
        else
        {
            UseV2=false;viewport.Content=Renderer;
            if(latest is {} overview)PresentLegacy(overview);
            UpdateFooter("Map V2 unavailable · classic map active");ContextChanged?.Invoke(latest?.MapContext??"Map unavailable");
        }
    }
    internal void EndVisibleTrail()
    {
        sessionKey=null;
        pump?.SetVisible(false);
        if(V2 is not {} renderer)return;
        // There is no reusable presentation before initialization completes.
        if(pump is null||!renderer.Ready.IsCompletedSuccessfully){DisposeRenderer();return;}
        dormantExpiry.Stop();dormantExpiry.Start();
        _=PauseAsync(renderer,serial);
    }
    private async Task PauseAsync(WebMapRenderer renderer,long ticket)
    {
        try{await renderer.SetVisibleAsync(false);}
        catch(Exception error){LastFailure=error.ToString();if(ticket==serial)await RecoverAsync(ticket);}
    }
    private void DisposeRenderer()
    {
        dormantExpiry.Stop();
        sessionKey=null;serial++;lifetime?.Cancel();lifetime=null;
        var oldPump=pump;pump=null;if(oldPump is not null)_=oldPump.DisposeAsync();
        var old=V2;V2=null;if(old is not null)_=old.DisposeAsync();viewport.Content=Renderer;
        if(activation is {IsCompleted:false})_=activation.ContinueWith(_=>Dispatcher.BeginInvoke(new Action(Activate)));
    }
    internal async Task WaitReadyAsync()
    {
        Activate();if(activation is {} pending)await pending;
        if(V2 is not null)await V2.Ready;
    }
    internal void Present(OperationsOverview overview)
    {
        latest=overview;
        if(UseV2)
        {
            if(PresentationVisible&&V2?.Paused!=true&&overview.MapState is {} state)pump?.Offer(state,overview.MapTrack,overview.MapDemo);
            Activate();UpdateContext();
        }
        else PresentLegacy(overview);
    }
    private void UpdateContext()
    {
        if(!UseV2||latest?.MapState is not {} state)return;
        ContextChanged?.Invoke(latest.MapDemo?"Replay preview · no live map traffic":state.DataCurrent&&state.Connected&&state.Telemetry is{HasPosition:true,Paused:false,Slewing:false}&&state.Reading?.Evidence?.SimulationRunning!=false&&!state.SuppressObservedFlight
            ?"Current flight · offline geography":"Planned geography · no current aircraft");
    }
    private void PresentLegacy(OperationsOverview overview)
    {
        var hasPosition=overview.MapTelemetry is not null;
        if(sessionKey!=overview.MapSessionKey||hadPosition&&!hasPosition)
        {
            Renderer=new FlightRouteMap();viewport.Content=Renderer;route=[];lastTelemetry=null;lastProgress=null;
            sessionKey=overview.MapSessionKey;
        }
        var routeChanged=!route.SequenceEqual(overview.MapRoute);
        if(routeChanged){route=overview.MapRoute.ToArray();Renderer.SetRoute(route);}
        if(hasPosition&&(routeChanged||lastTelemetry!=overview.MapTelemetry||lastProgress!=overview.MapProgress))Renderer.SetTelemetry(overview.MapTelemetry,overview.MapProgress);
        lastTelemetry=overview.MapTelemetry;lastProgress=overview.MapProgress;hadPosition=hasPosition;
    }
}
