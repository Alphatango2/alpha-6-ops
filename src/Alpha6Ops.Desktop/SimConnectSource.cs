using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

public record LiveReading(string Aircraft, Telemetry Telemetry, FlightEvidence? Evidence = null,
    string Source = "MSFS 2024", double? RouteProgress=null, DateTimeOffset? ReceivedUtc=null);

// Native ABI taken from MSFS 2024 SDK 1.7.3 SimConnect.h (packed receive records).
// All calls and dispatch reads stay on one dedicated worker. No simulator writes.
internal static partial class SimConnectSource
{
    static SimConnectSource()
    {
        NativeLibrary.SetDllImportResolver(typeof(SimConnectSource).Assembly, (name, assembly, path) =>
        {
            if (name != "SimConnect.dll") return IntPtr.Zero;
            var dll=ResolveSimConnectPath(AppContext.BaseDirectory,Environment.GetEnvironmentVariable("ALPHA6_SIMCONNECT_DLL"));
            if(dll is null)throw new DllNotFoundException("SimConnect.dll was not found. Install the MSFS 2024 SDK through Developer Mode, or set ALPHA6_SIMCONNECT_DLL to its full path.");
            return NativeLibrary.Load(dll);
        });
    }

    internal static string? ResolveSimConnectPath(string baseDirectory,string? environmentPath,IEnumerable<string>? driveRoots=null)
    {
        var candidates=new List<string>();
        if(!string.IsNullOrWhiteSpace(environmentPath))candidates.Add(environmentPath.Trim());
        var bundled=Path.Combine(baseDirectory,"SimConnect.dll");candidates.Add(bundled);
        var config=Path.Combine(baseDirectory,"simconnect-sdk-path.txt");
        if(File.Exists(config))candidates.Add(File.ReadAllText(config).Trim());
        var roots=driveRoots??DriveInfo.GetDrives().Where(drive=>drive.IsReady).Select(drive=>drive.RootDirectory.FullName);
        foreach(var root in roots)
        {
            candidates.Add(Path.Combine(root,"SimConnect SDK","lib","SimConnect.dll"));
            candidates.Add(Path.Combine(root,"MSFS 2024 SDK","SimConnect SDK","lib","SimConnect.dll"));
        }
        return candidates.FirstOrDefault(candidate=>!string.IsNullOrWhiteSpace(candidate)&&Path.IsPathFullyQualified(candidate)&&File.Exists(candidate));
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
                ("GENERAL ENG COMBUSTION:4", "Bool"), ("IS SLEW ACTIVE", "Bool"),
                ("PLANE LATITUDE", "degrees"), ("PLANE LONGITUDE", "degrees"),
                ("PLANE ALTITUDE", "feet"), ("PLANE HEADING DEGREES TRUE", "degrees"),
                ("AIRSPEED INDICATED", "knots"), ("VERTICAL SPEED", "feet per minute"),
                ("GEAR TOTAL PCT EXTENDED", "Percent Over 100"), ("PLANE ALT ABOVE GROUND", "feet"),
                ("FLAPS HANDLE PERCENT", "Percent Over 100"), ("PLANE PITCH DEGREES", "degrees"),
                ("PLANE BANK DEGREES", "degrees"), ("FUEL TOTAL QUANTITY WEIGHT", "pounds"),
                ("INDICATED ALTITUDE", "feet") };
            foreach (var field in fields) Check(AddDefinition(handle, 1, field.Name, field.Unit, 4, 0, uint.MaxValue), field.Name);
            Check(AddDefinition(handle, 1, "TITLE", null, 9, 0, uint.MaxValue), "Aircraft title");
            Check(Subscribe(handle, 10, "Pause_EX1"), "Pause subscription");
            Check(Request(handle, 1, 1, 0, 4, 0, 0, 0, 0), "Telemetry request");
            var context = new SimConnectContext(handle);
            var optional=new OptionalSensors(handle);
            var terminal=new TerminalSensors(FenixShutdownCapabilities.Read(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft Flight Simulator 2024","UserCfg.opt")));
            terminal.Register(handle);
            var engineTracker=new EngineEvidenceTracker();
            bool paused = true; // fail closed until initial Pause_EX1 state arrives
            bool opened = false;
            var lastPacket = DateTime.UtcNow;
            while (!token.IsCancellationRequested)
            {
                context.Poll();
                var processed = 0;
                while (processed++ < 100 && Next(handle, out var data, out var length) >= 0)
                {
                    if (length < 12) throw new InvalidDataException("Truncated SimConnect packet.");
                    var id = Marshal.ReadInt32(data, 8);
                    if (terminal.Dispatch(id,data,length))continue;
                    if (optional.Dispatch(id,data,length))continue;
                    if (context.Dispatch(id, data, length)) continue;
                    if (id == 2)
                    {
                        if (length < 268) throw new InvalidDataException("Truncated SimConnect connection acknowledgement.");
                        var server = Marshal.PtrToStringAnsi(IntPtr.Add(data, 12), 256)?.TrimEnd('\0') ?? "Simulator";
                        opened = true;
                        status("Connected to " + server + ". Waiting for aircraft data.");
                    }
                    else if (id == 3) throw new IOException("Simulator closed. Reconnect after loading your flight.");
                    else if (id == 1 && length >= 24) throw new IOException($"SimConnect exception {Marshal.ReadInt32(data, 12)}, request {Marshal.ReadInt32(data, 16)}, parameter {Marshal.ReadInt32(data, 20)}.");
                    else if (id == 4 && length >= 24 && Marshal.ReadInt32(data, 16) == 10) paused = Marshal.ReadInt32(data, 20) != 0;
                    else if (id == 8 && length >= 472 && Marshal.ReadInt32(data, 12) == 1 && Marshal.ReadInt32(data, 20) == 1 && Marshal.ReadInt32(data, 36) == 23)
                    {
                        var values = new double[22];
                        Marshal.Copy(IntPtr.Add(data, 40), values, 0, 22);
                        if (!double.IsFinite(values[0]) || values[0] <= 0 || values[0] > 315537897599d) throw new InvalidDataException("Invalid simulator UTC time.");
                        var at = DateTimeOffset.MinValue.AddSeconds(values[0]);
                        var title = Marshal.PtrToStringAnsi(IntPtr.Add(data, 216), 256)?.TrimEnd('\0') ?? "Unknown aircraft";
                        if (new[]{values[1],values[3],values[8]}.Any(value=>value is not (0 or 1))) throw new InvalidDataException("Invalid ground/brake/slew telemetry.");
                        var sample = new Telemetry(at, values[1] != 0, values[2], values[3] != 0,
                            values[4] != 0 || values[5] != 0 || values[6] != 0 || values[7] != 0, paused, values[8] != 0,
                            values[9],values[10],values[11],values[12],values[13],values[14],NormalizeGear(values[15]),values[16],
                            values[17],values[18],values[19],values[20],new[]{values[4],values[5],values[6],values[7]}.Count(value=>value!=0),
                            (values[4]!=0?1:0)|(values[5]!=0?2:0)|(values[6]!=0?4:0)|(values[7]!=0?8:0),values[21]);
                        if (!double.IsFinite(sample.GroundSpeedKnots) || sample.GroundSpeedKnots < 0 || !sample.HasPosition || !double.IsFinite(sample.AltitudeFeet) || !double.IsFinite(sample.HeadingDegrees) || !double.IsFinite(sample.IndicatedAirspeedKnots) || sample.IndicatedAirspeedKnots<0 || !double.IsFinite(sample.VerticalSpeedFeetPerMinute) || !double.IsFinite(sample.GearExtendedRatio) || sample.GearExtendedRatio is <0 or >1.1 || !double.IsFinite(sample.AltitudeAboveGroundFeet) || !double.IsFinite(sample.FlapsExtendedRatio) || sample.FlapsExtendedRatio is <0 or >1.1 || !double.IsFinite(sample.PitchDegrees) || !double.IsFinite(sample.BankDegrees) || !double.IsFinite(sample.FuelTotalWeightPounds) || sample.FuelTotalWeightPounds<0) throw new InvalidDataException("Invalid simulator aircraft telemetry.");
                        sample=optional.Enrich(sample,engineTracker);
                        var receivedUtc=DateTimeOffset.UtcNow;
                        sample=sample with{TerminalShutdown=terminal.Observe(title,sample,receivedUtc,optional.Value(0),values.Skip(4).Take(4).ToArray(),Enumerable.Range(0,4).Select(i=>optional.Value(8+i*5)).ToArray())};
                        lastPacket = DateTime.UtcNow;
                        received(new(title, sample, context.Evidence(),ReceivedUtc:receivedUtc));
                    }
                }
                if (DateTime.UtcNow - lastPacket > TimeSpan.FromSeconds(30))
                    throw new IOException(opened ? "No aircraft data for 30 seconds. Load a flight and reconnect." : "Simulator did not acknowledge the connection.");
                token.WaitHandle.WaitOne(100);
            }
        }
        finally { if (handle != IntPtr.Zero) Close(handle); }
    }, token);

    private sealed class OptionalSensors
    {
        private readonly Dictionary<int,(double Value,DateTime At)> readings=new();
        private readonly HashSet<uint> packets=new();
        internal OptionalSensors(IntPtr handle)
        {
            var fields=new List<(string,string)>{("NUMBER OF ENGINES","number"),("FLAPS HANDLE INDEX","number"),("FLAPS NUM HANDLE POSITIONS","number"),("TRAILING EDGE FLAPS LEFT ANGLE","degrees"),("TRAILING EDGE FLAPS RIGHT ANGLE","degrees")};
            for(var i=1;i<=4;i++){fields.Add(($"TURB ENG FUEL FLOW PPH:{i}","pounds per hour"));fields.Add(($"TURB ENG N1:{i}","percent"));fields.Add(($"TURB ENG N2:{i}","percent"));fields.Add(($"GENERAL ENG STARTER:{i}","Bool"));fields.Add(($"GENERAL ENG OIL PRESSURE:{i}","psf"));}
            fields.Add(("LEADING EDGE FLAPS LEFT ANGLE","degrees"));fields.Add(("LEADING EDGE FLAPS RIGHT ANGLE","degrees"));
            for(var i=0;i<fields.Count;i++)
            {
                var id=(uint)(200+i);var f=fields[i];
                if(AddDefinition(handle,id,f.Item1,f.Item2,4,0,uint.MaxValue)>=0){Remember(handle);Request(handle,id,id,0,4,0,0,0,0);Remember(handle);}
            }
        }
        private void Remember(IntPtr handle){if(LastSent(handle,out var id)>=0)packets.Add(id);}
        internal bool Dispatch(int kind,IntPtr data,uint length)
        {
            if(kind==1&&length>=24&&packets.Contains((uint)Marshal.ReadInt32(data,16)))return true;
            if(kind!=8||length<48)return false;var id=Marshal.ReadInt32(data,12);if(id<200||id>=227)return false;
            if(Marshal.ReadInt32(data,36)==1){var buffer=new double[1];Marshal.Copy(IntPtr.Add(data,40),buffer,0,1);readings[id-200]=(buffer[0],DateTime.UtcNow);}return true;
        }
        internal double? Value(int index)=>readings.TryGetValue(index,out var r)&&DateTime.UtcNow-r.At<TimeSpan.FromSeconds(3)&&double.IsFinite(r.Value)&&r.Value>=0?r.Value:null;
        internal Telemetry Enrich(Telemetry sample,EngineEvidenceTracker tracker)
        {
            var count=Value(0) is {} n&&n>=1&&n<=4?(int)n:0;
            var raw=new List<EngineReading>();for(var i=0;i<count;i++)raw.Add(new(i+1,(sample.RunningEngineMask&(1<<i))!=0?1:0,Value(5+i*5),Value(6+i*5),Value(7+i*5),Value(8+i*5),Value(9+i*5)));
            var states=tracker.Observe(raw,sample.At);var valid=count>0&&states.All(x=>x.Condition is EngineCondition.Off or EngineCondition.Running);
            var mask=states.Where(x=>x.Condition==EngineCondition.Running).Aggregate(0,(m,x)=>m|(1<<(x.Index-1)));
            return sample with{EngineReadings=raw,EngineStates=states,EngineDataValid=valid,EnginesRunning=mask!=0,RunningEngineMask=valid?mask:-1,RunningEngineCount=valid?states.Count(x=>x.Condition==EngineCondition.Running):-1,
                FlapHandleRatio=sample.FlapsExtendedRatio,FlapHandleIndex=(int)(Value(1)??-1),FlapHandlePositions=(int)(Value(2)??-1),LeftFlapDegrees=Value(3)??double.NaN,RightFlapDegrees=Value(4)??double.NaN,LeftSlatDegrees=Value(25)??double.NaN,RightSlatDegrees=Value(26)??double.NaN};
        }
    }
    [DllImport("SimConnect.dll",EntryPoint="SimConnect_GetLastSentPacketID",ExactSpelling=true)]private static extern int LastSent(IntPtr handle,out uint id);

    // MSFS 2024 currently reports GEAR TOTAL PCT EXTENDED as 0..0.01 when
    // requested with Percent Over 100, while other percentage simvars arrive
    // as 0..1. Normalize that simulator-specific scale at the boundary.
    internal static double NormalizeGear(double value)=>value is>=0 and<=.011?Math.Clamp(value*100,0,1):value;

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
