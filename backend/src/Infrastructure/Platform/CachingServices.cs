using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform;

namespace LifeFitAI.Infrastructure.Platform;

public sealed class CachingPermissionService : IPermissionService
{
    private readonly ITenantMembershipRepository _members;
    private readonly IRoleRepository _roles;
    private readonly ICacheService _cache;

    public CachingPermissionService(ITenantMembershipRepository members, IRoleRepository roles, ICacheService cache)
    {
        _members = members;
        _roles = roles;
        _cache = cache;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permissionCode, CancellationToken cancellationToken)
    {
        var key = TenantCache.Permissions(tenantId, userId);
        var codes = await _cache.GetAsync<string[]>(key, cancellationToken).ConfigureAwait(false);
        if (codes is null)
        {
            codes = await _members.GetActiveAsync(tenantId, userId, cancellationToken).ConfigureAwait(false) is null
                ? []
                : (await _roles.GetPermissionCodesAsync(userId, tenantId, cancellationToken).ConfigureAwait(false)).ToArray();
            await _cache.SetAsync(key, codes, TimeSpan.FromSeconds(45), cancellationToken).ConfigureAwait(false);
        }

        return codes.Contains(permissionCode, StringComparer.Ordinal);
    }

    public async Task RequirePermissionAsync(Guid userId, Guid tenantId, string permissionCode, CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(userId, tenantId, permissionCode, cancellationToken).ConfigureAwait(false))
        {
            throw new Application.Common.Exceptions.ForbiddenException("You do not have permission to perform this action.");
        }
    }
}

public sealed class CachingFeatureFlagService : IFeatureFlagService
{
    private readonly FeatureFlagService _inner;
    private readonly ICacheService _cache;

    public CachingFeatureFlagService(FeatureFlagService inner, ICacheService cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<bool> IsEnabledAsync(Guid tenantId, string featureCode, CancellationToken cancellationToken)
    {
        var key = TenantCache.Feature(tenantId, featureCode);
        var cached = await _cache.GetAsync<bool?>(key, cancellationToken).ConfigureAwait(false);
        if (cached is bool enabled)
        {
            return enabled;
        }

        enabled = await _inner.IsEnabledAsync(tenantId, featureCode, cancellationToken).ConfigureAwait(false);
        await _cache.SetAsync(key, (bool?)enabled, TimeSpan.FromSeconds(60), cancellationToken).ConfigureAwait(false);
        return enabled;
    }
}
