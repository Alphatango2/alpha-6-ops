using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Alpha6Ops.Desktop;

// Refuses a second launch and brings the running window forward instead, since the app is
// designed to live in the tray rather than exit and two copies would fight over the same
// log files, active-flight assignment and SimConnect connection.
internal static class SingleInstance
{
    private const int SW_RESTORE = 9;
    private static Mutex? mutex;

    internal static bool TryAcquire(string mainWindowTitle)
    {
        mutex = new Mutex(true, "Alpha6Designs.Alpha6OPS.SingleInstance", out var createdNew);
        if (createdNew) return true;
        var existing = FindWindow(null, mainWindowTitle);
        if (existing != IntPtr.Zero) { ShowWindow(existing, SW_RESTORE); SetForegroundWindow(existing); }
        mutex.Dispose();
        mutex = null;
        return false;
    }

    internal static void Release()
    {
        mutex?.ReleaseMutex();
        mutex?.Dispose();
        mutex = null;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
}
