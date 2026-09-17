using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Alpha6Ops.Accounts;
using Alpha6Ops.Identity;
using Alpha6Ops.Server;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

var migrateAccounts = args.Contains("--migrate-accounts", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(argument => argument != "--migrate-accounts").ToArray());
if (migrateAccounts)
{
    var connection = builder.Configuration.GetConnectionString("Accounts");
    if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Configure ConnectionStrings:Accounts with the migration database credential.");
    var options = new DbContextOptionsBuilder<AccountsDbContext>().UseNpgsql(connection).Options;
    await using var database = new AccountsDbContext(options);
    await database.Database.MigrateAsync();
    Console.WriteLine("Account database migrations applied.");
    return;
}
var settings = ServerSettings.Read(builder.Configuration);
if (!settings.IsConfigured && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Configure Auth0 Authority, Audience, ClientId, ClientSecret and the Accounts connection string before starting this environment.");
// Console output is portable on Windows and Azure and does not require Event Log privileges.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
var protection = builder.Services.AddDataProtection().SetApplicationName("Alpha6Ops.Server");
var keyDirectory = builder.Configuration["DataProtection:KeyDirectory"];
var certificatePath = builder.Configuration["DataProtection:CertificatePath"];
if (!builder.Environment.IsDevelopment() && (string.IsNullOrWhiteSpace(keyDirectory) || string.IsNullOrWhiteSpace(certificatePath)))
    throw new InvalidOperationException("Production requires DataProtection KeyDirectory and CertificatePath for a persistent encrypted cookie key ring.");
if (!string.IsNullOrWhiteSpace(keyDirectory) && !string.IsNullOrWhiteSpace(certificatePath))
{
    var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath, builder.Configuration["DataProtection:CertificatePassword"], X509KeyStorageFlags.EphemeralKeySet);
    if (!certificate.HasPrivateKey || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
        throw new InvalidOperationException("The data-protection certificate must include a private key and be unexpired.");
    protection.PersistKeysToFileSystem(new DirectoryInfo(keyDirectory)).ProtectKeysWithCertificate(certificate);
}
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<ReleaseSettings>(builder.Configuration.GetSection("Release"));
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 32 * 1024);
builder.Services.AddRazorPages(o => o.Conventions.AuthorizeFolder("/Account"));
builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "__Host-Alpha6.Antiforgery";
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("requests", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue("sub") ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
// This outer limiter protects authentication itself; the endpoint limiter uses validated identity.
builder.Services.AddSingleton(PartitionedRateLimiter.Create<HttpContext, string>(context =>
    RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
var auth = builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = settings.IsConfigured ? "Auth0" : CookieAuthenticationDefaults.AuthenticationScheme;
});
auth.AddCookie(o =>
{
    o.Cookie.Name = "__Host-Alpha6.Session";
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = false;
});
if (settings.IsConfigured)
{
    builder.Services.AddDbContext<AccountsDbContext>(o => o.UseNpgsql(settings.ConnectionString));
    builder.Services.AddScoped<AccountsService>();
    auth.AddJwtBearer(o =>
    {
        o.Authority = settings.Authority;
        o.Audience = settings.Audience;
        o.RequireHttpsMetadata = true;
        o.MapInboundClaims = false;
        o.IncludeErrorDetails = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = settings.Authority, ValidAudience = settings.Audience,
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
            ValidateIssuerSigningKey = true, RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.FromSeconds(30)
        };
        o.Events.OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await Results.Problem(statusCode: 401, title: "A valid access token is required.", extensions: new Dictionary<string, object?> { ["code"] = "authentication_required" }).ExecuteAsync(context.HttpContext);
        };
    });
    auth.AddOpenIdConnect("Auth0", o =>
    {
        o.Authority = settings.Authority;
        o.ClientId = settings.ClientId;
        o.ClientSecret = settings.ClientSecret;
        o.ResponseType = OpenIdConnectResponseType.Code;
        o.UsePkce = true;
        o.MapInboundClaims = false;
        o.SaveTokens = false;
        o.GetClaimsFromUserInfoEndpoint = false;
        o.CallbackPath = "/signin-oidc";
        o.SignedOutCallbackPath = "/signout-callback-oidc";
        o.Scope.Clear(); o.Scope.Add("openid"); o.Scope.Add("profile"); o.Scope.Add("email");
        o.TokenValidationParameters.ValidIssuer = settings.Authority;
        o.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256];
        o.TokenValidationParameters.NameClaimType = TrustedActor.Prefix + "display_name";
        o.Events.OnRedirectToIdentityProvider = context =>
        {
            context.ProtocolMessage.SetParameter("audience", settings.Audience);
            if (context.Properties.Items.ContainsKey("signup")) context.ProtocolMessage.SetParameter("screen_hint", "signup");
            if (context.Properties.Items.ContainsKey("stepup"))
            {
                context.ProtocolMessage.Prompt = "login";
                context.ProtocolMessage.MaxAge = "0";
                context.ProtocolMessage.AcrValues = "http://schemas.openid.net/pape/policies/2007/06/multi-factor";
            }
            return Task.CompletedTask;
        };
        o.Events.OnTokenValidated = context =>
        {
            try { TrustedActor.Read(context.Principal!, settings); }
            catch (IdentityException) { context.Fail("Required account claims are unavailable."); }
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToIdentityProviderForSignOut = context =>
        {
            context.ProtocolMessage.ClientId = settings.ClientId;
            return Task.CompletedTask;
        };
        o.Events.OnRemoteFailure = context =>
        {
            context.HandleResponse(); context.Response.Redirect("/AuthError"); return Task.CompletedTask;
        };
    });
}
builder.Services.AddAuthorization(o => o.AddPolicy("desktop", policy =>
    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser()));
