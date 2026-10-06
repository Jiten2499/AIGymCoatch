using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Identity;

public sealed class UserAccount : BaseEntity
{
    private UserAccount()
    {
    }

    public Guid TenantId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    public string PasswordHash { get; private set; } = string.Empty;

    public string PasswordSalt { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public UserStatus Status { get; private set; }

    public DateTime? LastLoginAt { get; private set; }

    public static UserAccount Create(
        Guid id,
        Guid personalTenantId,
        string email,
        string? phone,
        string passwordHash,
        string passwordSalt,
        string firstName,
        string lastName)
    {
        return new UserAccount
        {
            Id = id,
            TenantId = personalTenantId,
            Email = email,
            Phone = phone,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            FirstName = firstName,
            LastName = lastName,
            Status = UserStatus.Active,
        };
    }

    public void RecordLogin(DateTime utcNow) => LastLoginAt = utcNow;

    public void Rename(string firstName, string lastName)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
    }

    public void Activate() => Status = UserStatus.Active;

    public void ChangePassword(string passwordHash, string passwordSalt)
    {
        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
    }

    public void Disable() => Status = UserStatus.Disabled;
}
