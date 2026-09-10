using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal static class FlightLabSource
{
    internal static async Task RunAsync(Action<string> status, Action<LiveReading> received, CancellationToken token, string? pipeName = null)
    {
        using var pipe = new NamedPipeClientStream(".", pipeName ?? FlightLabProtocol.PipeName, PipeDirection.In, PipeOptions.Asynchronous);
        try { await pipe.ConnectAsync(2500, token); }
        catch (TimeoutException error) { throw new IOException("Alpha 6 Flight Lab is not running. Open Flight Lab and try again.", error); }
        status("Connected to Alpha 6 Flight Lab. Waiting for virtual aircraft data.");
        using var reader = new StreamReader(pipe);
        while (!token.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(token);
            if (line is null) throw new IOException("Alpha 6 Flight Lab stopped or simulated a simulator crash.");
            FlightLabFrame frame;
            try { frame = JsonSerializer.Deserialize<FlightLabFrame>(line) ?? throw new InvalidDataException("Empty Flight Lab frame."); }
            catch (JsonException error) { throw new IOException("Alpha 6 Flight Lab sent invalid telemetry.", error); }
            received(ParseFrame(frame));
        }
    }

    internal static LiveReading ParseFrame(FlightLabFrame frame)
    {
        if (frame.SchemaVersion != FlightLabProtocol.SchemaVersion) throw new InvalidDataException("Unsupported Flight Lab protocol version.");
        if (string.IsNullOrWhiteSpace(frame.Aircraft) || !double.IsFinite(frame.GroundSpeedKnots) || frame.GroundSpeedKnots < 0 || frame.RouteProgress is <0 or >1)
            throw new InvalidDataException("Invalid Flight Lab aircraft or groundspeed.");
        return new LiveReading(frame.Aircraft.Trim(),new Telemetry(frame.SimulatorUtc,frame.OnGround,frame.GroundSpeedKnots,
            frame.ParkingBrake,frame.EnginesRunning,frame.Paused,frame.Slewing,AltitudeFeet:frame.AltitudeFeet,
            IndicatedAirspeedKnots:frame.IndicatedAirspeedKnots,VerticalSpeedFeetPerMinute:frame.VerticalSpeedFeetPerMinute,
            GearExtendedRatio:frame.GearExtendedRatio,AltitudeAboveGroundFeet:frame.AltitudeAboveGroundFeet),"FLIGHT LAB",frame.ScenarioEvent,frame.RouteProgress);
    }
}
