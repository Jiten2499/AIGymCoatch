using System.Text.Json;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform.BackgroundJobs;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Platform;
using LifeFitAI.Infrastructure.Notifications;
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
        await QueuePushAsync(services, db, communication, recipients, utcNow, cancellationToken).ConfigureAwait(false);
        message.MarkDispatched(message.Payload, utcNow);
    }

    private static async Task QueuePushAsync(IServiceProvider services, ApplicationDbContext db, Communication communication, IReadOnlyList<Guid> recipients, DateTime utcNow, CancellationToken cancellationToken)
    {
        var already = await db.Notifications.AsNoTracking()
            .Where(item => item.TenantId == communication.TenantId && item.ReferenceId == communication.Id && item.Type == NotificationType.CircularPublished)
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var seen = already.ToHashSet();
        var created = new List<Guid>();
        var headline = string.IsNullOrWhiteSpace(communication.Title) ? "A new circular was published." : communication.Title.Trim();
        if (headline.Length > 180)
        {
            headline = headline[..180];
        }

        foreach (var userId in recipients.Distinct())
        {
            if (userId == Guid.Empty || !seen.Add(userId))
            {
                continue;
            }

            var alert = Notification.Create(Guid.NewGuid(), userId, communication.TenantId, NotificationType.CircularPublished, "New circular", headline, utcNow);
            alert.Bind("Communication", communication.Id, $"circular.published:{communication.Id:D}:{userId:D}");
            db.Notifications.Add(alert);
            created.Add(alert.Id);
        }

        if (created.Count == 0)
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var scheduler = services.GetRequiredService<IBackgroundJobScheduler>();
        foreach (var notificationId in created)
        {
            scheduler.Schedule<PushDispatchJob>(
                new BackgroundJobMetadata("Push.Deliver", SystemJobCatalog.NotificationsQueue, communication.TenantId, null, "Notification", notificationId.ToString("D"), null),
                job => job.DeliverAsync(notificationId),
                utcNow);
        }
    }
}
