using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Application.Tests.Identity;

internal sealed class FakeUsers : IUserAccountRepository
{
    private readonly IdentityStore _store;

    public FakeUsers(IdentityStore store) => _store = store;

    public Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(_store.VisibleUsers.SingleOrDefault(user => user.Email == email && user.DeletedAt == null));

    public Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_store.VisibleUsers.SingleOrDefault(user => user.Id == id && user.DeletedAt == null));

    public Task<IReadOnlyDictionary<Guid, string>> GetEmailsByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, string> emails = _store.VisibleUsers
            .Where(user => user.DeletedAt == null && userIds.Contains(user.Id))
            .ToDictionary(user => user.Id, user => user.Email);
        return Task.FromResult(emails);
    }

    public Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken) =>
        Task.FromResult(_store.VisibleUsers.Any(user => user.Phone == phone && user.DeletedAt == null));

    public Task<UserAccount?> GetByPhoneAsync(string phone, CancellationToken cancellationToken) =>
        Task.FromResult(_store.VisibleUsers.SingleOrDefault(user => user.Phone == phone && user.DeletedAt == null));

    public Task AddAsync(UserAccount user, CancellationToken cancellationToken)
    {
        _store.Stage(user);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(DirectoryScope scope, CancellationToken cancellationToken) =>
        Task.FromResult(VisibleDirectoryUsers(scope).Count());

    public Task<IReadOnlyList<PlatformUserResponse>> ListPageAsync(DirectoryScope scope, int skip, int take, CancellationToken cancellationToken)
    {
        IReadOnlyList<PlatformUserResponse> page = VisibleDirectoryUsers(scope)
            .OrderBy(user => user.Email)
            .Skip(skip)
            .Take(take)
            .Select(user => new PlatformUserResponse(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Status.ToString(),
                user.TenantId,
                _store.VisibleTenants.SingleOrDefault(tenant => tenant.Id == user.TenantId)?.Name ?? string.Empty,
                user.CreatedAt,
                string.Empty,
                user.PhotoStorageKey is not null))
            .ToArray();
        return Task.FromResult(page);
    }

    private IEnumerable<UserAccount> VisibleDirectoryUsers(DirectoryScope scope)
    {
        var users = _store.VisibleUsers.Where(user => user.DeletedAt == null && DirectoryRules.IsRealAccount(user.Email));
        if (scope.PlatformWide || scope.TenantId is not Guid tenantId)
        {
            return users;
        }

        var memberIds = _store.VisibleMembers
            .Where(member => member.DeletedAt == null && member.TenantId == tenantId && member.Status == MembershipStatus.Active)
            .Select(member => member.UserId)
            .ToHashSet();
        users = users.Where(user => memberIds.Contains(user.Id));
        if (!scope.StaffOnly)
        {
            return users;
        }

        var staffRoleIds = _store.Roles
            .Where(role => role.DeletedAt == null && role.IsActive && (RoleNames.BusinessStaff.Contains(role.Name) || role.TenantId == tenantId))
            .Select(role => role.Id)
            .ToHashSet();
        var staffUserIds = _store.VisibleUserRoles
            .Where(assignment => assignment.DeletedAt == null && assignment.TenantId == tenantId && staffRoleIds.Contains(assignment.RoleId))
            .Select(assignment => assignment.UserId)
            .ToHashSet();
        return users.Where(user => staffUserIds.Contains(user.Id));
    }
}

internal sealed class FakeTenants : ITenantRepository
{
    private readonly IdentityStore _store;

    public FakeTenants(IdentityStore store) => _store = store;

    public Task<Tenant?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_store.VisibleTenants.SingleOrDefault(tenant => tenant.Id == id && tenant.DeletedAt == null));

    public Task AddAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _store.Stage(tenant);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Tenant>> ListAsync(DirectoryScope scope, int skip, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Tenant>>(VisibleDirectoryTenants(scope).Skip(skip).Take(take).ToArray());

    public Task<int> CountAsync(DirectoryScope scope, CancellationToken cancellationToken) =>
        Task.FromResult(VisibleDirectoryTenants(scope).Count());

    private IEnumerable<Tenant> VisibleDirectoryTenants(DirectoryScope scope)
    {
        var realMemberIds = _store.VisibleUsers.Where(user => user.DeletedAt == null && DirectoryRules.IsRealAccount(user.Email)).Select(user => user.Id).ToHashSet();
        var tenantIds = _store.VisibleMembers
            .Where(member => member.DeletedAt == null && member.Status == MembershipStatus.Active && realMemberIds.Contains(member.UserId))
            .Select(member => member.TenantId)
            .ToHashSet();
        var tenants = _store.VisibleTenants.Where(tenant => tenant.DeletedAt == null && tenantIds.Contains(tenant.Id));
        return scope.PlatformWide || scope.TenantId is not Guid tenantId ? tenants : tenants.Where(tenant => tenant.Id == tenantId);
    }
}

internal sealed class FakeMembers : ITenantMembershipRepository
{
    private readonly IdentityStore _store;

    public FakeMembers(IdentityStore store) => _store = store;

    public Task AddAsync(TenantMember member, CancellationToken cancellationToken)
    {
        _store.Stage(member);
        return Task.CompletedTask;
    }

