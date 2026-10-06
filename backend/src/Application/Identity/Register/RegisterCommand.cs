using FluentValidation;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Identity.Register;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone) : IRequest<Result<AuthResponse>>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator(IPasswordPolicy passwords)
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Phone).MaximumLength(32);
        RuleFor(command => command.Password)
            .Must(passwords.IsValid)
            .WithMessage(_ => passwords.Description);
    }
}

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    private readonly IUserAccountRepository _users;
    private readonly ITenantRepository _tenants;
    private readonly ITenantMembershipRepository _members;
    private readonly IRoleRepository _roles;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionIssuer _sessions;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public RegisterCommandHandler(
        IUserAccountRepository users,
        ITenantRepository tenants,
        ITenantMembershipRepository members,
        IRoleRepository roles,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ISessionIssuer sessions,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _users = users;
        _tenants = tenants;
        _members = members;
        _roles = roles;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _sessions = sessions;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

        if (await _users.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result.Failure<AuthResponse>(IdentityErrors.EmailConflict);
        }

        if (phone is not null && await _users.PhoneExistsAsync(phone, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<AuthResponse>(IdentityErrors.PhoneConflict);
        }

        // A self-registered user is a normal user of the single platform tenant.
        // They inherit the platform tenant's personal license; no per-user tenant or license is created.
        var tenant = await _tenants.GetAsync(PlatformConstants.PlatformTenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            return Result.Failure<AuthResponse>(IdentityErrors.TenantNotFound);
        }

        var role = await _roles.GetByNameAsync(RoleNames.PersonalMember, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return Result.Failure<AuthResponse>(IdentityErrors.RoleNotFound);
        }

        AuthResponse? response = null;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = _clock.UtcNow;
            var userId = Guid.NewGuid();
            var (hash, salt) = _passwordHasher.HashPassword(request.Password);
            var user = UserAccount.Create(userId, PlatformConstants.PlatformTenantId, email, phone, hash, salt, request.FirstName.Trim(), request.LastName.Trim());
            await _users.AddAsync(user, ct).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

            await _members.AddAsync(TenantMember.Create(Guid.NewGuid(), PlatformConstants.PlatformTenantId, userId, now), ct).ConfigureAwait(false);
            await _roles.AddAsync(UserRole.Create(Guid.NewGuid(), userId, role.Id, PlatformConstants.PlatformTenantId), ct).ConfigureAwait(false);

            await _audit.AddAsync(
                AuditLog.Create(Guid.NewGuid(), PlatformConstants.PlatformTenantId, userId, "Register", "User", userId, null, null, _client.IpAddress, _client.UserAgent, now),
                ct).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            response = await _sessions.IssueAsync(user, tenant, null, ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return Result.Success(response!);
    }
}
