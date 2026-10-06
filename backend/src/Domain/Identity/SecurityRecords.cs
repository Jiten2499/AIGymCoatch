namespace LifeFitAI.Domain.Identity;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public string? CreatedByIp { get; private set; }

    public string? RevokedByIp { get; private set; }

    public string? UserAgent { get; private set; }

    public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;

    public static RefreshToken Create(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTime createdAt,
        DateTime expiresAt,
        string? ipAddress,
        string? userAgent)
    {
        return new RefreshToken
        {
            Id = id,
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            CreatedByIp = ipAddress,
            UserAgent = userAgent,
        };
    }

    public void Revoke(DateTime utcNow, string? ipAddress, Guid? replacedByTokenId)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = utcNow;
        RevokedByIp = ipAddress;
        ReplacedByTokenId = replacedByTokenId;
    }
}

public sealed class PasswordResetToken
{
    private PasswordResetToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }

    public DateTime? UsedAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public bool IsActive(DateTime utcNow) => UsedAt is null && ExpiresAt > utcNow;

    public static PasswordResetToken Create(Guid id, Guid userId, string tokenHash, DateTime createdAt, DateTime expiresAt)
    {
        return new PasswordResetToken
        {
            Id = id,
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
        };
    }

    public void MarkUsed(DateTime utcNow) => UsedAt = utcNow;
}

public sealed class AuditLog
{
    private AuditLog()
    {
    }

    public Guid Id { get; private set; }

    public Guid? TenantId { get; private set; }

    public Guid? UserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string Entity { get; private set; } = string.Empty;

    public Guid? EntityId { get; private set; }

    public string? OldValues { get; private set; }

    public string? NewValues { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public string? CorrelationId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static AuditLog Create(
        Guid id,
        Guid? tenantId,
        Guid? userId,
        string action,
        string entity,
        Guid? entityId,
        string? oldValues,
        string? newValues,
        string? ipAddress,
        string? userAgent,
        DateTime createdAt,
        string? correlationId = null)
    {
        return new AuditLog
        {
            Id = id,
            TenantId = tenantId,
            UserId = userId,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CorrelationId = correlationId,
            CreatedAt = createdAt,
        };
    }

    public void AttachCorrelation(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(CorrelationId) && !string.IsNullOrWhiteSpace(correlationId))
        {
            CorrelationId = correlationId.Length <= 64 ? correlationId : correlationId[..64];
        }
    }
}
