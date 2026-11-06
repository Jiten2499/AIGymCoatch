using System.Text.Json;
using LifeFitAI.Application.Gym.Expenses;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform.SystemJobs;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Platform;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Platform;

public sealed class MembershipExpiryProcessor : IMembershipExpiryProcessor
{
    public const int BatchSize = 500;

    private readonly ApplicationDbContext _db;
    private readonly IFeatureFlagService _features;
    private readonly IDateTimeProvider _clock;

    public MembershipExpiryProcessor(ApplicationDbContext db, IFeatureFlagService features, IDateTimeProvider clock)
    {
        _db = db;
        _features = features;
        _clock = clock;
    }

    public async Task<MembershipExpiryRun> ExecuteAsync(CancellationToken cancellationToken)
    {
        var processed = 0;
        var tenDay = 0;
        var fiveDay = 0;
        var todayCount = 0;
        var skipped = 0;
        var failed = 0;
        var tenants = await _db.Tenants.AsNoTracking()
            .Where(tenant => tenant.Status == TenantStatus.Active && tenant.TenantType != TenantType.Platform)
            .Select(tenant => tenant.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var tenantId in tenants)
        {
            try
            {
                if (!await _features.IsEnabledAsync(tenantId, FeatureCodes.GymCommunications, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                var zone = await _db.BusinessProfiles.AsNoTracking()
                    .Where(profile => profile.TenantId == tenantId)
                    .Select(profile => profile.TimeZone)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
                var today = ExpenseCalendar.LocalDate(_clock.UtcNow, ExpenseCalendar.Zone(zone));
                var counts = await ProcessTenantAsync(tenantId, today, cancellationToken).ConfigureAwait(false);
                processed += counts.Processed;
                tenDay += counts.TenDay;
                fiveDay += counts.FiveDay;
                todayCount += counts.Today;
                skipped += counts.Skipped;
                failed += counts.Failed;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;
            }
        }

        return new MembershipExpiryRun(processed, tenDay, fiveDay, todayCount, skipped, failed, false);
    }

    private async Task<MembershipExpiryRun> ProcessTenantAsync(Guid tenantId, DateOnly today, CancellationToken cancellationToken)
    {
        var five = today.AddDays(5);
        var ten = today.AddDays(10);
        DateOnly[] dates = [today, five, ten];
        var processed = 0;
        var tenDay = 0;
        var fiveDay = 0;
        var todayCount = 0;
        var skipped = 0;
        var failed = 0;
        Guid? after = null;
        while (true)
        {
            var batch = await LoadBatchAsync(tenantId, today, dates, after, cancellationToken).ConfigureAwait(false);
            if (batch.Count == 0)
            {
                break;
            }

            foreach (var candidate in batch)
            {
                processed++;
                var outcome = await RemindAsync(tenantId, candidate, today, five, ten, cancellationToken).ConfigureAwait(false);
                tenDay += outcome == MembershipExpiryReminderTypes.TenDays ? 1 : 0;
                fiveDay += outcome == MembershipExpiryReminderTypes.FiveDays ? 1 : 0;
                todayCount += outcome == MembershipExpiryReminderTypes.Today ? 1 : 0;
                skipped += outcome == "skipped" ? 1 : 0;
                failed += outcome == "failed" ? 1 : 0;
            }

            after = batch[^1].MembershipId;
            if (batch.Count < BatchSize)
            {
                break;
            }
        }

        return new MembershipExpiryRun(processed, tenDay, fiveDay, todayCount, skipped, failed, false);
    }

    private Task<List<ExpiryCandidate>> LoadBatchAsync(Guid tenantId, DateOnly today, DateOnly[] dates, Guid? after, CancellationToken cancellationToken)
    {
        var memberships = _db.GymMemberships.AsNoTracking()
            .Where(membership => membership.TenantId == tenantId && dates.Contains(membership.EndDate))
            .Where(membership => membership.Status == GymMembershipStatus.Active
                || membership.Status == GymMembershipStatus.Frozen
                || (membership.Status == GymMembershipStatus.Expired && membership.EndDate == today));
        if (after is Guid lastId)
        {
            memberships = memberships.Where(membership => membership.Id > lastId);
        }

        return memberships
            .OrderBy(membership => membership.Id)
            .Join(
                _db.MemberProfiles.AsNoTracking().Where(member => member.TenantId == tenantId),
                membership => membership.MemberProfileId,
                member => member.Id,
                (membership, member) => new ExpiryCandidate(membership.Id, membership.EndDate, member.UserId, member.Status))
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
    }

    private async Task<string> RemindAsync(Guid tenantId, ExpiryCandidate candidate, DateOnly today, DateOnly five, DateOnly ten, CancellationToken cancellationToken)
    {
        var reminderType = candidate.EndDate == ten
            ? MembershipExpiryReminderTypes.TenDays
            : candidate.EndDate == five
                ? MembershipExpiryReminderTypes.FiveDays
                : MembershipExpiryReminderTypes.Today;
        if (candidate.UserId is not Guid userId || candidate.ProfileStatus != ProfileStatus.Active)
        {
            return "skipped";
        }

        try
        {
            var key = MembershipExpiryMessages.IdempotencyKey(candidate.MembershipId, reminderType, candidate.EndDate);
            var exists = await _db.MembershipExpiryReminders.AnyAsync(
                reminder => reminder.TenantId == tenantId && reminder.MembershipId == candidate.MembershipId && reminder.ReminderType == reminderType && reminder.ExpiryDate == candidate.EndDate,
                cancellationToken).ConfigureAwait(false);
            if (exists || await _db.OutboxMessages.AnyAsync(message => message.TenantId == tenantId && message.IdempotencyKey == key, cancellationToken).ConfigureAwait(false))
            {
                return "skipped";
            }

            var (title, message) = MembershipExpiryMessages.Compose(reminderType, candidate.EndDate);
            var payload = JsonSerializer.Serialize(new MembershipExpiryPayload(tenantId, userId, candidate.MembershipId, reminderType, candidate.EndDate, title, message, null));
            _db.MembershipExpiryReminders.Add(MembershipExpiryReminder.Create(Guid.NewGuid(), tenantId, candidate.MembershipId, reminderType, candidate.EndDate, _clock.UtcNow));
            _db.OutboxMessages.Add(OutboxMessage.Create(Guid.NewGuid(), tenantId, MembershipExpiryMessages.MessageType, payload, _clock.UtcNow, null, key, _clock.UtcNow));
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return reminderType;
        }
        catch (DbUpdateException)
        {
            DetachPending();
            return "skipped";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            DetachPending();
            return "failed";
        }
    }

    private void DetachPending()
    {
        foreach (var entry in _db.ChangeTracker.Entries().Where(entry => entry.State is EntityState.Added or EntityState.Modified).ToArray())
        {
            entry.State = EntityState.Detached;
        }
    }

    private sealed record ExpiryCandidate(Guid MembershipId, DateOnly EndDate, Guid? UserId, ProfileStatus ProfileStatus);
}
