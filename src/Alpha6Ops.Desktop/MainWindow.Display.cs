using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Alpha6Ops.Desktop;

public partial class MainWindow
{
    private string? currentMonitor;
    private bool monitorFitQueued;
    private HwndSource? displaySource;

    internal static Size FitWindowSize(Size requested, Size available) => new(
        Math.Clamp(requested.Width, Math.Min(640, available.Width), available.Width),
        Math.Clamp(requested.Height, Math.Min(480, available.Height), available.Height));

    private void InitializeMonitorTracking()
    {
        var handle = new WindowInteropHelper(this).Handle;
        displaySource = HwndSource.FromHwnd(handle);
        displaySource?.AddHook(DisplayWindowMessage);
        currentMonitor = Forms.Screen.FromHandle(handle).DeviceName;
        DpiChanged += (_, _) => QueueMonitorFit();
        LocationChanged += (_, _) =>
        {
            var monitor = Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).DeviceName;
            if (monitor != currentMonitor) { currentMonitor = monitor; QueueMonitorFit(); }
        };
        SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
        Closed += (_, _) =>
        {
            SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
            displaySource?.RemoveHook(DisplayWindowMessage);
        };
    }

    private IntPtr DisplayWindowMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0024) // WM_GETMINMAXINFO: custom chrome must reserve the taskbar work area.
        {
            var screen = Forms.Screen.FromHandle(handle);
            var info = Marshal.PtrToStructure<WindowMinMaxInfo>(lParam);
            var bounds = MaximizedWorkBounds(screen.Bounds, screen.WorkingArea);
            info.MaxPosition = new NativePoint { X = bounds.X, Y = bounds.Y };
            info.MaxSize = new NativePoint { X = bounds.Width, Y = bounds.Height };
            // Preserve Windows/WPF tracking limits so manually spanning monitors still works.
            Marshal.StructureToPtr(info, lParam, false);
            handled = true;
        }
        else if (message == 0x001A) QueueMonitorFit(); // WM_SETTINGCHANGE, including taskbar/work-area changes.
        return IntPtr.Zero;
    }

    internal static System.Drawing.Rectangle MaximizedWorkBounds(System.Drawing.Rectangle monitor, System.Drawing.Rectangle work) =>
        new(work.Left - monitor.Left, work.Top - monitor.Top, work.Width, work.Height);

    private void DisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(QueueMonitorFit);
    }

    private void QueueMonitorFit()
    {
        if (monitorFitQueued || exiting) return;
        monitorFitQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            monitorFitQueued = false;
            if (exiting) return;
            var handle = new WindowInteropHelper(this).Handle;
            var screen = Forms.Screen.FromHandle(handle);
            currentMonitor = screen.DeviceName;
            var work = screen.WorkingArea;
            var dpi = VisualTreeHelper.GetDpi(this);
            var available = new Size(work.Width / dpi.DpiScaleX, work.Height / dpi.DpiScaleY);
            MinWidth = Math.Min(640, available.Width); MinHeight = Math.Min(480, available.Height);
            if (WindowState == WindowState.Normal)
            {
                var fitted = FitWindowSize(new Size(Width, Height), available);
                Width = fitted.Width; Height = fitted.Height;
                // Native screen positions are physical pixels, including negative monitor origins.
                if (GetWindowRect(handle, out var bounds))
                {
                    var x = Math.Clamp(bounds.Left, work.Left, Math.Max(work.Left, work.Right - (bounds.Right - bounds.Left)));
                    var y = Math.Clamp(bounds.Top, work.Top, Math.Max(work.Top, work.Bottom - (bounds.Bottom - bounds.Top)));
                    if (x != bounds.Left || y != bounds.Top) SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, 0x0015); // NOSIZE | NOZORDER | NOACTIVATE
                }
            }
            else if (WindowState == WindowState.Maximized)
            {
                SetWindowPos(handle, IntPtr.Zero, work.Left, work.Top, work.Width, work.Height, 0x0014); // NOZORDER | NOACTIVATE
            }
            UpdateResponsiveLayout();
        }, DispatcherPriority.Loaded);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowMinMaxInfo
    {
        public NativePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRectangle { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out WindowRectangle bounds);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
