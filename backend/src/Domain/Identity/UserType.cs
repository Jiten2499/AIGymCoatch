using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Identity;

public static class UserTypeCodes
{
    public const string NormalUser = "NORMAL_USER";
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string BusinessOwner = "BUSINESS_OWNER";
    public const string BusinessUser = "BUSINESS_USER";
    public const string Coach = "COACH";
    public const string Receptionist = "RECEPTIONIST";
    public const string Manager = "MANAGER";
    public const string GroupInstructor = "GROUP_INSTRUCTOR";
    public const string Nutritionist = "NUTRITIONIST";
    public const string Physiotherapist = "PHYSIOTHERAPIST";
    public const string MembershipAdvisor = "MEMBERSHIP_ADVISOR";
    public const string Housekeeping = "HOUSEKEEPING";
    public const string Maintenance = "MAINTENANCE";
}

public static class UserTypeCategories
{
    public const string Platform = "Platform";
    public const string Personal = "Personal";
    public const string Business = "Business";
    public const string Staff = "Staff";
}

public sealed class UserType : BaseEntity
{
    private UserType()
    {
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string Category { get; private set; } = string.Empty;

    public Guid? RoleId { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public static UserType Create(
        Guid id,
        string code,
        string name,
        string? description,
        string category,
        Guid? roleId,
        int sortOrder,
        bool isActive)
    {
        return new UserType
        {
            Id = id,
            Code = code,
            Name = name,
            Description = description,
            Category = category,
            RoleId = roleId,
            SortOrder = sortOrder,
            IsActive = isActive,
        };
    }
}
