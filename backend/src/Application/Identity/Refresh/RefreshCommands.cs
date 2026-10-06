using FluentValidation;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Identity.Refresh;

public sealed record RefreshCommand(string RefreshToken) : IRequest<Result<AuthResponse>>;

public sealed class RefreshCommandValidator : AbstractValidator<RefreshCommand>
{
    public RefreshCommandValidator() => RuleFor(command => command.RefreshToken).NotEmpty();
}

public sealed class RefreshCommandHandler : IRequestHandler<RefreshCommand, Result<AuthResponse>>
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ISecretTokenFactory _secretTokens;
    private readonly IUserAccountRepository _users;
    private readonly ITenantRepository _tenants;
    private readonly ITenantMembershipRepository _members;
    private readonly ISessionIssuer _sessions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;
    private readonly ICurrentActor _actor;

    public RefreshCommandHandler(
        IRefreshTokenRepository refreshTokens,
        ISecretTokenFactory secretTokens,
        IUserAccountRepository users,
        ITenantRepository tenants,
        ITenantMembershipRepository members,
        ISessionIssuer sessions,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client,
        ICurrentActor actor)
    {
        _refreshTokens = refreshTokens;
        _secretTokens = secretTokens;
        _users = users;
        _tenants = tenants;
        _members = members;
        _sessions = sessions;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
        _actor = actor;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        var existing = await _refreshTokens.GetByHashAsync(_secretTokens.Hash(request.RefreshToken), cancellationToken)
            .ConfigureAwait(false);
        var now = _clock.UtcNow;
        if (existing is null)
        {
            return Result.Failure<AuthResponse>(IdentityErrors.RefreshInvalid);
        }

        if (existing.RevokedAt is not null)
        {
            await _refreshTokens.RevokeActiveAsync(existing.UserId, now, _client.IpAddress, cancellationToken)
                .ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<AuthResponse>(IdentityErrors.RefreshInvalid);
        }

        if (!existing.IsActive(now))
        {
            return Result.Failure<AuthResponse>(IdentityErrors.RefreshInvalid);
        }

        var user = await _users.GetByIdAsync(existing.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<AuthResponse>(IdentityErrors.RefreshInvalid);
        }

        var tenantId = _actor.TenantId ?? user.TenantId;
        if (await _members.GetActiveAsync(tenantId, user.Id, cancellationToken).ConfigureAwait(false) is null)
        {
            tenantId = user.TenantId;
        }

        var tenant = await _tenants.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null || tenant.Status != TenantStatus.Active)
        {
            return Result.Failure<AuthResponse>(IdentityErrors.TenantUnavailable);
        }

        var replacementId = Guid.NewGuid();
        existing.Revoke(now, _client.IpAddress, replacementId);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await _sessions.IssueAsync(user, tenant, replacementId, cancellationToken).ConfigureAwait(false));
    }
}

public sealed record LogoutCommand(string RefreshToken) : IRequest<Result>;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator() => RuleFor(command => command.RefreshToken).NotEmpty();
}

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ISecretTokenFactory _secretTokens;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public LogoutCommandHandler(
        IRefreshTokenRepository refreshTokens,
        ISecretTokenFactory secretTokens,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _refreshTokens = refreshTokens;
        _secretTokens = secretTokens;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var existing = await _refreshTokens.GetByHashAsync(_secretTokens.Hash(request.RefreshToken), cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Success();
        }

        var now = _clock.UtcNow;
        existing.Revoke(now, _client.IpAddress, null);
        await _audit.AddAsync(
            AuditLog.Create(Guid.NewGuid(), null, existing.UserId, "Logout", "User", existing.UserId, null, null, _client.IpAddress, _client.UserAgent, now),
            cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
