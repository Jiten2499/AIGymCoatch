using FluentValidation;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Identity.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<AuthResponse>>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress();
        RuleFor(command => command.Password).NotEmpty();
    }
}

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly IUserAccountRepository _users;
    private readonly ITenantRepository _tenants;
    private readonly IPasswordHasher _passwords;
    private readonly ISessionIssuer _sessions;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public LoginCommandHandler(
        IUserAccountRepository users,
        ITenantRepository tenants,
        IPasswordHasher passwords,
        ISessionIssuer sessions,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _users = users;
        _tenants = tenants;
        _passwords = passwords;
        _sessions = sessions;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null || !_passwords.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return Result.Failure<AuthResponse>(IdentityErrors.InvalidCredentials);
        }

        if (user.Status != UserStatus.Active)
        {
            return Result.Failure<AuthResponse>(IdentityErrors.UserDisabled);
        }

        var tenant = await _tenants.GetAsync(user.TenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null || tenant.Status != TenantStatus.Active)
        {
            return Result.Failure<AuthResponse>(IdentityErrors.TenantUnavailable);
        }

        var now = _clock.UtcNow;
        user.RecordLogin(now);
        await _audit.AddAsync(
            AuditLog.Create(Guid.NewGuid(), tenant.Id, user.Id, "Login", "User", user.Id, null, null, _client.IpAddress, _client.UserAgent, now),
            cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(await _sessions.IssueAsync(user, tenant, null, cancellationToken).ConfigureAwait(false));
    }
}
