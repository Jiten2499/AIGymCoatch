using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Constants;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class RoleRepository : IRoleRepository
{
    private readonly ApplicationDbContext _db;
    private readonly IPermissionCacheInvalidator _permissions;

    public RoleRepository(ApplicationDbContext db, IPermissionCacheInvalidator permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken) =>
        _db.Roles.SingleOrDefaultAsync(role => role.TenantId == null && role.Name == name && role.IsActive, cancellationToken);

    public Task<Role?> GetAssignableAsync(Guid tenantId, string name, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        var templates = RoleNames.BusinessTemplates.Append(RoleNames.Member).ToList();
        return _db.Roles.SingleOrDefaultAsync(
            role => role.Name == trimmed
                && role.IsActive
                && ((role.TenantId == null && templates.Contains(role.Name)) || role.TenantId == tenantId),
            cancellationToken);
    }

    public async Task AddAsync(UserRole assignment, CancellationToken cancellationToken)
    {
        await _db.UserRoles.AddAsync(assignment, cancellationToken).ConfigureAwait(false);
        _permissions.Schedule(assignment.TenantId, assignment.UserId);
    }

    public async Task<IReadOnlyList<string>> GetPermissionCodesAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var fromRoles = await (
            from assignment in _db.UserRoles
            join role in _db.Roles on assignment.RoleId equals role.Id
            join link in _db.RolePermissions on role.Id equals link.RoleId
            join permission in _db.Permissions on link.PermissionId equals permission.Id
            where assignment.UserId == userId && assignment.TenantId == tenantId && role.IsActive
            select permission.Code)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var granted = await (
            from overrideRow in _db.UserPermissionOverrides.AsNoTracking()
            join permission in _db.Permissions.AsNoTracking() on overrideRow.PermissionId equals permission.Id
            where overrideRow.TenantId == tenantId && overrideRow.UserId == userId && overrideRow.Granted
            select permission.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var denied = await (
            from overrideRow in _db.UserPermissionOverrides.AsNoTracking()
            join permission in _db.Permissions.AsNoTracking() on overrideRow.PermissionId equals permission.Id
            where overrideRow.TenantId == tenantId && overrideRow.UserId == userId && !overrideRow.Granted
            select permission.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return fromRoles.Concat(granted).Distinct(StringComparer.Ordinal).Where(code => !denied.Contains(code, StringComparer.Ordinal)).ToArray();
    }

    public async Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        return await (
            from assignment in _db.UserRoles
            join role in _db.Roles on assignment.RoleId equals role.Id
            where assignment.UserId == userId && assignment.TenantId == tenantId
            select role.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RemoveForMemberAsync(Guid userId, Guid tenantId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var assignments = await _db.UserRoles
            .Where(assignment => assignment.UserId == userId && assignment.TenantId == tenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var assignment in assignments)
        {
            assignment.Remove(utcNow);
        }

        _permissions.Schedule(tenantId, userId);
    }
}

public sealed class SubscriptionRepository : ISubscriptionRepository
{
    private readonly ApplicationDbContext _db;

    public SubscriptionRepository(ApplicationDbContext db) => _db = db;

    public async Task AddAsync(Subscription subscription, CancellationToken cancellationToken) =>
        await _db.Subscriptions.AddAsync(subscription, cancellationToken).ConfigureAwait(false);

    public Task<Subscription?> GetActiveForTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _db.Subscriptions.FirstOrDefaultAsync(
            subscription => subscription.SubscriberType == SubscriberType.Tenant
                && subscription.TenantId == tenantId
                && subscription.Status == SubscriptionStatus.Active,
            cancellationToken);
}

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ApplicationDbContext _db;

    public RefreshTokenRepository(ApplicationDbContext db) => _db = db;

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken) =>
        await _db.RefreshTokens.AddAsync(token, cancellationToken).ConfigureAwait(false);

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        _db.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task RevokeActiveAsync(Guid userId, DateTime utcNow, string? ipAddress, CancellationToken cancellationToken)
    {
        var tokens = await _db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var token in tokens)
        {
            token.Revoke(utcNow, ipAddress, null);
        }
    }

    public async Task<int> PurgeExpiredAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var cutoff = utcNow.AddDays(-7);
        var stale = await _db.RefreshTokens
            .Where(token => token.ExpiresAt < utcNow || (token.RevokedAt != null && token.RevokedAt < cutoff))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        _db.RefreshTokens.RemoveRange(stale);
        return stale.Count;
    }
}

public sealed class PasswordResetRepository : IPasswordResetRepository
{
    private readonly ApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public PasswordResetRepository(ApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken) =>
        await _db.PasswordResetTokens.AddAsync(token, cancellationToken).ConfigureAwait(false);

    public async Task<PasswordResetToken?> GetActiveByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var token = await _db.PasswordResetTokens
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

        return token is not null && token.IsActive(_clock.UtcNow) ? token : null;
    }

    public async Task RevokeUnusedAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var active = await _db.PasswordResetTokens
            .Where(token => token.UserId == userId && token.UsedAt == null && token.ExpiresAt > utcNow)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var token in active)
        {
            token.MarkUsed(utcNow);
        }
    }
}

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly ApplicationDbContext _db;

    public AuditLogRepository(ApplicationDbContext db) => _db = db;

    public async Task AddAsync(AuditLog entry, CancellationToken cancellationToken)
    {
        if (Correlation.Id is not null)
        {
            entry.AttachCorrelation(Correlation.Id);
        }

        await _db.AuditLogs.AddAsync(entry, cancellationToken).ConfigureAwait(false);
    }
}
