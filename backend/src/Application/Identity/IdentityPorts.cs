using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Application.Identity;

public interface IUserAccountRepository
{
    Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken);

    Task AddAsync(UserAccount user, CancellationToken cancellationToken);

    Task<int> CountAsync(DirectoryScope scope, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlatformUserResponse>> ListPageAsync(DirectoryScope scope, int skip, int take, CancellationToken cancellationToken);
}

public interface ITenantRepository
{
    Task<Tenant?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Tenant tenant, CancellationToken cancellationToken);

    Task<IReadOnlyList<Tenant>> ListAsync(DirectoryScope scope, int skip, int take, CancellationToken cancellationToken);

    Task<int> CountAsync(DirectoryScope scope, CancellationToken cancellationToken);
}

public interface ITenantMembershipRepository
{
    Task AddAsync(TenantMember member, CancellationToken cancellationToken);

    Task<TenantMember?> GetActiveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceSummary>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IRoleRepository
{
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken);

    Task AddAsync(UserRole assignment, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetPermissionCodesAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);

    Task RemoveForMemberAsync(Guid userId, Guid tenantId, DateTime utcNow, CancellationToken cancellationToken);
}

public interface ISubscriptionRepository
{
    Task AddAsync(Subscription subscription, CancellationToken cancellationToken);

    Task<Subscription?> GetActiveForTenantAsync(Guid tenantId, CancellationToken cancellationToken);
}

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task RevokeActiveAsync(Guid userId, DateTime utcNow, string? ipAddress, CancellationToken cancellationToken);

    Task<int> PurgeExpiredAsync(DateTime utcNow, CancellationToken cancellationToken);
}

public interface IPasswordResetRepository
{
    Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken);

    Task<PasswordResetToken?> GetActiveByHashAsync(string tokenHash, CancellationToken cancellationToken);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
