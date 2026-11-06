using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public enum ApprovalRequestType
{
    MembershipFreeze = 1,
    MembershipExtension = 2,
    TrainerRewardAward = 3,
    StaffLeave = 4,
}

public enum ApprovalRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}

public sealed class ApprovalRequest : BaseEntity
{
    private ApprovalRequest()
    {
    }

    public Guid TenantId { get; private set; }

    public ApprovalRequestType RequestType { get; private set; }

    public ApprovalRequestStatus Status { get; private set; }

    public Guid SourceId { get; private set; }

    public string RequiredPermission { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Summary { get; private set; } = string.Empty;

    public string? Reason { get; private set; }

    public string? DecisionComment { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public string RequestedByName { get; private set; } = string.Empty;

    public Guid? DecidedByUserId { get; private set; }

    public string? DecidedByName { get; private set; }

    public DateTime? DecidedAt { get; private set; }

    public Guid? MemberProfileId { get; private set; }

    public string RelatedLabel { get; private set; } = string.Empty;

    public int? Days { get; private set; }

    public DateOnly? OriginalEndDate { get; private set; }

    public DateOnly? ProposedEndDate { get; private set; }

    public decimal? Amount { get; private set; }

    public string? Currency { get; private set; }

    public static ApprovalRequest Create(
        Guid id,
        Guid tenantId,
        ApprovalRequestType requestType,
        Guid sourceId,
        string requiredPermission,
        string title,
        string summary,
        string? reason,
        Guid requestedByUserId,
        string requestedByName,
        Guid? memberProfileId,
        string relatedLabel,
        int? days,
        DateOnly? originalEndDate,
        DateOnly? proposedEndDate,
        decimal? amount,
        string? currency) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            RequestType = requestType,
            Status = ApprovalRequestStatus.Pending,
            SourceId = sourceId,
            RequiredPermission = requiredPermission,
            Title = title.Trim(),
            Summary = summary.Trim(),
            Reason = Clean(reason),
            RequestedByUserId = requestedByUserId,
            RequestedByName = requestedByName.Trim(),
            MemberProfileId = memberProfileId,
            RelatedLabel = relatedLabel.Trim(),
            Days = days is > 0 ? days : null,
            OriginalEndDate = originalEndDate,
            ProposedEndDate = proposedEndDate,
            Amount = amount,
            Currency = string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant(),
        };

    public bool Approve(Guid userId, string decidedByName, DateTime utcNow, string? comment)
    {
        if (Status != ApprovalRequestStatus.Pending)
        {
            return false;
        }

        Status = ApprovalRequestStatus.Approved;
        DecidedByUserId = userId;
        DecidedByName = decidedByName.Trim();
        DecidedAt = utcNow;
        DecisionComment = Clean(comment);
        return true;
    }

    public bool Reject(Guid userId, string decidedByName, DateTime utcNow, string comment)
    {
        if (Status != ApprovalRequestStatus.Pending || string.IsNullOrWhiteSpace(comment))
        {
            return false;
        }

        Status = ApprovalRequestStatus.Rejected;
        DecidedByUserId = userId;
        DecidedByName = decidedByName.Trim();
        DecidedAt = utcNow;
        DecisionComment = comment.Trim();
        return true;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
