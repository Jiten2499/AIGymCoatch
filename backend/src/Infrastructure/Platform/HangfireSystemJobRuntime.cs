using Hangfire;
using Hangfire.Storage;
using LifeFitAI.Application.Platform.BackgroundJobs;
using LifeFitAI.Application.Platform.SystemJobs;

namespace LifeFitAI.Infrastructure.Platform;

public sealed class HangfireSystemJobRuntime : ISystemJobRuntime
{
    private readonly JobStorage _storage;

    public HangfireSystemJobRuntime(JobStorage storage) => _storage = storage;

    public SystemJobRuntime Read(string jobKey)
    {
        using var connection = _storage.GetConnection();
        var recurring = connection.GetRecurringJobs().FirstOrDefault(job => job.Id == jobKey);
        if (recurring is null)
        {
            return new SystemJobRuntime(null, null, null, null, null, null);
        }

        string? error = string.IsNullOrWhiteSpace(recurring.Error) ? null : BackgroundJobSafety.Redact(recurring.Error);
        double? duration = null;
        string? result = null;
        if (!string.IsNullOrWhiteSpace(recurring.LastJobId))
        {
            var details = _storage.GetMonitoringApi().JobDetails(recurring.LastJobId);
            var succeeded = details?.History.LastOrDefault(item => item.StateName == "Succeeded");
            var failed = details?.History.LastOrDefault(item => item.StateName == "Failed");
            if (succeeded?.Data is not null && succeeded.Data.TryGetValue("Result", out var raw))
            {
                result = raw;
            }

            if (failed?.Data is not null && failed.Data.TryGetValue("ExceptionMessage", out var message))
            {
                error = BackgroundJobSafety.Redact(message);
            }

            var started = details?.History.FirstOrDefault(item => item.StateName is "Processing" or "Enqueued")?.CreatedAt;
            var ended = succeeded?.CreatedAt ?? failed?.CreatedAt;
            if (started is not null && ended is not null)
            {
                duration = Math.Max(0, (ended.Value - started.Value).TotalSeconds);
            }
        }

        var status = string.IsNullOrWhiteSpace(recurring.LastJobState) ? null : BackgroundJobSafety.Normalize(recurring.LastJobState).ToString();
        return new SystemJobRuntime(recurring.LastExecution, recurring.NextExecution, status, duration, error, result);
    }
}
