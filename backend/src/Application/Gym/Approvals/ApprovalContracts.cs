using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym.Approvals;

public sealed record ApprovalRequestListItem(
    Guid Id,
    ApprovalRequestType RequestType,
    string Title,
    string Summary,
    string RequestedByName,
    DateTime RequestedAt,
    string RelatedLabel,
    decimal? Amount,
    string? Currency,
    int? Days,
    ApprovalRequestStatus Status);

public sealed record ApprovalHistoryEntry(string Action, DateTime At, string ActorName, string? Comment);

public sealed record ApprovalRequestDetails(
    Guid Id,
    ApprovalRequestType RequestType,
    string Title,
    string Summary,
    string? Reason,
    string RequestedByName,
    DateTime RequestedAt,
    ApprovalRequestStatus Status,
    string RelatedLabel,
    Guid? MemberProfileId,
    Guid SourceId,
    int? Days,
    DateOnly? OriginalEndDate,
    DateOnly? ProposedEndDate,
    decimal? Amount,
    string? Currency,
    string? DecisionComment,
    IReadOnlyList<ApprovalHistoryEntry> History);

public sealed record ApprovalInboxAccess(int PendingCount);

public sealed record ApprovalListQuery(
    ApprovalRequestStatus? Status,
    ApprovalRequestType? RequestType,
    Guid? RequestedByUserId,
    DateOnly? From,
    DateOnly? To,
    int Page,
    int PageSize,
    string? Sort);
