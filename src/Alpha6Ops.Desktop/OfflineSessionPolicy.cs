using System;
using Alpha6Ops.Identity;

namespace Alpha6Ops.Desktop;

internal static class OfflineSessionPolicy
{
    internal static bool MayOpen(BootstrapResponse bootstrap, DateTimeOffset receivedAt, DateTimeOffset lastSeenAt, DateTimeOffset now)
        => bootstrap.Account.Status == AccountStatus.Active
        && bootstrap.Account.Id != Guid.Empty
        && now >= receivedAt && now >= lastSeenAt
        && now - receivedAt < TimeSpan.FromDays(30)
        && bootstrap.ServerTime + (now - receivedAt) < bootstrap.OfflineExpiresAt;

    internal static WorkspaceSelection ValidateWorkspace(BootstrapResponse bootstrap, WorkspaceSelection requested)
    {
        if (requested.AirlineId is not { } id) return WorkspaceSelection.Personal;
        foreach (var airline in bootstrap.Airlines)
            if (airline.Id == id && airline.MembershipStatus == MembershipStatus.Active) return requested;
        return WorkspaceSelection.Personal;
    }
}
