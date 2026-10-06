using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym;

public interface IOperationGuard
{
    Task<Result> AllowAsync(string permissionCode, string featureCode, CancellationToken cancellationToken);
}

public sealed class OperationGuard : IOperationGuard
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly IFeatureFlagService _features;

    public OperationGuard(ICurrentActor actor, IPermissionService permissions, IFeatureFlagService features)
    {
        _actor = actor;
        _permissions = permissions;
        _features = features;
    }

    public async Task<Result> AllowAsync(string permissionCode, string featureCode, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure(IdentityErrors.InvalidCredentials);
        }

        if (!await _permissions.HasPermissionAsync(userId, tenantId, permissionCode, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(IdentityErrors.Forbidden);
        }

        if (!await _features.IsEnabledAsync(tenantId, featureCode, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(GymErrors.FeatureDisabled);
        }

        return Result.Success();
    }
}
