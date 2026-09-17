using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Alpha6Ops.Accounts;
using Alpha6Ops.Identity;
using Alpha6Ops.Server;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

var failures = 0;
void Check(bool condition, string scenario)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {scenario}");
    if (!condition) failures++;
}
using (var setup = new ServerFactory(null))
using (var client = setup.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false }))
{
    var landing = await client.GetAsync("/");
    Check(landing.StatusCode == HttpStatusCode.OK, "Development landing available without Auth0");
    Check((await landing.Content.ReadAsStringAsync()).Contains("not available on this installation"), "Unconfigured registration is explained");
    Check((await client.GetAsync("/api/v1/me/bootstrap")).StatusCode == HttpStatusCode.ServiceUnavailable, "Unconfigured API fails closed");
    Check((await client.GetAsync("/auth/login")).StatusCode == HttpStatusCode.ServiceUnavailable, "Unconfigured login fails closed");
    Check((await client.GetStringAsync("/Download")).Contains("not been published"), "Missing release cannot advertise a fake download");
    Check(landing.Headers.Contains("Content-Security-Policy"), "Security headers present");
}
var connection = Environment.GetEnvironmentVariable("ALPHA6_SERVER_TEST_DATABASE");
if (string.IsNullOrWhiteSpace(connection))
{
    Console.Error.WriteLine("ALPHA6_SERVER_TEST_DATABASE is required for authenticated integration tests.");
    return 1;
}
foreach (var productionConnection in new string?[] { null, connection })
{
    try
    {
        using var production = new ServerFactory(productionConnection, "Production");
        using var unexpectedClient = production.CreateClient();
        Check(false, "Production rejects missing identity or persistent encrypted keyring configuration");
    }
    catch (InvalidOperationException exception)
    {
        Check(exception.Message.Contains(productionConnection is null ? "Configure Auth0" : "DataProtection"),
            productionConnection is null ? "Production rejects missing identity configuration" : "Production rejects missing encrypted keyring configuration");
    }
}
using var host = new ServerFactory(connection);
using var anonymous = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
using (var scope = host.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<AccountsDbContext>().Database.MigrateAsync();
Check((await anonymous.GetAsync("/api/v1/me/bootstrap")).StatusCode == HttpStatusCode.Unauthorized, "Anonymous API is 401, never a browser redirect");
var run = Guid.NewGuid().ToString("N");
var owner = "owner-" + run;
using var pilot = host.Client(owner);
var bootstrap = await pilot.GetAsync("/api/v1/me/bootstrap");
Check(bootstrap.IsSuccessStatusCode, "Verified signed account bootstrap succeeds");
Check(string.Equals((await bootstrap.Content.ReadFromJsonAsync<BootstrapResponse>())?.Account.Email, owner + "@example.test", StringComparison.OrdinalIgnoreCase), "Bootstrap uses trusted token identity");

foreach (var scenario in new[] { "missing-claim", "wrong-issuer", "wrong-audience", "expired", "wrong-signature", "future-mfa" })
{
    using var bad = host.Client(owner, scenario);
    var response = await bad.GetAsync("/api/v1/me/bootstrap");
    Check(response.StatusCode == HttpStatusCode.Unauthorized, $"Reject {scenario}");
}
using var browser = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
var cookieOptions = host.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
var identity = new ClaimsIdentity(host.Claims(owner).Select(p => new Claim(p.Key, p.Value.ToString()!)), CookieAuthenticationDefaults.AuthenticationScheme);
var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) }, CookieAuthenticationDefaults.AuthenticationScheme);
browser.DefaultRequestHeaders.Add("Cookie", "__Host-Alpha6.Session=" + cookieOptions.TicketDataFormat.Protect(ticket));
Check((await browser.GetAsync("/api/v1/me/bootstrap")).StatusCode == HttpStatusCode.Unauthorized, "Website cookie cannot authenticate the desktop API");
var accountPage = await browser.GetAsync("/Account");
Check(accountPage.IsSuccessStatusCode, "Authenticated portal workspace renders");
var accountHtml = await accountPage.Content.ReadAsStringAsync();
Check(accountHtml.Contains("__RequestVerificationToken"), "Portal mutation forms contain antiforgery token");
Check((await browser.PostAsync("/Account?handler=Logout", new FormUrlEncodedContent([]))).StatusCode == HttpStatusCode.BadRequest, "Logout without CSRF token is rejected");
Check((await browser.PostAsync("/Account/Create", new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "Forged" }))).StatusCode == HttpStatusCode.BadRequest, "Airline creation without CSRF token is rejected");

