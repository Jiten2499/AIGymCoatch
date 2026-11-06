using System.Text.Json;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform.SystemJobs;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Platform;
using LifeFitAI.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LifeFitAI.Infrastructure.Platform;

public static class MembershipExpiryDelivery
{
    public static async Task ApplyAsync(IServiceProvider services, OutboxMessage message, DateTime utcNow, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<MembershipExpiryPayload>(message.Payload);
        if (payload is null)
        {
            message.Fail("The expiry reminder payload could not be read.");
            return;
        }

        var features = services.GetRequiredService<IFeatureFlagService>();
        if (!await features.IsEnabledAsync(payload.TenantId, FeatureCodes.GymCommunications, cancellationToken).ConfigureAwait(false))
        {
            message.Cancel(utcNow);
            return;
        }

        if (payload.NotificationId is Guid)
        {
            message.MarkDispatched(message.Payload, utcNow);
            return;
        }

        var notification = Notification.Create(Guid.NewGuid(), payload.UserId, payload.TenantId, NotificationType.MembershipReminder, payload.Title, payload.Message, utcNow);
        services.GetRequiredService<ApplicationDbContext>().Notifications.Add(notification);
        message.MarkDispatched(JsonSerializer.Serialize(payload with { NotificationId = notification.Id }), utcNow);
    }
}
