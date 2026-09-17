using Alpha6Ops.Accounts;
using Alpha6Ops.Identity;

namespace Alpha6Ops.Server;

public static class AccountApi
{
    public static void MapAccountApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization("desktop").RequireRateLimiting("requests");
        ActorIdentity Actor(HttpContext context) => TrustedActor.Read(context.User, app.Services.GetRequiredService<ServerSettings>());
        api.MapGet("/me/bootstrap", (HttpContext c, AccountsService s, CancellationToken ct) => s.BootstrapAsync(Actor(c), ct));
        api.MapPut("/me/workspace", async (HttpContext c, AccountsService s, WorkspaceSelection request, CancellationToken ct) =>
        { await s.SetWorkspaceAsync(Actor(c), request, ct); return Results.NoContent(); });
        api.MapPost("/virtual-airlines", (HttpContext c, AccountsService s, CreateAirlineRequest request, CancellationToken ct) => s.CreateAirlineAsync(Actor(c), request, ct));
        api.MapGet("/virtual-airlines/{id:guid}", (HttpContext c, AccountsService s, Guid id, CancellationToken ct) => s.GetAirlineAsync(Actor(c), id, ct));
        api.MapGet("/virtual-airlines/{id:guid}/members", (HttpContext c, AccountsService s, Guid id, CancellationToken ct) => s.ListMembersAsync(Actor(c), id, ct));
        api.MapGet("/virtual-airlines/{id:guid}/invitations", (HttpContext c, AccountsService s, Guid id, CancellationToken ct) => s.ListInvitationsAsync(Actor(c), id, ct));
        api.MapPost("/virtual-airlines/{id:guid}/invitations", (HttpContext c, AccountsService s, Guid id, InviteMemberRequest request, CancellationToken ct) => s.InviteAsync(Actor(c), id, request, ct));
        api.MapPost("/invitations/accept", (HttpContext c, AccountsService s, AcceptInvitationBody request, CancellationToken ct) => s.AcceptInvitationAsync(Actor(c), request.Token, ct));
        api.MapDelete("/virtual-airlines/{id:guid}/invitations/{invitationId:guid}", async (HttpContext c, AccountsService s, Guid id, Guid invitationId, CancellationToken ct) =>
        { await s.RevokeInvitationAsync(Actor(c), id, invitationId, ct); return Results.NoContent(); });
        api.MapPut("/virtual-airlines/{id:guid}/members/{membershipId:guid}/roles", async (HttpContext c, AccountsService s, Guid id, Guid membershipId, ChangeRolesRequest request, CancellationToken ct) =>
        { await s.ChangeRolesAsync(Actor(c), id, membershipId, request, ct); return Results.NoContent(); });
        api.MapPost("/virtual-airlines/{id:guid}/ownership-transfer", async (HttpContext c, AccountsService s, Guid id, TransferOwnershipRequest request, CancellationToken ct) =>
        { await s.TransferOwnershipAsync(Actor(c), id, request, ct); return Results.NoContent(); });
    }
}

public sealed record AcceptInvitationBody(string Token);
