using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Platform;

public static class PlatformAccess
{
    public static async Task<Result> RequireSuperAdminAsync(ICurrentActor actor, IRoleRepository roles, IPermissionService permissions, CancellationToken cancellationToken)
    {
        if (actor.UserId is not Guid userId || actor.TenantId is not Guid tenantId)
        {
            return Result.Failure(IdentityErrors.InvalidCredentials);
        }

        var names = await roles.GetRoleNamesAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
        if (!names.Contains(RoleNames.SuperAdmin, StringComparer.Ordinal)
            || !await permissions.HasPermissionAsync(userId, tenantId, PermissionCodes.PlatformAdmin, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(IdentityErrors.Forbidden);
        }

        return Result.Success();
    }
}
