using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed record TrainerListItem(Guid Id, Guid UserId, string Name, string Status, string? Specialization, int AssignedMembers, int ActivePtClients, DateOnly? JoiningDate, string? EmployeeCode, bool CommissionEligible);

public sealed record TrainerCandidate(Guid UserId, string Name, string Email);

public sealed record TrainerAssignmentItem(Guid Id, Guid MemberId, string MemberName, DateOnly StartDate, DateOnly? EndDate, string Status);

public sealed record TrainerIncentiveItem(string TargetType, decimal? Percent, string Status, decimal? Amount);

public sealed record TrainerTargetItem(Guid Id, string TargetType, string Period, DateOnly PeriodStart, DateOnly PeriodEnd, decimal TargetValue, decimal? Actual, decimal? AchievementPercent, string? Name, bool IsActive);

public sealed record TrainerPerformanceRow(Guid TrainerId, string Name, decimal? PtRevenue, int? PtSessions, int NewPtClients, decimal? TargetAchievementPercent);

public sealed record TrainerDetails(
    Guid Id,
    string Name,
    string Status,
    string? Specialization,
    string? Bio,
    string? EmployeeCode,
    DateOnly? JoiningDate,
    bool CommissionEligible,
    int AssignedMembers,
    int ActivePtClients,
    IReadOnlyList<TrainerAssignmentItem> Members,
    IReadOnlyList<TrainerTargetItem> Targets,
    IReadOnlyList<TrainerIncentiveItem> Incentives);

public sealed record CoachAssignmentRow(Guid Id, Guid MemberId, string MemberName, string? Phone, DateOnly StartDate, DateOnly? EndDate, string Status, string? MembershipStatus, DateOnly? MembershipEnd);

public sealed record CoachRuleRow(Guid Id, string Name, string Kind, string Detail, DateOnly Start, DateOnly? End, bool Active, bool RequiresApproval);

public sealed record CoachCalculationRow(Guid Id, string Kind, Guid RuleId, string RuleName, DateOnly PeriodStart, DateOnly PeriodEnd, decimal Amount, string Status, string Basis);

public sealed record CoachHistoryRow(
    string Id,
    DateTime At,
    string Category,
    string Action,
    string Activity,
    string Details,
    string Subject,
    string? PerformedBy,
    string? OldValues,
    string? NewValues);

public sealed record CoachWorkspace(
    Guid Id,
    string Name,
    string Email,
    string Status,
    string? Specialization,
    DateOnly? JoiningDate,
    string? EmployeeCode,
    int AssignedMembers,
    int ActivePtClients,
    int NewPtClients,
    decimal? AchievementPercent,
    decimal EstimatedCommission,
    decimal EstimatedIncentive,
    decimal PendingApproval,
    decimal ApprovedAmount,
    IReadOnlyList<CoachAssignmentRow> Assignments,
    IReadOnlyList<TrainerTargetItem> Targets,
    IReadOnlyList<CoachRuleRow> CommissionRules,
    IReadOnlyList<CoachRuleRow> IncentiveRules,
    IReadOnlyList<CoachCalculationRow> Calculations,
    IReadOnlyList<CoachHistoryRow> History,
    int PtSessionsCompleted = 0,
    int PtPackagesSold = 0);

public interface ITrainerWorkspace
{
    Task<IReadOnlyList<TrainerListItem>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainerCandidate>> CandidatesAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<TrainerProfile?> CreateFromUserAsync(Guid tenantId, Guid userId, string? employeeCode, string? specialization, string? bio, DateOnly? joiningDate, bool commissionEligible, CancellationToken cancellationToken);

    Task<bool> UpdateEmploymentAsync(Guid tenantId, Guid trainerId, string? employeeCode, string? specialization, string? bio, DateOnly? joiningDate, bool commissionEligible, ProfileStatus status, CancellationToken cancellationToken);

    Task<TrainerDetails?> DetailsAsync(Guid tenantId, Guid trainerId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<TrainerMemberAssignment?> AssignAsync(Guid tenantId, Guid trainerId, Guid memberId, DateOnly start, Guid assignedBy, CancellationToken cancellationToken);

    Task<bool> EndAssignmentAsync(Guid tenantId, Guid assignmentId, DateOnly end, CancellationToken cancellationToken);

    Task<TrainerMemberAssignment?> ChangeTrainerAsync(Guid tenantId, Guid assignmentId, Guid trainerId, DateOnly start, Guid assignedBy, CancellationToken cancellationToken);

    Task SyncMemberTrainerAsync(Guid tenantId, Guid memberId, Guid? trainerId, DateOnly start, Guid assignedBy, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainerTargetItem>> TargetsAsync(Guid tenantId, Guid trainerId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<TrainerTarget?> AddTargetAsync(Guid tenantId, Guid trainerId, TrainerTargetType type, TrainerTargetPeriod period, DateOnly start, DateOnly end, decimal value, CancellationToken cancellationToken, string? name = null);

    Task SaveIncentiveRuleAsync(Guid tenantId, TrainerTargetType type, decimal percent, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainerPerformanceRow>> PerformanceAsync(Guid tenantId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<bool> SetTargetActiveAsync(Guid tenantId, Guid targetId, bool active, CancellationToken cancellationToken);

    Task<TrainerCommissionRule?> AddCommissionRuleAsync(Guid tenantId, Guid trainerId, string name, CommissionRuleKind kind, decimal? percent, decimal? fixedAmount, DateOnly start, DateOnly? end, bool requiresApproval, CancellationToken cancellationToken);

    Task<TrainerPerformanceIncentive?> AddPerformanceIncentiveAsync(Guid tenantId, Guid trainerId, string name, TrainerTargetType metric, decimal threshold, IncentiveRewardKind rewardKind, decimal? fixedAmount, decimal? percent, DateOnly start, DateOnly? end, bool requiresApproval, CancellationToken cancellationToken);

    Task<int> CalculateCompensationAsync(Guid tenantId, Guid trainerId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<TrainerCompensationCalculation?> DecideCalculationAsync(Guid tenantId, Guid calculationId, CompensationStatus status, CancellationToken cancellationToken);

    Task<CoachWorkspace?> CoachAsync(Guid tenantId, Guid trainerId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
