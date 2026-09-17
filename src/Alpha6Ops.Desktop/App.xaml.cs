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
        if (e.Args.Length == 1 && e.Args[0] == "--preview-pilot")
        {
            // Explicit preview never loads credentials or another pilot's stored flights.
            var previewRoot = Path.Combine(AppContext.BaseDirectory, "pilot-preview-data", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(previewRoot);
            CrashReporter.Install(this, previewRoot);
            MainWindow = new MainWindow(previewRoot, null, pilotPreview: true);
            MainWindow.Show();
            return;
        }
        if (e.Args.Length == 1 && e.Args[0] == "--preview-login")
        {
            // Start at login every time; optional continuation uses isolated preview data only.
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow = new AccountWindow(null, preview: true, openPilotPreview: () =>
            {
                var previewRoot = Path.Combine(AppContext.BaseDirectory, "pilot-preview-data", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(previewRoot);
                CrashReporter.Install(this, previewRoot);
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                MainWindow = new MainWindow(previewRoot, null, pilotPreview: true);
                MainWindow.Show();
            });
            MainWindow.Show();
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
        if (diagnosticOutput is null)
        {
            if (!SingleInstance.TryAcquire()) { Shutdown(); return; }
        }
        DesktopAccountSession? account = null;
        if (diagnosticOutput is null)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                var configuration = IdentityConfiguration.Load();
                if (configuration is not null)
                {
                    account = new DesktopAccountSession(configuration);
                    var login = new AccountWindow(account, restore: true);
                    login.ShowDialog();
                    if (!login.Accepted) { Shutdown(); return; }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Account configuration could not be loaded. Ask the publisher to check alpha6-identity.json. Sign-in is required for a configured build.", "Alpha 6 OPS", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(); return;
            }
        }
        var window = diagnosticOutput is not null ? new MainWindow(diagnosticOutput) : new MainWindow(null, account);
        MainWindow = window;
        if(diagnosticOutput is null)SingleInstance.Listen(()=>Dispatcher.BeginInvoke(new Action(() => (MainWindow as MainWindow)?.RestoreWindow())));
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
