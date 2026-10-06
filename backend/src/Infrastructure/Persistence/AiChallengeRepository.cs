using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Gym;
using LifeFitAI.Domain.Fitness;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class AiRepository : IAiRepository
{
    private readonly ApplicationDbContext _db;

    public AiRepository(ApplicationDbContext db) => _db = db;

    public Task<AiConversation?> GetConversationAsync(Guid tenantId, Guid userId, Guid id, CancellationToken cancellationToken) =>
        _db.AiConversations.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Id == id, cancellationToken);

    public async Task<PageResult<AiConversation>> ListConversationsAsync(Guid tenantId, Guid userId, PageQuery page, CancellationToken cancellationToken)
    {
        var query = _db.AiConversations.AsNoTracking().Where(item => item.TenantId == tenantId && item.UserId == userId);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderByDescending(item => item.UpdatedAt).Skip(page.Skip).Take(page.NormalizedSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<AiConversation>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public Task AddConversationAsync(AiConversation conversation, CancellationToken cancellationToken) =>
        _db.AiConversations.AddAsync(conversation, cancellationToken).AsTask();

    public async Task<IReadOnlyList<AiMessage>> ListRecentMessagesAsync(Guid conversationId, int take, CancellationToken cancellationToken)
    {
        var items = await _db.AiMessages.AsNoTracking().Where(item => item.ConversationId == conversationId).OrderByDescending(item => item.CreatedAt).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);
        items.Reverse();
        return items;
    }

    public async Task<PageResult<AiMessage>> ListMessagesAsync(Guid conversationId, PageQuery page, CancellationToken cancellationToken)
    {
        var query = _db.AiMessages.AsNoTracking().Where(item => item.ConversationId == conversationId);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(item => item.CreatedAt).Skip(page.Skip).Take(page.NormalizedSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<AiMessage>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public Task AddMessageAsync(AiMessage message, CancellationToken cancellationToken) =>
        _db.AiMessages.AddAsync(message, cancellationToken).AsTask();

    public Task AddUsageAsync(AiUsageRecord usage, CancellationToken cancellationToken) =>
        _db.AiUsageRecords.AddAsync(usage, cancellationToken).AsTask();

    public Task<int> CountUsageAsync(Guid tenantId, Guid userId, DateTime fromUtc, CancellationToken cancellationToken) =>
        _db.AiUsageRecords.CountAsync(item => item.TenantId == tenantId && item.UserId == userId && item.CreatedAt >= fromUtc, cancellationToken);

    public Task<int> CountTenantUsageAsync(Guid tenantId, DateTime fromUtc, CancellationToken cancellationToken) =>
        _db.AiUsageRecords.CountAsync(item => item.TenantId == tenantId && item.CreatedAt >= fromUtc, cancellationToken);
}

public sealed class ChallengeRepository : IChallengeRepository
{
    private readonly ApplicationDbContext _db;

    public ChallengeRepository(ApplicationDbContext db) => _db = db;

    public async Task<PageResult<ChallengeResponse>> ListAsync(Guid tenantId, Guid userId, PageQuery page, CancellationToken cancellationToken)
    {
        var query = Visible(tenantId);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var challenges = await query.OrderBy(item => item.StartDate).Skip(page.Skip).Take(page.NormalizedSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        var ids = challenges.Select(item => item.Id).ToArray();
        var participants = await _db.ChallengeParticipants.AsNoTracking().Where(item => ids.Contains(item.ChallengeId) && item.UserId == userId && item.TenantId == tenantId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var items = challenges.Select(item => Map(item, participants.FirstOrDefault(participant => participant.ChallengeId == item.Id))).ToArray();
        return new PageResult<ChallengeResponse>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public Task<Challenge?> GetVisibleAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        Visible(tenantId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task AddAsync(Challenge challenge, CancellationToken cancellationToken) =>
        _db.Challenges.AddAsync(challenge, cancellationToken).AsTask();

    public Task<ChallengeParticipant?> GetParticipantAsync(Guid challengeId, Guid userId, CancellationToken cancellationToken) =>
        _db.ChallengeParticipants.FirstOrDefaultAsync(item => item.ChallengeId == challengeId && item.UserId == userId, cancellationToken);

    public Task AddParticipantAsync(ChallengeParticipant participant, CancellationToken cancellationToken) =>
        _db.ChallengeParticipants.AddAsync(participant, cancellationToken).AsTask();

    public Task AddNotificationAsync(Notification notification, CancellationToken cancellationToken) =>
        _db.Notifications.AddAsync(notification, cancellationToken).AsTask();

    public async Task<PageResult<Notification>> ListNotificationsAsync(Guid tenantId, Guid userId, PageQuery page, CancellationToken cancellationToken)
    {
        var query = _db.Notifications.AsNoTracking().Where(item => item.TenantId == tenantId && item.UserId == userId);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderByDescending(item => item.CreatedAt).Skip(page.Skip).Take(page.NormalizedSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<Notification>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public Task<Notification?> GetNotificationAsync(Guid tenantId, Guid userId, Guid id, CancellationToken cancellationToken) =>
        _db.Notifications.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Id == id, cancellationToken);

    public async Task RecordWorkoutProgressAsync(Guid tenantId, Guid userId, bool firstWorkoutToday, DateTime utcNow, CancellationToken cancellationToken)
    {
        var rows = await (
            from participant in _db.ChallengeParticipants
            join challenge in _db.Challenges on participant.ChallengeId equals challenge.Id
            where participant.UserId == userId && participant.TenantId == tenantId && participant.CompletedAt == null && challenge.Status == ChallengeStatus.Active
            select new { participant, challenge }).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var row in rows)
        {
            if (row.challenge.ChallengeType == ChallengeType.WorkoutCount || (row.challenge.ChallengeType == ChallengeType.ConsistencyDays && firstWorkoutToday))
            {
                row.participant.Record(row.participant.CurrentValue + 1, row.challenge.TargetValue, utcNow);
            }
        }
    }

    public async Task<ChallengeResponse?> ActiveChallengeAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var row = await (
            from participant in _db.ChallengeParticipants.AsNoTracking()
            join challenge in _db.Challenges.AsNoTracking() on participant.ChallengeId equals challenge.Id
            where participant.UserId == userId && participant.TenantId == tenantId && challenge.Status == ChallengeStatus.Active && participant.CompletedAt == null
            orderby challenge.EndDate
            select new { challenge, participant }).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row is null ? null : Map(row.challenge, row.participant);
    }

    private IQueryable<Challenge> Visible(Guid tenantId) =>
        _db.Challenges.Where(item => item.TenantId == null || item.TenantId == tenantId);

    private static ChallengeResponse Map(Challenge challenge, ChallengeParticipant? participant) =>
        new(challenge.Id, challenge.Name, challenge.Description, challenge.ChallengeType, challenge.StartDate, challenge.EndDate, challenge.TargetValue, challenge.Status, challenge.IsSystem, participant?.CurrentValue, participant?.CompletedAt);
}
