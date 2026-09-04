using Alpha6Ops.Core;

var builder = WebApplication.CreateBuilder(args);
// Fail closed: this unsigned demonstration must never become a production service by accident.
if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Demo API requires Development. Production authentication is not implemented.");
builder.WebHost.UseUrls("http://127.0.0.1:5080");
var app = builder.Build();
string ResolveReplayPath() => Path.GetFullPath(builder.Configuration["ReplayPath"] ?? "../../samples/delayed-flight.jsonl", app.Environment.ContentRootPath);
string SamplesDirectory() => Path.GetDirectoryName(ResolveReplayPath())!;
string[] ListFixtures() => Directory.Exists(SamplesDirectory())
    ? Directory.GetFiles(SamplesDirectory(), "*.jsonl").Select(Path.GetFileName).OfType<string>().OrderBy(f => f, StringComparer.Ordinal).ToArray()
    : [];
// A requested fixture is only ever looked up against files this process already enumerated in
// its own samples directory; the query string itself never reaches the filesystem as a path.
string? ResolveFixturePath(string? fixture) => fixture is null
    ? ResolveReplayPath()
    : ListFixtures().Contains(fixture) ? Path.Combine(SamplesDirectory(), fixture) : null;
app.MapGet("/api/health", () => new { status = "ok", mode = "local-demo" });
app.MapGet("/api/tenants/{tenantId}/rotation", (string tenantId) =>
    tenantId == "alpha6" ? Results.Ok(new { tenantId, aircraftId = "N600A6", legs = RotationPlanner.Project(Demo.Rotation()) }) : Results.NotFound());
app.MapGet("/api/tenants/{tenantId}/replay/fixtures", (string tenantId) =>
    tenantId == "alpha6" ? Results.Ok(new { tenantId, fixtures = ListFixtures() }) : Results.NotFound());
// Read-only simulation: each request owns its state. Nothing changes a real assignment.
app.MapGet("/api/tenants/{tenantId}/replay", async (string tenantId, string? fixture, CancellationToken cancellationToken) =>
{
    if (tenantId != "alpha6") return Results.NotFound();
    var path = ResolveFixturePath(fixture);
    if (path is null) return Results.NotFound();
    var session = new FlightSession(Demo.Rotation());
    var events = new List<FlightEvent>();
    await foreach (var sample in new JsonReplay(path).ReadAsync(cancellationToken))
        if (session.Observe(sample) is { } e) events.Add(e);
    return Results.Ok(new { tenantId, aircraftId = "N600A6", phase = session.Phase.ToString(), events,
        legs = RotationPlanner.Project(session.Rotation) });
});
// Read-only: one precomputed timeline per request. Snapshots are for client-side scrubbing,
// never for re-driving the phase detector — it stays a forward-only, single-pass state machine.
app.MapGet("/api/tenants/{tenantId}/replay/timeline", async (string tenantId, string? fixture, CancellationToken cancellationToken) =>
{
    if (tenantId != "alpha6") return Results.NotFound();
    var path = ResolveFixturePath(fixture);
    if (path is null) return Results.NotFound();
    var timeline = await TimelineBuilder.BuildAsync(new JsonReplay(path), cancellationToken);
    return Results.Ok(new
    {
        tenantId,
        aircraftId = "N600A6",
        phase = timeline.FinalPhase.ToString(),
        snapshots = timeline.Snapshots.Select(s => new { s.Index, s.Sample, phase = s.Phase.ToString(), s.EventsFiredCount }),
        events = timeline.Events.Select(e => new { phase = e.Phase.ToString(), e.At })
    });
});
// Read-only: a post-flight summary, not a live/scrubbable view — milestone events plus the
// derived duration of each confirmed phase, alongside the same delay projection as /replay.
app.MapGet("/api/tenants/{tenantId}/replay/debrief", async (string tenantId, string? fixture, CancellationToken cancellationToken) =>
{
    if (tenantId != "alpha6") return Results.NotFound();
    var path = ResolveFixturePath(fixture);
    if (path is null) return Results.NotFound();
    var session = new FlightSession(Demo.Rotation());
    var events = new List<FlightEvent>();
    await foreach (var sample in new JsonReplay(path).ReadAsync(cancellationToken))
        if (session.Observe(sample) is { } e) events.Add(e);
    return Results.Ok(new
    {
        tenantId,
        aircraftId = "N600A6",
        phase = session.Phase.ToString(),
        events = events.Select(e => new { phase = e.Phase.ToString(), e.At }),
        segments = DebriefSummary.Segments(events).Select(s => new { phase = s.Phase.ToString(), s.StartedAt, s.EndedAt }),
        legs = RotationPlanner.Project(session.Rotation)
    });
});
app.Run();
