using System;
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
        var window = new MainWindow();
        MainWindow = window;
        if (e.Args.Length == 2 && e.Args[0] == "--smoke-test")
        {
            await DesktopSmokeTest.RunAsync(window, e.Args[1]);
            return;
        }
        window.Show();
    }
}
