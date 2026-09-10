using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal static class FlightLabSmokeTest
{
    internal static async Task RunAsync(Action<bool,string> check)
    {
        var pipeName=FlightLabProtocol.PipeName+"."+Guid.NewGuid().ToString("N");
        var at=new DateTimeOffset(2026,9,8,15,0,0,TimeSpan.Zero);
        var frame=new FlightLabFrame(FlightLabProtocol.SchemaVersion,"Alpha 6 Test A321",at,true,18,false,true,false,false,"TAXI OUT","SMOKE_TEST",.25);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var server=Task.Run(async()=>
        {
            using var pipe=new NamedPipeServerStream(pipeName,PipeDirection.Out,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous);
            await pipe.WaitForConnectionAsync(timeout.Token);
            using var writer=new System.IO.StreamWriter(pipe){AutoFlush=true};
            await writer.WriteLineAsync(JsonSerializer.Serialize(frame));
            await Task.Delay(250,timeout.Token);
        },timeout.Token);
        LiveReading? received=null;var statuses=new List<string>();
        try{await FlightLabSource.RunAsync(statuses.Add,value=>{received=value;timeout.Cancel();},timeout.Token,pipeName);}
        catch(OperationCanceledException)when(received is not null){}
        try{await server;}catch(OperationCanceledException)when(received is not null){}
        check(statuses.Exists(s=>s.Contains("Connected to Alpha 6 Flight Lab",StringComparison.Ordinal)),"Flight Lab named-pipe source reports a connection");
        check(received is {Source:"FLIGHT LAB",Aircraft:"Alpha 6 Test A321",ScenarioEvent:"SMOKE_TEST",RouteProgress:.25}&&received.Telemetry.OnGround&&received.Telemetry.GroundSpeedKnots==18,"Flight Lab telemetry and route progress enter the live-reading boundary unchanged");
    }
}
