using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Alpha6Ops.Identity;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient;

namespace Alpha6Ops.Desktop;

internal sealed record SavedAccountSession(string ConfigurationKey, string? RefreshToken, BootstrapResponse Bootstrap,
    DateTimeOffset ReceivedAt, DateTimeOffset LastSeenAt, WorkspaceSelection Workspace);

internal sealed class DesktopAccountSession
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string sessionPath;
    private readonly string configurationKey;
    private readonly string rootDirectory;
    private readonly Func<HttpMessageHandler>? handlerFactory;
    private string? accessToken;
    private SavedAccountSession? saved;
    internal IdentityConfiguration Configuration { get; }
    internal BootstrapResponse? Bootstrap => saved?.Bootstrap;
    internal WorkspaceSelection Workspace => saved?.Workspace ?? WorkspaceSelection.Personal;
    internal bool IsOffline { get; private set; }
    internal bool MayStartNewFlight(DateTimeOffset now) => saved is not null
        && OfflineSessionPolicy.MayOpen(saved.Bootstrap, saved.ReceivedAt, saved.LastSeenAt, now);
    internal string WorkspaceName => Workspace.AirlineId is { } id
        ? Bootstrap?.Airlines.FirstOrDefault(a => a.Id == id)?.Name ?? "Flying as a Pilot" : "Flying as a Pilot";
    internal string DataDirectory => Path.Combine(rootDirectory, "accounts", Bootstrap!.Account.Id.ToString("N"),
        Workspace.AirlineId is { } id ? Path.Combine("airlines", id.ToString("N")) : "personal");

    internal DesktopAccountSession(IdentityConfiguration configuration, string? directory = null, Func<HttpMessageHandler>? httpHandlerFactory = null)
    {
        Configuration = configuration;
        rootDirectory = directory ?? CrashReporter.RootDirectory;
        handlerFactory = httpHandlerFactory;
        configurationKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(configuration))));
        sessionPath = Path.Combine(rootDirectory, "Identity", "session.bin");
    }

    private OidcClient Client(SystemLoginBrowser? browser = null) => new(new OidcClientOptions
    {
        Authority = Configuration.Authority, ClientId = Configuration.ClientId,
        Scope = "openid profile email offline_access", RedirectUri = browser?.RedirectUri ?? "http://127.0.0.1/callback/",
        Browser = browser, LoadProfile = false
    });

    internal async Task LoginAsync(CancellationToken token, string? loginHint = null)
    {
        await gate.WaitAsync(token);
        try
        {
            Clear(); // Account changes always require a fresh verified network response.
            using var browser = new SystemLoginBrowser(Configuration.CallbackPort);
            var parameters = new System.Collections.Generic.Dictionary<string, string> { ["audience"] = Configuration.Audience, ["prompt"] = "login" };
            if (!string.IsNullOrWhiteSpace(loginHint)) parameters["login_hint"] = loginHint;
            var result = await Client(browser).LoginAsync(new LoginRequest
            { FrontChannelExtraParameters = new Parameters(parameters) }, token);
            if (result.IsError) throw new InvalidOperationException("Sign-in was canceled or could not be verified. Try again in your browser.");
            await EstablishAsync(result.AccessToken, result.RefreshToken, token, preserveWorkspace: false);
        }
        finally { gate.Release(); }
    }

    internal async Task<bool> RestoreAsync(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            saved ??= Read();
            if (saved is null) return false;
            if (string.IsNullOrEmpty(saved.RefreshToken)) return TryOffline();
            try
            {
                using var http = Http();
                // Use the maintained protocol client. All refresh exchanges are serialized by gate.
                var result = await http.RequestRefreshTokenAsync(new RefreshTokenRequest
                {
                    Address = Configuration.Authority.TrimEnd('/') + "/oauth/token", ClientId = Configuration.ClientId,
                    ClientCredentialStyle = ClientCredentialStyle.PostBody, RefreshToken = saved.RefreshToken
                }, token);
                if (result.IsError)
                {
                    if (result.HttpStatusCode == 0 || (int)result.HttpStatusCode >= 500) return TryOffline();
                    Clear(); return false; // invalid_grant and every explicit provider rejection fail closed.
                }
                if (string.IsNullOrWhiteSpace(result.AccessToken)) { Clear(); return false; }
                // Persist the new refresh token before bootstrap; an API outage must not lose rotation.
                Save(saved with { RefreshToken = result.RefreshToken ?? saved.RefreshToken });
                await EstablishAsync(result.AccessToken, result.RefreshToken, token);
                return true;
            }
            catch (HttpRequestException error) when (error.StatusCode is null || (int)error.StatusCode >= 500)
            { return TryOffline(); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { return TryOffline(); }
        }
        finally { gate.Release(); }
    }

    private async Task EstablishAsync(string access, string? refresh, CancellationToken token, bool preserveWorkspace = true)
    {
        using var client = Api(access);
        using var response = await client.GetAsync("api/v1/me/bootstrap", token);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) { Clear(); throw new InvalidOperationException("This account is not authorized. Sign in again or contact support."); }
        response.EnsureSuccessStatusCode();
        var bootstrap = await response.Content.ReadFromJsonAsync<BootstrapResponse>(cancellationToken: token)
            ?? throw new InvalidDataException("The account service returned an empty response.");
        if (bootstrap.Account.Status != AccountStatus.Active || bootstrap.Account.Id == Guid.Empty) { Clear(); throw new InvalidOperationException("This account is not active."); }
        if (preserveWorkspace && saved is not null && saved.Bootstrap.Account.Id != bootstrap.Account.Id)
        { Clear(); throw new InvalidOperationException("The account identity changed. Sign in again."); }
        var now = DateTimeOffset.UtcNow;
        accessToken = access; IsOffline = false;
        var selected = preserveWorkspace && saved is not null ? saved.Workspace : bootstrap.LastWorkspace;
        Save(new(configurationKey, refresh ?? saved?.RefreshToken, bootstrap, now, now, OfflineSessionPolicy.ValidateWorkspace(bootstrap, selected)));
    }

    private bool TryOffline()
    {
        var now = DateTimeOffset.UtcNow;
        if (saved is null || !OfflineSessionPolicy.MayOpen(saved.Bootstrap, saved.ReceivedAt, saved.LastSeenAt, now)) { Clear(); return false; }
        accessToken = null; IsOffline = true;
        // Retain the flight's workspace for local capture. Cloud operations still require fresh authorization.
        Save(saved with { LastSeenAt = now });
        return true;
    }

    internal async Task SelectWorkspaceAsync(WorkspaceSelection selection, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            if (saved is null) throw new InvalidOperationException("Sign in to choose a workspace.");
            if (IsOffline && selection.AirlineId is not null && selection != saved.Workspace)
                throw new InvalidOperationException("Connect to the internet to choose a different virtual airline.");
            if (OfflineSessionPolicy.ValidateWorkspace(saved.Bootstrap, selection) != selection) throw new InvalidOperationException("This airline membership is unavailable.");
            if (!IsOffline)
            {
                using var client = Api(accessToken!);
                using var response = await client.PutAsJsonAsync("api/v1/me/workspace", selection, token);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) { Clear(); throw new InvalidOperationException("Your access changed. Sign in again."); }
                response.EnsureSuccessStatusCode();
            }
            Save(saved with { Workspace = selection });
        }
        finally { gate.Release(); }
    }

    internal async Task SignOutAsync()
    {
        await gate.WaitAsync();
        try
        {
            var refresh = saved?.RefreshToken;
            Clear();
            if (refresh is null) return;
            // Auth0's public-client revocation endpoint; always clear locally even if offline.
            using var client = Http();
            try
            {
                using var response = await client.PostAsJsonAsync(Configuration.Authority.TrimEnd('/') + "/oauth/revoke",
                    new { client_id = Configuration.ClientId, token = refresh, token_type_hint = "refresh_token" });
            }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException) { }
        }
        finally { gate.Release(); }
    }

    private HttpClient Api(string access)
    {
        var client = Http();
        client.BaseAddress = new Uri(Configuration.ApiBaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        return client;
    }
    private HttpClient Http() => new(handlerFactory?.Invoke() ?? new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(20) };
    private SavedAccountSession? Read()
    {
        try
        {
            if (!File.Exists(sessionPath)) return null;
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(sessionPath), null, DataProtectionScope.CurrentUser);
            try
            {
                var session = JsonSerializer.Deserialize<SavedAccountSession>(bytes);
                return session?.ConfigurationKey == configurationKey ? session : null;
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        catch (Exception error) when (error is CryptographicException or IOException or JsonException or UnauthorizedAccessException) { return null; }
    }
    private void Save(SavedAccountSession session)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(session);
        try
        {
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            var temporary = sessionPath + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { stream.Write(protectedBytes); stream.Flush(true); }
            File.Move(temporary, sessionPath, true);
            saved = session;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    private void Clear()
    {
        saved = null; accessToken = null; IsOffline = false;
        File.Delete(sessionPath); File.Delete(sessionPath + ".tmp");
    }
}
