using System;
using System.IO;
using System.Text.Json;

namespace Alpha6Ops.Desktop;

internal sealed record IdentityConfiguration(string Authority, string ClientId, string Audience, string ApiBaseUrl, string PortalUrl, int CallbackPort = 42879)
{
    internal static IdentityConfiguration? Load()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "alpha6-identity.json");
        if (!File.Exists(file)) return null;
        var config = JsonSerializer.Deserialize<IdentityConfiguration>(File.ReadAllText(file))
            ?? throw new InvalidDataException("Identity configuration is empty.");
        foreach (var address in new[] { config.Authority, config.ApiBaseUrl, config.PortalUrl })
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new InvalidDataException("Identity endpoints must be absolute HTTPS addresses without credentials, query strings or fragments.");
        if (string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.Audience))
            throw new InvalidDataException("Identity ClientId and Audience are required.");
        if (new Uri(config.Authority).AbsolutePath != "/" || config.CallbackPort is < 1024 or > 65535)
            throw new InvalidDataException("Identity Authority must be a domain root and CallbackPort must be an unprivileged port.");
        return config;
    }
}
