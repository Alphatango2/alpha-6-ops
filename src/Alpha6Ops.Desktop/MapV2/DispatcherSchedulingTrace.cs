using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Alpha6Ops.Desktop.MapV2;

// Explicit scheduling diagnostics only. Bounded in-memory evidence, never payloads.
internal sealed class DispatcherSchedulingTrace : EventListener
{
    internal readonly record struct Row(double T,string Kind,long Id,string Name,int Priority,double Value,int Thread);
    private sealed record Operation(long Id,string Name);
    private readonly ConcurrentQueue<Row> rows=new();
    private readonly ConcurrentDictionary<DispatcherOperation,Operation> operations=new();
    private readonly ConcurrentDictionary<MethodInfo,string> names=new();
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private readonly Dispatcher dispatcher;
    private readonly MainWindow window;
    private readonly DispatcherTimer dashboard;
    private readonly ActivityListener activities;
    private readonly CancellationTokenSource stop=new();
    private readonly Task observer;
    private readonly List<object> os=new();
    private readonly int uiThread;
    private readonly bool renderObserver;
    private long sequence,recorded,dropped,hookTicks;
    private const int Limit=2_000_000;
    private bool disposed;
    private static readonly FieldInfo? MethodField=typeof(DispatcherOperation).GetField("_method",BindingFlags.Instance|BindingFlags.NonPublic);
    private static readonly FieldInfo? ArgsField=typeof(DispatcherOperation).GetField("_args",BindingFlags.Instance|BindingFlags.NonPublic);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    internal double Now=>clock.Elapsed.TotalMilliseconds;
    internal long Dropped=>Interlocked.Read(ref dropped);
    internal object EventSnapshot()=>new{timestampFrequency=Stopwatch.Frequency,events=rows.ToArray(),dropped=Dropped,partial=true};
    internal DispatcherSchedulingTrace(MainWindow window,DispatcherTimer dashboard,int[] browserPids,bool renderObserver=false)
    {
        this.window=window;this.dashboard=dashboard;this.renderObserver=renderObserver;dispatcher=window.Dispatcher;uiThread=(int)GetCurrentThreadId();
        dispatcher.Hooks.OperationPosted+=Posted;dispatcher.Hooks.OperationStarted+=Started;dispatcher.Hooks.OperationCompleted+=Completed;
        dispatcher.Hooks.OperationAborted+=Aborted;dispatcher.Hooks.OperationPriorityChanged+=Priority;
        window.LayoutUpdated+=Layout;window.SizeChanged+=Size;
        // Rendering subscribers themselves keep WPF's animation/render loop active.
        // Use existing render-operation envelopes by default; explicit observer control only.
        if(renderObserver)CompositionTarget.Rendering+=Rendering;
        activities=new(){ShouldListenTo=s=>s.Name is "Alpha6Ops.Presentation" or "Alpha6Ops.Recovery",Sample=(ref ActivityCreationOptions<ActivityContext> _)=>ActivitySamplingResult.AllData,
            ActivityStarted=a=>Mark("activity-begin",a.Source.Name+"/"+a.OperationName),ActivityStopped=a=>Mark("activity-end",a.Source.Name+"/"+a.OperationName,a.Duration.TotalMilliseconds)};
        ActivitySource.AddActivityListener(activities);
        // Existing EventSources may have been announced by EventListener's base ctor.
        foreach(var source in EventSource.GetSources())if(source.Name=="Microsoft-Windows-DotNETRuntime")EnableEvents(source,EventLevel.Informational,(EventKeywords)1);
        observer=Task.Run(async()=>
        {
            while(!stop.IsCancellationRequested)
            {
                var started=Stopwatch.GetTimestamp();
                try
                {
                    using var process=Process.GetCurrentProcess();
                    var ui=process.Threads.Cast<ProcessThread>().FirstOrDefault(t=>t.Id==uiThread);
                    var gc=GC.GetGCMemoryInfo();var browser=new List<object>();
                    foreach(var pid in browserPids)try{using var p=Process.GetProcessById(pid);browser.Add(new{pid,cpuMs=p.TotalProcessorTime.TotalMilliseconds,workingBytes=p.WorkingSet64});}catch(ArgumentException){}catch(InvalidOperationException){}
                    os.Add(new{t=Now,hostCpuMs=process.TotalProcessorTime.TotalMilliseconds,uiCpuMs=ui?.TotalProcessorTime.TotalMilliseconds,uiState=ui?.ThreadState.ToString(),
                        waitReason=ui?.ThreadState==System.Diagnostics.ThreadState.Wait?ui.WaitReason.ToString():null,allocatedBytes=GC.GetTotalAllocatedBytes(false),
                        gen0=GC.CollectionCount(0),gen1=GC.CollectionCount(1),gen2=GC.CollectionCount(2),gcIndex=gc.Index,gcPausesMs=gc.PauseDurations.ToArray().Select(p=>p.TotalMilliseconds).ToArray(),
                        heapBytes=gc.HeapSizeBytes,lohBytes=gc.GenerationInfo.Length>3?gc.GenerationInfo[3].SizeAfterBytes:0,browser,samplerMs=Stopwatch.GetElapsedTime(started).TotalMilliseconds});
                    foreach(ProcessThread t in process.Threads)t.Dispose();
                }
                catch(Exception e){Mark("os-error",e.GetType().Name);}
                await Task.Delay(250,stop.Token).ConfigureAwait(false);
            }
        });
    }
    internal void Mark(string kind,string name="",double value=0,long id=0,int priority=0)
    {
        if(rows is null)return; // EventListener base constructor can call the override.
        if(Interlocked.Increment(ref recorded)>Limit){Interlocked.Increment(ref dropped);return;}
        rows.Enqueue(new(Now,kind,id,name,priority,value,System.Environment.CurrentManagedThreadId));
    }
    private Operation Info(DispatcherOperation op)=>operations.GetOrAdd(op,o=>
    {
        var method=MethodField?.GetValue(o) as Delegate;
        var name=method is null?"unknown":names.GetOrAdd(method.Method,m=>(m.DeclaringType?.FullName??"")+"."+m.Name);
        var timer=method?.Target as DispatcherTimer??ArgsField?.GetValue(o) as DispatcherTimer;
        if(timer is not null)name+="/"+(ReferenceEquals(timer,dashboard)?"dashboard-clock":$"timer-{timer.Interval.TotalMilliseconds}ms");
        return new(Interlocked.Increment(ref sequence),name);
    });
    private void Event(DispatcherHookEventArgs e,string kind,bool remove=false)
    {
        long began=Stopwatch.GetTimestamp();var info=Info(e.Operation);
        Mark(kind,info.Name,0,info.Id,(int)e.Operation.Priority);
        if(remove)operations.TryRemove(e.Operation,out _);
        Interlocked.Add(ref hookTicks,Stopwatch.GetTimestamp()-began);
    }
    private void Posted(object? sender,DispatcherHookEventArgs e)=>Event(e,"posted");
    private void Started(object? sender,DispatcherHookEventArgs e)=>Event(e,"begin");
    private void Completed(object? sender,DispatcherHookEventArgs e)=>Event(e,"end",true);
    private void Aborted(object? sender,DispatcherHookEventArgs e)=>Event(e,"aborted",true);
    private void Priority(object? sender,DispatcherHookEventArgs e)=>Event(e,"priority");
    private void Layout(object? sender,EventArgs e)=>Mark("layout-notification");
    private void Rendering(object? sender,EventArgs e)=>Mark("render-notification");
    private void Size(object sender,SizeChangedEventArgs e)=>Mark("resize",$"{e.NewSize.Width}x{e.NewSize.Height}");
    protected override void OnEventSourceCreated(EventSource source){if(source.Name=="Microsoft-Windows-DotNETRuntime")EnableEvents(source,EventLevel.Informational,(EventKeywords)1);}
    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        if(e.EventName is {} name&&(name.StartsWith("GCStart",StringComparison.Ordinal)||name.StartsWith("GCEnd",StringComparison.Ordinal)||name.StartsWith("GCSuspendEE",StringComparison.Ordinal)||name.StartsWith("GCRestartEE",StringComparison.Ordinal)))Mark("gc",name);
    }
    internal async Task<object> FinishAsync()
    {
        stop.Cancel();try{await observer;}catch(OperationCanceledException){}
        Detach();
        return new{timestampFrequency=Stopwatch.Frequency,events=rows.ToArray(),dropped=Dropped,operationMethodAvailable=MethodField is not null,timerArgsAvailable=ArgsField is not null,renderObserver,
            hookCostMs=Interlocked.Read(ref hookTicks)*1000d/Stopwatch.Frequency,os,
            limitations="Instrumented diagnostic workload. Dispatcher callback envelopes include nested work; union intervals avoid double counting. Rendering/LayoutUpdated notifications are not GPU/layout cost. CPU/state snapshots every250ms cannot prove context-switch causality. Allocation totals include this bounded trace. GC events and latest-pause data are correlated, not assumed causal."};
    }
    private void Detach()
    {
        if(disposed)return;disposed=true;activities.Dispose();
        dispatcher.Hooks.OperationPosted-=Posted;dispatcher.Hooks.OperationStarted-=Started;dispatcher.Hooks.OperationCompleted-=Completed;dispatcher.Hooks.OperationAborted-=Aborted;dispatcher.Hooks.OperationPriorityChanged-=Priority;
        window.LayoutUpdated-=Layout;window.SizeChanged-=Size;CompositionTarget.Rendering-=Rendering;
    }
    public override void Dispose(){if(dispatcher is not null){stop.Cancel();Detach();}base.Dispose();}
}
