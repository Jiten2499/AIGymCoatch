using LifeFitAI.Domain.Fitness;
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

    public string? Designation { get; private set; }

    public string? Department { get; private set; }

    public EmploymentType? EmploymentType { get; private set; }

    public Guid? ReportingManagerUserId { get; private set; }

    public string? EmploymentNotes { get; private set; }

    public string? JobDescription { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    public string? Gender { get; private set; }

    public string? Address { get; private set; }

    public string? EmergencyContactName { get; private set; }

    public string? EmergencyContactPhone { get; private set; }

    public static StaffProfile Create(
        Guid id,
        Guid tenantId,
        Guid userId,
        string employeeCode,
        Guid? branchId,
        StaffType staffType,
        DateOnly joiningDate,
        ProfileStatus status = ProfileStatus.Active)
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
            Status = status,
        };
    }

    public void Update(Guid? branchId, StaffType staffType, ProfileStatus status)
    {
        BranchId = branchId;
        StaffType = staffType;
        Status = status;
    }

    public void SetPersonal(DateOnly? dateOfBirth, string? gender, string? address, string? emergencyContactName, string? emergencyContactPhone)
    {
        DateOfBirth = dateOfBirth;
        Gender = string.IsNullOrWhiteSpace(gender) ? null : gender.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        EmergencyContactName = string.IsNullOrWhiteSpace(emergencyContactName) ? null : emergencyContactName.Trim();
        EmergencyContactPhone = string.IsNullOrWhiteSpace(emergencyContactPhone) ? null : emergencyContactPhone.Trim();
    }

    public void SetEmployment(
        string designation,
        string? department,
        EmploymentType? employmentType,
        Guid? reportingManagerUserId,
        string? employmentNotes,
        string? jobDescription,
        DateOnly joiningDate,
        Guid? branchId)
    {
        Designation = designation.Trim();
        Department = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
        EmploymentType = employmentType;
        ReportingManagerUserId = reportingManagerUserId;
        EmploymentNotes = string.IsNullOrWhiteSpace(employmentNotes) ? null : employmentNotes.Trim();
        JobDescription = string.IsNullOrWhiteSpace(jobDescription) ? null : jobDescription.Trim();
        JoiningDate = joiningDate;
        BranchId = branchId;
    }

    public void SetStatus(ProfileStatus status) => Status = status;

    public void SetEmployeeCode(string employeeCode) => EmployeeCode = employeeCode.Trim().ToUpperInvariant();

    public void Remove(DateTime utcNow)
    {
        Status = ProfileStatus.Inactive;
        SoftDelete(utcNow);
    }
}

public sealed class StaffBranchAccess : BaseEntity
{
    private StaffBranchAccess()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid StaffProfileId { get; private set; }

    public Guid BranchId { get; private set; }

    public static StaffBranchAccess Create(Guid id, Guid tenantId, Guid staffProfileId, Guid branchId)
    {
        return new StaffBranchAccess
        {
            Id = id,
            TenantId = tenantId,
            StaffProfileId = staffProfileId,
            BranchId = branchId,
        };
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

    public string? EmployeeCode { get; private set; }

    public DateOnly? JoiningDate { get; private set; }

    public bool CommissionEligible { get; private set; }

    public string? PhotoStorageKey { get; private set; }

    public string? PhotoContentType { get; private set; }

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

    public void SetEmployment(string? employeeCode, DateOnly? joiningDate, bool commissionEligible)
    {
        EmployeeCode = string.IsNullOrWhiteSpace(employeeCode) ? null : employeeCode.Trim();
        JoiningDate = joiningDate;
        CommissionEligible = commissionEligible;
    }

    public void SetPhoto(string storageKey, string contentType)
    {
        PhotoStorageKey = storageKey;
        PhotoContentType = contentType;
    }

    public void ClearPhoto()
    {
        PhotoStorageKey = null;
        PhotoContentType = null;
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

    public Guid? UserId { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    public string MemberCode { get; private set; } = string.Empty;

    public DateOnly? DateOfBirth { get; private set; }

    public string? Gender { get; private set; }

    public string? EmergencyContactName { get; private set; }

    public string? EmergencyContactPhone { get; private set; }

    public string? Address { get; private set; }

    public string? City { get; private set; }

    public string? State { get; private set; }

    public string? PostalCode { get; private set; }

    public string? EmergencyContactRelationship { get; private set; }

    public FitnessGoalType? FitnessGoal { get; private set; }

    public Guid? AssignedTrainerId { get; private set; }

    public string? PhotoStorageKey { get; private set; }

    public string? PhotoContentType { get; private set; }

    public int? PhotoFileSize { get; private set; }

    public DateOnly JoinDate { get; private set; }

    public ProfileStatus Status { get; private set; }

    public static MemberProfile Create(Guid id, Guid tenantId, Guid? userId, string memberCode, DateOnly joinDate, string firstName = "", string lastName = "", string? phone = null)
    {
        var profile = new MemberProfile
        {
            Id = id,
            TenantId = tenantId,
            UserId = userId,
            MemberCode = memberCode,
            JoinDate = joinDate,
            Status = ProfileStatus.Active,
        };
        profile.SetContact(firstName, lastName, phone);
        return profile;
    }

    public void SetContact(string firstName, string lastName, string? phone)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
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
        ProfileStatus status,
        string? emergencyContactRelationship = null,
        FitnessGoalType? fitnessGoal = null,
        Guid? assignedTrainerId = null,
        DateOnly? joinDate = null)
    {
        DateOfBirth = dateOfBirth;
        Gender = gender;
        EmergencyContactName = emergencyContactName;
        EmergencyContactPhone = emergencyContactPhone;
        EmergencyContactRelationship = emergencyContactRelationship;
        Address = address;
        City = city;
        State = state;
        PostalCode = postalCode;
        FitnessGoal = fitnessGoal;
        AssignedTrainerId = assignedTrainerId;
        if (joinDate is DateOnly date)
        {
            JoinDate = date;
        }

        Status = status;
    }

    public void ChangeStatus(ProfileStatus status) => Status = status;

    /// <summary>
    /// Links an optional LifeFit User login. Gym membership and app login stay separate;
    /// a member may exist without UserId until app access is granted.
    /// </summary>
    public bool LinkLogin(Guid userId)
    {
        if (UserId is Guid existing)
        {
            return existing == userId;
        }

        UserId = userId;
        return true;
    }

    public void SetPhoto(string storageKey, string contentType, int fileSize)
    {
        PhotoStorageKey = storageKey;
        PhotoContentType = contentType;
        PhotoFileSize = fileSize;
    }

    public void ClearPhoto()
    {
        PhotoStorageKey = null;
        PhotoContentType = null;
        PhotoFileSize = null;
    }

    public void ClearAssignedTrainer() => AssignedTrainerId = null;

    public void Remove(DateTime utcNow) => Deactivate(utcNow);

    public void Deactivate(DateTime utcNow)
    {
        Status = ProfileStatus.Inactive;
        SoftDelete(utcNow);
    }
}
