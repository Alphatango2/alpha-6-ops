using Alpha6Ops.Accounts;
using Alpha6Ops.Identity;
using Microsoft.EntityFrameworkCore;

var connection = Environment.GetEnvironmentVariable("ALPHA6_TEST_DATABASE");
if (args.Contains("--schema"))
{
    using var schema = new AccountsDbContext(new DbContextOptionsBuilder<AccountsDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
    Console.WriteLine(schema.Database.GenerateCreateScript());
    return;
}

var now = DateTimeOffset.UtcNow;
var actor = new ActorIdentity("https://accounts.example/", "owner", "Owner", "owner@example.com", true, true, now);
var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    passed++; Console.WriteLine("PASS: " + name);
}
void Reject(Action action, string code)
{
    try { action(); } catch (IdentityException ex) when (ex.Code == code) { Check(true, code); return; }
    throw new InvalidOperationException("Expected " + code);
}
async Task RejectAsync(Func<Task> action, string code)
{
    try { await action(); } catch (IdentityException ex) when (ex.Code == code) { Check(true, code); return; }
    throw new InvalidOperationException("Expected " + code);
}
async Task RejectDatabaseAsync(Func<Task> action, string name)
{
    try { await action(); }
    catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pg && pg.SqlState is "23503" or "23514")
    { Check(true, name); return; }
    throw new InvalidOperationException("Expected database rejection: " + name);
}

Check(AccountRules.Email(" Pilot@Example.com ") == "PILOT@EXAMPLE.COM", "email normalization");
Reject(() => AccountRules.Email("Name <pilot@example.com>"), "invalid_email");
Reject(() => AccountRules.Roles([(AirlineRole)99]), "invalid_roles");
Reject(() => AccountRules.Roles([AirlineRole.Owner]), "invalid_roles");
Reject(() => AccountRules.Roles([]), "invalid_roles");
Check(AccountRules.Roles([AirlineRole.Pilot, AirlineRole.Pilot]).Length == 1, "duplicate roles normalized");
Reject(() => AccountRules.RequireRecentMfa(actor with { HasMfa = false }, now), "mfa_required");
Reject(() => AccountRules.RequireRecentMfa(actor with { AuthenticatedAt = now.AddMinutes(-6) }, now), "mfa_required");
Reject(() => AccountRules.RequireRecentMfa(actor with { AuthenticatedAt = now.AddMinutes(1) }, now), "mfa_required");
Reject(() => AccountRules.RequireRecentMfa(actor with { EmailVerified = false }, now), "email_verification_required");
Check(AccountRules.Slug(" Test-Airline ") == "test-airline", "slug normalized");
Reject(() => AccountRules.Slug("../admin"), "invalid_slug");
Check(AccountRules.HashToken("secret").Length == 64 && AccountRules.HashToken("secret") != AccountRules.HashToken("other"), "invitation hash stable and nonplaintext");

if (string.IsNullOrWhiteSpace(connection))
{
    Console.WriteLine($"{passed} pure checks passed. SKIPPED PostgreSQL integration: ALPHA6_TEST_DATABASE is not configured.");
    return;
}
var builder = new Npgsql.NpgsqlConnectionStringBuilder(connection);
if (builder.Database != "alpha6_identity_test" || builder.Host is not ("127.0.0.1" or "localhost"))
    throw new InvalidOperationException("Integration checks require the disposable local alpha6_identity_test database.");

// Start from an empty schema on every run, without dropping any existing user or test data.
var schemaName = "account_test_" + Guid.NewGuid().ToString("N");
await using (var admin = new Npgsql.NpgsqlConnection(connection))
{
    await admin.OpenAsync();
    await using var command = new Npgsql.NpgsqlCommand($"CREATE SCHEMA {schemaName}", admin);
    await command.ExecuteNonQueryAsync();
}
builder.SearchPath = schemaName;
connection = builder.ConnectionString;

