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
}
