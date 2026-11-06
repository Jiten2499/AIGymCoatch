using System.Text.Json;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Platform;
using LifeFitAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LifeFitAI.Infrastructure.Platform;

public static class CircularPublishedDelivery
{
    public static async Task ApplyAsync(IServiceProvider services, OutboxMessage message, DateTime utcNow, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<CircularPublishedNotice>(message.Payload);
        if (payload is null || payload.TenantId != message.TenantId || payload.CommunicationId == Guid.Empty)
        {
            message.Fail("The circular publication payload could not be read.");
            message.Cancel(utcNow);
            return;
        }

        var features = services.GetRequiredService<IFeatureFlagService>();
        if (!await features.IsEnabledAsync(payload.TenantId, FeatureCodes.GymCommunications, cancellationToken).ConfigureAwait(false))
        {
            message.Cancel(utcNow);
            return;
        }

        var db = services.GetRequiredService<ApplicationDbContext>();
        var communication = await db.Communications.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == payload.CommunicationId && item.TenantId == payload.TenantId, cancellationToken)
            .ConfigureAwait(false);
        if (communication is null || !communication.IsVisibleToMembers(utcNow))
        {
            message.Cancel(utcNow);
            return;
        }

        var recipients = await db.CommunicationRecipients.AsNoTracking()
            .Where(item => item.TenantId == payload.TenantId && item.CommunicationId == payload.CommunicationId && item.DeletedAt == null)
            .Select(item => item.UserId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var notice = new CircularPublishedNotice(communication.Id, communication.TenantId, communication.PublishedAt ?? payload.PublishedAtUtc, communication.AudienceType.ToString());
        await services.GetRequiredService<ICircularNotifier>().PublishedAsync(notice, recipients, cancellationToken).ConfigureAwait(false);
        message.MarkDispatched(message.Payload, utcNow);
    }
}
