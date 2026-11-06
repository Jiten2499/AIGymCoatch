using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public enum LeaveType
{
    Annual = 1,
    Sick = 2,
    Casual = 3,
    Unpaid = 4,
    Other = 5,
}

public enum LeaveRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
}

public sealed class StaffLeaveRequest : BaseEntity
{
    private StaffLeaveRequest()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid StaffProfileId { get; private set; }
    public Guid UserId { get; private set; }
    public LeaveType LeaveType { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public int DurationDays { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid? AttachmentFileId { get; private set; }
    public LeaveRequestStatus Status { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public string? DecisionComment { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public DateTime? DecidedAt { get; private set; }

    public static int CalculateDuration(DateOnly start, DateOnly end) => end.DayNumber - start.DayNumber + 1;

    public static bool RangesOverlap(DateOnly startA, DateOnly endA, DateOnly startB, DateOnly endB) =>
        startA <= endB && startB <= endA;

    public static StaffLeaveRequest Create(
        Guid id,
        Guid tenantId,
        Guid staffProfileId,
        Guid userId,
        LeaveType leaveType,
        DateOnly start,
        DateOnly end,
        string reason,
        Guid? attachmentFileId,
        DateTime utcNow)
    {
        if (end < start)
        {
            throw new ArgumentException("Leave end date must be on or after the start date.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Leave reason is required.");
        }

        return new StaffLeaveRequest
        {
            Id = id,
            TenantId = tenantId,
            StaffProfileId = staffProfileId,
            UserId = userId,
            LeaveType = leaveType,
            StartDate = start,
            EndDate = end,
            DurationDays = CalculateDuration(start, end),
            Reason = reason.Trim(),
            AttachmentFileId = attachmentFileId,
            Status = LeaveRequestStatus.Pending,
            SubmittedAt = utcNow,
        };
    }

    public void LinkApproval(Guid approvalRequestId) => ApprovalRequestId = approvalRequestId;

    public bool Cancel(DateTime utcNow)
    {
        if (Status != LeaveRequestStatus.Pending)
        {
            return false;
        }

        Status = LeaveRequestStatus.Cancelled;
        DecidedAt = utcNow;
        DecisionComment = "Cancelled by requester";
        return true;
    }

    public bool Approve(Guid decidedByUserId, string? comment, DateTime utcNow)
    {
        if (Status != LeaveRequestStatus.Pending)
        {
            return false;
        }

        Status = LeaveRequestStatus.Approved;
        DecidedByUserId = decidedByUserId;
        DecisionComment = Clean(comment);
        DecidedAt = utcNow;
        return true;
    }

    public bool Reject(Guid decidedByUserId, string comment, DateTime utcNow)
    {
        if (Status != LeaveRequestStatus.Pending || string.IsNullOrWhiteSpace(comment))
        {
            return false;
        }

        Status = LeaveRequestStatus.Rejected;
        DecidedByUserId = decidedByUserId;
        DecisionComment = comment.Trim();
        DecidedAt = utcNow;
        return true;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
