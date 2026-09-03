using Alpha6Ops.Core;
using System.Text.Json;

if (args.Contains("--simconnect"))
{
    Console.Error.WriteLine("SimConnect adapter is not implemented. Install the MSFS 2024 SDK and implement the Windows message-pump bridge described in docs/simconnect.md. Use replay today.");
    return 2;
}
var path = args.FirstOrDefault() ?? "samples/delayed-flight.jsonl";
var session = new FlightSession(Demo.Rotation());
await foreach (var sample in new JsonReplay(path).ReadAsync())
    if (session.Observe(sample) is { } e) Console.WriteLine($"{e.At:O} {e.Phase}");
Console.WriteLine(JsonSerializer.Serialize(RotationPlanner.Project(session.Rotation), new JsonSerializerOptions { WriteIndented = true }));
return session.Phase == FlightPhase.Complete ? 0 : 1;
