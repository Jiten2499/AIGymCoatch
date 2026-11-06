using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Communications;

public interface ICommunicationRepository
{
    Task AddAsync(Communication communication, CancellationToken cancellationToken);

    Task<Communication?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task ReplaceAudienceAsync(Guid tenantId, Guid communicationId, IReadOnlyList<Guid> memberIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> AudienceMemberIdsAsync(Guid tenantId, Guid communicationId, CancellationToken cancellationToken);

    Task<PageResult<CommunicationListItem>> SearchAsync(Guid tenantId, CommunicationSearch search, CancellationToken cancellationToken);

    Task<CommunicationDetails?> ReadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<PageResult<MemberCommunicationItem>> ListForMemberAsync(
        Guid tenantId,
        Guid memberProfileId,
        Guid userId,
        CommunicationType? type,
        int page,
        int pageSize,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<bool> IsAudienceMatchAsync(
        Guid tenantId,
        Communication communication,
        Guid memberProfileId,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task MarkReadAsync(CommunicationRead read, CancellationToken cancellationToken);

    Task<bool> HasReadAsync(Guid tenantId, Guid communicationId, Guid userId, CancellationToken cancellationToken);

    Task SoftDeleteAudienceAsync(Guid tenantId, Guid communicationId, DateTime utcNow, CancellationToken cancellationToken);

    Task ReplaceTargetsAsync(Guid tenantId, Guid communicationId, CommunicationTargetKind kind, IReadOnlyList<Guid> entityIds, CancellationToken cancellationToken);

    Task SoftDeleteTargetsAsync(Guid tenantId, Guid communicationId, DateTime utcNow, CancellationToken cancellationToken);

    Task<PageResult<MemberCommunicationItem>> ListInboxAsync(Guid tenantId, Guid userId, CommunicationType? type, bool unreadOnly, int page, int pageSize, DateTime utcNow, CancellationToken cancellationToken);

    Task<MemberCommunicationItem?> ReadInboxAsync(Guid tenantId, Guid userId, Guid communicationId, DateTime utcNow, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid tenantId, Guid userId, DateTime utcNow, CancellationToken cancellationToken);

    Task<DateTime?> LatestInboxActivityAsync(Guid tenantId, Guid userId, DateTime utcNow, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberCommunicationItem>> SyncInboxAsync(Guid tenantId, Guid userId, DateTime sinceUtc, DateTime utcNow, int take, CancellationToken cancellationToken);

    Task<CommunicationRecipient?> GetRecipientAsync(Guid tenantId, Guid userId, Guid communicationId, CancellationToken cancellationToken);

    Task MarkAllReadAsync(Guid tenantId, Guid userId, DateTime utcNow, CancellationToken cancellationToken);
}
