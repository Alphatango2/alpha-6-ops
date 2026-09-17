using System.Security.Cryptography;
using Alpha6Ops.Identity;
using Microsoft.EntityFrameworkCore;

namespace Alpha6Ops.Accounts;

/// <summary>Scoped service. Each request rechecks authorization inside a PostgreSQL transaction.</summary>
public sealed class AccountsService(AccountsDbContext db, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    // One transaction-scoped lock deliberately serializes this first, low-volume account service.
    // It also makes concurrent first-login provisioning and cross-airline ownership limits safe.
    private const long LockId = 0x416C706861364F;

    private async Task<T> Execute<T>(ActorIdentity actor, Func<UserAccount, DateTimeOffset, Task<T>> action, CancellationToken ct)
    {
        if (db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            throw new InvalidOperationException("Accounts requires PostgreSQL; an in-memory authentication store is not supported.");
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({LockId})", ct);
        var now = clock.GetUtcNow();
        var issuer = AccountRules.Text(actor.Issuer, 255, "Issuer");
        var subject = AccountRules.Text(actor.Subject, 255, "Subject");
        if (issuer != actor.Issuer || subject != actor.Subject)
            throw new IdentityException("invalid_identity", "The identity issuer and subject must be exact identifiers.", 401);
        // Never merge accounts by email. Only the validated OIDC issuer and subject establish identity.
        var user = await db.Users.SingleOrDefaultAsync(x => x.Issuer == issuer && x.Subject == subject, ct);
        if (user is not null && user.Status != AccountStatus.Active)
            throw new IdentityException("account_suspended", "This account is suspended.");
        var email = AccountRules.Email(actor.Email);
        var displayName = AccountRules.Text(actor.DisplayName, 150, "Display name");
        if (user is null)
        {
            user = new UserAccount { Id = Guid.NewGuid(), Issuer = issuer, Subject = subject, DisplayName = displayName,
                Email = email, EmailVerified = actor.EmailVerified, CreatedAt = now };
            db.Users.Add(user);
            Audit(user.Id, null, "account.created", user.Id, "", now);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            user.DisplayName = displayName; user.Email = email; user.EmailVerified = actor.EmailVerified;
        }
        var result = await action(user, now);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public Task<BootstrapResponse> BootstrapAsync(ActorIdentity actor, CancellationToken ct = default) => Execute(actor, async (user, now) =>
    {
        var memberships = await db.Memberships.Include(x => x.Roles).Where(x => x.UserId == user.Id && x.Status == MembershipStatus.Active).ToArrayAsync(ct);
        var ids = memberships.Select(x => x.AirlineId).ToArray();
        var airlines = await db.Airlines.Where(x => ids.Contains(x.Id) && x.Status == "active").ToArrayAsync(ct);
        var workspaces = airlines.OrderBy(x => x.Name).Select(x => Workspace(x, memberships.Single(m => m.AirlineId == x.Id), user, now)).ToArray();
        if (user.LastAirlineId is { } last && workspaces.All(x => x.Id != last)) user.LastAirlineId = null;
        var plan = IsCurrent(user.SubscriptionStatus, user.SubscriptionExpiresAt, now) && Enum.IsDefined(user.Plan) ? user.Plan : PersonalPlan.Free;
        return new BootstrapResponse(new(user.Id, user.DisplayName, user.Email, user.EmailVerified, user.Status),
            new(plan, user.SubscriptionStatus, user.SubscriptionExpiresAt), Capabilities.Personal, workspaces,
            new(user.LastAirlineId), now, now.AddDays(30));
    }, ct);

    public async Task SetWorkspaceAsync(ActorIdentity actor, WorkspaceSelection request, CancellationToken ct = default) =>
        await Execute(actor, async (user, now) =>
        {
            if (request.AirlineId is { } id) await MemberAccess(user, id, false, actor, now, ct);
            user.LastAirlineId = request.AirlineId;
            return true;
        }, ct);

    public Task<AirlineWorkspace> CreateAirlineAsync(ActorIdentity actor, CreateAirlineRequest request, CancellationToken ct = default) =>
        Execute(actor, async (user, now) =>
        {
            AccountRules.RequireRecentMfa(actor, now);
            var slug = AccountRules.Slug(request.Slug);
            if (await HasCommunityAirline(user.Id, now, ct))
                throw new IdentityException("plan_limit_reached", "You already own a Community airline.", 409);
            if (await db.Airlines.AnyAsync(x => x.Slug == slug, ct)) throw new IdentityException("slug_unavailable", "This airline address is already in use.", 409);
            var airline = new VirtualAirline { Id = Guid.NewGuid(), Slug = slug, Name = AccountRules.Text(request.Name, 100, "Airline name"),
                Callsign = AccountRules.Text(request.Callsign, 20, "Callsign"), FounderUserId = user.Id, OwnerUserId = user.Id, CreatedAt = now };
            var member = new Membership { Id = Guid.NewGuid(), AirlineId = airline.Id, UserId = user.Id, JoinedAt = now };
            member.Roles.Add(new() { AirlineId = airline.Id, MembershipId = member.Id, Role = AirlineRole.Pilot });
            db.Airlines.Add(airline); db.Memberships.Add(member);
            Audit(user.Id, airline.Id, "airline.created", airline.Id, "Community; founder and owner assigned", now);
            return Workspace(airline, member, user, now);
        }, ct);

    public Task<AirlineWorkspace> GetAirlineAsync(ActorIdentity actor, Guid airlineId, CancellationToken ct = default) => Execute(actor, async (user, now) =>
    {
        var (airline, member) = await MemberAccess(user, airlineId, false, actor, now, ct);
        return Workspace(airline, member, user, now);
    }, ct);

    public Task<MemberResponse[]> ListMembersAsync(ActorIdentity actor, Guid airlineId, CancellationToken ct = default) => Execute(actor, async (user, now) =>
    {
        var (airline, _) = await MemberAccess(user, airlineId, true, actor, now, ct);
        var members = await db.Memberships.Include(x => x.Roles).Where(x => x.AirlineId == airlineId).ToArrayAsync(ct);
        var ids = members.Select(x => x.UserId).ToArray();
        var users = await db.Users.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return members.Select(m => new MemberResponse(m.Id, m.UserId, users[m.UserId].DisplayName, users[m.UserId].Email, m.Status, Roles(airline, m))).ToArray();
    }, ct);

    public Task<IssuedInvitation> InviteAsync(ActorIdentity actor, Guid airlineId, InviteMemberRequest request, CancellationToken ct = default) => Execute(actor, async (user, now) =>
    {
        await MemberAccess(user, airlineId, true, actor, now, ct);
        var email = AccountRules.Email(request.Email); var roles = AccountRules.Roles(request.Roles);
        var duplicates = await db.Invitations.Where(x => x.AirlineId == airlineId && x.Email == email && x.Status == "pending").ToArrayAsync(ct);
        foreach (var duplicate in duplicates)
        {
            duplicate.Status = "revoked";
            Audit(user.Id, airlineId, "invitation.revoked", duplicate.Id, "Replaced by a new invitation", now);
        }
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var invitation = new Invitation { Id = Guid.NewGuid(), AirlineId = airlineId, IssuedByUserId = user.Id, Email = email,
            Roles = roles.Select(x => (int)x).ToArray(), TokenHash = AccountRules.HashToken(token), ExpiresAt = now.AddDays(7), CreatedAt = now };
        db.Invitations.Add(invitation);
        Audit(user.Id, airlineId, "invitation.issued", invitation.Id, string.Join(',', roles), now);
        return new IssuedInvitation(InvitationView(invitation, now), token);
    }, ct);

    public Task<InvitationResponse[]> ListInvitationsAsync(ActorIdentity actor, Guid airlineId, CancellationToken ct = default) => Execute(actor, async (user, now) =>
    {
        await MemberAccess(user, airlineId, true, actor, now, ct);
        var invitations = await db.Invitations.Where(x => x.AirlineId == airlineId).OrderByDescending(x => x.CreatedAt).Take(200).ToArrayAsync(ct);
        return invitations.Select(x => InvitationView(x, now)).ToArray();
    }, ct);

    public Task<AirlineWorkspace> AcceptInvitationAsync(ActorIdentity actor, string token, CancellationToken ct = default) => Execute(actor, async (user, now) =>
    {
        if (!actor.EmailVerified) throw new IdentityException("email_verification_required", "Verify your email before accepting an invitation.");
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128) throw InvalidInvitation();
        var hash = AccountRules.HashToken(token);
        var invitation = await db.Invitations.SingleOrDefaultAsync(x => x.TokenHash == hash, ct) ?? throw InvalidInvitation();
        if (invitation.Email != user.Email) throw new IdentityException("invitation_email_mismatch", "Sign in with the verified email this invitation was sent to.");
        if (invitation.Status == "revoked") throw InvalidInvitation();
        var airline = await db.Airlines.SingleOrDefaultAsync(x => x.Id == invitation.AirlineId && x.Status == "active", ct) ?? throw InvalidInvitation();
        var member = await db.Memberships.Include(x => x.Roles).SingleOrDefaultAsync(x => x.AirlineId == airline.Id && x.UserId == user.Id, ct);
        if (member is not null && member.Status != MembershipStatus.Active) throw new IdentityException("membership_inactive", "This membership is no longer active.");
        if (invitation.Status == "accepted")
        {
            if (invitation.AcceptedByUserId != user.Id || member is null) throw InvalidInvitation();
            return Workspace(airline, member, user, now);
        }
        if (invitation.Status != "pending") throw InvalidInvitation();
        if (invitation.ExpiresAt <= now) throw new IdentityException("invitation_expired", "This invitation has expired.", 410);
        var roles = AccountRules.Roles(invitation.Roles.Select(x => (AirlineRole)x).ToArray());
        if (roles.Contains(AirlineRole.Administrator)) AccountRules.RequireRecentMfa(actor, now);
        if (member is null)
        {
            member = new() { Id = Guid.NewGuid(), AirlineId = airline.Id, UserId = user.Id, JoinedAt = now };
            db.Memberships.Add(member);
        }
        // Accepting a second invitation can add roles, but never removes current roles or ownership.
        foreach (var role in roles.Where(role => member.Roles.All(r => r.Role != role)))
            member.Roles.Add(new() { AirlineId = airline.Id, MembershipId = member.Id, Role = role });
        invitation.Status = "accepted"; invitation.AcceptedByUserId = user.Id; invitation.AcceptedAt = now;
        Audit(user.Id, airline.Id, "invitation.accepted", invitation.Id, string.Join(',', roles), now);
        return Workspace(airline, member, user, now);
    }, ct);

    public async Task RevokeInvitationAsync(ActorIdentity actor, Guid airlineId, Guid invitationId, CancellationToken ct = default) => await Execute(actor, async (user, now) =>
    {
        await MemberAccess(user, airlineId, true, actor, now, ct);
        var invitation = await db.Invitations.SingleOrDefaultAsync(x => x.Id == invitationId && x.AirlineId == airlineId, ct) ?? throw InvalidInvitation();
        if (invitation.Status == "accepted") throw new IdentityException("invitation_already_accepted", "This invitation has already been accepted.", 409);
        if (invitation.Status != "revoked")
        {
            invitation.Status = "revoked";
            Audit(user.Id, airlineId, "invitation.revoked", invitationId, "", now);
        }
        return true;
    }, ct);

    public async Task ChangeRolesAsync(ActorIdentity actor, Guid airlineId, Guid membershipId, ChangeRolesRequest request, CancellationToken ct = default) => await Execute(actor, async (user, now) =>
    {
        var (airline, _) = await MemberAccess(user, airlineId, true, actor, now, ct);
        var roles = AccountRules.Roles(request.Roles);
        var member = await db.Memberships.Include(x => x.Roles).SingleOrDefaultAsync(x => x.Id == membershipId && x.AirlineId == airlineId, ct)
            ?? throw new IdentityException("membership_required", "Membership not found.", 404);
        if (member.Status != MembershipStatus.Active) throw new IdentityException("membership_inactive", "Only active memberships may change roles.");
        if (member.UserId == airline.OwnerUserId) throw new IdentityException("ownership_invariant", "Use ownership transfer to change the owner's access.", 409);
        var before = string.Join(',', member.Roles.Select(x => x.Role));
        foreach (var old in member.Roles.Where(x => !roles.Contains(x.Role)).ToArray()) { db.MembershipRoles.Remove(old); member.Roles.Remove(old); }
        foreach (var role in roles.Where(r => member.Roles.All(x => x.Role != r))) member.Roles.Add(new() { MembershipId = member.Id, AirlineId = airlineId, Role = role });
        Audit(user.Id, airlineId, "membership.roles_changed", member.Id, $"{before} -> {string.Join(',', roles)}", now);
        return true;
    }, ct);

    public async Task TransferOwnershipAsync(ActorIdentity actor, Guid airlineId, TransferOwnershipRequest request, CancellationToken ct = default) => await Execute(actor, async (user, now) =>
    {
        var (airline, oldMember) = await MemberAccess(user, airlineId, true, actor, now, ct);
        if (airline.OwnerUserId != user.Id) throw new IdentityException("owner_required", "Only the current owner can transfer ownership.");
        if (request.NewOwnerUserId == user.Id) return true;
        var target = await db.Memberships.SingleOrDefaultAsync(x => x.AirlineId == airlineId && x.UserId == request.NewOwnerUserId && x.Status == MembershipStatus.Active, ct);
        var targetUser = await db.Users.SingleOrDefaultAsync(x => x.Id == request.NewOwnerUserId && x.Status == AccountStatus.Active && x.EmailVerified, ct);
        if (target is null || targetUser is null) throw new IdentityException("active_member_required", "The new owner must be an active member with a verified account.");
        if (EffectivePlan(airline, now) == AirlinePlan.Community && await HasCommunityAirline(request.NewOwnerUserId, now, ct))
            throw new IdentityException("plan_limit_reached", "The selected member already owns a Community airline.", 409);
        if (oldMember.Roles.All(x => x.Role != AirlineRole.Administrator)) oldMember.Roles.Add(new() { AirlineId = airlineId, MembershipId = oldMember.Id, Role = AirlineRole.Administrator });
        airline.OwnerUserId = request.NewOwnerUserId;
        Audit(user.Id, airlineId, "airline.ownership_transferred", airlineId, $"{user.Id} -> {request.NewOwnerUserId}", now);
        return true;
    }, ct);

    private async Task<(VirtualAirline Airline, Membership Member)> MemberAccess(UserAccount user, Guid airlineId, bool manage, ActorIdentity actor, DateTimeOffset now, CancellationToken ct)
    {
        var member = await db.Memberships.Include(x => x.Roles).SingleOrDefaultAsync(x => x.AirlineId == airlineId && x.UserId == user.Id && x.Status == MembershipStatus.Active, ct);
        var airline = await db.Airlines.SingleOrDefaultAsync(x => x.Id == airlineId && x.Status == "active", ct);
        if (member is null || airline is null) throw new IdentityException("membership_required", "An active airline membership is required.");
        var roles = Roles(airline, member);
        if (manage)
        {
            if (!roles.Contains(AirlineRole.Owner) && !roles.Contains(AirlineRole.Administrator))
                throw new IdentityException("administrator_required", "An airline administrator is required.");
            AccountRules.RequireRecentMfa(actor, now);
        }
        return (airline, member);
    }

    private static AirlineRole[] Roles(VirtualAirline airline, Membership member)
    {
        var roles = member.Roles.Select(x => x.Role).ToArray();
        if (roles.Length == 0 || roles.Any(x => !Enum.IsDefined(x) || x == AirlineRole.Owner))
            throw new IdentityException("invalid_membership_roles", "The stored membership roles require administrator review.");
        return (airline.OwnerUserId == member.UserId ? roles.Append(AirlineRole.Owner) : roles).Distinct().Order().ToArray();
    }

    private static bool IsCurrent(string status, DateTimeOffset? expires, DateTimeOffset now) => status == "active" && (expires is null || expires > now);
    private Task<bool> HasCommunityAirline(Guid userId, DateTimeOffset now, CancellationToken ct) => db.Airlines.AnyAsync(x =>
        x.OwnerUserId == userId && x.Status == "active" && !(x.Plan == AirlinePlan.Pro && x.SubscriptionStatus == "active" &&
        (x.SubscriptionExpiresAt == null || x.SubscriptionExpiresAt > now)), ct);
    private static AirlinePlan EffectivePlan(VirtualAirline airline, DateTimeOffset now) => airline.Plan == AirlinePlan.Pro &&
        IsCurrent(airline.SubscriptionStatus, airline.SubscriptionExpiresAt, now) ? AirlinePlan.Pro : AirlinePlan.Community;
    private static AirlineWorkspace Workspace(VirtualAirline airline, Membership member, UserAccount user, DateTimeOffset now)
    {
        var roles = Roles(airline, member);
        var plan = EffectivePlan(airline, now);
        return new(airline.Id, airline.Slug, airline.Name, airline.Callsign, plan, airline.SubscriptionStatus, member.Status,
            roles, airline.FounderUserId == user.Id, Capabilities.ForMembership(member.Status, roles));
    }

    private static InvitationResponse InvitationView(Invitation invitation, DateTimeOffset now) => new(invitation.Id, invitation.Email,
        AccountRules.Roles(invitation.Roles.Select(x => (AirlineRole)x).ToArray()), invitation.ExpiresAt,
        invitation.Status == "pending" && invitation.ExpiresAt <= now ? "expired" : invitation.Status);
    private static IdentityException InvalidInvitation() => new("invitation_invalid", "This invitation is unavailable.", 404);
    private void Audit(Guid actor, Guid? airline, string action, Guid target, string details, DateTimeOffset now) => db.AuditEvents.Add(new()
        { Id = Guid.NewGuid(), ActorUserId = actor, AirlineId = airline, Action = action, TargetId = target, Details = details, CreatedAt = now });
}
