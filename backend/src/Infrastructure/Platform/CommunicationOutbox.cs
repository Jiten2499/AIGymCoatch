using System.Text.Json;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform.BackgroundJobs;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Platform;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Platform;

public sealed record CommunicationPublishPayload(Guid CommunicationId, Guid TenantId, string? CorrelationId, DateTime PublishAtUtc, string? JobId);

public sealed class OutboxStore : IOutboxStore
{
    private readonly ApplicationDbContext _db;

    public OutboxStore(ApplicationDbContext db) => _db = db;

    public Task<OutboxMessage?> FindAsync(Guid tenantId, string idempotencyKey, CancellationToken cancellationToken) =>
        _db.OutboxMessages.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken) =>
        _db.OutboxMessages.AddAsync(message, cancellationToken).AsTask();

    public async Task<IReadOnlyList<OutboxMessage>> ListPendingAsync(int take, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _db.OutboxMessages
            .Where(item => item.Status == OutboxMessageStatus.Pending && item.AvailableAtUtc <= now)
            .OrderBy(item => item.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed class CommunicationScheduleService : ICommunicationSchedule
{
    private readonly IBusinessProfileRepository _profiles;
    private readonly ITenantRepository _tenants;
    private readonly IOutboxStore _outbox;
    private readonly IBackgroundJobScheduler _scheduler;
    private readonly IDateTimeProvider _clock;

    public CommunicationScheduleService(IBusinessProfileRepository profiles, ITenantRepository tenants, IOutboxStore outbox, IBackgroundJobScheduler scheduler, IDateTimeProvider clock)
    {
        _profiles = profiles;
        _tenants = tenants;
        _outbox = outbox;
        _scheduler = scheduler;
        _clock = clock;
    }

    public async Task<Result<CommunicationScheduleTimes>> NormalizeAsync(Guid tenantId, DateTime? publishAt, DateTime? expiresAt, bool scheduling, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (scheduling)
        {
            var tenant = await _tenants.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
            if (tenant is null || tenant.Status != TenantStatus.Active)
            {
                return Result.Failure<CommunicationScheduleTimes>(GymErrors.InvalidState);
            }
        }

        return CommunicationScheduleRules.Normalize(publishAt, expiresAt, scheduling, _clock.UtcNow, profile?.TimeZone);
    }

    public async Task SyncAsync(Communication communication, CancellationToken cancellationToken)
    {
        var key = OutboxMessage.PublishKey(communication.Id);
        var existing = await _outbox.FindAsync(communication.TenantId, key, cancellationToken).ConfigureAwait(false);
        if (communication.Status != CommunicationStatus.Scheduled || communication.PublishAt is not DateTime publishAt)
        {
            Cancel(existing);
            return;
        }

        var correlationId = existing?.CorrelationId ?? Guid.NewGuid().ToString("N");
        var payload = JsonSerializer.Serialize(new CommunicationPublishPayload(communication.Id, communication.TenantId, correlationId, publishAt, null));
        if (existing is null)
        {
            await _outbox.AddAsync(OutboxMessage.Create(Guid.NewGuid(), communication.TenantId, "Communication.Publish", payload, publishAt, correlationId, key, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
            return;
        }

        var previous = JsonSerializer.Deserialize<CommunicationPublishPayload>(existing.Payload);
        if (!string.IsNullOrWhiteSpace(previous?.JobId))
        {
            _scheduler.Remove(previous.JobId);
        }

        existing.Reschedule(payload, publishAt, correlationId);
    }

    private void Cancel(OutboxMessage? existing)
    {
        if (existing is null || existing.Status == OutboxMessageStatus.Cancelled)
        {
            return;
        }

        var previous = JsonSerializer.Deserialize<CommunicationPublishPayload>(existing.Payload);
        if (!string.IsNullOrWhiteSpace(previous?.JobId))
        {
            _scheduler.Remove(previous.JobId);
        }

        existing.Cancel(_clock.UtcNow);
    }
}
