using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Alpha6Ops.Desktop.MapV2;

internal sealed class MapMeasurement
{
    private readonly object sync=new();
    private readonly Queue<double> samples=new();
    internal void Add(double value) { lock(sync){if(samples.Count==4096)samples.Dequeue();samples.Enqueue(value);} }
    internal object Read() { lock(sync){var sorted=samples.Order().ToArray();return new {count=sorted.Length,mean=sorted.Length==0?0:sorted.Average(),p50=sorted.Length==0?0:sorted[(int)((sorted.Length-1)*.50)],p95=sorted.Length==0?0:sorted[(int)((sorted.Length-1)*.95)],p99=sorted.Length==0?0:sorted[(int)((sorted.Length-1)*.99)],max=sorted.LastOrDefault()};} }
}
internal sealed class MapBridgeMetrics
{
    internal readonly MapMeasurement ProjectionMs=new(),SerializationMs=new(),DispatcherQueueMs=new(),PostMs=new(),AcknowledgmentMs=new(),PostToAcknowledgmentMs=new();
    internal long Rejected,PayloadBytes,Produced;
    internal double ReadyMs;
    internal Func<object>? ProjectionQueue;
    internal object Read(LatestMapMailbox<MapSnapshot>? mailbox)=>new
    {
        produced=Interlocked.Read(ref Produced),offered=mailbox?.Offered??0,coalesced=mailbox?.Coalesced??0,acknowledged=mailbox?.Consumed??0,
        pending=mailbox?.HasPending??false,rejected=Interlocked.Read(ref Rejected),lastPayloadBytes=Interlocked.Read(ref PayloadBytes),readyMs=ReadyMs,
        projectionQueue=ProjectionQueue?.Invoke(),
        projectionMs=ProjectionMs.Read(),serializationMs=SerializationMs.Read(),dispatcherQueueMs=DispatcherQueueMs.Read(),postMs=PostMs.Read(),acknowledgmentMs=AcknowledgmentMs.Read(),postToAcknowledgmentMs=PostToAcknowledgmentMs.Read()
    };
}
