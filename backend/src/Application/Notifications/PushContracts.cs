using LifeFitAI.Domain.Platform;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Notifications;

public static class PushErrors
{
    public static readonly Error InvalidDevice = new("push.invalid_device", "The device registration is not valid.");

    public static readonly Error TooManyDevices = new("push.too_many_devices", "This account already has the maximum number of devices.");
}

public sealed record RegisterPushDeviceCommand(string InstallationId, string ExpoPushToken, string Platform, string? AppVersion) : MediatR.IRequest<Result<PushDeviceResponse>>;

public sealed record DeactivatePushDeviceCommand(string InstallationId) : MediatR.IRequest<Result>;

public sealed record GetNotificationPreferencesQuery : MediatR.IRequest<Result<NotificationPreferenceResponse>>;

public sealed record UpdateNotificationPreferencesCommand(bool ChatEnabled, bool WorkoutRemindersEnabled, bool WorkoutUpdatesEnabled, bool ProgressEnabled, bool AccountEnabled, bool SoundEnabled, bool VibrationEnabled) : MediatR.IRequest<Result<NotificationPreferenceResponse>>;

public sealed record MarkAllNotificationsReadCommand : MediatR.IRequest<Result>;

public sealed record PushDeviceResponse(Guid Id, string InstallationId, string Platform, bool IsActive, DateTime LastRegisteredAt);

public sealed record NotificationPreferenceResponse(bool ChatEnabled, bool WorkoutRemindersEnabled, bool WorkoutUpdatesEnabled, bool ProgressEnabled, bool AccountEnabled, bool SoundEnabled, bool VibrationEnabled);

public interface IPushStore
{
    Task<PushDevice?> FindInstallationAsync(Guid userId, string installationId, CancellationToken cancellationToken);

    Task<PushDevice?> FindActiveTokenAsync(string token, CancellationToken cancellationToken);

    Task<int> CountActiveAsync(Guid userId, CancellationToken cancellationToken);

    void Add(PushDevice device);

    Task<NotificationPreference?> FindPreferenceAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    void Add(NotificationPreference preference);

    Task MarkAllReadAsync(Guid tenantId, Guid userId, DateTime utcNow, CancellationToken cancellationToken);
}
