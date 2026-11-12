using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym;

public sealed record PageQuery(int Page = 1, int PageSize = 20, string? Sort = null)
{
    public int NormalizedPage => Page < 1 ? 1 : Page;

    public int NormalizedSize => PageSize is < 1 or > 100 ? 20 : PageSize;

    public int Skip => (NormalizedPage - 1) * NormalizedSize;
}

public sealed record PageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record BusinessProfileResponse(
    Guid Id,
    Guid TenantId,
    string BusinessName,
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

public sealed record BranchResponse(
    Guid Id,
    string Name,
    string Code,
    string? Address,
    string? City,
    string? State,
    string? Country,
    string? PostalCode,
    string? Phone,
    string? Email,
    string TimeZone,
    BranchStatus Status);

public sealed record MemberResponse(
    Guid Id,
    Guid? UserId,
    string MemberCode,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    DateOnly? DateOfBirth,
    string? Gender,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    DateOnly JoinDate,
    ProfileStatus Status,
    string? EmergencyContactRelationship,
    LifeFitAI.Domain.Fitness.FitnessGoalType? FitnessGoal,
    Guid? AssignedTrainerId,
    string? AssignedTrainerName,
    bool HasPhoto,
    string? TemporaryPassword = null);

public sealed record MemberMatchItem(
    Guid Id,
    string FullName,
    string? Phone,
    string? Email,
    string MemberCode,
    ProfileStatus Status,
    DateOnly JoinDate);

public sealed record MemberListItem(
    Guid Id,
    Guid? UserId,
    string FullName,
    string? Mobile,
    bool HasPhoto,
    ProfileStatus Status,
    DateOnly JoinDate,
    string? Email);

public sealed record StaffResponse(
    Guid Id,
    Guid UserId,
    string EmployeeCode,
    string Email,
    string FirstName,
    string LastName,
    Guid? BranchId,
    StaffType StaffType,
    DateOnly JoiningDate,
    ProfileStatus Status);

public sealed record TrainerResponse(
    Guid Id,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string? Bio,
    string? Specialization,
    int? ExperienceYears,
    string? Certification,
    string? ProfileImageUrl,
    Guid? BranchId,
    ProfileStatus Status,
    int AssignedMembers = 0,
    string? EmployeeCode = null,
    string? Phone = null,
    DateOnly? JoiningDate = null,
    bool CommissionEligible = false,
    bool HasPhoto = false,
    Guid? StaffProfileId = null,
    string? Designation = null,
    DateOnly? DateOfBirth = null,
    string? Gender = null,
    string? Address = null,
    string? EmergencyContactName = null,
    string? EmergencyContactPhone = null,
    EmploymentType? EmploymentType = null,
    Guid? ReportingManagerUserId = null,
    string? EmploymentNotes = null,
    Guid? SubscriptionId = null,
    string? PlanName = null);

public sealed record PlanResponse(
    Guid Id,
    string Name,
    string? Description,
    int DurationDays,
    MembershipType MembershipType,
    int? MaxVisits,
    decimal Price,
    string Currency,
    bool IsActive,
    PackageCategory Category = PackageCategory.Standard,
    PackageDurationUnit DurationUnit = PackageDurationUnit.Day,
    int DurationCount = 0,
    int IncludedPtSessions = 0);

public sealed record GymMembershipResponse(
    Guid Id,
    Guid MemberProfileId,
    Guid? BranchId,
    Guid MembershipPlanId,
    string MembershipNumber,
    DateOnly StartDate,
    DateOnly EndDate,
    GymMembershipStatus Status,
    bool AutoRenew,
    string? Notes,
    string? MemberName = null,
    string? PlanName = null,
    decimal PackagePrice = 0,
    decimal DiscountAmount = 0,
    decimal FinalAmount = 0,
    string Currency = "USD",
    Guid? DiscountRuleId = null,
    string? DiscountName = null,
    decimal TotalPaid = 0,
    decimal Outstanding = 0,
    MembershipSettlementStatus SettlementStatus = MembershipSettlementStatus.Unpaid,
    Guid? ApprovalRequestId = null,
    int IncludedPtSessions = 0,
    int PtSessionsRemaining = 0);

public sealed record MembershipHistoryResponse(
    Guid Id,
    MembershipAction Action,
    GymMembershipStatus? PreviousStatus,
    GymMembershipStatus NewStatus,
    DateOnly EffectiveDate,
    string? Reason,
    Guid? PerformedByUserId,
    DateTime CreatedAt);

public sealed record AttendanceResponse(
    Guid Id,
    Guid BranchId,
    Guid MemberProfileId,
    Guid GymMembershipId,
    DateTime CheckInTime,
    DateTime? CheckOutTime,
    CheckInMethod CheckInMethod);

public sealed record QrTokenResponse(string Token, DateTime ExpiresAt);

public sealed record BusinessKpiCounts(
    int TotalMembers,
    int ActiveMembers,
    int NewMembersThisMonth,
    int CheckInsToday,
    int ActiveMemberships,
    int ExpiringIn7Days,
    int ExpiringIn30Days,
    int ExpiredMemberships,
    int ActiveTrainers,
    int ActiveStaff);

public sealed record BusinessDashboard(
    int ActiveMembers,
    int NewMembersThisMonth,
    int ActiveMemberships,
    int ExpiredMemberships,
    int ExpiringSoon,
    int TodayCheckIns,
    int MonthCheckIns,
    int ActiveTrainers,
    int ActiveBranches);
