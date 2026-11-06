using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Application.Authorization;

public interface IAccessRoleStore
{
    Task<IReadOnlyList<Role>> ListVisibleAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<Role?> FindVisibleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken);

    Task<bool> NameTakenAsync(Guid tenantId, string name, Guid? exceptRoleId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> PermissionCodesAsync(Guid roleId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string[]>> PermissionCodesAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<AccessScopeInput>> ScopesAsync(Guid roleId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, int>> AssigneeCountsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<int> AssigneeCountAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> AssigneeIdsAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken);

    Task AddAsync(Role role, CancellationToken cancellationToken);

    Task ReplaceGrantsAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, IReadOnlyCollection<AccessScopeInput> scopes, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, Guid>> PermissionIdsAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken);

    Task<IReadOnlyList<AccessAssignment>> AssignmentsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task ReplaceManagedRoleAsync(Guid tenantId, Guid userId, Role role, DateTime utcNow, CancellationToken cancellationToken);

    Task AddUserPermissionOverrideAsync(Guid tenantId, Guid userId, Guid permissionId, bool granted, CancellationToken cancellationToken);

    Task AddUserResourceScopeAsync(Guid tenantId, Guid userId, string resource, string scope, CancellationToken cancellationToken);
}
