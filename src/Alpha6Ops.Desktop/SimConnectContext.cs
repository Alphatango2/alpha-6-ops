using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

// Optional context has a separate data definition. An unavailable add-on variable, plan or
// facilities API must not break the basic aircraft telemetry connection.
internal sealed class SimConnectContext
{
    private readonly IntPtr handle;
    private readonly Dictionary<uint, string> optionalPackets = new();
    private readonly Dictionary<string, AirportReference> airports = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AirportReference> pendingAirports = new(StringComparer.OrdinalIgnoreCase);
    private DateTime nextState, nextFacilities, positionAt, stateAt, routeAt;
    private GeoPosition? position;
    private string? registration, warning;
    private bool? running;
    private SimulatorRoute? route;
    private string planStatus = "Simulator plan unavailable";
    private NearbyAirport? nearby;

    internal SimConnectContext(IntPtr handle)
    {
        this.handle = handle;
        Optional(() => AddDefinition(handle, 2, "PLANE LATITUDE", "degrees", 4, 0, uint.MaxValue), "Live position");
        Optional(() => AddDefinition(handle, 2, "PLANE LONGITUDE", "degrees", 4, 0, uint.MaxValue), "Live position");
        Optional(() => AddDefinition(handle, 2, "ATC ID", null, 9, 0, uint.MaxValue), "Aircraft registration");
        Optional(() => Request(handle, 2, 2, 0, 4, 0, 0, 0, 0), "Live position / registration");
    }
    private void Optional(Func<int> operation, string name)
    {
        try
        {
            if (operation() < 0) { warning = name + " unavailable"; return; }
            if (LastPacket(handle, out var packet) >= 0) optionalPackets[packet] = name;
            if (optionalPackets.Count > 256) optionalPackets.Remove(optionalPackets.Keys.First());
        }
        catch (EntryPointNotFoundException) { warning = name + " unsupported by this SimConnect runtime"; }
    }
    internal void Poll()
    {
        var now = DateTime.UtcNow;
        if (now >= nextState)
        {
            nextState = now.AddSeconds(5);
            Optional(() => SystemState(handle, 101, "Sim"), "Simulation state");
            Optional(() => SystemState(handle, 102, "FlightPlan"), "Simulator flight plan");
        }
        if (now >= nextFacilities)
        {
            nextFacilities = now.AddSeconds(30);
            pendingAirports.Clear();
            Optional(() => Facilities(handle, 0, 200), "Simulator airport facilities");
        }
    }
    internal FlightEvidence Evidence()
    {
        var now = DateTime.UtcNow;
        var fresh = now - positionAt < TimeSpan.FromSeconds(5);
        return new(fresh ? position : null, fresh ? registration : null,
            now - routeAt < TimeSpan.FromSeconds(15) ? route : null,
            now - stateAt < TimeSpan.FromSeconds(15) ? running : null,
            fresh ? nearby : null, planStatus, warning);
    }
    internal bool Dispatch(int id, IntPtr data, uint length)
    {
        if (id == 1 && length >= 24 && optionalPackets.TryGetValue(unchecked((uint)Marshal.ReadInt32(data, 16)), out var name))
        {
            warning = name + " unavailable (SimConnect " + Marshal.ReadInt32(data, 12) + ")";
            return true;
        }
        if (id == 8 && length >= 40 && Marshal.ReadInt32(data, 12) == 2)
        {
            if (length < 312 || Marshal.ReadInt32(data, 20) != 2 || Marshal.ReadInt32(data, 36) != 3)
            { position = null; registration = null; warning = "Incomplete live position / registration"; return true; }
            var values = new double[2]; Marshal.Copy(IntPtr.Add(data, 40), values, 0, 2);
            var candidate = new GeoPosition(values[0], values[1]);
            position = candidate.IsValid ? candidate : null;
            registration = Marshal.PtrToStringAnsi(IntPtr.Add(data, 56), 256)?.TrimEnd('\0').Trim();
            positionAt = DateTime.UtcNow;
            nearby = AirportCatalog.Nearest(position, airports.Values);
            return true;
        }
        if (id == 15 && length >= 284)
        {
            var request = Marshal.ReadInt32(data, 12);
            if (request == 101) { running = Marshal.ReadInt32(data, 16) == 1; stateAt = DateTime.UtcNow; return true; }
            if (request == 102)
            {
                var path = Marshal.PtrToStringAnsi(IntPtr.Add(data, 24), 260)?.TrimEnd('\0') ?? "";
                (route, planStatus) = SimulatorFlightPlan.Read(path); routeAt = DateTime.UtcNow; return true;
            }
        }
        // SDK 1.7.3 packed airport record: Ident[9], Region[3], lat/lon/alt doubles = 36 bytes.
        if (id == 18 && length >= 28 && Marshal.ReadInt32(data, 12) == 200)
        {
            var count = Marshal.ReadInt32(data, 16);
            if (count < 0 || count > 10000 || 28L + count * 36L > length)
            { warning = "Incomplete simulator airport list; using offline reference"; airports.Clear(); return true; }
            for (var i = 0; i < count; i++)
            {
                var offset = 28 + i * 36;
                var ident = Marshal.PtrToStringAnsi(IntPtr.Add(data, offset), 9)?.TrimEnd('\0') ?? "";
                var coords = new double[2]; Marshal.Copy(IntPtr.Add(data, offset + 12), coords, 0, 2);
                var location = new GeoPosition(coords[0], coords[1]);
                if (!location.IsValid || !FlightIdentity.IsAirportId(ident)) continue;
                var reference = AirportCatalog.Find(ident);
                pendingAirports[ident] = new(ident, reference?.Name ?? ident, reference?.City ?? "", location, "MSFS airport facilities");
            }
            if (Marshal.ReadInt32(data, 20) + 1 == Marshal.ReadInt32(data, 24))
            {
                airports.Clear(); foreach (var entry in pendingAirports) airports[entry.Key] = entry.Value;
            }
            return true;
        }
        return false;
    }
    [DllImport("SimConnect.dll", EntryPoint = "SimConnect_AddToDataDefinition", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern int AddDefinition(IntPtr handle, uint definition, string name, string? units, int type, float epsilon, uint id);
    [DllImport("SimConnect.dll", EntryPoint = "SimConnect_RequestDataOnSimObject", ExactSpelling = true)]
    private static extern int Request(IntPtr handle, uint request, uint definition, uint obj, int period, uint flags, uint origin, uint interval, uint limit);
    [DllImport("SimConnect.dll", EntryPoint = "SimConnect_RequestSystemState", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern int SystemState(IntPtr handle, uint request, string state);
    [DllImport("SimConnect.dll", EntryPoint = "SimConnect_RequestFacilitiesList_EX1", ExactSpelling = true)]
    private static extern int Facilities(IntPtr handle, int type, uint request);
    [DllImport("SimConnect.dll", EntryPoint = "SimConnect_GetLastSentPacketID", ExactSpelling = true)]
    private static extern int LastPacket(IntPtr handle, out uint packet);
}