var app = builder.Build();
app.UseExceptionHandler("/Error");
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self'; img-src 'self'; script-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    if (context.Request.Path.StartsWithSegments("/Account") || context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/auth"))
        context.Response.Headers.CacheControl = "no-store";
    if (!settings.IsConfigured && (context.Request.Path.StartsWithSegments("/auth") || context.Request.Path.StartsWithSegments("/Account") || context.Request.Path.StartsWithSegments("/api")))
    {
        await Results.Problem(statusCode: 503, title: "Account services are not configured.", extensions: new Dictionary<string, object?> { ["code"] = "identity_unavailable" }).ExecuteAsync(context);
        return;
    }
    try { await next(context); }
    catch (IdentityException ex)
    {
        await Results.Problem(statusCode: ex.StatusCode, title: ex.Message, extensions: new Dictionary<string, object?> { ["code"] = ex.Code }).ExecuteAsync(context);
    }
});
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.Use(async (context, next) =>
{
    using var lease = await context.RequestServices.GetRequiredService<PartitionedRateLimiter<HttpContext>>()
        .AcquireAsync(context, 1, context.RequestAborted);
    if (!lease.IsAcquired)
    {
        context.Response.Headers.RetryAfter = "60";
        await Results.Problem(statusCode: 429, title: "Too many requests. Try again shortly.").ExecuteAsync(context);
        return;
    }
    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapRazorPages().RequireRateLimiting("requests");
app.MapGet("/auth/login", () => Results.Challenge(new AuthenticationProperties { RedirectUri = "/Account" }, ["Auth0"])).RequireRateLimiting("requests");
app.MapGet("/auth/signup", () =>
{
    var properties = new AuthenticationProperties { RedirectUri = "/Account" };
    properties.Items["signup"] = "true";
    return Results.Challenge(properties, ["Auth0"]);
}).RequireRateLimiting("requests");
app.MapGet("/auth/step-up", () =>
{
    var properties = new AuthenticationProperties { RedirectUri = "/Account" };
    properties.Items["stepup"] = "true";
    return Results.Challenge(properties, ["Auth0"]);
}).RequireAuthorization().RequireRateLimiting("requests");
if (settings.IsConfigured) app.MapAccountApi();
app.Run();

public partial class Program { }
