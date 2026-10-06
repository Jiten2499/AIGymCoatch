using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym;

public interface IAccountLinker
{
    Task<Result<UserAccount>> ResolveAsync(
        string email,
        string? password,
        string firstName,
        string lastName,
        string? phone,
        CancellationToken cancellationToken);
}

public sealed class AccountLinker : IAccountLinker
{
    private readonly IUserAccountRepository _users;
    private readonly ITenantMembershipRepository _members;
    private readonly IRoleRepository _roles;
    private readonly IPasswordHasher _passwords;
    private readonly IPasswordPolicy _policy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public AccountLinker(
        IUserAccountRepository users,
        ITenantMembershipRepository members,
        IRoleRepository roles,
        IPasswordHasher passwords,
        IPasswordPolicy policy,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _users = users;
        _members = members;
        _roles = roles;
        _passwords = passwords;
        _policy = policy;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<UserAccount>> ResolveAsync(
        string email,
        string? password,
        string firstName,
        string lastName,
        string? phone,
        CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var existing = await _users.GetByEmailAsync(normalized, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Result.Success(existing);
        }

        if (string.IsNullOrWhiteSpace(password) || !_policy.IsValid(password))
        {
            return Result.Failure<UserAccount>(GymErrors.PasswordRequired);
        }

        if (phone is not null && await _users.PhoneExistsAsync(phone, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<UserAccount>(IdentityErrors.PhoneConflict);
        }

        var role = await _roles.GetByNameAsync(RoleNames.PersonalMember, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return Result.Failure<UserAccount>(IdentityErrors.RoleNotFound);
        }

        // A newly onboarded person is a normal user of the platform tenant. The business
        // membership (TenantMember, role, profile) is added by the gym handler on top of this identity.
        var now = _clock.UtcNow;
        var (hash, salt) = _passwords.HashPassword(password);
        var user = UserAccount.Create(Guid.NewGuid(), PlatformConstants.PlatformTenantId, normalized, phone, hash, salt, firstName.Trim(), lastName.Trim());
        await _users.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _members.AddAsync(TenantMember.Create(Guid.NewGuid(), PlatformConstants.PlatformTenantId, user.Id, now), cancellationToken).ConfigureAwait(false);
        await _roles.AddAsync(UserRole.Create(Guid.NewGuid(), user.Id, role.Id, PlatformConstants.PlatformTenantId), cancellationToken).ConfigureAwait(false);
        return Result.Success(user);
    }
}
