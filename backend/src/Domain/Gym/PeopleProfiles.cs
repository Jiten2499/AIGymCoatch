using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public sealed class StaffProfile : BaseEntity
{
    private StaffProfile()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public string EmployeeCode { get; private set; } = string.Empty;

    public Guid? BranchId { get; private set; }

    public StaffType StaffType { get; private set; }

    public DateOnly JoiningDate { get; private set; }

    public ProfileStatus Status { get; private set; }

    public static StaffProfile Create(
        Guid id,
        Guid tenantId,
        Guid userId,
        string employeeCode,
        Guid? branchId,
        StaffType staffType,
        DateOnly joiningDate)
    {
        return new StaffProfile
        {
            Id = id,
            TenantId = tenantId,
            UserId = userId,
            EmployeeCode = employeeCode,
            BranchId = branchId,
            StaffType = staffType,
            JoiningDate = joiningDate,
            Status = ProfileStatus.Active,
        };
    }

    public void Update(Guid? branchId, StaffType staffType, ProfileStatus status)
    {
        BranchId = branchId;
        StaffType = staffType;
        Status = status;
    }

    public void Remove(DateTime utcNow)
    {
        Status = ProfileStatus.Inactive;
        SoftDelete(utcNow);
    }
}

public sealed class TrainerProfile : BaseEntity
{
    private TrainerProfile()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public string? Bio { get; private set; }

    public string? Specialization { get; private set; }

    public int? ExperienceYears { get; private set; }

    public string? Certification { get; private set; }

    public string? ProfileImageUrl { get; private set; }

    public Guid? BranchId { get; private set; }

    public ProfileStatus Status { get; private set; }

    public static TrainerProfile Create(Guid id, Guid tenantId, Guid userId, Guid? branchId)
    {
        return new TrainerProfile
        {
            Id = id,
            TenantId = tenantId,
            UserId = userId,
            BranchId = branchId,
            Status = ProfileStatus.Active,
        };
    }

    public void Update(
        string? bio,
        string? specialization,
        int? experienceYears,
        string? certification,
        string? profileImageUrl,
        Guid? branchId,
        ProfileStatus status)
    {
        Bio = bio;
        Specialization = specialization;
        ExperienceYears = experienceYears;
        Certification = certification;
        ProfileImageUrl = profileImageUrl;
        BranchId = branchId;
        Status = status;
    }

    public void Remove(DateTime utcNow)
    {
        Status = ProfileStatus.Inactive;
        SoftDelete(utcNow);
    }
}

public sealed class MemberProfile : BaseEntity
{
    private MemberProfile()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public string MemberCode { get; private set; } = string.Empty;

    public DateOnly? DateOfBirth { get; private set; }

    public string? Gender { get; private set; }

    public string? EmergencyContactName { get; private set; }

    public string? EmergencyContactPhone { get; private set; }

    public string? Address { get; private set; }

    public string? City { get; private set; }

    public string? State { get; private set; }

    public string? PostalCode { get; private set; }

    public DateOnly JoinDate { get; private set; }

    public ProfileStatus Status { get; private set; }

    public static MemberProfile Create(Guid id, Guid tenantId, Guid userId, string memberCode, DateOnly joinDate)
    {
        return new MemberProfile
        {
            Id = id,
            TenantId = tenantId,
            UserId = userId,
            MemberCode = memberCode,
            JoinDate = joinDate,
            Status = ProfileStatus.Active,
        };
    }

    public void Update(
        DateOnly? dateOfBirth,
        string? gender,
        string? emergencyContactName,
        string? emergencyContactPhone,
        string? address,
        string? city,
        string? state,
        string? postalCode,
        ProfileStatus status)
    {
        DateOfBirth = dateOfBirth;
        Gender = gender;
        EmergencyContactName = emergencyContactName;
        EmergencyContactPhone = emergencyContactPhone;
        Address = address;
        City = city;
        State = state;
        PostalCode = postalCode;
        Status = status;
    }

    public void Deactivate(DateTime utcNow)
    {
        Status = ProfileStatus.Inactive;
        SoftDelete(utcNow);
    }
}
