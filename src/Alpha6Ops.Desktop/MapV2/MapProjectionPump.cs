using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha6Ops.Desktop.MapV2;

// Owns only visible presentation history. Never called back by a renderer.
internal sealed class MapProjectionPump : IAsyncDisposable
{
    private sealed record Input(FlightSessionSnapshot State,FlightTrackPresentation? Track,bool Demo,DateTimeOffset At,long Continuity,long VisibilityContinuity);
    private readonly LatestMapMailbox<Input> mailbox;
    private readonly object producerSync=new();
    private FlightSessionSnapshot? previous;
    private long continuity,visibilityContinuity;
    private bool visible=true;
    internal long Offered=>mailbox.Offered;
    internal long Coalesced=>mailbox.Coalesced;
    internal MapProjectionPump(IMapRenderer renderer,long generation,MapBridgeMetrics metrics,Action<Exception> failed)
    {
        var projection=new MapProjectionService();long sequence=0,projectedContinuity=-1,projectedVisibility=-1;
        mailbox=new(async (input,token)=>
        {
            token.ThrowIfCancellationRequested();
            lock(producerSync)if(!visible||input.Continuity!=continuity||input.VisibilityContinuity!=visibilityContinuity)return;
            var watch=Stopwatch.StartNew();
            if(projectedContinuity!=input.Continuity){projection.Reset();projectedContinuity=input.Continuity;}
            else if(projectedVisibility!=input.VisibilityContinuity)projection.BreakContinuity();
            projectedVisibility=input.VisibilityContinuity;
            var snapshot=projection.Project(input.State,input.Track,input.Demo,generation,++sequence,input.At);
            metrics.ProjectionMs.Add(watch.Elapsed.TotalMilliseconds);
            Interlocked.Increment(ref metrics.Produced);
            PublishIfCurrent(renderer,snapshot,input.Continuity,input.VisibilityContinuity);
            await Task.CompletedTask;
        },failed,TimeSpan.FromMilliseconds(200));
        metrics.ProjectionQueue=()=>new{offered=mailbox.Offered,coalesced=mailbox.Coalesced,consumed=mailbox.Consumed,pending=mailbox.HasPending};
    }
    private void PublishIfCurrent(IMapRenderer renderer,MapSnapshot snapshot,long inputContinuity,long inputVisibility)
    {
        // Projection runs outside the producer lock. Visibility may have changed
        // while it ran, including a complete hide/return and fresh bridge mailbox.
        // Offer only enqueues; keep its final eligibility check atomic with hide.
        lock(producerSync)
        {
            if(!visible||inputContinuity!=continuity||inputVisibility!=visibilityContinuity)return;
            renderer.Offer(snapshot);
        }
    }
    internal void Offer(FlightSessionSnapshot state,bool demo)=>Offer(state,null,demo);
    internal void Offer(FlightSessionSnapshot state,FlightTrackPresentation? track,bool demo)
    {
        // Latch interruptions BEFORE coalescing. Work here stays constant per telemetry sample.
        lock(producerSync)
        {
            if(!visible)return;
            var sample=state.Telemetry;
            var interrupted=demo||!state.DataCurrent||!state.Connected||state.SuppressObservedFlight||sample is not{HasPosition:true,Paused:false,Slewing:false}||state.Reading?.Evidence?.SimulationRunning==false;
            if(previous is {} prior)
            {
                interrupted|=prior.ConnectionGeneration!=state.ConnectionGeneration||MapProjectionService.Identity(prior)!=MapProjectionService.Identity(state);
                if(prior.Telemetry is {} before&&sample is not null)
                {
                    var seconds=(sample.At-before.At).TotalSeconds;
                    interrupted|=seconds<0||seconds>15;
                    if(before.HasPosition&&sample.HasPosition)
                        interrupted|=MapGeography.Angle(new(before.LongitudeDegrees,before.LatitudeDegrees),new(sample.LongitudeDegrees,sample.LatitudeDegrees))*3440.065>2+Math.Max(0,seconds)/3600*1500;
                }
            }
            if(interrupted)continuity++;
            previous=state;
            mailbox.Offer(new(state,track,demo,DateTimeOffset.UtcNow,continuity,visibilityContinuity));
        }
    }
    internal void SetVisible(bool value)
    {
        lock(producerSync)
        {
            if(visible==value)return;
            visible=value;
            // Preserve the renderer's sequence, but never join observations
            // across a workspace interval in which this map was not observing.
            visibilityContinuity++;previous=null;
        }
    }
    public ValueTask DisposeAsync()=>mailbox.DisposeAsync();
}
