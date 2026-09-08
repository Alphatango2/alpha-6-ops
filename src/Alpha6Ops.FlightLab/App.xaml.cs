using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Alpha6Ops.FlightLab;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window=new MainWindow();MainWindow=window;window.Show();
        if(e.Args.Length==2&&e.Args[0]=="--smoke-test")
        {
            try
            {
                await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                window.UpdateLayout();
                if(window.PhaseText.Text!="AT GATE"||window.ServerStatusText.Text.Length==0)throw new InvalidOperationException("Flight Lab did not initialize its gate state and local link.");
                Directory.CreateDirectory(e.Args[1]);
                var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(e.Args[1],"flight-lab.png"));encoder.Save(file);
                File.WriteAllText(Path.Combine(e.Args[1],"flight-lab-smoke.json"),"{\"passed\":true,\"phase\":\"AT GATE\"}");
                Shutdown(0);
            }
            catch(Exception error){File.WriteAllText(Path.Combine(e.Args[1],"flight-lab-failure.txt"),error.ToString());Shutdown(1);}
        }
    }
}
