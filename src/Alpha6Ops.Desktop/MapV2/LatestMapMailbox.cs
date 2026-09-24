using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha6Ops.Desktop.MapV2;

internal sealed class LatestMapMailbox<T> : IAsyncDisposable where T : class
{
    private readonly object sync=new();
    private readonly SemaphoreSlim wake=new(0,1);
    private readonly CancellationTokenSource stop=new();
    private readonly Task worker;
    private T? pending;
    private bool closed;
    private long offered,coalesced,consumed;
    internal LatestMapMailbox(Func<T,CancellationToken,Task> consume, Action<Exception> failed, TimeSpan cadence)
    {
        worker=Task.Run(async ()=>
        {
            try
            {
                while(!stop.IsCancellationRequested)
                {
                    await wake.WaitAsync(stop.Token).ConfigureAwait(false);
                    await Task.Delay(cadence,stop.Token).ConfigureAwait(false);
                    T? value;
                    lock(sync){value=pending;pending=null;}
                    if(value is null)continue;
                    await consume(value,stop.Token).ConfigureAwait(false);
                    Interlocked.Increment(ref consumed);
                }
            }
            catch(OperationCanceledException) when(stop.IsCancellationRequested) { }
            catch(Exception error)
            {
                lock(sync){closed=true;pending=null;}
                failed(error);
            }
        });
    }
    internal long Offered => Interlocked.Read(ref offered);
    internal long Coalesced => Interlocked.Read(ref coalesced);
    internal long Consumed => Interlocked.Read(ref consumed);
    internal bool HasPending { get { lock(sync)return pending is not null; } }
    internal void Offer(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock(sync)
        {
            if(closed)return;
            Interlocked.Increment(ref offered);
            if(pending is not null)Interlocked.Increment(ref coalesced);
            pending=value;
            if(wake.CurrentCount==0)wake.Release();
        }
    }
    public async ValueTask DisposeAsync()
    {
        lock(sync){closed=true;pending=null;}
        stop.Cancel();
        await worker.ConfigureAwait(false);
        // Synchronization objects intentionally remain valid for idempotent disposal.
        // They own no wait handle unless WaitHandle is requested (it never is here).
    }
}
