using LifeFitAI.Application.Gym.Dashboard;
using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym;

public interface IBusinessProfileRepository
{
    Task<BusinessProfile?> GetAsync(Guid tenantId, CancellationToken cancellationToken);

    Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken);
}

public interface IBranchRepository
{
    Task<Branch?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(Guid tenantId, string code, CancellationToken cancellationToken);

    Task AddAsync(Branch branch, CancellationToken cancellationToken);

    Task<PageResult<BranchResponse>> ListAsync(Guid tenantId, PageQuery page, CancellationToken cancellationToken);
}

public interface IPeopleRepository
{
    Task<StaffProfile?> GetStaffAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<StaffProfile?> GetStaffByUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task AddStaffAsync(StaffProfile profile, CancellationToken cancellationToken);

    Task<PageResult<StaffResponse>> ListStaffAsync(Guid tenantId, PageQuery page, CancellationToken cancellationToken);

    Task<TrainerProfile?> GetTrainerAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<TrainerProfile?> GetTrainerByUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task AddTrainerAsync(TrainerProfile profile, CancellationToken cancellationToken);

    Task<PageResult<TrainerResponse>> ListTrainersAsync(Guid tenantId, PageQuery page, CancellationToken cancellationToken);

    Task<MemberProfile?> GetMemberAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<MemberProfile?> GetMemberByUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task AddMemberAsync(MemberProfile profile, CancellationToken cancellationToken);

    Task<PageResult<MemberResponse>> SearchMembersAsync(
        Guid tenantId,
        string? term,
        ProfileStatus? status,
        Guid? branchId,
        GymMembershipStatus? membershipStatus,
        PageQuery page,
        CancellationToken cancellationToken);
}

public interface IMembershipRepository
{
    Task<MembershipPlan?> GetPlanAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<bool> PlanNameExistsAsync(Guid tenantId, string name, CancellationToken cancellationToken);

    Task AddPlanAsync(MembershipPlan plan, CancellationToken cancellationToken);

    Task<PageResult<PlanResponse>> ListPlansAsync(Guid tenantId, bool? activeOnly, PageQuery page, CancellationToken cancellationToken);

    Task<GymMembership?> GetMembershipAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task AddMembershipAsync(GymMembership membership, CancellationToken cancellationToken);

    Task AddHistoryAsync(MembershipHistoryEntry entry, CancellationToken cancellationToken);

    Task<PageResult<GymMembershipResponse>> ListMembershipsAsync(
        Guid tenantId,
        GymMembershipStatus? status,
        Guid? planId,
        Guid? branchId,
        DateOnly? expiringFrom,
        DateOnly? expiringTo,
        PageQuery page,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MembershipHistoryResponse>> ListHistoryAsync(Guid tenantId, Guid membershipId, CancellationToken cancellationToken);

    Task<int> CountVisitsAsync(Guid tenantId, Guid membershipId, CancellationToken cancellationToken);
}

public interface IAttendanceRepository
{
    Task<AttendanceRecord?> GetOpenAsync(Guid tenantId, Guid memberProfileId, CancellationToken cancellationToken);

    Task<AttendanceRecord?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task AddAsync(AttendanceRecord record, CancellationToken cancellationToken);

    Task<PageResult<AttendanceResponse>> ListAsync(
        Guid tenantId,
        Guid? branchId,
        Guid? memberProfileId,
        DateTime? from,
        DateTime? to,
        PageQuery page,
        CancellationToken cancellationToken);

    Task AddTokenAsync(QrCheckInToken token, CancellationToken cancellationToken);

    Task<QrCheckInToken?> GetTokenByHashAsync(string tokenHash, CancellationToken cancellationToken);
}

public interface IDashboardQuery
{
    Task<BusinessDashboard> GetAsync(Guid tenantId, Guid? branchId, DateTime utcNow, TimeZoneInfo timeZone, CancellationToken cancellationToken);

    Task<BusinessKpiCounts> CountAsync(Guid tenantId, DateTime utcNow, TimeZoneInfo timeZone, KpiScope scope, CancellationToken cancellationToken);
}

public interface IDashboardAnalytics
{
    Task<IReadOnlyList<DateCount>> CountMemberJoinsAsync(Guid tenantId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<IReadOnlyList<DateCount>> CountCheckInsByLocalDateAsync(Guid tenantId, DateTime fromUtc, DateTime toUtc, TimeZoneInfo timeZone, CancellationToken cancellationToken);

    Task<IReadOnlyList<MembershipActivityCount>> CountMembershipActivityAsync(Guid tenantId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

public interface IQrSettings
{
    TimeSpan Lifetime { get; }
}
