using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Alpha6Ops.Desktop;

internal static class SimulatorLauncher
{
    // Verified from the installed Microsoft Store package manifest. Windows resolves the current
    // installation/version and invokes GameLaunchHelper, preserving the Store/Xbox launch flow.
    internal const string StoreApplicationId = "Microsoft.Limitless_8wekyb3d8bbwe!App";
    internal static bool IsRunning()
    {
        var processes = Process.GetProcessesByName("FlightSimulator2024");
        try { return processes.Length > 0; }
        finally { foreach (var process in processes) process.Dispose(); }
    }
    internal static bool LaunchIfNeeded()
    {
        if (IsRunning()) return false;
        object? manager = null;
        try
        {
            manager = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"), true)!);
            var result = ((IApplicationActivationManager)manager!).ActivateApplication(StoreApplicationId, null, 2, out _);
            if (result < 0) throw new COMException("MSFS activation failed", result);
            return true;
        }
        catch (Exception error) when (error is COMException or InvalidCastException)
        {
            throw new IOException($"Windows could not launch the Microsoft Store edition of MSFS 2024 (0x{error.HResult:X8}). Open it from Xbox or Start, resolve any sign-in/update prompt, then click Connect again.");
        }
        finally { if (manager is not null && Marshal.IsComObject(manager)) Marshal.ReleaseComObject(manager); }
    }
    [ComImport, Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string? arguments, uint options, out uint processId);
    }
}
