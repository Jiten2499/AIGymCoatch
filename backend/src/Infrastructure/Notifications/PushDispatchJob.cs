using Hangfire;
using LifeFitAI.Application.Chat;
using LifeFitAI.Application.Notifications;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Platform;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LifeFitAI.Infrastructure.Notifications;

public sealed class PushDispatchJob
{
    private readonly ApplicationDbContext _db;
    private readonly IExpoPushClient _expo;
    private readonly IChatPresence _presence;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<PushDispatchJob> _logger;

    public PushDispatchJob(ApplicationDbContext db, IExpoPushClient expo, IChatPresence presence, IDateTimeProvider clock, ILogger<PushDispatchJob> logger)
    {
        _db = db;
        _expo = expo;
        _presence = presence;
        _clock = clock;
        _logger = logger;
    }

    [Queue("notifications")]
    public async Task DeliverAsync(Guid notificationId)
    {
        var notification = await _db.Notifications.FirstOrDefaultAsync(item => item.Id == notificationId).ConfigureAwait(false);
        if (notification is null)
        {
            return;
        }

        if (notification.Type == NotificationType.ChatMessage && notification.ReferenceId is Guid conversationId && _presence.IsWatching(notification.UserId, conversationId))
        {
            return;
        }

        var preference = await _db.NotificationPreferences.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == notification.TenantId && item.UserId == notification.UserId).ConfigureAwait(false);
        if (!PushRules.Allows(preference, notification.Type))
        {
            return;
        }

        var devices = await _db.PushDevices.Where(item => item.UserId == notification.UserId && item.TenantId == notification.TenantId && item.IsActive).ToListAsync().ConfigureAwait(false);
        var pending = new List<(PushDevice Device, PushDelivery Delivery)>();
        foreach (var device in devices)
        {
            var delivery = await _db.PushDeliveries.FirstOrDefaultAsync(item => item.NotificationId == notification.Id && item.DeviceId == device.Id).ConfigureAwait(false);
            if (delivery is null)
            {
                delivery = PushDelivery.Queue(Guid.NewGuid(), notification.Id, device.Id, notification.TenantId, _clock.UtcNow);
                _db.PushDeliveries.Add(delivery);
            }

            if (delivery.Status is PushDeliveryStatus.Accepted or PushDeliveryStatus.ProviderConfirmed || !delivery.CanSend)
            {
                continue;
            }

            pending.Add((device, delivery));
        }

        if (pending.Count == 0)
        {
            await _db.SaveChangesAsync().ConfigureAwait(false);
            return;
        }

        IReadOnlyList<ExpoSendTicket> tickets;
        try
        {
            tickets = await _expo.SendAsync(pending.Select(item => Message(notification, preference, item.Device)).ToArray(), CancellationToken.None).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Expo push request failed for notification {NotificationId}", notification.Id);
            throw;
        }

        for (var index = 0; index < pending.Count; index++)
        {
            var ticket = index < tickets.Count ? tickets[index] : new ExpoSendTicket(false, null, "MissingTicket");
            var pair = pending[index];
            if (ticket.Accepted)
            {
                pair.Delivery.Accept(ticket.TicketId, _clock.UtcNow);
                continue;
            }

            pair.Delivery.Fail(ticket.Error ?? "PushRejected", _clock.UtcNow);
            if (ExpoReceipts.DeviceGone(ticket.Error))
            {
                pair.Device.Disable(_clock.UtcNow);
            }
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static ExpoPushMessage Message(Notification notification, NotificationPreference? preference, PushDevice device)
    {
        var data = new Dictionary<string, string>
        {
            ["type"] = PushRules.DataType(notification.Type),
            ["tenantId"] = notification.TenantId.ToString("D"),
            ["notificationId"] = notification.Id.ToString("D"),
        };
        if (notification.ReferenceId is Guid referenceId)
        {
            data["referenceId"] = referenceId.ToString("D");
            if (notification.Type == NotificationType.ChatMessage)
            {
                data["conversationId"] = referenceId.ToString("D");
            }
        }

        if (notification.ReferenceType is not null)
        {
            data["referenceType"] = notification.ReferenceType;
        }

        var messageId = PushRules.MessageId(notification);
        if (messageId is not null)
        {
            data["messageId"] = messageId;
        }

        var chat = notification.Type == NotificationType.ChatMessage;
        return new ExpoPushMessage(device.ExpoPushToken, notification.Title, PushRules.PushBody(notification.Type, notification.Message), preference?.SoundEnabled == false ? null : "default", PushRules.Channel(notification.Type), chat ? "high" : "default", data);
    }
}