AccountsDbContext Context() => new(new DbContextOptionsBuilder<AccountsDbContext>().UseNpgsql(connection).Options);
async Task<T> Run<T>(Func<AccountsService, Task<T>> action)
{
    await using var db = Context();
    return await action(new AccountsService(db));
}
async Task Mutation(Func<AccountsService, Task> action)
{
    await using var db = Context();
    await action(new AccountsService(db));
}
await using (var db = Context())
{
    Check(!db.Database.HasPendingModelChanges(), "migration snapshot matches current model");
    await db.Database.MigrateAsync();
    await db.Database.MigrateAsync();
    Check((await db.Database.GetAppliedMigrationsAsync()).Count() == 1, "initial migration is repeatable");
}
var prefix = Guid.NewGuid().ToString("N")[..10];
actor = actor with { Subject = prefix + "-owner", Email = prefix + "-owner@example.com" };
var pilot = actor with { Subject = prefix + "-pilot", DisplayName = "Pilot", Email = prefix + "-pilot@example.com", HasMfa = false };
var outsider = actor with { Subject = prefix + "-outsider", Email = prefix + "-outsider@example.com" };
var ownerBootstrap = await Run(s => s.BootstrapAsync(actor));
Check(ownerBootstrap.PersonalEntitlement.Plan == PersonalPlan.Free && ownerBootstrap.Airlines.Length == 0, "new account is Personal Free");
var concurrentUsers = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Run(s => s.BootstrapAsync(pilot))));
Check(concurrentUsers.Select(x => x.Account.Id).Distinct().Count() == 1, "concurrent account provisioning is idempotent");
var pilotId = concurrentUsers[0].Account.Id;
var changedProfile = await Run(s => s.BootstrapAsync(actor with { DisplayName = "New name" }));
Check(changedProfile.Account.Id == ownerBootstrap.Account.Id, "issuer and subject survive profile changes");
var separate = await Run(s => s.BootstrapAsync(outsider with { Email = actor.Email, EmailVerified = false }));
Check(separate.Account.Id != ownerBootstrap.Account.Id, "matching email never links accounts");
await RejectAsync(() => Run(s => s.CreateAirlineAsync(pilot, new("Blocked", prefix + "blocked", "BLK"))), "mfa_required");
var va = await Run(s => s.CreateAirlineAsync(actor, new("Test Airline", prefix + "va", "TEST")));
Check(va.Roles.Contains(AirlineRole.Owner) && va.IsFounder, "creator is current owner and historical founder");
await RejectDatabaseAsync(async () =>
{
    await using var db = Context();
    (await db.Airlines.SingleAsync(x => x.Id == va.Id)).OwnerUserId = pilotId;
    await db.SaveChangesAsync();
}, "database rejects owner without same-airline membership");
await RejectDatabaseAsync(async () =>
{
    await using var db = Context();
    (await db.Memberships.SingleAsync(x => x.AirlineId == va.Id && x.UserId == ownerBootstrap.Account.Id)).Status = MembershipStatus.Suspended;
    await db.SaveChangesAsync();
}, "database rejects suspending current owner membership");
await RejectDatabaseAsync(async () =>
{
    await using var db = Context();
    (await db.Airlines.SingleAsync(x => x.Id == va.Id)).FounderUserId = pilotId;
    await db.SaveChangesAsync();
}, "database preserves immutable founder");
await RejectAsync(() => Run(s => s.CreateAirlineAsync(actor, new("Second", prefix + "second", "TWO"))), "plan_limit_reached");
await RejectAsync(() => Run(s => s.GetAirlineAsync(outsider, va.Id)), "membership_required");
await RejectAsync(() => Run(s => s.ListMembersAsync(outsider, va.Id)), "membership_required");
await RejectAsync(() => Mutation(s => s.SetWorkspaceAsync(outsider, new(va.Id))), "membership_required");
await RejectAsync(() => Run(s => s.InviteAsync(actor, va.Id, new(pilot.Email, [AirlineRole.Owner]))), "invalid_roles");
var issued = await Run(s => s.InviteAsync(actor, va.Id, new(pilot.Email, [AirlineRole.Pilot])));
await using (var db = Context())
{
    var stored = await db.Invitations.SingleAsync(x => x.Id == issued.Invitation.Id);
    Check(stored.TokenHash == AccountRules.HashToken(issued.Token) && stored.TokenHash != issued.Token, "database stores only invitation hash");
}
await RejectAsync(() => Run(s => s.AcceptInvitationAsync(outsider, issued.Token)), "invitation_email_mismatch");
await RejectAsync(() => Run(s => s.AcceptInvitationAsync(pilot with { EmailVerified = false }, issued.Token)), "email_verification_required");
var accepted = await Run(s => s.AcceptInvitationAsync(pilot, issued.Token));
Check(accepted.Id == va.Id && accepted.Roles.SequenceEqual([AirlineRole.Pilot]), "verified invited pilot joins");
var repeated = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Run(s => s.AcceptInvitationAsync(pilot, issued.Token))));
Check(repeated.All(x => x.Id == va.Id), "concurrent acceptance retries are idempotent");
await RejectAsync(() => Run(s => s.ListMembersAsync(pilot, va.Id)), "administrator_required");
await Mutation(s => s.SetWorkspaceAsync(pilot, new(va.Id)));
Check((await Run(s => s.BootstrapAsync(pilot))).LastWorkspace.AirlineId == va.Id, "workspace selection persisted");
var members = await Run(s => s.ListMembersAsync(actor, va.Id));
var pilotMembership = members.Single(x => x.UserId == pilotId);
await RejectDatabaseAsync(async () =>
{
    await using var db = Context();
    db.MembershipRoles.Add(new() { AirlineId = va.Id, MembershipId = pilotMembership.Id, Role = (AirlineRole)99 });
    await db.SaveChangesAsync();
}, "database rejects unknown role values");
await using (var db = Context())
{
    db.MembershipRoles.RemoveRange(await db.MembershipRoles.Where(x => x.MembershipId == pilotMembership.Id).ToArrayAsync());
    await db.SaveChangesAsync();
}
await RejectAsync(() => Run(s => s.GetAirlineAsync(pilot, va.Id)), "invalid_membership_roles");
await using (var db = Context())
{
    db.MembershipRoles.Add(new() { AirlineId = va.Id, MembershipId = pilotMembership.Id, Role = AirlineRole.Pilot });
    await db.SaveChangesAsync();
}
await RejectAsync(() => Mutation(s => s.ChangeRolesAsync(actor, va.Id, pilotMembership.Id, new([(AirlineRole)666]))), "invalid_roles");
await Mutation(s => s.ChangeRolesAsync(actor, va.Id, pilotMembership.Id, new([AirlineRole.Pilot, AirlineRole.Administrator])));
await RejectAsync(() => Run(s => s.ListMembersAsync(pilot, va.Id)), "mfa_required");
var pilotMfa = pilot with { HasMfa = true, AuthenticatedAt = DateTimeOffset.UtcNow };
Check((await Run(s => s.ListMembersAsync(pilotMfa, va.Id))).Length == 2, "administrator requires recent MFA");
await Mutation(s => s.ChangeRolesAsync(actor, va.Id, pilotMembership.Id, new([AirlineRole.Pilot])));
await RejectAsync(() => Run(s => s.ListMembersAsync(pilotMfa, va.Id)), "administrator_required");
var outsiderVa = await Run(s => s.CreateAirlineAsync(outsider, new("Other airline", prefix + "other", "OTHR")));
await RejectDatabaseAsync(async () =>
{
    await using var db = Context();
    db.MembershipRoles.Add(new() { AirlineId = outsiderVa.Id, MembershipId = pilotMembership.Id, Role = AirlineRole.Dispatcher });
    await db.SaveChangesAsync();
}, "composite foreign key rejects role from another tenant");
await RejectAsync(() => Mutation(s => s.ChangeRolesAsync(outsider, outsiderVa.Id, pilotMembership.Id, new([AirlineRole.Dispatcher]))), "membership_required");
var otherInvite = await Run(s => s.InviteAsync(outsider, outsiderVa.Id, new(pilot.Email, [AirlineRole.Dispatcher])));
await Run(s => s.AcceptInvitationAsync(pilot, otherInvite.Token));
Check((await Run(s => s.BootstrapAsync(pilot))).Airlines.Length == 2, "pilot can belong to multiple airlines");
var revoked = await Run(s => s.InviteAsync(actor, va.Id, new(outsider.Email, [AirlineRole.Pilot])));
await Mutation(s => s.RevokeInvitationAsync(actor, va.Id, revoked.Invitation.Id));
await RejectAsync(() => Run(s => s.AcceptInvitationAsync(outsider, revoked.Token)), "invitation_invalid");
var expired = await Run(s => s.InviteAsync(actor, va.Id, new(outsider.Email, [AirlineRole.Pilot])));
await using (var db = Context())
{
    (await db.Invitations.SingleAsync(x => x.Id == expired.Invitation.Id)).ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
    await db.SaveChangesAsync();
}
await RejectAsync(() => Run(s => s.AcceptInvitationAsync(outsider, expired.Token)), "invitation_expired");
await using (var db = Context())
{
    var airline = await db.Airlines.SingleAsync(x => x.Id == outsiderVa.Id);
    airline.Plan = AirlinePlan.Pro; airline.SubscriptionExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
    await db.SaveChangesAsync();
}
Check((await Run(s => s.GetAirlineAsync(outsider, outsiderVa.Id))).Plan == AirlinePlan.Community, "expired Pro resolves to Community");
await RejectAsync(() => Run(s => s.CreateAirlineAsync(outsider, new("Third", prefix + "third", "THRD"))), "plan_limit_reached");
var ownerInvite = await Run(s => s.InviteAsync(actor, va.Id, new(outsider.Email, [AirlineRole.Pilot])));
await Run(s => s.AcceptInvitationAsync(outsider, ownerInvite.Token));
await RejectAsync(() => Mutation(s => s.TransferOwnershipAsync(actor, va.Id, new(separate.Account.Id))), "plan_limit_reached");
await using (var db = Context())
{
    var airline = await db.Airlines.SingleAsync(x => x.Id == outsiderVa.Id);
    airline.SubscriptionExpiresAt = null; airline.SubscriptionStatus = "canceled";
    await db.SaveChangesAsync();
}
await RejectAsync(() => Run(s => s.CreateAirlineAsync(outsider, new("Fourth", prefix + "fourth", "FRTH"))), "plan_limit_reached");
await Mutation(s => s.TransferOwnershipAsync(actor, va.Id, new(pilotId)));
var after = await Run(s => s.GetAirlineAsync(actor, va.Id));
Check(after.IsFounder && !after.Roles.Contains(AirlineRole.Owner) && after.Roles.Contains(AirlineRole.Administrator), "transfer retains founder history and previous owner's administrator access");
Check((await Run(s => s.GetAirlineAsync(pilotMfa, va.Id))).Roles.Contains(AirlineRole.Owner), "transfer assigns exactly one new owner");
await RejectAsync(() => Mutation(s => s.TransferOwnershipAsync(actor, va.Id, new(ownerBootstrap.Account.Id))), "owner_required");
await RejectAsync(() => Mutation(s => s.ChangeRolesAsync(actor, va.Id, pilotMembership.Id, new([AirlineRole.Pilot]))), "ownership_invariant");
await using (var db = Context())
{
    var otherMembership = await db.Memberships.SingleAsync(x => x.UserId == pilotId && x.AirlineId == outsiderVa.Id);
    otherMembership.Status = MembershipStatus.Suspended;
    await db.SaveChangesAsync();
}
await RejectAsync(() => Run(s => s.AcceptInvitationAsync(pilot, otherInvite.Token)), "membership_inactive");
Check((await Run(s => s.BootstrapAsync(pilot))).Airlines.Length == 1, "suspended membership filtered");
await Mutation(s => s.SetWorkspaceAsync(outsider, new(outsiderVa.Id)));
await using (var db = Context())
{
    (await db.Airlines.SingleAsync(x => x.Id == outsiderVa.Id)).Status = "suspended";
    await db.SaveChangesAsync();
}
var hidden = await Run(s => s.BootstrapAsync(outsider));
Check(hidden.Airlines.All(x => x.Id != outsiderVa.Id) && hidden.LastWorkspace.AirlineId is null, "suspended airline hidden and stale preference returns to personal");
await using (var db = Context())
{
    (await db.Users.SingleAsync(x => x.Id == pilotId)).Status = AccountStatus.Suspended;
    await db.SaveChangesAsync();
}
await RejectAsync(() => Run(s => s.BootstrapAsync(pilot)), "account_suspended");
await RejectAsync(() => Run(s => s.GetAirlineAsync(pilot, va.Id)), "account_suspended");
await using (var db = Context())
{
    var audits = await db.AuditEvents.Where(x => x.AirlineId == va.Id).ToArrayAsync();
    Check(audits.Any(x => x.Action == "airline.ownership_transferred"), "ownership changes are audited");
    Check(audits.All(x => !x.Details.Contains(issued.Token) && !x.Details.Contains(revoked.Token)), "audit excludes invitation secrets");
    Check(audits.Count(x => x.Action == "invitation.accepted" && x.TargetId == issued.Invitation.Id) == 1, "concurrent acceptance has exactly one audit event");
}
var racingOwner = actor with { Subject = prefix + "-race", Email = prefix + "-race@example.com" };
var races = await Task.WhenAll(Enumerable.Range(0, 4).Select(async i =>
{
    try { await Run(s => s.CreateAirlineAsync(racingOwner, new("Race", prefix + "race" + i, "RACE"))); return "created"; }
    catch (IdentityException ex) { return ex.Code; }
}));
Check(races.Count(x => x == "created") == 1 && races.Count(x => x == "plan_limit_reached") == 3, "concurrent creation cannot bypass Community ownership limit");
Console.WriteLine($"{passed} account checks passed, including real PostgreSQL integration.");
Console.WriteLine($"Disposable test schema retained for inspection: {schemaName}");
