using System.Linq.Expressions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Platform.SystemJobs;

namespace LifeFitAI.Application.Platform.BackgroundJobs;

public enum BackgroundJobStatus
{
    Queued = 0,
    Scheduled = 1,
    Processing = 2,
    Succeeded = 3,
    Failed = 4,
    Deleted = 5,
    Awaiting = 6,
}

public sealed record BackgroundJobMetadata(
    string JobType,
    string Queue,
    Guid? TenantId,
    string? CorrelationId,
    string? SourceType,
    string? SourceId,
    Guid? CreatedByUserId);

public sealed record BackgroundJobListItem(
    string JobId,
    string JobType,
    string Queue,
    BackgroundJobStatus Status,
    Guid? TenantId,
    string? TenantName,
    string? CorrelationId,
    DateTime CreatedAt,
    DateTime? ScheduledAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    double? DurationSeconds,
    int AttemptCount,
    string? ErrorMessage,
    Guid? CreatedByUserId,
    string? SourceType,
    string? SourceId);

public sealed record BackgroundJobStateChange(BackgroundJobStatus Status, DateTime Timestamp, string? Reason);

public sealed record BackgroundJobDetails(
    BackgroundJobListItem Job,
    IReadOnlyList<BackgroundJobStateChange> History);

public sealed record BackgroundJobSummary(
    int Queued,
    int Scheduled,
    int Processing,
    int Succeeded,
    int Failed,
    int JobsToday,
    int JobsLast24Hours,
    double? AverageDurationSeconds,
    IReadOnlyList<string> Queues);

public sealed record BackgroundJobSearch(
    string? Term,
    string? JobType,
    BackgroundJobStatus? Status,
    string? Queue,
    Guid? TenantId,
    DateTime? From,
    DateTime? To,
    string? CorrelationId,
    string? SourceType,
    int Page,
    int PageSize,
    string? Sort,
    bool Descending);

public interface IBackgroundJobScheduler
{
    string Enqueue(BackgroundJobMetadata metadata, Expression<Func<Task>> method);

    string Schedule<T>(BackgroundJobMetadata metadata, Expression<Func<T, Task>> method, DateTime enqueueAtUtc);

    bool Remove(string jobId);

    void SyncRecurring(RecurringJobRegistration registration);

    string EnqueueSystemJob(RecurringJobRegistration registration, BackgroundJobMetadata metadata);
}

public interface IBackgroundJobMonitor
{
    Task<PageResult<BackgroundJobListItem>> SearchAsync(BackgroundJobSearch search, CancellationToken cancellationToken);

    Task<BackgroundJobDetails?> GetAsync(string jobId, CancellationToken cancellationToken);

    Task<BackgroundJobSummary> SummaryAsync(Guid? tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> QueuesAsync(CancellationToken cancellationToken);

    Task<bool> RetryAsync(string jobId, CancellationToken cancellationToken);

    Task<bool> CancelAsync(string jobId, CancellationToken cancellationToken);
}