var created = await pilot.PostAsJsonAsync("/api/v1/virtual-airlines", new CreateAirlineRequest("Server Test " + run[..8], "server-" + run[..12], "TEST"));
Check(created.IsSuccessStatusCode, "Owner creates airline with recent MFA proof");
if (created.IsSuccessStatusCode)
{
    var airline = (await created.Content.ReadFromJsonAsync<AirlineWorkspace>())!;
    using var outsider = host.Client("outsider-" + run);
    var denied = await outsider.GetAsync($"/api/v1/virtual-airlines/{airline.Id}/members");
    Check(denied.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, "Cross-tenant roster is denied");
    var invite = await pilot.PostAsJsonAsync($"/api/v1/virtual-airlines/{airline.Id}/invitations", new InviteMemberRequest("outsider-" + run + "@example.test", [AirlineRole.Pilot]));
    Check(invite.IsSuccessStatusCode, "Authorized invitation issuance");
    if (invite.IsSuccessStatusCode)
    {
        var invitation = (await invite.Content.ReadFromJsonAsync<IssuedInvitation>())!;
        Check((await outsider.PostAsJsonAsync("/api/v1/invitations/accept", new { token = invitation.Token })).IsSuccessStatusCode, "Invitation accepts body token without secret in URL");
        Check(!(await pilot.GetStringAsync($"/api/v1/virtual-airlines/{airline.Id}/invitations")).Contains(invitation.Token), "Invitation listing never returns raw token");
    }
}
using (var limited = host.Client("rate-" + run, "missing-claim"))
{
    var beforeLimit = true;
    for (var i = 0; i < 120; i++)
        beforeLimit &= (await limited.GetAsync("/api/v1/me/bootstrap")).StatusCode == HttpStatusCode.Unauthorized;
    Check(beforeLimit && (await limited.GetAsync("/api/v1/me/bootstrap")).StatusCode == HttpStatusCode.TooManyRequests,
        "Validated bearer subject receives per-account rate limit");
    using var other = host.Client("other-rate-" + run, "missing-claim");
    Check((await other.GetAsync("/api/v1/me/bootstrap")).StatusCode == HttpStatusCode.Unauthorized,
        "A second account on the same network has its own endpoint rate bucket");
}
var anonymousLimited = false;
for (var i = 0; i < 301 && !anonymousLimited; i++)
    anonymousLimited = (await anonymous.GetAsync("/api/v1/me/bootstrap")).StatusCode == HttpStatusCode.TooManyRequests;
Check(anonymousLimited, "Anonymous authentication traffic is rate limited before token validation");
Console.WriteLine($"Server integration tests complete: {failures} failures.");
return failures == 0 ? 0 : 1;

sealed class ServerFactory(string? connection, string environment = "Development") : WebApplicationFactory<ServerSettings>
{
    private readonly RSA key = RSA.Create(2048);
    private const string Issuer = "https://identity.example.test/";
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Alpha6Ops.Server")));
        builder.UseEnvironment(environment);
        builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
        builder.UseSetting("Auth0:Authority", connection is null ? "" : Issuer);
        builder.UseSetting("Auth0:Audience", connection is null ? "" : "https://api.example.test");
        builder.UseSetting("Auth0:ClientId", connection is null ? "" : "test-client");
        builder.UseSetting("Auth0:ClientSecret", connection is null ? "" : "test-only-not-a-real-secret");
        builder.UseSetting("ConnectionStrings:Accounts", connection ?? "");
        builder.UseSetting("DataProtection:KeyDirectory", "");
        builder.UseSetting("DataProtection:CertificatePath", "");
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
            var config = new OpenIdConnectConfiguration { Issuer = Issuer };
            config.SigningKeys.Add(new RsaSecurityKey(key) { KeyId = "integration-only" });
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(config);
            });
        });
    }
    public Dictionary<string, object> Claims(string subject) => new()
    {
        ["sub"] = subject, ["iss"] = Issuer,
        [TrustedActor.Prefix + "email"] = subject + "@example.test",
        [TrustedActor.Prefix + "email_verified"] = true,
        [TrustedActor.Prefix + "display_name"] = subject,
        [TrustedActor.Prefix + "mfa"] = true,
        [TrustedActor.Prefix + "mfa_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    };
    public HttpClient Client(string subject, string? variant = null)
    {
        var claims = Claims(subject);
        claims.Remove("iss");
        if (variant == "missing-claim") claims.Remove(TrustedActor.Prefix + "email_verified");
        if (variant == "future-mfa") claims[TrustedActor.Prefix + "mfa_at"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        using var otherKey = RSA.Create(2048);
        var securityKey = new RsaSecurityKey(variant == "wrong-signature" ? otherKey : key) { KeyId = "integration-only" };
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = variant == "wrong-issuer" ? "https://attacker.example.test/" : Issuer,
            Audience = variant == "wrong-audience" ? "wrong" : "https://api.example.test",
            Claims = claims, IssuedAt = DateTime.UtcNow.AddMinutes(-20), NotBefore = DateTime.UtcNow.AddMinutes(-20),
            Expires = variant == "expired" ? DateTime.UtcNow.AddMinutes(-10) : DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new(securityKey, SecurityAlgorithms.RsaSha256)
        };
        var client = CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JsonWebTokenHandler().CreateToken(descriptor));
        return client;
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) key.Dispose(); }
}
