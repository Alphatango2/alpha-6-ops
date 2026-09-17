using Alpha6Ops.Identity;

namespace Alpha6Ops.Accounts;

public sealed class UserAccount
{
    public Guid Id { get; set; }
    public string Issuer { get; set; } = "";
    public string Subject { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public bool EmailVerified { get; set; }
    public AccountStatus Status { get; set; }
    public PersonalPlan Plan { get; set; }
    public string SubscriptionStatus { get; set; } = "active";
    public DateTimeOffset? SubscriptionExpiresAt { get; set; }
    public Guid? LastAirlineId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class VirtualAirline
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Callsign { get; set; } = "";
    public string Status { get; set; } = "active";
    public Guid FounderUserId { get; set; }
    public Guid OwnerUserId { get; set; }
    public AirlinePlan Plan { get; set; }
    public string SubscriptionStatus { get; set; } = "active";
    public DateTimeOffset? SubscriptionExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Membership
{
    public Guid Id { get; set; }
    public Guid AirlineId { get; set; }
    public Guid UserId { get; set; }
    public MembershipStatus Status { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public List<MembershipRole> Roles { get; set; } = [];
}

// Owner is derived exclusively from VirtualAirline.OwnerUserId, avoiding two competing owners.
public sealed class MembershipRole
{
    public Guid AirlineId { get; set; }
    public Guid MembershipId { get; set; }
    public AirlineRole Role { get; set; }
}

public sealed class Invitation
{
    public Guid Id { get; set; }
    public Guid AirlineId { get; set; }
    public Guid IssuedByUserId { get; set; }
    public string Email { get; set; } = "";
    public int[] Roles { get; set; } = [];
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public string Status { get; set; } = "pending";
    public Guid? AcceptedByUserId { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AuditEvent
{
    public Guid Id { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid? AirlineId { get; set; }
    public string Action { get; set; } = "";
    public Guid TargetId { get; set; }
    public string Details { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
