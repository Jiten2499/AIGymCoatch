namespace LifeFitAI.Domain.Platform;

public static class SystemJobCatalog
{
    public const string MembershipExpiryKey = "MEMBERSHIP_EXPIRY_NOTIFICATION";

    public const string MembershipExpiryType = "MembershipExpiryNotificationJob";

    public const string DailyCron = "0 1 * * *";

    public const string NotificationsQueue = "notifications";

    public static string DescribeSchedule(string cronExpression) =>
        cronExpression == DailyCron ? "Daily at 01:00 UTC" : cronExpression;
}

public sealed class SystemJobDefinition
{
    private SystemJobDefinition()
    {
    }

    public Guid Id { get; private set; }

    public string JobKey { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string JobType { get; private set; } = string.Empty;

    public string CronExpression { get; private set; } = string.Empty;

    public string Queue { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public string? FeatureKey { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public static SystemJobDefinition Create(Guid id, string jobKey, string name, string? description, string jobType, string cronExpression, string queue, bool isEnabled, string? featureKey, DateTime utcNow) =>
        new()
        {
            Id = id,
            JobKey = jobKey,
            Name = name,
            Description = description,
            JobType = jobType,
            CronExpression = cronExpression,
            Queue = queue,
            IsEnabled = isEnabled,
            FeatureKey = featureKey,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

    public void SetEnabled(bool isEnabled, DateTime utcNow)
    {
        IsEnabled = isEnabled;
        UpdatedAt = utcNow;
    }
}
