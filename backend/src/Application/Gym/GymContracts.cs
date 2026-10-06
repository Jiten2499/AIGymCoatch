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
    Guid UserId,
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
    ProfileStatus Status);

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
    ProfileStatus Status);

public sealed record PlanResponse(
    Guid Id,
    string Name,
    string? Description,
    int DurationDays,
    MembershipType MembershipType,
    int? MaxVisits,
    decimal Price,
    string Currency,
    bool IsActive);

public sealed record GymMembershipResponse(
    Guid Id,
    Guid MemberProfileId,
    Guid BranchId,
    Guid MembershipPlanId,
    string MembershipNumber,
    DateOnly StartDate,
    DateOnly EndDate,
    GymMembershipStatus Status,
    bool AutoRenew,
    string? Notes);

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
