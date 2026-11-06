using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym.Leave;

public sealed record StaffLeaveListItem(
    Guid Id,
    Guid StaffProfileId,
    Guid UserId,
    string StaffName,
    string EmployeeCode,
    string LeaveType,
    DateOnly StartDate,
    DateOnly EndDate,
    int DurationDays,
    string Status,
    string Reason,
    Guid? ApprovalRequestId,
    DateTime SubmittedAt);

public sealed record StaffLeaveDetails(
    Guid Id,
    Guid StaffProfileId,
    Guid UserId,
    string StaffName,
    string EmployeeCode,
    string LeaveType,
    DateOnly StartDate,
    DateOnly EndDate,
    int DurationDays,
    string Status,
    string Reason,
    Guid? AttachmentFileId,
    Guid? ApprovalRequestId,
    DateTime SubmittedAt,
    string? DecisionComment,
    Guid? DecidedByUserId,
    DateTime? DecidedAt,
    bool CanCancel);

public sealed record StaffLeaveCalendarQuery(
    DateOnly From,
    DateOnly To,
    Guid? StaffProfileId,
    LeaveType? LeaveType,
    LeaveRequestStatus? Status);

public interface ILeaveStore
{
    Task AddAsync(StaffLeaveRequest request, CancellationToken cancellationToken);

    Task<StaffLeaveRequest?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<StaffLeaveRequest?> GetByApprovalAsync(Guid tenantId, Guid approvalRequestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffLeaveRequest>> ListForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffLeaveRequest>> SearchAsync(Guid tenantId, StaffLeaveCalendarQuery query, CancellationToken cancellationToken);

    Task<bool> HasOverlapAsync(Guid tenantId, Guid staffProfileId, DateOnly start, DateOnly end, Guid? excludeId, CancellationToken cancellationToken);
}
