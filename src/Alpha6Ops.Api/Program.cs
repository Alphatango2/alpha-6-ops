using Alpha6Ops.Core;

var builder = WebApplication.CreateBuilder(args);
// Fail closed: this unsigned demonstration must never become a production service by accident.
if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Demo API requires Development. Production authentication is not implemented.");
builder.WebHost.UseUrls("http://127.0.0.1:5080");
var app = builder.Build();
app.MapGet("/api/health", () => new { status = "ok", mode = "local-demo" });
app.MapGet("/api/tenants/{tenantId}/rotation", (string tenantId) =>
    tenantId == "alpha6" ? Results.Ok(new { tenantId, aircraftId = "N600A6", legs = RotationPlanner.Project(Demo.Rotation()) }) : Results.NotFound());
// Read-only simulation: each request owns its state. Nothing changes a real assignment.
app.MapGet("/api/tenants/{tenantId}/replay", async (string tenantId, CancellationToken cancellationToken) =>
{
    if (tenantId != "alpha6") return Results.NotFound();
    var session = new FlightSession(Demo.Rotation());
    var events = new List<FlightEvent>();
    var path = Path.GetFullPath(builder.Configuration["ReplayPath"] ?? "../../samples/delayed-flight.jsonl", app.Environment.ContentRootPath);
    await foreach (var sample in new JsonReplay(path).ReadAsync(cancellationToken))
        if (session.Observe(sample) is { } e) events.Add(e);
    return Results.Ok(new { tenantId, aircraftId = "N600A6", phase = session.Phase.ToString(), events,
        legs = RotationPlanner.Project(session.Rotation) });
});
app.Run();
