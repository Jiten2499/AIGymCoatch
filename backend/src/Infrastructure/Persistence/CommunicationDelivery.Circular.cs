using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed partial class CommunicationDelivery
{
    public async Task<int> IncludeMissingCircularTrainersAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var circulars = await _db.Communications
            .Where(item => item.Type == CommunicationType.Circular && item.Status == CommunicationStatus.Published && item.PublishAt != null && item.PublishAt <= utcNow && (item.ExpiresAt == null || item.ExpiresAt > utcNow))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var changed = 0;
        foreach (var communication in circulars)
        {
            var before = _db.ChangeTracker.Entries<CommunicationRecipient>().Count(entry => entry.State == EntityState.Added);
            await DeliverAsync(communication, utcNow, cancellationToken).ConfigureAwait(false);
            if (_db.ChangeTracker.Entries<CommunicationRecipient>().Count(entry => entry.State == EntityState.Added) > before)
            {
                changed++;
            }
        }

        return changed;
    }

    private async Task<List<ResolvedRecipient>> PeopleForAsync(Communication communication, DateTime utcNow, CancellationToken cancellationToken)
    {
        var selectedMembers = await _db.CommunicationAudienceMembers.AsNoTracking()
            .Where(item => item.TenantId == communication.TenantId && item.CommunicationId == communication.Id)
            .Select(item => item.MemberProfileId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var targets = await _db.CommunicationAudienceTargets.AsNoTracking()
            .Where(item => item.TenantId == communication.TenantId && item.CommunicationId == communication.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var people = await ResolveAsync(
            communication.TenantId,
            communication.AudienceType,
            communication.AudiencePackageId,
            communication.AudienceTrainerId,
            communication.ExpiringWithinDays,
            selectedMembers,
            targets.Where(item => item.TargetKind == CommunicationTargetKind.Trainer).Select(item => item.EntityId).ToList(),
            targets.Where(item => item.TargetKind == CommunicationTargetKind.Staff).Select(item => item.EntityId).ToList(),
            utcNow,
            cancellationToken).ConfigureAwait(false);
        if (communication.Type == CommunicationType.Circular)
        {
            await AddTrainersAsync(communication.TenantId, people, cancellationToken).ConfigureAwait(false);
        }

        return people;
    }

    private async Task AddTrainersAsync(Guid tenantId, List<ResolvedRecipient> people, CancellationToken cancellationToken)
    {
        var known = people.Select(item => item.UserId).ToHashSet();
        foreach (var trainer in await TrainersAsync(tenantId, null, cancellationToken).ConfigureAwait(false))
        {
            if (known.Add(trainer.UserId))
            {
                people.Add(trainer);
            }
        }
    }
}