    public Task<TenantMember?> GetActiveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.VisibleMembers.SingleOrDefault(member =>
            member.TenantId == tenantId && member.UserId == userId && member.Status == MembershipStatus.Active && member.DeletedAt == null));

    public Task<IReadOnlyList<WorkspaceSummary>> ListForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkspaceSummary> workspaces = _store.VisibleMembers
            .Where(member => member.UserId == userId && member.Status == MembershipStatus.Active && member.DeletedAt == null)
            .Select(member =>
            {
                var tenant = _store.VisibleTenants.Single(tenant => tenant.Id == member.TenantId);
                var roles = _store.VisibleUserRoles
                    .Where(role => role.UserId == userId && role.TenantId == tenant.Id && role.DeletedAt == null)
                    .Select(role => _store.Roles.Single(item => item.Id == role.RoleId).Name)
                    .ToArray();
                return new WorkspaceSummary(tenant.Id, tenant.Name, tenant.TenantType, tenant.Status, roles);
            })
            .ToArray();
        return Task.FromResult(workspaces);
    }
}

internal sealed class FakeRoles : IRoleRepository
{
    private readonly IdentityStore _store;

    public FakeRoles(IdentityStore store) => _store = store;

    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Roles.SingleOrDefault(role => role.Name == name && role.DeletedAt == null));

    public Task<Role?> GetAssignableAsync(Guid tenantId, string name, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        var role = _store.Roles.SingleOrDefault(item =>
            item.Name == trimmed
            && item.DeletedAt == null
            && item.IsActive
            && ((item.TenantId == null && (RoleNames.BusinessTemplates.Contains(item.Name) || item.Name == RoleNames.Member)) || item.TenantId == tenantId));
        return Task.FromResult(role);
    }

    public Task AddAsync(UserRole assignment, CancellationToken cancellationToken)
    {
        _store.Stage(assignment);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetPermissionCodesAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> codes = _store.VisibleUserRoles
            .Where(assignment => assignment.UserId == userId && assignment.TenantId == tenantId && assignment.DeletedAt == null)
            .SelectMany(assignment => _store.RolePermissions.Where(link => link.RoleId == assignment.RoleId))
            .Select(link => _store.Permissions.Single(permission => permission.Id == link.PermissionId).Code)
            .Distinct()
            .ToArray();
        return Task.FromResult(codes);
    }

    public Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> names = _store.VisibleUserRoles
            .Where(assignment => assignment.UserId == userId && assignment.TenantId == tenantId && assignment.DeletedAt == null)
            .Select(assignment => _store.Roles.Single(role => role.Id == assignment.RoleId).Name)
            .ToArray();
        return Task.FromResult(names);
    }

    public Task RemoveForMemberAsync(Guid userId, Guid tenantId, DateTime utcNow, CancellationToken cancellationToken)
    {
        foreach (var assignment in _store.VisibleUserRoles.Where(assignment => assignment.UserId == userId && assignment.TenantId == tenantId && assignment.DeletedAt == null))
        {
            assignment.Remove(utcNow);
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeSubscriptions : ISubscriptionRepository
{
    private readonly IdentityStore _store;

    public FakeSubscriptions(IdentityStore store) => _store = store;

    public Task AddAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        _store.Stage(subscription);
        return Task.CompletedTask;
    }

    public Task<Subscription?> GetActiveForTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.VisibleSubscriptions.FirstOrDefault(subscription =>
            subscription.SubscriberType == SubscriberType.Tenant
            && subscription.TenantId == tenantId
            && subscription.Status == SubscriptionStatus.Active
            && subscription.DeletedAt == null));
}

internal sealed class FakeRefreshTokens : IRefreshTokenRepository
{
    private readonly IdentityStore _store;

    public FakeRefreshTokens(IdentityStore store) => _store = store;

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        _store.Stage(token);
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(_store.VisibleRefresh.SingleOrDefault(token => token.TokenHash == tokenHash));

    public Task RevokeActiveAsync(Guid userId, DateTime utcNow, string? ipAddress, CancellationToken cancellationToken)
    {
        foreach (var token in _store.VisibleRefresh.Where(token => token.UserId == userId && token.RevokedAt == null))
        {
            token.Revoke(utcNow, ipAddress, null);
        }

        return Task.CompletedTask;
    }

    public Task<int> PurgeExpiredAsync(DateTime utcNow, CancellationToken cancellationToken) =>
        Task.FromResult(0);
}

internal sealed class FakeResets : IPasswordResetRepository
{
    private readonly IdentityStore _store;

    public FakeResets(IdentityStore store) => _store = store;

    public Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken)
    {
        _store.Stage(token);
        return Task.CompletedTask;
    }

    public Task<PasswordResetToken?> GetActiveByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(_store.VisibleResets.SingleOrDefault(token => token.TokenHash == tokenHash && token.IsActive(_store.Clock.UtcNow)));

    public Task RevokeUnusedAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken)
    {
        foreach (var token in _store.VisibleResets.Where(token => token.UserId == userId && token.IsActive(utcNow)))
        {
            token.MarkUsed(utcNow);
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeAudit : IAuditLogRepository
{
    private readonly IdentityStore _store;

    public FakeAudit(IdentityStore store) => _store = store;

    public Task AddAsync(AuditLog entry, CancellationToken cancellationToken)
    {
        _store.Stage(entry);
        return Task.CompletedTask;
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly IdentityStore _store;

    public FakeUnitOfWork(IdentityStore store) => _store = store;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _store.SaveChangesAsync();

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default) =>
        _store.ExecuteAsync(action, cancellationToken);
}
