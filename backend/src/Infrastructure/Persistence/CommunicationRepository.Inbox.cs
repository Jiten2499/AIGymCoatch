using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed partial class CommunicationRepository
{
    public async Task ReplaceTargetsAsync(Guid tenantId, Guid communicationId, CommunicationTargetKind kind, IReadOnlyList<Guid> entityIds, CancellationToken cancellationToken)
    {
        var existing = await _db.CommunicationAudienceTargets
            .Where(item => item.TenantId == tenantId && item.CommunicationId == communicationId && item.TargetKind == kind)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        _db.CommunicationAudienceTargets.RemoveRange(existing);
        foreach (var entityId in entityIds.Distinct())
        {
            await _db.CommunicationAudienceTargets.AddAsync(
                CommunicationAudienceTarget.Create(Guid.NewGuid(), tenantId, communicationId, kind, entityId),
                cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SoftDeleteTargetsAsync(Guid tenantId, Guid communicationId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var existing = await _db.CommunicationAudienceTargets
            .Where(item => item.TenantId == tenantId && item.CommunicationId == communicationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var row in existing)
        {
            row.SoftDelete(utcNow);
        }
    }

    public async Task<PageResult<MemberCommunicationItem>> ListInboxAsync(Guid tenantId, Guid userId, CommunicationType? type, bool unreadOnly, int page, int pageSize, DateTime utcNow, CancellationToken cancellationToken)
    {
        var query = Visible(tenantId, userId, utcNow, type, unreadOnly);
        var pageQuery = new PageQuery(page, pageSize);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(item => item.PublishAt)
            .Skip(pageQuery.Skip)
            .Take(pageQuery.NormalizedSize)
            .Select(item => new MemberCommunicationItem(
                item.Id,
                item.Type,
                item.Title,
                item.Message,
                item.Priority,
                item.PublishAt!.Value,
                item.ExpiresAt,
                _db.CommunicationRecipients.Any(recipient => recipient.TenantId == tenantId && recipient.UserId == userId && recipient.CommunicationId == item.Id && recipient.ReadAt != null),
                item.ImageStorageKey,
                _db.Users.Where(user => user.Id == item.CreatedByUserId).Select(user => user.FirstName + " " + user.LastName).FirstOrDefault()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PageResult<MemberCommunicationItem>(items, pageQuery.NormalizedPage, pageQuery.NormalizedSize, total);
    }

    public async Task<MemberCommunicationItem?> ReadInboxAsync(Guid tenantId, Guid userId, Guid communicationId, DateTime utcNow, CancellationToken cancellationToken)
    {
        return await Visible(tenantId, userId, utcNow, null, false)
            .Where(item => item.Id == communicationId)
            .Select(item => new MemberCommunicationItem(
                item.Id,
                item.Type,
                item.Title,
                item.Message,
                item.Priority,
                item.PublishAt!.Value,
                item.ExpiresAt,
                _db.CommunicationRecipients.Any(recipient => recipient.TenantId == tenantId && recipient.UserId == userId && recipient.CommunicationId == item.Id && recipient.ReadAt != null),
                item.ImageStorageKey,
                _db.Users.Where(user => user.Id == item.CreatedByUserId).Select(user => user.FirstName + " " + user.LastName).FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<int> CountUnreadAsync(Guid tenantId, Guid userId, DateTime utcNow, CancellationToken cancellationToken) =>
        Visible(tenantId, userId, utcNow, null, true).CountAsync(cancellationToken);

    public async Task<DateTime?> LatestInboxActivityAsync(Guid tenantId, Guid userId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var latestPublish = await Visible(tenantId, userId, utcNow, null, false)
            .MaxAsync(item => item.PublishAt, cancellationToken)
            .ConfigureAwait(false);
        var latestRead = await _db.CommunicationRecipients.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId && item.ReadAt != null)
            .MaxAsync(item => item.ReadAt, cancellationToken)
            .ConfigureAwait(false);
        var latestDelivered = await _db.CommunicationRecipients.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .MaxAsync(item => (DateTime?)item.DeliveredAt, cancellationToken)
            .ConfigureAwait(false);
        DateTime? latest = latestPublish;
        if (latestRead is DateTime read && (latest is null || read > latest))
        {
            latest = read;
        }

        if (latestDelivered is DateTime delivered && (latest is null || delivered > latest))
        {
            latest = delivered;
        }

        return latest;
    }

    public async Task<IReadOnlyList<MemberCommunicationItem>> SyncInboxAsync(Guid tenantId, Guid userId, DateTime sinceUtc, DateTime utcNow, int take, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(take, 1, 50);
        return await Visible(tenantId, userId, utcNow, null, false)
            .Where(item =>
                item.PublishAt > sinceUtc
                || _db.CommunicationRecipients.Any(recipient =>
                    recipient.TenantId == tenantId
                    && recipient.UserId == userId
                    && recipient.CommunicationId == item.Id
                    && (recipient.DeliveredAt > sinceUtc || (recipient.ReadAt != null && recipient.ReadAt > sinceUtc))))
            .OrderByDescending(item => item.PublishAt)
            .Take(limit)
            .Select(item => new MemberCommunicationItem(
                item.Id,
                item.Type,
                item.Title,
                item.Message,
                item.Priority,
                item.PublishAt!.Value,
                item.ExpiresAt,
                _db.CommunicationRecipients.Any(recipient => recipient.TenantId == tenantId && recipient.UserId == userId && recipient.CommunicationId == item.Id && recipient.ReadAt != null),
                item.ImageStorageKey,
                _db.Users.Where(user => user.Id == item.CreatedByUserId).Select(user => user.FirstName + " " + user.LastName).FirstOrDefault()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<CommunicationRecipient?> GetRecipientAsync(Guid tenantId, Guid userId, Guid communicationId, CancellationToken cancellationToken) =>
        _db.CommunicationRecipients.FirstOrDefaultAsync(
            item => item.TenantId == tenantId && item.UserId == userId && item.CommunicationId == communicationId,
            cancellationToken);

    public async Task MarkAllReadAsync(Guid tenantId, Guid userId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var unread = await _db.CommunicationRecipients
            .Where(item => item.TenantId == tenantId && item.UserId == userId && item.ReadAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var recipient in unread)
        {
            recipient.MarkRead(utcNow);
        }
    }

    private IQueryable<Communication> Visible(Guid tenantId, Guid userId, DateTime utcNow, CommunicationType? type, bool unreadOnly) =>
        _db.Communications.AsNoTracking().Where(item =>
            item.TenantId == tenantId
            && item.Status == CommunicationStatus.Published
            && item.PublishAt != null
            && item.PublishAt <= utcNow
            && (item.ExpiresAt == null || item.ExpiresAt > utcNow)
            && (type == null || item.Type == type)
            && _db.CommunicationRecipients.Any(recipient =>
                recipient.TenantId == tenantId
                && recipient.UserId == userId
                && recipient.CommunicationId == item.Id
                && (!unreadOnly || recipient.ReadAt == null)));
}
