using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Infrastructure.Platform;

public sealed class CircularAudience : ICircularAudience
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;

    public CircularAudience(ICurrentActor actor, IPermissionService permissions)
    {
        _actor = actor;
        _permissions = permissions;
    }

    public async Task<string?> OperatorGroupAsync(CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId || _actor.TenantId is not Guid tenantId)
        {
            return null;
        }

        var canManage = await _permissions.HasPermissionAsync(userId, tenantId, PermissionCodes.CommunicationCreate, cancellationToken).ConfigureAwait(false);
        return canManage ? CircularGroups.Operators(tenantId) : null;
    }
}
