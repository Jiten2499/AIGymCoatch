using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Platform;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class CommunicationDelivery : ICommunicationDelivery
{
    private readonly ApplicationDbContext _db;

    public CommunicationDelivery(ApplicationDbContext db) => _db = db;

    public async Task<AudiencePreview> PreviewAsync(
        Guid tenantId,
        CommunicationAudienceType audienceType,
        Guid? packageId,
        Guid? trainerId,
        int? expiringWithinDays,
        IReadOnlyList<Guid> selectedMemberIds,
        IReadOnlyList<Guid> selectedTrainerIds,
        IReadOnlyList<Guid> selectedStaffIds,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var people = await ResolveAsync(tenantId, audienceType, packageId, trainerId, expiringWithinDays, selectedMemberIds, selectedTrainerIds, selectedStaffIds, utcNow, cancellationToken).ConfigureAwait(false);
        return new AudiencePreview(
            people.Count(item => item.Kind == CommunicationRecipientKind.Member),
            people.Count(item => item.Kind == CommunicationRecipientKind.Trainer),
            people.Count(item => item.Kind == CommunicationRecipientKind.Staff),
            people.Count(item => item.Kind == CommunicationRecipientKind.BusinessUser),
            people.Count);
    }

    public async Task DeliverAsync(Communication communication, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (communication.Status != CommunicationStatus.Published)
        {
            return;
        }

        var tracked = _db.CommunicationRecipients.Local.Any(item => item.TenantId == communication.TenantId && item.CommunicationId == communication.Id && item.DeletedAt is null);
        if (tracked || await _db.CommunicationRecipients.AnyAsync(item => item.TenantId == communication.TenantId && item.CommunicationId == communication.Id, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

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
        foreach (var person in people)
        {
            await _db.CommunicationRecipients.AddAsync(
                CommunicationRecipient.Create(Guid.NewGuid(), communication.TenantId, communication.Id, person.UserId, person.Kind, person.EntityId, utcNow),
                cancellationToken).ConfigureAwait(false);
        }

        await CircularPublicationOutbox.QueueAsync(_db, communication, utcNow, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> PublishDueAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var due = await _db.Communications
            .Where(item => item.Status == CommunicationStatus.Scheduled && item.PublishAt != null && item.PublishAt <= utcNow)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var communication in due)
        {
            communication.Publish(utcNow);
            await DeliverAsync(communication, utcNow, cancellationToken).ConfigureAwait(false);
        }

        return due.Count;
    }

    public async Task<int> DeliverPublishedWithoutRecipientsAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var missing = await _db.Communications
            .Where(item => item.Status == CommunicationStatus.Published && !_db.CommunicationRecipients.Any(recipient => recipient.CommunicationId == item.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var communication in missing)
        {
            await DeliverAsync(communication, communication.PublishedAt ?? communication.PublishAt ?? utcNow, cancellationToken).ConfigureAwait(false);
        }

        return missing.Count;
    }

    private async Task<List<ResolvedRecipient>> ResolveAsync(
        Guid tenantId,
        CommunicationAudienceType audienceType,
        Guid? packageId,
        Guid? trainerId,
        int? expiringWithinDays,
        IReadOnlyList<Guid> selectedMemberIds,
        IReadOnlyList<Guid> selectedTrainerIds,
        IReadOnlyList<Guid> selectedStaffIds,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var resolved = new Dictionary<Guid, ResolvedRecipient>();
        if (audienceType is CommunicationAudienceType.AllMembers or CommunicationAudienceType.ActiveMembers or CommunicationAudienceType.ExpiringMembers or CommunicationAudienceType.MembershipPackage or CommunicationAudienceType.TrainerMembers or CommunicationAudienceType.SelectedMembers)
        {
            foreach (var member in await MembersAsync(tenantId, audienceType, packageId, trainerId, expiringWithinDays, selectedMemberIds, utcNow, cancellationToken).ConfigureAwait(false))
            {
                resolved.TryAdd(member.UserId, member);
            }
        }

        if (audienceType == CommunicationAudienceType.AllTrainers)
        {
            foreach (var trainer in await TrainersAsync(tenantId, null, cancellationToken).ConfigureAwait(false))
            {
                resolved.TryAdd(trainer.UserId, trainer);
            }
        }

        if (audienceType == CommunicationAudienceType.SelectedTrainers)
        {
            foreach (var trainer in await TrainersAsync(tenantId, selectedTrainerIds, cancellationToken).ConfigureAwait(false))
            {
                resolved.TryAdd(trainer.UserId, trainer);
            }
        }

        if (audienceType == CommunicationAudienceType.AllStaff)
        {
            foreach (var staff in await StaffAsync(tenantId, null, cancellationToken).ConfigureAwait(false))
            {
                resolved.TryAdd(staff.UserId, staff);
            }
        }

        if (audienceType == CommunicationAudienceType.SelectedStaff)
        {
            foreach (var staff in await StaffAsync(tenantId, selectedStaffIds, cancellationToken).ConfigureAwait(false))
            {
                resolved.TryAdd(staff.UserId, staff);
            }
        }

        if (audienceType == CommunicationAudienceType.AllBusinessUsers)
        {
            var users = await _db.TenantMembers.AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.Status == MembershipStatus.Active)
                .Join(_db.Users.AsNoTracking().Where(user => user.Status == UserStatus.Active), member => member.UserId, user => user.Id, (member, user) => user.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var userId in users.Distinct())
            {
                resolved.TryAdd(userId, new ResolvedRecipient(userId, CommunicationRecipientKind.BusinessUser, null));
            }
        }

        return resolved.Values.ToList();
    }

    private async Task<List<ResolvedRecipient>> MembersAsync(Guid tenantId, CommunicationAudienceType audienceType, Guid? packageId, Guid? trainerId, int? expiringWithinDays, IReadOnlyList<Guid> selectedMemberIds, DateTime utcNow, CancellationToken cancellationToken)
    {
        var query = _db.MemberProfiles.AsNoTracking().Where(item => item.TenantId == tenantId && item.UserId != null);
        if (audienceType == CommunicationAudienceType.ActiveMembers)
        {
            query = query.Where(item => item.Status == ProfileStatus.Active);
        }

        if (audienceType == CommunicationAudienceType.TrainerMembers && trainerId is Guid assignedTrainer)
        {
            query = query.Where(item => item.AssignedTrainerId == assignedTrainer);
        }

        if (audienceType == CommunicationAudienceType.SelectedMembers)
        {
            var ids = selectedMemberIds.Distinct().ToArray();
            query = query.Where(item => ids.Contains(item.Id));
        }

        var members = await query.Select(item => new { item.Id, UserId = item.UserId!.Value }).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (audienceType == CommunicationAudienceType.ExpiringMembers)
        {
            var today = DateOnly.FromDateTime(utcNow);
            var until = today.AddDays(Math.Clamp(expiringWithinDays ?? 7, 1, 90));
            var expiring = await _db.GymMemberships.AsNoTracking()
                .Where(item => item.TenantId == tenantId && (item.Status == GymMembershipStatus.Active || item.Status == GymMembershipStatus.Frozen) && item.EndDate >= today && item.EndDate <= until)
                .Select(item => item.MemberProfileId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var allowed = expiring.ToHashSet();
            members = members.Where(item => allowed.Contains(item.Id)).ToList();
        }

        if (audienceType == CommunicationAudienceType.MembershipPackage && packageId is Guid planId)
        {
            var today = DateOnly.FromDateTime(utcNow);
            var covered = await _db.GymMemberships.AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.MembershipPlanId == planId && (item.Status == GymMembershipStatus.Active || item.Status == GymMembershipStatus.Pending || item.Status == GymMembershipStatus.Frozen) && item.EndDate >= today)
                .Select(item => item.MemberProfileId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var allowed = covered.ToHashSet();
            members = members.Where(item => allowed.Contains(item.Id)).ToList();
        }

        return members.Select(item => new ResolvedRecipient(item.UserId, CommunicationRecipientKind.Member, item.Id)).ToList();
    }

    private async Task<List<ResolvedRecipient>> TrainersAsync(Guid tenantId, IReadOnlyList<Guid>? ids, CancellationToken cancellationToken)
    {
        var query = _db.TrainerProfiles.AsNoTracking().Where(item => item.TenantId == tenantId && item.Status == ProfileStatus.Active);
        if (ids is not null)
        {
            var selected = ids.Distinct().ToArray();
            query = query.Where(item => selected.Contains(item.Id));
        }

        var trainers = await query.Select(item => new { item.Id, item.UserId }).ToListAsync(cancellationToken).ConfigureAwait(false);
        return trainers.Select(item => new ResolvedRecipient(item.UserId, CommunicationRecipientKind.Trainer, item.Id)).ToList();
    }

    private async Task<List<ResolvedRecipient>> StaffAsync(Guid tenantId, IReadOnlyList<Guid>? ids, CancellationToken cancellationToken)
    {
        var query = _db.StaffProfiles.AsNoTracking().Where(item => item.TenantId == tenantId && item.Status == ProfileStatus.Active);
        if (ids is not null)
        {
            var selected = ids.Distinct().ToArray();
            query = query.Where(item => selected.Contains(item.Id));
        }

        var staff = await query.Select(item => new { item.Id, item.UserId }).ToListAsync(cancellationToken).ConfigureAwait(false);
        return staff.Select(item => new ResolvedRecipient(item.UserId, CommunicationRecipientKind.Staff, item.Id)).ToList();
    }

    private sealed record ResolvedRecipient(Guid UserId, CommunicationRecipientKind Kind, Guid? EntityId);
}
