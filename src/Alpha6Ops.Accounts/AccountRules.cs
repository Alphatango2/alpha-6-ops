using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Alpha6Ops.Identity;

namespace Alpha6Ops.Accounts;

public static partial class AccountRules
{
    public static string Email(string value)
    {
        value = (value ?? "").Trim();
        if (value.Length > 254 || !MailAddress.TryCreate(value, out var parsed) || parsed.Address != value)
            throw new IdentityException("invalid_email", "Enter a valid email address.", 400);
        return value.ToUpperInvariant();
    }

    public static AirlineRole[] Roles(AirlineRole[]? roles)
    {
        if (roles is null || roles.Length == 0 || roles.Length > 3 ||
            roles.Any(x => !Enum.IsDefined(x) || x == AirlineRole.Owner))
            throw new IdentityException("invalid_roles", "Choose Pilot, Dispatcher, or Administrator. Ownership requires a transfer.", 400);
        return roles.Distinct().Order().ToArray();
    }

    public static void RequireRecentMfa(ActorIdentity actor, DateTimeOffset now)
    {
        if (!actor.EmailVerified) throw new IdentityException("email_verification_required", "Verify your email first.");
        if (!actor.HasMfa || actor.AuthenticatedAt is not { } at || at > now.AddSeconds(30) || at < now.AddMinutes(-5))
            throw new IdentityException("mfa_required", "Confirm your identity with a passkey or MFA to continue.");
    }

    public static string Slug(string value)
    {
        value = (value ?? "").Trim().ToLowerInvariant();
        if (!SlugPattern().IsMatch(value)) throw new IdentityException("invalid_slug", "Use 3 to 64 lowercase letters, digits, and internal hyphens.", 400);
        return value;
    }

    public static string Text(string value, int limit, string field)
    {
        value = (value ?? "").Trim();
        if (value.Length == 0 || value.Length > limit || value.Any(char.IsControl))
            throw new IdentityException("invalid_input", $"{field} must contain 1 to {limit} characters without control characters.", 400);
        return value;
    }

    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,62}[a-z0-9]$")]
    private static partial Regex SlugPattern();
}
