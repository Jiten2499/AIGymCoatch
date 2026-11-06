using LifeFitAI.Application.Tenancy;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class StaffDetailReader : IStaffDetailReader
{
    private readonly ApplicationDbContext _db;

    public StaffDetailReader(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<string, string>> UserScopesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var userScopes = await _db.UserResourceScopes.AsNoTracking()
            .Where(scope => scope.TenantId == tenantId && scope.UserId == userId)
            .Select(scope => new { scope.Resource, scope.Scope })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (userScopes.Count > 0)
        {
            return userScopes.ToDictionary(scope => scope.Resource, scope => scope.Scope, StringComparer.Ordinal);
        }

        var roleIds = await _db.UserRoles.AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId && assignment.UserId == userId)
            .Select(assignment => assignment.RoleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var roleScopes = await _db.RoleResourceScopes.AsNoTracking()
            .Where(scope => roleIds.Contains(scope.RoleId))
            .Select(scope => new { scope.Resource, scope.Scope })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return roleScopes
            .GroupBy(scope => scope.Resource, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => AccessScopes.MostPermissive(group.Select(item => item.Scope)), StringComparer.Ordinal);
    }
}
