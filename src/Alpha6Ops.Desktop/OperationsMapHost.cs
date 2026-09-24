using System;
using System.Linq;
using System.Windows.Controls;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

// Replaceable presentation boundary for Map V2. The geographic renderer is unchanged.
internal sealed class OperationsMapHost : ContentControl
{
    internal FlightRouteMap Renderer { get; private set; } = new();
    private string? sessionKey;
    private bool hadPosition;
    private FlightRoutePoint[] route = [];
    private Telemetry? lastTelemetry;
    private double? lastProgress;

    public OperationsMapHost() { Content=Renderer; }

    // Samples are deliberately not rendered while hidden. End the visible trail
    // so reopening cannot join two positions across an unobserved interval.
    internal void EndVisibleTrail() => sessionKey=null;

    internal void Present(OperationsOverview overview)
    {
        var hasPosition=overview.MapTelemetry is not null;
        // Recreate presentation only on session change or loss of current position, so
        // the renderer cannot retain an old aircraft marker/track as live information.
        if(sessionKey!=overview.MapSessionKey || hadPosition && !hasPosition)
        {
            Renderer=new FlightRouteMap();Content=Renderer;route=[];lastTelemetry=null;lastProgress=null;
            sessionKey=overview.MapSessionKey;
        }
        var routeChanged=!route.SequenceEqual(overview.MapRoute);
        if(routeChanged) { route=overview.MapRoute.ToArray();Renderer.SetRoute(route); }
        if(hasPosition && (routeChanged || lastTelemetry!=overview.MapTelemetry || lastProgress!=overview.MapProgress))
            Renderer.SetTelemetry(overview.MapTelemetry,overview.MapProgress);
        lastTelemetry=overview.MapTelemetry;lastProgress=overview.MapProgress;hadPosition=hasPosition;
    }
}
