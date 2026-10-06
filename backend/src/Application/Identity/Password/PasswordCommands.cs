using FluentValidation;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Identity.Password;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<Result>;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator(IPasswordPolicy passwords)
    {
        RuleFor(command => command.CurrentPassword).NotEmpty();
        RuleFor(command => command.NewPassword).Must(passwords.IsValid).WithMessage(_ => passwords.Description);
    }
}

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IUserAccountRepository _users;
    private readonly IPasswordHasher _passwords;
    private readonly IPasswordPolicy _policy;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public ChangePasswordCommandHandler(
        ICurrentActor actor,
        IUserAccountRepository users,
        IPasswordHasher passwords,
        IPasswordPolicy policy,
        IRefreshTokenRepository refreshTokens,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _actor = actor;
        _users = users;
        _passwords = passwords;
        _policy = policy;
        _refreshTokens = refreshTokens;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId)
        {
            return Result.Failure(IdentityErrors.InvalidCredentials);
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || !_passwords.Verify(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
        {
            return Result.Failure(IdentityErrors.CurrentPasswordRejected);
        }

        if (!_policy.IsValid(request.NewPassword))
        {
            return Result.Failure(IdentityErrors.PasswordRejected);
        }

        var now = _clock.UtcNow;
        var (hash, salt) = _passwords.HashPassword(request.NewPassword);
        user.ChangePassword(hash, salt);
        await _refreshTokens.RevokeActiveAsync(user.Id, now, _client.IpAddress, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(
            AuditLog.Create(Guid.NewGuid(), _actor.TenantId, user.Id, "ChangePassword", "User", user.Id, null, null, _client.IpAddress, _client.UserAgent, now),
            cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}

public sealed record ForgotPasswordCommand(string Email) : IRequest<Result>;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator() => RuleFor(command => command.Email).NotEmpty().EmailAddress();
}

public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result>
{
    private readonly IUserAccountRepository _users;
    private readonly IPasswordResetRepository _resets;
    private readonly ISecretTokenFactory _tokens;
    private readonly IPasswordResetNotifier _notifier;
    private readonly ITokenSettings _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public ForgotPasswordCommandHandler(
        IUserAccountRepository users,
        IPasswordResetRepository resets,
        ISecretTokenFactory tokens,
        IPasswordResetNotifier notifier,
        ITokenSettings settings,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _users = users;
        _resets = resets;
        _tokens = tokens;
        _notifier = notifier;
        _settings = settings;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken)
            .ConfigureAwait(false);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Success();
        }

        var raw = _tokens.Create();
        var now = _clock.UtcNow;
        await _resets.AddAsync(
            PasswordResetToken.Create(Guid.NewGuid(), user.Id, _tokens.Hash(raw), now, now.Add(_settings.ResetLifetime)),
            cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _notifier.NotifyAsync(user.Email, raw, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}

public sealed record ResetPasswordCommand(string Token, string NewPassword) : IRequest<Result>;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator(IPasswordPolicy passwords)
    {
        RuleFor(command => command.Token).NotEmpty();
        RuleFor(command => command.NewPassword).Must(passwords.IsValid).WithMessage(_ => passwords.Description);
    }
}

public sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result>
{
    private readonly IPasswordResetRepository _resets;
    private readonly IUserAccountRepository _users;
    private readonly ISecretTokenFactory _tokens;
    private readonly IPasswordHasher _passwords;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public ResetPasswordCommandHandler(
        IPasswordResetRepository resets,
        IUserAccountRepository users,
        ISecretTokenFactory tokens,
        IPasswordHasher passwords,
        IRefreshTokenRepository refreshTokens,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _resets = resets;
        _users = users;
        _tokens = tokens;
        _passwords = passwords;
        _refreshTokens = refreshTokens;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var reset = await _resets.GetActiveByHashAsync(_tokens.Hash(request.Token), cancellationToken).ConfigureAwait(false);
        var now = _clock.UtcNow;
        if (reset is null || !reset.IsActive(now))
        {
            return Result.Failure(IdentityErrors.ResetInvalid);
        }

        var user = await _users.GetByIdAsync(reset.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure(IdentityErrors.ResetInvalid);
        }

        var (hash, salt) = _passwords.HashPassword(request.NewPassword);
        user.ChangePassword(hash, salt);
        reset.MarkUsed(now);
        await _refreshTokens.RevokeActiveAsync(user.Id, now, _client.IpAddress, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(
            AuditLog.Create(Guid.NewGuid(), user.TenantId, user.Id, "ResetPassword", "User", user.Id, null, null, _client.IpAddress, _client.UserAgent, now),
            cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
