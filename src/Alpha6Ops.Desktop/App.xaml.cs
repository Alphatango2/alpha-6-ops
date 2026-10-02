using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace Alpha6Ops.Desktop;

public partial class App : Application
{
    private FlightSessionCoordinator? flightSession;
#if DIAGNOSTIC_HARNESSES
    private MapV2.NativeMapInputTrace? nativeMapTrace;
#endif
    private readonly System.Collections.Generic.List<FlightSessionCoordinator> diagnosticSessions=[];

    internal static FlightSessionCoordinator GetFlightSession(string? diagnosticDirectory)
    {
        var app=(App)Current;
        if(diagnosticDirectory is null)
        {
            if(app.flightSession is null)
            {
                app.flightSession=new(CrashReporter.RootDirectory,false,new WpfFlightRuntimeDispatcher(app.Dispatcher));
                app.flightSession.SetPilotName(UserPreferencesStore.Load()?.PilotName??"");
            }
            return app.flightSession;
        }
        var diagnostic=new FlightSessionCoordinator(diagnosticDirectory,true,new WpfFlightRuntimeDispatcher(app.Dispatcher));
        app.diagnosticSessions.Add(diagnostic);
        return diagnostic;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
#if DIAGNOSTIC_HARNESSES
        if(e.Args.Length==4&&e.Args[0]=="--map-performance-checks")
        {
            CrashReporter.Install(this,e.Args[1]);var result=await MapV2.MapPerformanceChecks.RunAsync(e.Args[1],e.Args[2],double.Parse(e.Args[3],System.Globalization.CultureInfo.InvariantCulture));Shutdown(result);return;
        }
        if(e.Args.Length==2&&e.Args[0] is "--map-v2-checks" or "--map-contention-checks" or "--map-delivery-checks")
        {
            CrashReporter.Install(this,e.Args[1]);var result=await MapV2.MapV2Checks.RunAsync(e.Args[1],e.Args[0]=="--map-contention-checks",e.Args[0]=="--map-delivery-checks");Shutdown(result);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--map-model-checks")
        {
            Directory.CreateDirectory(e.Args[1]);var checks=new System.Collections.Generic.List<object>();var failed=0;
            try { void Record(bool passed,string name){checks.Add(new{passed,name});if(!passed)failed++;} MapV2.MapModelChecks.Run(Record);await MapV2.MapV2Checks.PumpChecks(Record); }
            catch(Exception error){checks.Add(new{passed=false,name=error.ToString()});failed++;}
            File.WriteAllText(Path.Combine(e.Args[1],"model.json"),JsonSerializer.Serialize(new{passed=failed==0,failed,checks}));Shutdown(failed==0?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--observed-cruise-native-checks")
        {
            Shutdown(ObservedCruiseNativeProfileChecks.Run(e.Args[1]));return;
        }
        if(e.Args.Length==3&&e.Args[0]=="--replay-journals")
        {
            try{RecordedFlightReplay.Run(e.Args[1],e.Args[2]);Shutdown(0);}
            catch(Exception error){Directory.CreateDirectory(e.Args[2]);File.WriteAllText(Path.Combine(e.Args[2],"error.txt"),error.ToString());Shutdown(1);}
            return;
        }
        var diagnosticOutput = e.Args.Length == 2 && e.Args[0] == "--smoke-test" ? e.Args[1] : null;
        CrashReporter.Install(this,diagnosticOutput);
        if(e.Args.Length==2&&e.Args[0]=="--activation-smoke")
        {
            var output=e.Args[1];Directory.CreateDirectory(output);
            if(!SingleInstance.TryAcquire()){File.WriteAllText(Path.Combine(output,"activation-smoke.json"),"{\"passed\":false,\"reason\":\"another instance was already running\"}");Shutdown();return;}
            var smokeWindow=new MainWindow(output);MainWindow=smokeWindow;smokeWindow.Show();smokeWindow.MinimizeToTray();
            var restored=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            SingleInstance.Listen(()=>smokeWindow.Dispatcher.BeginInvoke(new Action(()=>{smokeWindow.RestoreWindow();restored.TrySetResult(smokeWindow.IsVisible&&smokeWindow.WindowState==WindowState.Normal);})));
            using var child=Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true});
            var completed=await Task.WhenAny(restored.Task,Task.Delay(TimeSpan.FromSeconds(8)));var passed=completed==restored.Task&&await restored.Task;
            if(child is not null)await child.WaitForExitAsync();
            File.WriteAllText(Path.Combine(output,"activation-smoke.json"),JsonSerializer.Serialize(new{passed,restored=smokeWindow.IsVisible,childExited=child?.HasExited??false}));
            smokeWindow.ExitApplication(output);return;
        }
        if (e.Args.Length == 2 && e.Args[0] is "--simconnect-probe" or "--simulator-launch-probe")
        {
            await SimConnectProbe.RunAsync(e.Args[1], e.Args[0] == "--simulator-launch-probe");
            Shutdown();
            return;
        }
#else
        string? diagnosticOutput=null;
        CrashReporter.Install(this);
#endif
        if (diagnosticOutput is null)
        {
            if (!SingleInstance.TryAcquire()) { Shutdown(); return; }
            ProductStorage.Initialize();
            var retention=ProductionRetention.Apply(ProductStorage.Current,DateTimeOffset.UtcNow);
            if(retention.Errors.Count>0)CrashReporter.Write("production_retention",null,new{errors=retention.Errors});
        }
        var window = diagnosticOutput is not null ? new MainWindow(diagnosticOutput) : new MainWindow();
        MainWindow = window;
        if(diagnosticOutput is null)SingleInstance.Listen(()=>window.Dispatcher.BeginInvoke(window.RestoreWindow));
        if (diagnosticOutput is not null)
        {
#if DIAGNOSTIC_HARNESSES
            await DesktopSmokeTest.RunAsync(window, diagnosticOutput);
#endif
            return;
        }
        window.Show();
#if DIAGNOSTIC_HARNESSES
        if(e.Args.Length==2&&e.Args[0]=="--trace-map-input")
        {
            try{nativeMapTrace=new(window,e.Args[1]);}
            catch(Exception error){CrashReporter.Write("map_input_trace_start",error);}
        }
#endif
    }

    protected override void OnExit(ExitEventArgs e)
    {
#if DIAGNOSTIC_HARNESSES
        nativeMapTrace?.Dispose();
#endif
        flightSession?.Dispose();
        foreach(var session in diagnosticSessions)session.Dispose();
        SingleInstance.Release();
        base.OnExit(e);
    }
}
