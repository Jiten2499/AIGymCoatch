using System.Linq.Expressions;
using Hangfire;
using Hangfire.States;
using LifeFitAI.Application.Platform.BackgroundJobs;
using LifeFitAI.Application.Platform.SystemJobs;
using LifeFitAI.Domain.Platform;

namespace LifeFitAI.Infrastructure.Platform;

public sealed class HangfireJobScheduler : IBackgroundJobScheduler
{
    private readonly IBackgroundJobClient _client;
    private readonly JobStorage _storage;
    private readonly IRecurringJobManager _recurring;

    public JobStorage Storage => _storage;

    public HangfireJobScheduler(IBackgroundJobClient client, JobStorage storage)
    {
        _client = client;
        _storage = storage;
        _recurring = new RecurringJobManager(storage);
    }

    public string Enqueue(BackgroundJobMetadata metadata, Expression<Func<Task>> method)
    {
        var queue = string.IsNullOrWhiteSpace(metadata.Queue) ? "default" : metadata.Queue.Trim();
        var id = _client.Create(Hangfire.Common.Job.FromExpression(method), new EnqueuedState(queue));
        using var connection = _storage.GetConnection();
        Set(connection, id, "JobType", metadata.JobType);
        Set(connection, id, "Queue", queue);
        Set(connection, id, "TenantId", metadata.TenantId?.ToString("D"));
        Set(connection, id, "CorrelationId", metadata.CorrelationId);
        Set(connection, id, "SourceType", metadata.SourceType);
        Set(connection, id, "SourceId", metadata.SourceId);
        Set(connection, id, "CreatedBy", metadata.CreatedByUserId?.ToString("D"));
        return id;
    }

    public string Schedule<T>(BackgroundJobMetadata metadata, Expression<Func<T, Task>> method, DateTime enqueueAtUtc)
    {
        var queue = string.IsNullOrWhiteSpace(metadata.Queue) ? "default" : metadata.Queue.Trim();
        var when = DateTime.SpecifyKind(enqueueAtUtc, DateTimeKind.Utc);
        var id = _client.Create(Hangfire.Common.Job.FromExpression(method), new ScheduledState(when));
        using var connection = _storage.GetConnection();
        Set(connection, id, "JobType", metadata.JobType);
        Set(connection, id, "Queue", queue);
        Set(connection, id, "TenantId", metadata.TenantId?.ToString("D"));
        Set(connection, id, "CorrelationId", metadata.CorrelationId);
        Set(connection, id, "SourceType", metadata.SourceType);
        Set(connection, id, "SourceId", metadata.SourceId);
        Set(connection, id, "CreatedBy", metadata.CreatedByUserId?.ToString("D"));
        return id;
    }

    public bool Remove(string jobId) => !string.IsNullOrWhiteSpace(jobId) && _client.Delete(jobId);

    public void SyncRecurring(RecurringJobRegistration registration)
    {
        if (!registration.IsEnabled || registration.JobType != SystemJobCatalog.MembershipExpiryType)
        {
            _recurring.RemoveIfExists(registration.JobKey);
            return;
        }

        var queue = string.IsNullOrWhiteSpace(registration.Queue) ? SystemJobCatalog.NotificationsQueue : registration.Queue.Trim();
        var options = new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc };
#pragma warning disable CS0618
        options.QueueName = queue;
#pragma warning restore CS0618
        _recurring.AddOrUpdate<MembershipExpiryNotificationJob>(registration.JobKey, job => job.RunAsync(false), registration.CronExpression, options);
    }

    public string EnqueueSystemJob(RecurringJobRegistration registration, BackgroundJobMetadata metadata)
    {
        if (registration.JobType != SystemJobCatalog.MembershipExpiryType)
        {
            throw new InvalidOperationException("This system job cannot be started.");
        }

        var queue = string.IsNullOrWhiteSpace(metadata.Queue) ? registration.Queue : metadata.Queue.Trim();
        var id = _client.Create(Hangfire.Common.Job.FromExpression<MembershipExpiryNotificationJob>(job => job.RunAsync(true)), new EnqueuedState(queue));
        using var connection = _storage.GetConnection();
        Set(connection, id, "JobType", metadata.JobType);
        Set(connection, id, "Queue", queue);
        Set(connection, id, "TenantId", metadata.TenantId?.ToString("D"));
        Set(connection, id, "CorrelationId", metadata.CorrelationId);
        Set(connection, id, "SourceType", metadata.SourceType);
        Set(connection, id, "SourceId", metadata.SourceId);
        Set(connection, id, "CreatedBy", metadata.CreatedByUserId?.ToString("D"));
        return id;
    }

    private static void Set(Hangfire.Storage.IStorageConnection connection, string id, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            connection.SetJobParameter(id, name, value);
        }
    }
}
