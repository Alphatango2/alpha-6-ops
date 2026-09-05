using System;
using System.Reflection;
using System.Windows;

namespace Alpha6Ops.Desktop;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        CrashReporter.Install(this);
        if (e.Args.Length == 2 && e.Args[0] == "--simconnect-probe")
        {
            await SimConnectProbe.RunAsync(e.Args[1]);
            Shutdown();
            return;
        }
        var diagnosticOutput = e.Args.Length == 2 && e.Args[0] == "--smoke-test" ? e.Args[1] : null;
        if (diagnosticOutput is null)
        {
            var title = $"Alpha 6 OPS v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown"} — Desktop Preview";
            if (!SingleInstance.TryAcquire(title)) { Shutdown(); return; }
        }
        var window = diagnosticOutput is not null ? new MainWindow(diagnosticOutput) : new MainWindow();
        MainWindow = window;
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
