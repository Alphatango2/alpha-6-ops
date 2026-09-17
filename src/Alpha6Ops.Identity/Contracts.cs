using System.Text.Json.Serialization;

namespace Alpha6Ops.Identity;

[JsonConverter(typeof(JsonStringEnumConverter<AccountStatus>))]
public enum AccountStatus { Active, Suspended }
[JsonConverter(typeof(JsonStringEnumConverter<PersonalPlan>))]
public enum PersonalPlan { Free, Premium }
[JsonConverter(typeof(JsonStringEnumConverter<AirlinePlan>))]
public enum AirlinePlan { Community, Pro }
[JsonConverter(typeof(JsonStringEnumConverter<MembershipStatus>))]
public enum MembershipStatus { Active, Suspended, Removed }
[JsonConverter(typeof(JsonStringEnumConverter<AirlineRole>))]
public enum AirlineRole { Pilot, Dispatcher, Administrator, Owner }

public sealed record AccountProfile(Guid Id, string DisplayName, string Email, bool EmailVerified, AccountStatus Status);
public sealed record PersonalEntitlement(PersonalPlan Plan, string Status, DateTimeOffset? ExpiresAt);
public sealed record AirlineWorkspace(Guid Id, string Slug, string Name, string Callsign,
    AirlinePlan Plan, string SubscriptionStatus, MembershipStatus MembershipStatus,
    AirlineRole[] Roles, bool IsFounder, string[] Capabilities);
public sealed record WorkspaceSelection(Guid? AirlineId)
{
    public static WorkspaceSelection Personal => new((Guid?)null);
}
public sealed record BootstrapResponse(AccountProfile Account, PersonalEntitlement PersonalEntitlement,
    string[] PersonalCapabilities, AirlineWorkspace[] Airlines, WorkspaceSelection LastWorkspace,
    DateTimeOffset ServerTime, DateTimeOffset OfflineExpiresAt);
public sealed record CreateAirlineRequest(string Name, string Slug, string Callsign);
public sealed record InviteMemberRequest(string Email, AirlineRole[] Roles);
public sealed record ChangeRolesRequest(AirlineRole[] Roles);
public sealed record TransferOwnershipRequest(Guid NewOwnerUserId);
public sealed record MemberResponse(Guid Id, Guid UserId, string DisplayName, string Email,
    MembershipStatus Status, AirlineRole[] Roles);
public sealed record InvitationResponse(Guid Id, string Email, AirlineRole[] Roles, DateTimeOffset ExpiresAt,
    string Status);
// Raw tokens are returned only once to the issuing administrator, never in listings or logs.
public sealed record IssuedInvitation(InvitationResponse Invitation, string Token);
public sealed record ActorIdentity(string Issuer, string Subject, string DisplayName, string Email,
    bool EmailVerified, bool HasMfa, DateTimeOffset? AuthenticatedAt);

public static class Capabilities
{
    public const string Logbook = "personal.logbook";
    public const string Dispatch = "personal.dispatch";
    public const string Tracker = "personal.tracker";
    public const string Weather = "personal.weather";
    public const string Import = "personal.import";
    public const string AirlineRead = "airline.read";
    public const string DispatchRead = "airline.dispatch.read";
    public const string DispatchManage = "airline.dispatch.manage";
    public const string MembersManage = "airline.members.manage";
    public const string OwnershipManage = "airline.ownership.manage";
    public static string[] Personal => [Logbook, Dispatch, Tracker, Weather, Import];

    public static string[] ForMembership(MembershipStatus status, IEnumerable<AirlineRole> roles)
    {
        if (status != MembershipStatus.Active) return [];
        var set = roles.ToHashSet();
        var result = new List<string> { AirlineRead, DispatchRead };
        if (set.Overlaps([AirlineRole.Dispatcher, AirlineRole.Administrator, AirlineRole.Owner])) result.Add(DispatchManage);
        if (set.Overlaps([AirlineRole.Administrator, AirlineRole.Owner])) result.Add(MembersManage);
        if (set.Contains(AirlineRole.Owner)) result.Add(OwnershipManage);
        return result.ToArray();
    }
}

public sealed class IdentityException(string code, string message, int statusCode = 403) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
