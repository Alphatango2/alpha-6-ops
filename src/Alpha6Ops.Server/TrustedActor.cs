using System.Globalization;
using System.Security.Claims;
using Alpha6Ops.Identity;

namespace Alpha6Ops.Server;

public static class TrustedActor
{
    public const string Prefix = "https://alpha6ops.com/claims/";
    public static ActorIdentity Read(ClaimsPrincipal principal, ServerSettings settings, TimeProvider? clock = null)
    {
        string Required(string key) => principal.FindFirst(key)?.Value is { Length: > 0 } value
            ? value : throw new IdentityException("identity_claims_missing", "Sign in again. Required account claims are unavailable.", 401);
        if (principal.Identity?.IsAuthenticated != true)
            throw new IdentityException("authentication_required", "Sign in to continue.", 401);
        var issuer = Required("iss");
        if (!string.Equals(issuer, settings.Authority, StringComparison.Ordinal))
            throw new IdentityException("invalid_identity", "The account issuer is invalid.", 401);
        var subject = Required("sub");
        var email = Required(Prefix + "email");
        if (!bool.TryParse(Required(Prefix + "email_verified"), out var verified)
            || !bool.TryParse(Required(Prefix + "mfa"), out var mfa))
            throw new IdentityException("identity_claims_missing", "Sign in again. Required account claims are unavailable.", 401);
        DateTimeOffset? authenticatedAt = null;
        if (mfa)
        {
            if (!long.TryParse(Required(Prefix + "mfa_at"), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
                || seconds < 0 || seconds > 253402300799)
                throw new IdentityException("invalid_identity", "The authentication proof is invalid.", 401);
            authenticatedAt = DateTimeOffset.FromUnixTimeSeconds(seconds);
            if (authenticatedAt > (clock ?? TimeProvider.System).GetUtcNow().AddSeconds(30))
                throw new IdentityException("invalid_identity", "The authentication proof is invalid.", 401);
        }
        return new(issuer, subject, Required(Prefix + "display_name"), email, verified, mfa, authenticatedAt);
    }
}
