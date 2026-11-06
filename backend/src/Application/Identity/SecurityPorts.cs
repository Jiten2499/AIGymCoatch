namespace LifeFitAI.Application.Identity;

public interface IPasswordHasher
{
    (string Hash, string Salt) HashPassword(string password);

    bool Verify(string password, string hash, string salt);
}

public interface IPasswordPolicy
{
    bool IsValid(string password);

    string Description { get; }
}

public interface ISecretTokenFactory
{
    string Create();

    string Hash(string rawToken);
}

public interface IAccessTokenFactory
{
    (string Token, DateTime ExpiresAt) Create(Guid userId, Guid tenantId);
}

public interface ITokenSettings
{
    TimeSpan RefreshLifetime { get; }

    TimeSpan ResetLifetime { get; }
}

public interface IPasswordResetNotifier
{
    Task NotifyAsync(string email, string rawToken, CancellationToken cancellationToken);

    Task NotifyAsync(string email, string rawToken, string templateKey, Guid? relatedTenantId, CancellationToken cancellationToken);
}

public interface ICurrentActor
{
    Guid? UserId { get; }

    Guid? TenantId { get; }
}

public interface IClientContext
{
    string? IpAddress { get; }

    string? UserAgent { get; }
}
