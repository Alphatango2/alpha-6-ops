using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Alpha6Ops.Desktop;

internal static class MonitorSmokeTest
{
    internal static async Task RunAsync(MainWindow window, string outputDirectory, Action<bool, string> check)
    {
        var handle = new WindowInteropHelper(window).Handle;
        check(GetWindowRect(handle, out var original), "Diagnostic window exposes its physical monitor bounds");
        foreach (var (monitor, work, expected) in new[]
        {
            (new System.Drawing.Rectangle(0, 0, 2560, 1440), new System.Drawing.Rectangle(0, 0, 2560, 1392), new System.Drawing.Rectangle(0, 0, 2560, 1392)),
            (new System.Drawing.Rectangle(-1920, 0, 1920, 1080), new System.Drawing.Rectangle(-1872, 0, 1872, 1080), new System.Drawing.Rectangle(48, 0, 1872, 1080)),
            (new System.Drawing.Rectangle(0, -1440, 2560, 1440), new System.Drawing.Rectangle(0, -1392, 2560, 1392), new System.Drawing.Rectangle(0, 48, 2560, 1392)),
            (new System.Drawing.Rectangle(2560, 0, 1920, 1080), new System.Drawing.Rectangle(2560, 0, 1872, 1080), new System.Drawing.Rectangle(0, 0, 1872, 1080))
        })
            check(MainWindow.MaximizedWorkBounds(monitor, work) == expected, $"Maximized work area handles taskbar offsets and monitor origin {monitor.Location}");
        var monitors = new List<object>();
        try
        {
            var screens = Forms.Screen.AllScreens;
            // Visit a secondary display first to exercise the monitor-change resize path.
            Array.Reverse(screens);
            foreach (var screen in screens)
            {
                var work = screen.WorkingArea;
                var changingMonitor = Forms.Screen.FromHandle(handle).DeviceName != screen.DeviceName;
                check(SetWindowPos(handle, IntPtr.Zero, work.Left + (changingMonitor ? 20 : 0), work.Top + (changingMonitor ? 20 : 0), work.Width + (changingMonitor ? 100 : 0), work.Height + (changingMonitor ? 100 : 0), 0x0014),
                    $"Move diagnostic window to {screen.DeviceName}");
                await Task.Delay(250);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                window.UpdateLayout();
                check(GetWindowRect(handle, out var bounds) && bounds.Left >= work.Left && bounds.Top >= work.Top && bounds.Right <= work.Right && bounds.Bottom <= work.Bottom,
                    $"Window fits usable area on {screen.DeviceName}; bounds {bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom}; work {work}; WPF {window.Width}x{window.Height}, actual {window.ActualWidth}x{window.ActualHeight}");
                var dpi = VisualTreeHelper.GetDpi(window);
                monitors.Add(new { screen.DeviceName, scaleX = dpi.DpiScaleX, scaleY = dpi.DpiScaleY, work.Left, work.Top, work.Width, work.Height });
                var restore = window.RestoreBounds;
                window.WindowState = WindowState.Maximized;
                await Task.Delay(250);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                window.UpdateLayout();
                check(GetWindowRect(handle, out var maximized) && maximized.Left == work.Left && maximized.Top == work.Top && maximized.Right == work.Right && maximized.Bottom == work.Bottom,
                    $"Maximized window fills work area without covering taskbar on {screen.DeviceName}; bounds {maximized.Left},{maximized.Top},{maximized.Right},{maximized.Bottom}");
                var exitTop = window.ExitOpsButton.PointToScreen(new Point(0, 0));
                var exitBottom = window.ExitOpsButton.PointToScreen(new Point(window.ExitOpsButton.ActualWidth, window.ExitOpsButton.ActualHeight));
                check(exitTop.X >= work.Left && exitTop.Y >= work.Top && exitBottom.X <= work.Right && exitBottom.Y <= work.Bottom,
                    $"Exit OPS is fully above the taskbar on {screen.DeviceName}; button {exitTop} to {exitBottom}");
                var center = window.PointFromScreen(new Point((exitTop.X + exitBottom.X) / 2, (exitTop.Y + exitBottom.Y) / 2));
                var hit = window.InputHitTest(center) as DependencyObject;
                check(hit == window.ExitOpsButton || (hit is not null && window.ExitOpsButton.IsAncestorOf(hit)),
                    $"Exit OPS remains hit-testable when maximized on {screen.DeviceName}");
                DashboardSmokeTest.Capture(window, Path.Combine(outputDirectory, $"maximized-monitor-{monitors.Count}.png"));
                window.WindowState = WindowState.Normal;
                await Task.Delay(150);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                check(Math.Abs(window.Width - restore.Width) <= 1 && Math.Abs(window.Height - restore.Height) <= 1,
                    $"Restore retains normal window dimensions on {screen.DeviceName}");
            }
            File.WriteAllText(Path.Combine(outputDirectory, "monitor-smoke.json"), JsonSerializer.Serialize(new { monitors }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            window.WindowState = WindowState.Normal;
            SetWindowPos(handle, IntPtr.Zero, original.Left, original.Top, original.Right - original.Left, original.Bottom - original.Top, 0x0014);
            await Task.Delay(100);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Bounds { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out Bounds bounds);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
