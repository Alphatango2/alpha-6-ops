using System.IO;
using System.Text.Json;
using System.Windows;
namespace Sierra4.MapPrototype;
internal static class Program
{
    [STAThread] public static int Main(string[] args)
    {
        if(args.FirstOrDefault()=="--contracts")
        {
            try { File.WriteAllText(args[1],JsonSerializer.Serialize(ContractChecks.Run())); return 0; }
            catch(Exception e) { File.WriteAllText(args[1],e.ToString()); return 1; }
        }
        var verify=args.FirstOrDefault()=="--verify";
        var directory=Path.GetFullPath(args.Length>1?args[1]:"work/checkpoint-7.21/interactive");Directory.CreateDirectory(directory);
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        app.Startup+=async(_,_)=>
        {
            try
            {
                File.WriteAllText(Path.Combine(directory,"contracts.json"),JsonSerializer.Serialize(ContractChecks.Run()));
                for(var cycle=0;cycle<(verify?3:1);cycle++)
                {
                    var window=new PrototypeWindow(directory,cycle);
                    if(!verify)window.Closed+=(_,_)=>app.Shutdown();
                    window.Show();
                    await window.InitializeAsync();
                    if(!verify)return;
                    await window.VerifyAsync(cycle==0);
                    window.DisposeRenderer();window.Close();
                }
                File.WriteAllText(Path.Combine(directory,"complete.json"),JsonSerializer.Serialize(new{passed=true,recreatedViews=3,contractChecks=8}));
                app.Shutdown(0);
            }
            catch(Exception e) { File.WriteAllText(Path.Combine(directory,"failure.txt"),e.ToString());app.Shutdown(1); }
        };
        return app.Run();
    }
}
