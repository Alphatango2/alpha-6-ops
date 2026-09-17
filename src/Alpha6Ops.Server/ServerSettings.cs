namespace Alpha6Ops.Server;

public sealed class ServerSettings
{
    public string Authority { get; init; } = "";
    public string Audience { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
    public string ConnectionString { get; init; } = "";
    public bool IsConfigured => Uri.TryCreate(Authority, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment) && string.IsNullOrEmpty(uri.UserInfo)
        && !string.IsNullOrWhiteSpace(Audience) && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret) && !string.IsNullOrWhiteSpace(ConnectionString);
    public static ServerSettings Read(IConfiguration config) => new()
    {
        Authority = (config["Auth0:Authority"] ?? "").TrimEnd('/') + "/",
        Audience = config["Auth0:Audience"] ?? "",
        ClientId = config["Auth0:ClientId"] ?? "",
        ClientSecret = config["Auth0:ClientSecret"] ?? "",
        ConnectionString = config.GetConnectionString("Accounts") ?? ""
    };
}

public sealed class ReleaseSettings
{
    public string Version { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public bool Available => !string.IsNullOrWhiteSpace(Version) && Sha256.Length == 64
        && Sha256.All(Uri.IsHexDigit) && Uri.TryCreate(DownloadUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo);
}
