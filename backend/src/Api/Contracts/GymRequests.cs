namespace LifeFitAI.Api.Contracts;

public sealed record UpdateBusinessProfileRequest(
    string? LegalName,
    string? Description,
    string? Email,
    string? Phone,
    string? Website,
    string? Address,
    string? City,
    string? State,
    string? Country,
    string? PostalCode,
    string? LogoUrl,
    string TimeZone,
    string Currency);

public sealed record CreateBranchRequest(
    string Name,
    string Code,
    string? Address,
    string? City,
    string? State,
    string? Country,
    string? PostalCode,
    string? Phone,
    string? Email,
    string TimeZone);

public sealed record UpdateBranchRequest(
    string Name,
    string? Address,
    string? City,
    string? State,
    string? Country,
    string? PostalCode,
    string? Phone,
    string? Email,
    string TimeZone);

public sealed record CreateMemberRequest(
    string Email,
    string? Password,
    string FirstName,
    string LastName,
    string? Phone,
    Guid? BranchId,
    Guid? PlanId,
    DateOnly? StartDate,
    bool AutoRenew,
    DateOnly? DateOfBirth,
    string? Gender,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelationship,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    DateOnly? JoinDate,
    string? FitnessGoal,
    Guid? AssignedTrainerId,
    bool AssignAppAccess = false,
    Guid? AppSubscriptionId = null,
    bool EnableBasicAppAccess = false,
    decimal? PaymentAmount = null,
    LifeFitAI.Domain.Gym.MembershipPaymentMethod? PaymentMethod = null,
    DateOnly? PaymentDate = null,
    string? PaymentReferenceNumber = null,
    string? PaymentNotes = null,
    Guid? PtPackageId = null,
    bool SendActivationLink = false);

public sealed record AssignMemberAppAccessRequest(Guid SubscriptionId, string? Email = null);

public sealed record EnableMemberBasicAccessRequest(string Email);

public sealed record UpdateMemberRequest(
    string? FirstName,
    string? LastName,
    string? Phone,
    DateOnly? DateOfBirth,
    string? Gender,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelationship,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    DateOnly? JoinDate,
    string? FitnessGoal,
    Guid? AssignedTrainerId,
    LifeFitAI.Domain.Gym.ProfileStatus Status);

public sealed record ChangeMemberStatusRequest(LifeFitAI.Domain.Gym.ProfileStatus Status);

public sealed record AddStaffRequest(
    string Email,
    string? Password,
    string FirstName,
    string LastName,
    string? Phone,
    LifeFitAI.Domain.Gym.StaffType StaffType,
    Guid? BranchId);

public sealed record UpdateStaffRequest(Guid? BranchId, LifeFitAI.Domain.Gym.StaffType StaffType, LifeFitAI.Domain.Gym.ProfileStatus Status);

public sealed record AddCoachRequest(
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string? Specialization,
    Guid? BranchId,
    bool SendActivationLink = false);

public sealed record UpdateTrainerRequest(
    string? Bio,
    string? Specialization,
    int? ExperienceYears,
    string? Certification,
    string? ProfileImageUrl,
    Guid? BranchId,
    LifeFitAI.Domain.Gym.ProfileStatus Status,
    string? FirstName = null,
    string? LastName = null,
    string? Phone = null,
    bool SetPhone = false,
    string? EmployeeCode = null,
    DateOnly? JoiningDate = null,
    bool CommissionEligible = false,
    bool SetEmployment = false);

public sealed record PlanRequest(
    string Name,
    string? Description,
    int DurationDays,
    LifeFitAI.Domain.Gym.MembershipType MembershipType,
    int? MaxVisits,
    decimal Price,
    string Currency,
    LifeFitAI.Domain.Gym.PackageCategory Category = LifeFitAI.Domain.Gym.PackageCategory.Standard,
    LifeFitAI.Domain.Gym.PackageDurationUnit DurationUnit = LifeFitAI.Domain.Gym.PackageDurationUnit.Day,
    int? DurationCount = null,
    int IncludedPtSessions = 0);

public sealed record CreateMembershipRequest(
    Guid MemberProfileId,
    Guid? BranchId,
    Guid PlanId,
    DateOnly StartDate,
    bool AutoRenew,
    string? Notes,
    bool AssignAppAccess = false,
    Guid? AppSubscriptionId = null,
    Guid? DiscountRuleId = null,
    decimal? PaymentAmount = null,
    LifeFitAI.Domain.Gym.MembershipPaymentMethod? PaymentMethod = null,
    DateOnly? PaymentDate = null,
    string? PaymentReferenceNumber = null,
    string? PaymentNotes = null);

public sealed record ChangeMembershipRequest(string? Reason, bool AutoRenew, int Days = 0);

public sealed record RenewMembershipRequest(
    string? Reason,
    bool AutoRenew,
    int Days = 0,
    Guid? PlanId = null,
    DateOnly? StartDate = null,
    Guid? TrainerId = null,
    bool ClearTrainer = false,
    Guid? PtPackageId = null,
    Guid? DiscountRuleId = null,
    decimal? PaymentAmount = null,
    LifeFitAI.Domain.Gym.MembershipPaymentMethod? PaymentMethod = null,
    DateOnly? PaymentDate = null,
    string? PaymentReferenceNumber = null,
    string? PaymentNotes = null);

public sealed record CheckInRequest(Guid? MemberProfileId, Guid BranchId, LifeFitAI.Domain.Gym.CheckInMethod Method, string? QrToken);

public sealed record CheckOutRequest(Guid AttendanceId);

public sealed record IssueQrTokenRequest(Guid MemberProfileId);
