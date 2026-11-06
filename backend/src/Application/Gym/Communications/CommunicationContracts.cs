using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym.Communications;

public sealed record CommunicationListItem(
    Guid Id,
    CommunicationType Type,
    string Title,
    CommunicationPriority Priority,
    CommunicationStatus Status,
    CommunicationAudienceType AudienceType,
    DateTime? PublishAt,
    DateTime? ExpiresAt,
    string? CreatedBy,
    DateTime CreatedAt);

public sealed record CommunicationDetails(
    Guid Id,
    CommunicationType Type,
    string Title,
    string Message,
    CommunicationPriority Priority,
    CommunicationStatus Status,
    CommunicationAudienceType AudienceType,
    Guid? AudiencePackageId,
    Guid? AudienceTrainerId,
    int? ExpiringWithinDays,
    IReadOnlyList<Guid> SelectedMemberIds,
    string? ImageStorageKey,
    DateTime? PublishAt,
    DateTime? ExpiresAt,
    Guid CreatedByUserId,
    string? CreatedBy,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    DateTime? ArchivedAt);

public sealed record MemberCommunicationItem(
    Guid Id,
    CommunicationType Type,
    string Title,
    string Message,
    CommunicationPriority Priority,
    DateTime PublishAt,
    DateTime? ExpiresAt,
    bool IsRead,
    string? ImageStorageKey,
    string? CreatedBy = null);

public sealed record CommunicationInboxSummaryResponse(int UnreadCount, DateTime? LatestActivityAt, DateTime ServerTimeUtc);

public sealed record CommunicationInboxSyncResponse(IReadOnlyList<MemberCommunicationItem> Items, int UnreadCount, DateTime ServerTimeUtc);

public sealed record CommunicationSearch(
    string? Term,
    CommunicationType? Type,
    CommunicationStatus? Status,
    int Page,
    int PageSize,
    CommunicationAudienceType? AudienceType = null,
    DateTime? PublishFrom = null,
    DateTime? PublishTo = null);
