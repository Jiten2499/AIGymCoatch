namespace LifeFitAI.Domain.Platform;

public enum PushDeliveryStatus
{
    Queued = 0,
    Accepted = 1,
    Failed = 2,
    ProviderConfirmed = 3,
}

public sealed class PushDevice
{
    private PushDevice()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public string InstallationId { get; private set; } = string.Empty;

    public string ExpoPushToken { get; private set; } = string.Empty;

    public string Platform { get; private set; } = string.Empty;

    public string? AppVersion { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime LastRegisteredAt { get; private set; }

    public DateTime LastSeenAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public DateTime? DisabledAt { get; private set; }

    public static PushDevice Register(Guid id, Guid userId, Guid tenantId, string installationId, string token, string platform, string? appVersion, DateTime utcNow) =>
        new()
        {
            Id = id,
            UserId = userId,
            TenantId = tenantId,
            InstallationId = installationId,
            ExpoPushToken = token,
            Platform = platform,
            AppVersion = appVersion,
            IsActive = true,
            LastRegisteredAt = utcNow,
            LastSeenAt = utcNow,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

    public void Refresh(Guid tenantId, string token, string platform, string? appVersion, DateTime utcNow)
    {
        TenantId = tenantId;
        ExpoPushToken = token;
        Platform = platform;
        AppVersion = appVersion;
        IsActive = true;
        DisabledAt = null;
        LastRegisteredAt = utcNow;
        LastSeenAt = utcNow;
        UpdatedAt = utcNow;
    }

    public void Disable(DateTime utcNow)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        DisabledAt = utcNow;
        UpdatedAt = utcNow;
    }
}

public sealed class PushDelivery
{
    private PushDelivery()
    {
    }

    public Guid Id { get; private set; }

    public Guid NotificationId { get; private set; }

    public Guid DeviceId { get; private set; }

    public Guid TenantId { get; private set; }

    public PushDeliveryStatus Status { get; private set; }

    public string? TicketId { get; private set; }

    public string? ReceiptError { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public static PushDelivery Queue(Guid id, Guid notificationId, Guid deviceId, Guid tenantId, DateTime utcNow) =>
        new()
        {
            Id = id,
            NotificationId = notificationId,
            DeviceId = deviceId,
            TenantId = tenantId,
            Status = PushDeliveryStatus.Queued,
            AttemptCount = 0,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

    public bool CanSend => Status is PushDeliveryStatus.Queued or PushDeliveryStatus.Failed && AttemptCount < 3;

    public void Accept(string? ticketId, DateTime utcNow)
    {
        Status = PushDeliveryStatus.Accepted;
        TicketId = ticketId;
        ReceiptError = null;
        AttemptCount++;
        UpdatedAt = utcNow;
    }

    public void Fail(string error, DateTime utcNow)
    {
        Status = PushDeliveryStatus.Failed;
        ReceiptError = error.Length <= 200 ? error : error[..200];
        AttemptCount++;
        UpdatedAt = utcNow;
    }

    public void Confirm(DateTime utcNow)
    {
        Status = PushDeliveryStatus.ProviderConfirmed;
        ReceiptError = null;
        UpdatedAt = utcNow;
    }
}

public sealed class NotificationPreference
{
    private NotificationPreference()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public bool ChatEnabled { get; private set; } = true;

    public bool WorkoutRemindersEnabled { get; private set; } = true;

    public bool WorkoutUpdatesEnabled { get; private set; } = true;

    public bool ProgressEnabled { get; private set; } = true;

    public bool AccountEnabled { get; private set; } = true;

    public bool SoundEnabled { get; private set; } = true;

    public bool VibrationEnabled { get; private set; } = true;

    public DateTime UpdatedAt { get; private set; }

    public static NotificationPreference CreateDefault(Guid tenantId, Guid userId, DateTime utcNow) =>
        new()
        {
            TenantId = tenantId,
            UserId = userId,
            ChatEnabled = true,
            WorkoutRemindersEnabled = true,
            WorkoutUpdatesEnabled = true,
            ProgressEnabled = true,
            AccountEnabled = true,
            SoundEnabled = true,
            VibrationEnabled = true,
            UpdatedAt = utcNow,
        };

    public void Update(bool chat, bool reminders, bool updates, bool progress, bool account, bool sound, bool vibration, DateTime utcNow)
    {
        ChatEnabled = chat;
        WorkoutRemindersEnabled = reminders;
        WorkoutUpdatesEnabled = updates;
        ProgressEnabled = progress;
        AccountEnabled = account;
        SoundEnabled = sound;
        VibrationEnabled = vibration;
        UpdatedAt = utcNow;
    }
}
