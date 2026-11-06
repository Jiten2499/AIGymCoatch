using LifeFitAI.Domain.Platform;

namespace LifeFitAI.Application.Platform.SystemJobs;

public static class MembershipExpiryReminderTypes
{
    public const string TenDays = "EXPIRING_IN_10_DAYS";

    public const string FiveDays = "EXPIRING_IN_5_DAYS";

    public const string Today = "EXPIRING_TODAY";
}

public static class MembershipExpiryMessages
{
    public const string MessageType = "Membership.ExpiryNotification";

    public static string IdempotencyKey(Guid membershipId, string reminderType, DateOnly expiry) =>
        $"Membership.Expiry:{membershipId:D}:{reminderType}:{expiry:yyyy-MM-dd}";

    public static (string Title, string Message) Compose(string reminderType, DateOnly expiry)
    {
        var date = expiry.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        return reminderType switch
        {
            MembershipExpiryReminderTypes.TenDays => ("Membership expiry", $"Your gym membership expires in 10 days on {date}."),
            MembershipExpiryReminderTypes.FiveDays => ("Membership expiry", $"Your gym membership expires in 5 days on {date}."),
            MembershipExpiryReminderTypes.Today => ("Membership expiry", "Your gym membership expires today."),
            _ => throw new ArgumentOutOfRangeException(nameof(reminderType)),
        };
    }
}

public sealed record MembershipExpiryPayload(
    Guid TenantId,
    Guid UserId,
    Guid MembershipId,
    string ReminderType,
    DateOnly ExpiryDate,
    string Title,
    string Message,
    Guid? NotificationId);

public sealed record MembershipExpiryRun(int Processed, int TenDay, int FiveDay, int Today, int Skipped, int Failed, bool Disabled);

public sealed record RecurringJobRegistration(string JobKey, string JobType, string CronExpression, string Queue, bool IsEnabled);

public sealed record SystemJobRuntime(DateTime? LastRunUtc, DateTime? NextRunUtc, string? LastStatus, double? LastDurationSeconds, string? LastError, string? LastResult);

public sealed record SystemJobStatistics(int MembersProcessed, int TenDay, int FiveDay, int Today, int Skipped, int Failed);

public sealed record SystemJobListItem(
    string JobKey,
    string Name,
    string JobType,
    string Schedule,
    string Queue,
    bool IsEnabled,
    DateTime? LastRunUtc,
    DateTime? NextRunUtc,
    string? LastStatus);

public sealed record SystemJobDetails(
    string JobKey,
    string Name,
    string? Description,
    string JobType,
    string Schedule,
    string CronExpression,
    string Queue,
    bool IsEnabled,
    string? FeatureKey,
    DateTime? LastRunUtc,
    string? LastStatus,
    double? LastDurationSeconds,
    DateTime? NextRunUtc,
    string? LastError,
    SystemJobStatistics? Statistics);

public interface ISystemJobCatalog
{
    Task<IReadOnlyList<SystemJobDefinition>> ListAsync(CancellationToken cancellationToken);

    Task<SystemJobDefinition?> FindAsync(string jobKey, CancellationToken cancellationToken);
}

public interface ISystemJobRuntime
{
    SystemJobRuntime Read(string jobKey);
}

public interface IMembershipExpiryProcessor
{
    Task<MembershipExpiryRun> ExecuteAsync(CancellationToken cancellationToken);
}

public static class SystemJobStatisticsReader
{
    public static SystemJobStatistics? Read(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        if (text.StartsWith('"'))
        {
            text = System.Text.Json.JsonSerializer.Deserialize<string>(text) ?? text;
        }

        var run = System.Text.Json.JsonSerializer.Deserialize<MembershipExpiryRun>(text, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return run is null || run.Disabled
            ? null
            : new SystemJobStatistics(run.Processed, run.TenDay, run.FiveDay, run.Today, run.Skipped, run.Failed);
    }
}

public static class SystemJobMapping
{
    public static RecurringJobRegistration Registration(SystemJobDefinition job) =>
        new(job.JobKey, job.JobType, job.CronExpression, job.Queue, job.IsEnabled);
}
