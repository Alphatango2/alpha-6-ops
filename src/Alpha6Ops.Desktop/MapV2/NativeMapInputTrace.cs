using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Alpha6Ops.Desktop.MapV2;

// Explicit, bounded observation of the actual installed input/connection path.
// No artificial input, sample filtering, simulator commands or cadence changes.
internal sealed class NativeMapInputTrace : IDisposable
{
    internal static volatile Action<string,DateTimeOffset,long>? TelemetryObserver;
    internal static long Arrival(DateTimeOffset at)
    {
        if(TelemetryObserver is not {} observer)return 0;
        long ticks=Stopwatch.GetTimestamp();observer("arrival",at,ticks);return ticks;
    }
    internal static void Processed(DateTimeOffset at,long arrivalTicks)
    { if(arrivalTicks!=0)TelemetryObserver?.Invoke("processed",at,arrivalTicks); }

#if DIAGNOSTIC_HARNESSES
    private readonly MainWindow main;
    private readonly string directory;
    private readonly DispatcherSchedulingTrace trace;
    private readonly DispatcherTimer duration;
    private bool stopped;

    internal NativeMapInputTrace(MainWindow main,string directory)
    {
        this.main=main;this.directory=Path.GetFullPath(directory);
        if(Directory.Exists(this.directory))throw new IOException("Native map trace requires a new evidence directory.");
        Directory.CreateDirectory(this.directory);
        File.WriteAllText(Path.Combine(this.directory,"record.json"),JsonSerializer.Serialize(new{
            startedUtc=DateTimeOffset.UtcNow,durationSeconds=120,process=Environment.ProcessId,
            source="Explicit --trace-map-input normal installed session",limits="No synthetic input. F9 adds a user stall marker. Timing instrumentation may affect scheduling. Does not itself assert physical success."}));
        var clock=(DispatcherTimer)FlightSessionDiagnosticAccess.Get(main,"dashboardClock")!;
        trace=new(main,clock,[]);
        TelemetryObserver=(kind,at,ticks)=>trace.Mark("native-telemetry-"+kind,at.ToString("O"),
            kind=="processed"?Stopwatch.GetElapsedTime(ticks).TotalMilliseconds:0);
        main.AddHandler(Mouse.PreviewMouseDownEvent,new MouseButtonEventHandler(Press),true);
        main.AddHandler(Mouse.PreviewMouseUpEvent,new MouseButtonEventHandler(Press),true);
        main.AddHandler(Mouse.PreviewMouseMoveEvent,new MouseEventHandler(Move),true);
        main.AddHandler(Keyboard.PreviewKeyDownEvent,new KeyEventHandler(Key),true);
        duration=new(DispatcherPriority.Background,main.Dispatcher){Interval=TimeSpan.FromMinutes(2)};
        duration.Tick+=Finish;duration.Start();
        trace.Mark("native-trace-start","Physical pointer and real source callbacks; diagnostic observers affect scheduling");
    }
    private void Press(object sender,MouseButtonEventArgs args)=>trace.Mark("physical-button",args.ChangedButton+"/"+args.ButtonState,
        Math.Max(0,unchecked(Environment.TickCount-args.Timestamp)));
    private void Move(object sender,MouseEventArgs args)
    {
        if(args.LeftButton==MouseButtonState.Pressed)
        {
            var host=main.OperationsView.MapHost;var point=args.GetPosition(host);
            trace.Mark("physical-drag",$"{point.X:0.0},{point.Y:0.0}",Math.Max(0,unchecked(Environment.TickCount-args.Timestamp)));
        }
    }
    private void Key(object sender,KeyEventArgs args){if(args.Key==System.Windows.Input.Key.F9)trace.Mark("physical-user-stall-marker");}
    private async void Finish(object? sender,EventArgs args)
    {
        if(stopped)return;
        Detach();trace.Mark("native-trace-end");
        try
        {
            var evidence=await trace.FinishAsync();
            await Task.Run(()=>File.WriteAllText(Path.Combine(directory,"trace.json"),JsonSerializer.Serialize(evidence)));
        }
        catch(Exception error){CrashReporter.Write("map_input_trace",error,directory:directory);}
        finally{trace.Dispose();}
    }
    private void Detach()
    {
        stopped=true;duration.Stop();TelemetryObserver=null;
        main.RemoveHandler(Mouse.PreviewMouseDownEvent,new MouseButtonEventHandler(Press));
        main.RemoveHandler(Mouse.PreviewMouseUpEvent,new MouseButtonEventHandler(Press));
        main.RemoveHandler(Mouse.PreviewMouseMoveEvent,new MouseEventHandler(Move));
        main.RemoveHandler(Keyboard.PreviewKeyDownEvent,new KeyEventHandler(Key));
    }
    public void Dispose()
    {
        if(stopped)return;
        Detach();trace.Mark("native-trace-early-exit");
        try{File.WriteAllText(Path.Combine(directory,"early-exit-events.json"),JsonSerializer.Serialize(trace.EventSnapshot()));}
        catch(Exception error){CrashReporter.Write("map_input_trace",error,directory:directory);}
        finally{trace.Dispose();}
    }
#else
    public void Dispose() { }
#endif
}
