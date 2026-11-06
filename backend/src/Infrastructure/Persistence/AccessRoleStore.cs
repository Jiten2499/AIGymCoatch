using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class AccessRoleStore : IAccessRoleStore
{
    private readonly ApplicationDbContext _db;
    private readonly IPermissionCacheInvalidator _permissions;

    public AccessRoleStore(ApplicationDbContext db, IPermissionCacheInvalidator permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<Role>> ListVisibleAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var templates = RoleNames.BusinessTemplates.ToList();
        return await _db.Roles.AsNoTracking()
            .Where(role => role.TenantId == tenantId || (role.TenantId == null && templates.Contains(role.Name)))
            .OrderBy(role => role.IsSystem ? 0 : 1)
            .ThenBy(role => role.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Role?> FindVisibleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(item => item.Id == roleId, cancellationToken).ConfigureAwait(false);
        return role is not null && AccessRoleRules.IsManaged(role, tenantId) ? role : null;
    }

    public Task<bool> NameTakenAsync(Guid tenantId, string name, Guid? exceptRoleId, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        return _db.Roles.AnyAsync(
            role => role.Name == trimmed
                && role.Id != exceptRoleId
                && (role.TenantId == null || role.TenantId == tenantId),
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> PermissionCodesAsync(Guid roleId, CancellationToken cancellationToken)
    {
        return await (
            from link in _db.RolePermissions.AsNoTracking()
            join permission in _db.Permissions.AsNoTracking() on link.PermissionId equals permission.Id
            where link.RoleId == roleId
            select permission.Code).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, string[]>> PermissionCodesAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        var rows = await (
            from link in _db.RolePermissions.AsNoTracking()
            join permission in _db.Permissions.AsNoTracking() on link.PermissionId equals permission.Id
            where roleIds.Contains(link.RoleId)
            select new { link.RoleId, permission.Code }).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.GroupBy(row => row.RoleId).ToDictionary(group => group.Key, group => group.Select(row => row.Code).Distinct().ToArray());
    }

    public async Task<IReadOnlyList<AccessScopeInput>> ScopesAsync(Guid roleId, CancellationToken cancellationToken)
    {
        return await _db.RoleResourceScopes.AsNoTracking()
            .Where(scope => scope.RoleId == roleId)
            .Select(scope => new AccessScopeInput(scope.Resource, scope.Scope))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> AssigneeCountsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _db.UserRoles.AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId)
            .GroupBy(assignment => assignment.RoleId)
            .Select(group => new { group.Key, Count = group.Select(item => item.UserId).Distinct().Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.ToDictionary(row => row.Key, row => row.Count);
    }

    public Task<int> AssigneeCountAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken) =>
        _db.UserRoles.AsNoTracking().CountAsync(assignment => assignment.TenantId == tenantId && assignment.RoleId == roleId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> AssigneeIdsAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken) =>
        await _db.UserRoles.AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId && assignment.RoleId == roleId)
            .Select(assignment => assignment.UserId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(Role role, CancellationToken cancellationToken) =>
        await _db.Roles.AddAsync(role, cancellationToken).ConfigureAwait(false);

    public async Task ReplaceGrantsAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, IReadOnlyCollection<AccessScopeInput> scopes, CancellationToken cancellationToken)
    {
        var links = await _db.RolePermissions.Where(link => link.RoleId == roleId).ToListAsync(cancellationToken).ConfigureAwait(false);
        _db.RolePermissions.RemoveRange(links);
        foreach (var permissionId in permissionIds.Distinct())
        {
            _db.RolePermissions.Add(RolePermission.Create(roleId, permissionId));
        }

        var existing = await _db.RoleResourceScopes.Where(scope => scope.RoleId == roleId).ToListAsync(cancellationToken).ConfigureAwait(false);
        _db.RoleResourceScopes.RemoveRange(existing);
        foreach (var scope in scopes)
        {
            _db.RoleResourceScopes.Add(RoleResourceScope.Create(roleId, scope.Resource, scope.Scope));
        }
    }

    public async Task<IReadOnlyDictionary<string, Guid>> PermissionIdsAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken)
    {
        var rows = await _db.Permissions.AsNoTracking()
            .Where(permission => codes.Contains(permission.Code))
            .Select(permission => new { permission.Code, permission.Id })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.ToDictionary(row => row.Code, row => row.Id, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<AccessAssignment>> AssignmentsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var people = await (
            from member in _db.TenantMembers.AsNoTracking()
            join user in _db.Users.AsNoTracking() on member.UserId equals user.Id
            where member.TenantId == tenantId
            select new { user.Id, user.Email, user.FirstName, user.LastName, Account = user.Status, user.PhotoStorageKey, Membership = member.Status }).ToListAsync(cancellationToken).ConfigureAwait(false);
        var roleRows = await (
            from assignment in _db.UserRoles.AsNoTracking()
            join role in _db.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            where assignment.TenantId == tenantId && role.IsActive
            select new { assignment.UserId, role.Id, role.Name, role.TenantId }).ToListAsync(cancellationToken).ConfigureAwait(false);
        var seated = await (
            from seat in _db.SubscriptionSeats.AsNoTracking()
            join subscription in _db.Subscriptions.AsNoTracking() on seat.SubscriptionId equals subscription.Id
            where subscription.TenantId == tenantId && seat.Status == SeatStatus.Active
            select seat.UserId).Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);
        var seatedIds = seated.ToHashSet();
        return people.Select(person =>
        {
            var managed = roleRows.FirstOrDefault(row => row.UserId == person.Id && (row.TenantId == tenantId || RoleNames.BusinessTemplates.Contains(row.Name)));
            var label = managed is null ? "No business role" : AccessRoleCatalog.Label(managed.Name);
            return new AccessAssignment(
                person.Id,
                $"{person.FirstName} {person.LastName}".Trim(),
                person.Email,
                managed?.Id,
                managed?.Name ?? string.Empty,
                label,
                person.PhotoStorageKey is not null,
                person.Account.ToString(),
                seatedIds.Contains(person.Id) ? "Active" : "No seat",
                person.Membership.ToString());
        }).OrderBy(row => row.DisplayName).ToList();
    }

    public async Task ReplaceManagedRoleAsync(Guid tenantId, Guid userId, Role role, DateTime utcNow, CancellationToken cancellationToken)
    {
        var assignments = await (
            from assignment in _db.UserRoles
            join current in _db.Roles on assignment.RoleId equals current.Id
            where assignment.TenantId == tenantId && assignment.UserId == userId
            select new { assignment, current }).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (assignments.Any(row => row.current.Id == role.Id))
        {
            _permissions.Schedule(tenantId, userId);
            return;
        }

        foreach (var row in assignments.Where(row => AccessRoleRules.IsManaged(row.current, tenantId)))
        {
            row.assignment.Remove(utcNow);
        }

        await _db.UserRoles.AddAsync(UserRole.Create(Guid.NewGuid(), userId, role.Id, tenantId), cancellationToken).ConfigureAwait(false);

        _permissions.Schedule(tenantId, userId);
    }

    public async Task AddUserPermissionOverrideAsync(Guid tenantId, Guid userId, Guid permissionId, bool granted, CancellationToken cancellationToken)
    {
        var existing = await _db.UserPermissionOverrides
            .FirstOrDefaultAsync(row => row.TenantId == tenantId && row.UserId == userId && row.PermissionId == permissionId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return;
        }

        await _db.UserPermissionOverrides.AddAsync(UserPermissionOverride.Create(Guid.NewGuid(), tenantId, userId, permissionId, granted), cancellationToken).ConfigureAwait(false);
        _permissions.Schedule(tenantId, userId);
    }

    public async Task AddUserResourceScopeAsync(Guid tenantId, Guid userId, string resource, string scope, CancellationToken cancellationToken)
    {
        var existing = await _db.UserResourceScopes
            .FirstOrDefaultAsync(row => row.TenantId == tenantId && row.UserId == userId && row.Resource == resource, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            existing.SoftDelete(DateTime.UtcNow);
        }

        await _db.UserResourceScopes.AddAsync(UserResourceScope.Create(Guid.NewGuid(), tenantId, userId, resource, scope), cancellationToken).ConfigureAwait(false);
    }
}
