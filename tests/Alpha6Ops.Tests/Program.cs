using Alpha6Ops.Core;

var count = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine($"PASS {name}"); count++; }
void Reject(Action action, string name) { try { action(); } catch (ArgumentException) { Check(true, name); return; } throw new Exception(name); }
var rotation = Demo.Rotation();
var normal = RotationPlanner.Project(rotation);
Check(normal.All(x => x.DepartureDelayMinutes == 0), "on-time rotation");
var session = new FlightSession(rotation);
var events = new List<FlightEvent>();
await foreach (var sample in new JsonReplay("samples/delayed-flight.jsonl").ReadAsync())
    if (session.Observe(sample) is { } e) events.Add(e);
Check(events.Select(e => e.Phase).SequenceEqual(new[] { FlightPhase.TaxiOut, FlightPhase.Airborne, FlightPhase.TaxiIn, FlightPhase.Complete }), "replay milestones");
var projected = RotationPlanner.Project(session.Rotation);
Check(projected[0].DepartureDelayMinutes == 25 && projected[0].ArrivalDelayMinutes == 30, "actual milestone timing");
Check(projected[1].DepartureDelayMinutes == 30 && projected[2].DepartureDelayMinutes == 5, "delay propagation and slack recovery");
Check(projected[0].Completed && !projected[1].Completed, "actual versus projected completion");
var t = DateTimeOffset.Parse("2026-09-02T23:59:58Z");
var detector = new PhaseDetector();
Telemetry S(int seconds, bool ground = true, double speed = 3, bool brake = false, bool engines = true) => new(t.AddSeconds(seconds), ground, speed, brake, engines);
Check(detector.Observe(S(0)) is null && detector.Observe(S(2)) is null, "debounce transient movement");
Check(detector.Observe(S(3))?.At == t, "midnight transition preserves first timestamp");
Check(detector.Observe(S(3, false)) is null && detector.Observe(S(1, false)) is null, "duplicates and stale samples ignored");
detector.Observe(S(4, false));
detector.Observe(S(5, false) with { Paused = true });
Check(detector.Observe(S(8, false)) is null, "pause clears pending transition");
Check(detector.Observe(S(11, false))?.Phase == FlightPhase.Airborne, "resume needs fresh confirmation");
detector.Observe(S(12));
detector.Observe(S(13, false));
Check(detector.Observe(S(15)) is null && detector.Phase == FlightPhase.Airborne, "brief ground bounce ignored");
Check(detector.Observe(S(18))?.Phase == FlightPhase.TaxiIn, "confirmed landing");
detector.Observe(S(19, false));
Check(detector.Observe(S(22, false))?.Phase == FlightPhase.Airborne, "go-around reopens airborne phase");
detector.Observe(S(23)); detector.Observe(S(26));
detector.Observe(S(27, speed: 0, brake: true, engines: false));
Check(detector.Observe(S(50, speed: 0, brake: true, engines: false)) is null, "telemetry gaps cannot confirm block-in");
detector.Observe(S(51, speed: 0, brake: true, engines: false));
Check(detector.Observe(S(54, speed: 0, brake: true, engines: false))?.Phase == FlightPhase.Complete, "block-in requires stable shutdown");
Check(detector.Observe(S(55, false)) is null, "completed flight remains complete");
Reject(() => new PhaseDetector().Observe(S(0, speed: double.NaN)), "reject invalid telemetry");
Reject(() => RotationPlanner.Project(rotation with { MinimumTurnMinutes = -1 }), "reject negative turn");
Reject(() => RotationPlanner.Project(rotation with { Legs = [rotation.Legs[0] with { ActualIn = t }] }), "reject arrival without departure");
Reject(() => RotationPlanner.Project(rotation with { Legs = [rotation.Legs[0], rotation.Legs[1] with { Origin = "KLAX" }] }), "reject disconnected rotation");
Reject(() => RotationPlanner.Project(rotation with { Legs = [rotation.Legs[0], rotation.Legs[1] with { ActualOut = rotation.Legs[0].ScheduledIn }] }), "reject impossible actual turnaround");
var early = RotationPlanner.Project(rotation with { Legs = [rotation.Legs[0] with { ActualOut = rotation.Legs[0].ScheduledOut.AddMinutes(-5), ActualIn = rotation.Legs[0].ScheduledIn.AddMinutes(-10) }, rotation.Legs[1]] });
Check(early[0].ArrivalDelayMinutes == -10 && early[1].DepartureDelayMinutes == 0, "early arrival does not pull next departure before schedule");
var logDirectory = Path.Combine("work", "log-tests", Guid.NewGuid().ToString("N"));
using (var journal = new TestFlightLog(logDirectory, "test", "replay"))
{
    journal.Record("telemetry", t, new { onGround = true, aircraft = "Test aircraft" });
    journal.SaveExport();
    using (var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(journal.ExportPath)))
    {
        Check(json.RootElement.GetProperty("eventCount").GetInt32() == 2, "export while recording includes flushed events");
        Check(!json.RootElement.GetProperty("sessionEnded").GetBoolean(), "in-progress export is marked incomplete");
        Check(json.RootElement.GetProperty("events")[1].GetProperty("simulatorUtc").GetDateTimeOffset() == t, "log preserves simulator UTC independently of receipt time");
    }
    journal.End("test_complete"); journal.End("duplicate_end");
    using var ended = System.Text.Json.JsonDocument.Parse(File.ReadAllText(journal.ExportPath));
    Check(ended.RootElement.GetProperty("eventCount").GetInt32() == 3 && ended.RootElement.GetProperty("sessionEnded").GetBoolean(), "session end is recorded exactly once");
}
string interruptedJournal;
using (var journal = new TestFlightLog(logDirectory, "test", "interrupted")) { interruptedJournal = journal.JournalPath; }
File.AppendAllText(interruptedJournal, "{\"partial\":");
var recoveredPath = Path.ChangeExtension(interruptedJournal, ".json");
TestFlightLog.Export(interruptedJournal, recoveredPath);
using (var recovered = System.Text.Json.JsonDocument.Parse(File.ReadAllText(recoveredPath)))
    Check(recovered.RootElement.GetProperty("incompleteLastLine").GetBoolean() && recovered.RootElement.GetProperty("eventCount").GetInt32() == 1, "interrupted final write is flagged and earlier events recovered");
Console.WriteLine($"{count} checks passed.");
