using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Alpha6Ops.Desktop;

// Exercises the real WPF window and event loop. Only runs with an explicit diagnostic flag.
internal static class DesktopSmokeTest
{
    internal static async Task RunAsync(MainWindow window, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        try
        {
            window.Show();
            window.SetAdvanced(true);
            var replay = window.RunReplayAsync(20);
            window.Close(); // normal close must hide, not dispose the window or end replay
            if (window.IsVisible || !window.TrayVisible || !window.IsRunning)
                throw new InvalidOperationException("Close-to-tray did not preserve the running replay.");
            await replay;
            var legs = window.Projection;
            if (legs[1].DepartureDelayMinutes != 30 || legs[2].DepartureDelayMinutes != 5 || !legs[0].Completed)
                throw new InvalidOperationException("Desktop replay results differ from the domain contract.");
            window.RestoreWindow();
            if (!window.IsVisible) throw new InvalidOperationException("Tray restore failed.");
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var image = File.Create(Path.Combine(outputDirectory, "desktop-preview.png"))) encoder.Save(image);
            window.ResetPreview();
            var catalog = FleetDatabase.Read();
            if (catalog.Count != 1006 || catalog.Count(a => a.Status == "Parked") != 7 || catalog.Single(a => a.Registration == "N414DZ").SerialNumber != "1996")
                throw new InvalidOperationException("Aircraft database did not match the verified import.");
            var fleet = new FleetWindow();
            fleet.Show();
            if (fleet.Search("n414dz") != 1 || fleet.Search("no-such-registration") != 0) throw new InvalidOperationException("Fleet search failed.");
            fleet.Search("");
            if (fleet.FilterStatus("Parked") != 7) throw new InvalidOperationException("Parked filter failed.");
            fleet.FilterStatus("All statuses");
            fleet.Search("330-94");
            fleet.UpdateLayout();
            var fleetBitmap = new RenderTargetBitmap((int)fleet.ActualWidth,(int)fleet.ActualHeight,96,96,PixelFormats.Pbgra32);
            fleetBitmap.Render(fleet);
            var fleetEncoder = new PngBitmapEncoder();
            fleetEncoder.Frames.Add(BitmapFrame.Create(fleetBitmap));
            using (var image = File.Create(Path.Combine(outputDirectory,"fleet-preview.png"))) fleetEncoder.Save(image);
            fleet.Close();
            if (window.Projection.Any(leg => leg.DepartureDelayMinutes != 0)) throw new InvalidOperationException("Reset failed.");
            File.WriteAllText(Path.Combine(outputDirectory, "desktop-smoke.json"), JsonSerializer.Serialize(new
            {
                passed = true, checks = new[] { "WPF startup", "embedded replay", "close-to-tray preserves replay", "downstream delays", "tray restore", "reset", "SQLite fleet counts and N414DZ identity", "case-insensitive fleet search and no-results state" },
                runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory(), legs
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(outputDirectory, "desktop-smoke-error.txt"), error.ToString());
            Environment.ExitCode = 1;
        }
        finally { window.ExitApplication(); }
    }
}
