using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;

namespace LifeFitAI.Application.Identity;

public interface ISessionIssuer
{
    Task<AuthResponse> IssueAsync(
        UserAccount user,
        Tenant tenant,
        Guid? refreshTokenId,
        CancellationToken cancellationToken);
}

public sealed class SessionIssuer : ISessionIssuer
{
    private readonly IAccessTokenFactory _accessTokens;
    private readonly ISecretTokenFactory _secretTokens;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenSettings _tokenSettings;

    public SessionIssuer(
        IAccessTokenFactory accessTokens,
        ISecretTokenFactory secretTokens,
        IRefreshTokenRepository refreshTokens,
        IDateTimeProvider clock,
        IClientContext client,
        IUnitOfWork unitOfWork,
        ITokenSettings tokenSettings)
    {
        _accessTokens = accessTokens;
        _secretTokens = secretTokens;
        _refreshTokens = refreshTokens;
        _clock = clock;
        _client = client;
        _unitOfWork = unitOfWork;
        _tokenSettings = tokenSettings;
    }

    public async Task<AuthResponse> IssueAsync(
        UserAccount user,
        Tenant tenant,
        Guid? refreshTokenId,
        CancellationToken cancellationToken)
    {
        var rawRefresh = _secretTokens.Create();
        var now = _clock.UtcNow;
        var refresh = RefreshToken.Create(
            refreshTokenId ?? Guid.NewGuid(),
            user.Id,
            _secretTokens.Hash(rawRefresh),
            now,
            now.Add(_tokenSettings.RefreshLifetime),
            _client.IpAddress,
            _client.UserAgent);

        await _refreshTokens.AddAsync(refresh, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var (access, expiresAt) = _accessTokens.Create(user.Id, tenant.Id);
        return new AuthResponse(
            access,
            rawRefresh,
            expiresAt,
            UserProfiles.From(user),
            new TenantSummary(tenant.Id, tenant.Name, tenant.TenantType, tenant.Status));
    }
}
