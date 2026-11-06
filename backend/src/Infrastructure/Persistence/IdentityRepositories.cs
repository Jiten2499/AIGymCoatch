using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _db;
    private readonly IPermissionCacheInvalidator _permissions;
    private int _transactionDepth;

    public UnitOfWork(ApplicationDbContext db, IPermissionCacheInvalidator permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (_transactionDepth == 0)
            {
                await _permissions.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            return count;
        }
        catch
        {
            if (_transactionDepth == 0)
            {
                _permissions.Discard();
            }

            throw;
        }
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            _transactionDepth++;
            try
            {
                await action(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                await _permissions.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                _permissions.Discard();
                throw;
            }
            finally
            {
                _transactionDepth--;
            }
        }).ConfigureAwait(false);
    }
}

public sealed class UserAccountRepository : IUserAccountRepository
{
    private readonly ApplicationDbContext _db;

    public UserAccountRepository(ApplicationDbContext db) => _db = db;

    public Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        _db.Users.SingleOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken) =>
        _db.Users.AnyAsync(user => user.Phone == phone, cancellationToken);

    public Task<UserAccount?> GetByPhoneAsync(string phone, CancellationToken cancellationToken) =>
        _db.Users.SingleOrDefaultAsync(user => user.Phone == phone, cancellationToken);

    public async Task AddAsync(UserAccount user, CancellationToken cancellationToken) =>
        await _db.Users.AddAsync(user, cancellationToken).ConfigureAwait(false);

    public Task<int> CountAsync(DirectoryScope scope, CancellationToken cancellationToken) =>
        DirectoryUsers(scope).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<PlatformUserResponse>> ListPageAsync(DirectoryScope scope, int skip, int take, CancellationToken cancellationToken)
    {
        var rows = await (
            from user in DirectoryUsers(scope)
            join tenant in _db.Tenants.AsNoTracking() on user.TenantId equals tenant.Id into tenants
            from tenant in tenants.DefaultIfEmpty()
            orderby user.Email
            select new { user.Id, user.Email, user.FirstName, user.LastName, user.Status, user.TenantId, TenantName = tenant == null ? string.Empty : tenant.Name, user.CreatedAt, HasPhoto = user.PhotoStorageKey != null })
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return Array.Empty<PlatformUserResponse>();
        }

        var ids = rows.Select(row => row.Id).ToArray();
        var plans = await (
            from subscription in _db.Subscriptions.AsNoTracking()
            join plan in _db.Plans.AsNoTracking() on subscription.PlanId equals plan.Id
            where subscription.SubscriberType == SubscriberType.User
                && subscription.Status == SubscriptionStatus.Active
                && subscription.UserId != null
                && ids.Contains(subscription.UserId.Value)
            select new { UserId = subscription.UserId!.Value, plan.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var planNames = plans.ToDictionary(row => row.UserId, row => row.Name);
        return rows.Select(row => new PlatformUserResponse(row.Id, row.Email, row.FirstName, row.LastName, row.Status.ToString(), row.TenantId, row.TenantName, row.CreatedAt, planNames.GetValueOrDefault(row.Id) ?? string.Empty, row.HasPhoto)).ToArray();
    }

    private IQueryable<UserAccount> DirectoryUsers(DirectoryScope scope)
    {
        var users = _db.Users.AsNoTracking().Where(user => !user.Email.ToLower().EndsWith(DirectoryRules.ReservedTestDomain));
        if (scope.PlatformWide || scope.TenantId is not Guid tenantId)
        {
            return users;
        }

        users = users.Where(user => _db.TenantMembers.Any(member =>
            member.UserId == user.Id && member.TenantId == tenantId && member.Status == MembershipStatus.Active));
        if (!scope.StaffOnly)
        {
            return users;
        }

        var staffRoles = RoleNames.BusinessStaff.ToList();
        var staffRoleIds = _db.Roles
            .Where(role => role.IsActive && (role.TenantId == tenantId || (role.TenantId == null && staffRoles.Contains(role.Name))))
            .Select(role => role.Id);
        return users.Where(user => _db.UserRoles.Any(assignment =>
            assignment.UserId == user.Id
            && assignment.TenantId == tenantId
            && staffRoleIds.Contains(assignment.RoleId)));
    }
}

public sealed class TenantRepository : ITenantRepository
{
    private readonly ApplicationDbContext _db;

    public TenantRepository(ApplicationDbContext db) => _db = db;

    public Task<Tenant?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Tenants.SingleOrDefaultAsync(tenant => tenant.Id == id, cancellationToken);

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken) =>
        await _db.Tenants.AddAsync(tenant, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Tenant>> ListAsync(DirectoryScope scope, int skip, int take, CancellationToken cancellationToken) =>
        await DirectoryTenants(scope).OrderBy(tenant => tenant.Name).Skip(skip).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<int> CountAsync(DirectoryScope scope, CancellationToken cancellationToken) =>
        DirectoryTenants(scope).CountAsync(cancellationToken);

    private IQueryable<Tenant> DirectoryTenants(DirectoryScope scope)
    {
        var tenants = _db.Tenants.AsNoTracking().Where(tenant => _db.TenantMembers.Any(member =>
            member.TenantId == tenant.Id
            && member.Status == MembershipStatus.Active
            && _db.Users.Any(user => user.Id == member.UserId && !user.Email.ToLower().EndsWith(DirectoryRules.ReservedTestDomain))));
        return scope.PlatformWide || scope.TenantId is not Guid tenantId
            ? tenants
            : tenants.Where(tenant => tenant.Id == tenantId);
    }
}

public sealed class TenantMembershipRepository : ITenantMembershipRepository
{
    private readonly ApplicationDbContext _db;

    public TenantMembershipRepository(ApplicationDbContext db) => _db = db;

    public async Task AddAsync(TenantMember member, CancellationToken cancellationToken) =>
        await _db.TenantMembers.AddAsync(member, cancellationToken).ConfigureAwait(false);

    public Task<TenantMember?> GetActiveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        _db.TenantMembers.SingleOrDefaultAsync(
            member => member.TenantId == tenantId && member.UserId == userId && member.Status == MembershipStatus.Active,
            cancellationToken);

    public async Task<IReadOnlyList<WorkspaceSummary>> ListForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var workspaces = await (
            from member in _db.TenantMembers
            join tenant in _db.Tenants on member.TenantId equals tenant.Id
            where member.UserId == userId && member.Status == MembershipStatus.Active
            select new { tenant.Id, tenant.Name, tenant.TenantType, tenant.Status })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var roles = await (
            from assignment in _db.UserRoles
            join role in _db.Roles on assignment.RoleId equals role.Id
            where assignment.UserId == userId
            select new { assignment.TenantId, role.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return workspaces
            .Select(workspace => new WorkspaceSummary(
                workspace.Id,
                workspace.Name,
                workspace.TenantType,
                workspace.Status,
                roles.Where(role => role.TenantId == workspace.Id).Select(role => role.Name).ToArray()))
            .ToArray();
    }
}
