using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace Alpha6Ops.Desktop;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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
        if (diagnosticOutput is null)
        {
            if (!SingleInstance.TryAcquire()) { Shutdown(); return; }
        }
        var window = diagnosticOutput is not null ? new MainWindow(diagnosticOutput) : new MainWindow();
        MainWindow = window;
        if(diagnosticOutput is null)SingleInstance.Listen(()=>window.Dispatcher.BeginInvoke(window.RestoreWindow));
        if (diagnosticOutput is not null)
        {
            await DesktopSmokeTest.RunAsync(window, diagnosticOutput);
            return;
        }
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SingleInstance.Release();
        base.OnExit(e);
    }
}
