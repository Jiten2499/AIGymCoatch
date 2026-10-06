using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Application.Authorization;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permissionCode, CancellationToken cancellationToken);

    Task RequirePermissionAsync(Guid userId, Guid tenantId, string permissionCode, CancellationToken cancellationToken);
}

public sealed class PermissionService : IPermissionService
{
    private readonly ITenantMembershipRepository _members;
    private readonly IRoleRepository _roles;

    public PermissionService(ITenantMembershipRepository members, IRoleRepository roles)
    {
        _members = members;
        _roles = roles;
    }

    public async Task<bool> HasPermissionAsync(
        Guid userId,
        Guid tenantId,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        if (await _members.GetActiveAsync(tenantId, userId, cancellationToken).ConfigureAwait(false) is null)
        {
            return false;
        }

        var codes = await _roles.GetPermissionCodesAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
        return codes.Contains(permissionCode, StringComparer.Ordinal);
    }

    public async Task RequirePermissionAsync(
        Guid userId,
        Guid tenantId,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(userId, tenantId, permissionCode, cancellationToken).ConfigureAwait(false))
        {
            throw new Common.Exceptions.ForbiddenException("You do not have permission to perform this action.");
        }
    }
}
