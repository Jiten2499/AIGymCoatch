using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LifeFitAI.Application.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LifeFitAI.Infrastructure.Security;

public sealed class JwtAccessTokenFactory : IAccessTokenFactory
{
    private readonly JwtOptions _options;
    private readonly IDateTimeProvider _clock;

    public JwtAccessTokenFactory(IOptions<JwtOptions> options, IDateTimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public (string Token, DateTime ExpiresAt) Create(Guid userId, Guid tenantId)
    {
        var expiresAt = _clock.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            [
                new Claim("sub", userId.ToString()),
                new Claim("tenant_id", tenantId.ToString()),
                new Claim("jti", Guid.NewGuid().ToString()),
            ],
            notBefore: _clock.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

public sealed class TokenSettings : ITokenSettings
{
    private readonly JwtOptions _options;

    public TokenSettings(IOptions<JwtOptions> options) => _options = options.Value;

    public TimeSpan RefreshLifetime => TimeSpan.FromDays(Math.Max(1, _options.RefreshTokenDays));

    public TimeSpan ResetLifetime => TimeSpan.FromMinutes(Math.Max(5, _options.PasswordResetMinutes));
}

public sealed class NoopPasswordResetNotifier : IPasswordResetNotifier
{
    public Task NotifyAsync(string email, string rawToken, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

public sealed class AnonymousActor : ICurrentActor
{
    public Guid? UserId => null;

    public Guid? TenantId => null;
}

public sealed class AnonymousClient : IClientContext
{
    public string? IpAddress => null;

    public string? UserAgent => null;
}
