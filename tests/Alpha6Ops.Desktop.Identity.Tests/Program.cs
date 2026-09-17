using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Alpha6Ops.Desktop;
using Alpha6Ops.Identity;

var count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    Console.WriteLine("PASS " + name); count++;
}
var now = DateTimeOffset.UtcNow;
var airline = new AirlineWorkspace(Guid.NewGuid(), "test-air", "Test Airline", "TEST", AirlinePlan.Community,
    "active", MembershipStatus.Active, [AirlineRole.Pilot], false, [Capabilities.AirlineRead]);
var bootstrap = new BootstrapResponse(new(Guid.NewGuid(), "Test Pilot", "pilot@example.invalid", true, AccountStatus.Active),
    new(PersonalPlan.Free, "active", null), Capabilities.Personal, [airline], new(airline.Id), now, now.AddDays(30));
Check(OfflineSessionPolicy.MayOpen(bootstrap, now, now, now.AddDays(29)), "trusted offline window allows local recording before expiry");
Check(!OfflineSessionPolicy.MayOpen(bootstrap, now, now, now.AddDays(30)), "offline lease expires at thirty days exactly");
Check(!OfflineSessionPolicy.MayOpen(bootstrap, now, now.AddDays(2), now.AddDays(1)), "clock rollback cannot extend offline access");
Check(!OfflineSessionPolicy.MayOpen(bootstrap with { OfflineExpiresAt = now.AddDays(1) }, now, now, now.AddDays(2)), "server expiry is honored when shorter than local maximum");
Check(!OfflineSessionPolicy.MayOpen(bootstrap with { Account = bootstrap.Account with { Status = AccountStatus.Suspended } }, now, now, now), "suspended account cannot open cached data");
Check(OfflineSessionPolicy.ValidateWorkspace(bootstrap, new(Guid.NewGuid())) == WorkspaceSelection.Personal, "unknown airline falls back to personal");
Check(OfflineSessionPolicy.ValidateWorkspace(bootstrap with { Airlines = [airline with { MembershipStatus = MembershipStatus.Suspended }] }, new(airline.Id)) == WorkspaceSelection.Personal, "suspended airline cannot remain selected");

var directory = Path.GetFullPath(Path.Combine("work", "identity-client-tests", Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(directory);
var config = new IdentityConfiguration("https://identity.example.invalid", "test-native", "https://api.example.invalid", "https://api.example.invalid/", "https://portal.example.invalid/");
var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(config))));
var sessionPath = Path.Combine(directory, "Identity", "session.bin");
SavedAccountSession Seed(string refresh = "old-refresh")
{
    var saved = new SavedAccountSession(key, refresh, bootstrap, now, now, new(airline.Id));
    Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
    File.WriteAllBytes(sessionPath, ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(saved), null, DataProtectionScope.CurrentUser));
    return saved;
}
SavedAccountSession Read() => JsonSerializer.Deserialize<SavedAccountSession>(ProtectedData.Unprotect(File.ReadAllBytes(sessionPath), null, DataProtectionScope.CurrentUser))!;
HttpResponseMessage Json(object value, HttpStatusCode code = HttpStatusCode.OK) => new(code) { Content = JsonContent.Create(value) };
var mode = "offline";
var tokensSeen = new List<string>();
async Task<HttpResponseMessage> Handle(HttpRequestMessage request)
{
    if (mode == "offline") throw new HttpRequestException("Simulated network outage");
    if (request.RequestUri!.AbsolutePath == "/oauth/token")
    {
        tokensSeen.Add(await request.Content!.ReadAsStringAsync());
        if (mode == "revoked") return Json(new { error = "invalid_grant" }, HttpStatusCode.BadRequest);
        return Json(new { access_token = "test-access", refresh_token = "new-refresh", token_type = "Bearer", expires_in = 300 });
    }
    if (request.RequestUri.AbsolutePath == "/api/v1/me/bootstrap")
    {
        Check(request.Headers.Authorization?.Parameter == "test-access", "bootstrap sends bearer credential only to configured API");
        if (mode == "bootstrap-down") return Json(new { error = "temporarily_unavailable" }, HttpStatusCode.ServiceUnavailable);
        if (mode == "forbidden") return Json(new { code = "account_suspended" }, HttpStatusCode.Forbidden);
        if (mode == "other-account") return Json(bootstrap with { Account = bootstrap.Account with { Id = Guid.NewGuid() } });
        return Json(bootstrap);
    }
    return new(HttpStatusCode.NoContent);
}
DesktopAccountSession Session() => new(config, directory, () => new FakeHandler(Handle));
Seed();
var session = Session();
Check(await session.RestoreAsync(default) && session.IsOffline, "network failure restores trusted local mode");
Check(session.MayStartNewFlight(now.AddDays(29)) && !session.MayStartNewFlight(now.AddDays(30)), "open app cannot start a new flight after offline authorization expires");
Check(Read().RefreshToken == "old-refresh", "network failure preserves refresh credential for reconnection");
Check(session.Workspace.AirlineId == airline.Id, "offline restore retains the original flight workspace");
Check(session.DataDirectory.Contains(bootstrap.Account.Id.ToString("N")) && session.DataDirectory.EndsWith(airline.Id.ToString("N")), "local data is scoped to immutable account and airline IDs");
mode = "bootstrap-down";
session = Session();
Check(await session.RestoreAsync(default) && session.IsOffline, "API outage after token refresh preserves trusted local mode");
Check(Read().RefreshToken == "new-refresh", "new rotating credential is durable before API bootstrap");
mode = "online";
session = Session();
Check(await session.RestoreAsync(default) && !session.IsOffline, "reconnection revalidates account and memberships");
Check(tokensSeen.Last().Contains("refresh_token=new-refresh"), "reconnection uses the most recent rotating token");
var protectedBytes = File.ReadAllBytes(sessionPath);
Check(!Encoding.UTF8.GetString(protectedBytes).Contains("new-refresh"), "refresh credential is protected on disk");
mode = "revoked";
Check(!await session.RestoreAsync(default) && session.Bootstrap is null && !File.Exists(sessionPath), "known token revocation clears cache instead of offline fallback");
foreach (var failure in new[] { "forbidden", "other-account" })
{
    Seed(); mode = failure; session = Session();
    try { await session.RestoreAsync(default); throw new Exception("Expected authorization failure"); }
    catch (InvalidOperationException) { Check(session.Bootstrap is null && !File.Exists(sessionPath), failure + " cannot retain cached access"); }
}
Seed(); mode = "online"; session = Session(); await session.RestoreAsync(default);
await session.SignOutAsync();
Check(session.Bootstrap is null && !File.Exists(sessionPath), "logout removes protected credentials");
Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
File.WriteAllBytes(sessionPath, [1, 2, 3]);
Check(!await Session().RestoreAsync(default), "corrupt credential cache fails closed");
Console.WriteLine($"{count} desktop identity checks passed. Artifacts: {directory}");

sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handle(request);
}
