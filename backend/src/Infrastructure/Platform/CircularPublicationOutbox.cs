using System.Text.Json;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Platform;
using LifeFitAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Platform;

public static class CircularPublicationOutbox
{
    public static async Task QueueAsync(ApplicationDbContext db, Communication communication, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (communication.Status != CommunicationStatus.Published || communication.PublishedAt is not DateTime publishedAt)
        {
            return;
        }

        var key = CircularPublication.Key(communication.Id);
        var queued = db.OutboxMessages.Local.Any(item => item.TenantId == communication.TenantId && item.IdempotencyKey == key)
            || await db.OutboxMessages.AnyAsync(item => item.TenantId == communication.TenantId && item.IdempotencyKey == key, cancellationToken).ConfigureAwait(false);
        if (queued)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new CircularPublishedNotice(communication.Id, communication.TenantId, publishedAt, communication.AudienceType.ToString()));
        await db.OutboxMessages.AddAsync(
            OutboxMessage.Create(Guid.NewGuid(), communication.TenantId, CircularPublication.MessageType, payload, utcNow, null, key, utcNow),
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task ReopenAsync(ApplicationDbContext db, Communication communication, DateTime utcNow, CancellationToken cancellationToken)
    {
        var key = CircularPublication.Key(communication.Id);
        var message = db.OutboxMessages.Local.FirstOrDefault(item => item.TenantId == communication.TenantId && item.IdempotencyKey == key)
            ?? await db.OutboxMessages.FirstOrDefaultAsync(item => item.TenantId == communication.TenantId && item.IdempotencyKey == key, cancellationToken).ConfigureAwait(false);
        if (message is null)
        {
            await QueueAsync(db, communication, utcNow, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (message.Status != OutboxMessageStatus.Pending)
        {
            message.Reschedule(message.Payload, utcNow, message.CorrelationId);
        }
    }
}
