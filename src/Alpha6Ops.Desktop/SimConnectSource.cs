using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

public record LiveReading(string Aircraft, Telemetry Telemetry);

// Native ABI taken from MSFS 2024 SDK 1.7.3 SimConnect.h (packed receive records).
// All calls and dispatch reads stay on one dedicated worker. No simulator writes.
internal static class SimConnectSource
{
    static SimConnectSource()
    {
        NativeLibrary.SetDllImportResolver(typeof(SimConnectSource).Assembly, (name, assembly, path) =>
        {
            if (name != "SimConnect.dll") return IntPtr.Zero;
            var config = Path.Combine(AppContext.BaseDirectory, "simconnect-sdk-path.txt");
            var dll = Environment.GetEnvironmentVariable("ALPHA6_SIMCONNECT_DLL");
            if (string.IsNullOrWhiteSpace(dll) && File.Exists(config)) dll = File.ReadAllText(config).Trim();
            if (string.IsNullOrWhiteSpace(dll) || !Path.IsPathFullyQualified(dll))
                throw new DllNotFoundException("Set the full SDK SimConnect.dll path in simconnect-sdk-path.txt beside Alpha6OPS.exe.");
            return NativeLibrary.Load(dll);
        });
    }

    internal static Task RunAsync(Action<string> status, Action<LiveReading> received, CancellationToken token) => Task.Run(() =>
    {
        IntPtr handle = IntPtr.Zero;
        try
        {
            Check(Open(out handle, "Alpha 6 OPS", IntPtr.Zero, 0, IntPtr.Zero, uint.MaxValue), "Connect: start MSFS 2024 and load a flight, then try again");
            var fields = new (string Name, string Unit)[] {
                ("ABSOLUTE TIME", "seconds"), ("SIM ON GROUND", "Bool"), ("GROUND VELOCITY", "knots"),
                ("BRAKE PARKING POSITION", "Bool"), ("GENERAL ENG COMBUSTION:1", "Bool"),
                ("GENERAL ENG COMBUSTION:2", "Bool"), ("GENERAL ENG COMBUSTION:3", "Bool"),
                ("GENERAL ENG COMBUSTION:4", "Bool"), ("IS SLEW ACTIVE", "Bool") };
            foreach (var field in fields) Check(AddDefinition(handle, 1, field.Name, field.Unit, 4, 0, uint.MaxValue), field.Name);
            Check(AddDefinition(handle, 1, "TITLE", null, 9, 0, uint.MaxValue), "Aircraft title");
            Check(Subscribe(handle, 10, "Pause_EX1"), "Pause subscription");
            Check(Request(handle, 1, 1, 0, 4, 0, 0, 0, 0), "Telemetry request");
            bool paused = true; // fail closed until initial Pause_EX1 state arrives
            bool opened = false;
            var lastPacket = DateTime.UtcNow;
            while (!token.IsCancellationRequested)
            {
                var processed = 0;
                while (processed++ < 100 && Next(handle, out var data, out var length) >= 0)
                {
                    if (length < 12) throw new InvalidDataException("Truncated SimConnect packet.");
                    var id = Marshal.ReadInt32(data, 8);
                    if (id == 2)
                    {
                        var server = Marshal.PtrToStringAnsi(IntPtr.Add(data, 12), 256)?.TrimEnd('\0') ?? "Simulator";
                        opened = true;
                        status("Connected to " + server + ". Waiting for aircraft data.");
                    }
                    else if (id == 3) throw new IOException("Simulator closed. Reconnect after loading your flight.");
                    else if (id == 1 && length >= 24) throw new IOException($"SimConnect exception {Marshal.ReadInt32(data, 12)}, request {Marshal.ReadInt32(data, 16)}, parameter {Marshal.ReadInt32(data, 20)}.");
                    else if (id == 4 && length >= 24 && Marshal.ReadInt32(data, 16) == 10) paused = Marshal.ReadInt32(data, 20) != 0;
                    else if (id == 8 && length >= 368 && Marshal.ReadInt32(data, 12) == 1 && Marshal.ReadInt32(data, 20) == 1 && Marshal.ReadInt32(data, 36) == 10)
                    {
                        var values = new double[9];
                        Marshal.Copy(IntPtr.Add(data, 40), values, 0, 9);
                        if (!double.IsFinite(values[0]) || values[0] <= 0 || values[0] > 315537897599d) throw new InvalidDataException("Invalid simulator UTC time.");
                        var at = DateTimeOffset.MinValue.AddSeconds(values[0]);
                        var title = Marshal.PtrToStringAnsi(IntPtr.Add(data, 112), 256)?.TrimEnd('\0') ?? "Unknown aircraft";
                        var sample = new Telemetry(at, values[1] != 0, values[2], values[3] != 0,
                            values[4] != 0 || values[5] != 0 || values[6] != 0 || values[7] != 0, paused, values[8] != 0);
                        if (!double.IsFinite(sample.GroundSpeedKnots) || sample.GroundSpeedKnots < 0) throw new InvalidDataException("Invalid simulator groundspeed.");
                        lastPacket = DateTime.UtcNow;
                        received(new(title, sample));
                    }
                }
                if (DateTime.UtcNow - lastPacket > TimeSpan.FromSeconds(30))
                    throw new IOException(opened ? "No aircraft data for 30 seconds. Load a flight and reconnect." : "Simulator did not acknowledge the connection.");
                token.WaitHandle.WaitOne(100);
            }
        }
        finally { if (handle != IntPtr.Zero) Close(handle); }
    }, token);

    private static void Check(int result, string operation)
    {
        if (result < 0) throw new IOException($"{operation} (0x{result:X8}).");
    }
    [DllImport("SimConnect.dll", EntryPoint = "SimConnect_Open", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern int Open(out IntPtr handle, string name, IntPtr window, uint message, IntPtr signal, uint config);
    [DllImport("SimConnect.dll", EntryPoint = "SimConnect_Close", ExactSpelling = true)] private static extern int Close(IntPtr handle);
    [DllImport("SimConnect.dll", EntryPoint = "SimConnect_AddToDataDefinition", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern int AddDefinition(IntPtr handle, uint definition, string name, string? units, int type, float epsilon, uint id);
    [DllImport("SimConnect.dll", EntryPoint = "SimConnect_RequestDataOnSimObject", ExactSpelling = true)]
    private static extern int Request(IntPtr handle, uint request, uint definition, uint obj, int period, uint flags, uint origin, uint interval, uint limit);
    [DllImport("SimConnect.dll", EntryPoint = "SimConnect_SubscribeToSystemEvent", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern int Subscribe(IntPtr handle, uint id, string name);
    [DllImport("SimConnect.dll", EntryPoint = "SimConnect_GetNextDispatch", ExactSpelling = true)]
    private static extern int Next(IntPtr handle, out IntPtr data, out uint length);
}
