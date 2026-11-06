using LifeFitAI.Application.Authorization;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class MemberAccessScopeReader : IMemberAccessScope
{
    private readonly ApplicationDbContext _db;

    public MemberAccessScopeReader(ApplicationDbContext db) => _db = db;

    public async Task<MemberAccessLimit> LimitAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var roles = await (
            from assignment in _db.UserRoles.AsNoTracking()
            join role in _db.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            where assignment.TenantId == tenantId && assignment.UserId == userId && role.IsActive
            select new { role.Id, role.Name }).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (roles.Count == 0)
        {
            return MemberAccessLimit.Block();
        }

        var userScope = await _db.UserResourceScopes.AsNoTracking()
            .Where(scope => scope.TenantId == tenantId && scope.UserId == userId && scope.Resource == "members")
            .Select(scope => scope.Scope)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        string winner;
        if (userScope is not null)
        {
            winner = userScope;
        }
        else
        {
            var roleIds = roles.Select(role => role.Id).ToArray();
            var stored = await _db.RoleResourceScopes.AsNoTracking()
                .Where(scope => scope.Resource == "members" && roleIds.Contains(scope.RoleId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            winner = AccessScopes.MostPermissive(roles.Select(role => stored.FirstOrDefault(scope => scope.RoleId == role.Id)?.Scope ?? AccessRoleCatalog.FallbackScope(role.Name)));
        }

        if (winner == AccessScopes.All)
        {
            return MemberAccessLimit.AllowAll();
        }

        if (winner is AccessScopes.Own or AccessScopes.Branch)
        {
            return new MemberAccessLimit(winner, userId, null, false);
        }

        var trainerId = await _db.TrainerProfiles.AsNoTracking()
            .Where(trainer => trainer.TenantId == tenantId && trainer.UserId == userId)
            .Select(trainer => (Guid?)trainer.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return trainerId is null ? MemberAccessLimit.Block() : new MemberAccessLimit(winner, null, trainerId, false);
    }
}
