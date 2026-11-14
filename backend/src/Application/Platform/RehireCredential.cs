using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Application.Platform;

internal static class RehireCredential
{
    public static async Task<bool> KeepsPasswordAsync(
        UserAccount? existing,
        ITenantMembershipRepository memberships,
        CancellationToken cancellationToken)
    {
        if (existing is null)
        {
            return false;
        }

        var workspaces = await memberships.ListForUserAsync(existing.Id, cancellationToken).ConfigureAwait(false);
        return workspaces?.Any(workspace => workspace.Type != TenantType.Platform) == true;
    }
}
