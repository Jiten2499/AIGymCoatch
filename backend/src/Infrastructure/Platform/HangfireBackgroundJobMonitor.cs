using Hangfire;
using Hangfire.Storage.Monitoring;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform.BackgroundJobs;

namespace LifeFitAI.Infrastructure.Platform;

public sealed partial class HangfireBackgroundJobMonitor : IBackgroundJobMonitor
{
    private readonly JobStorage _storage;
    private readonly string? _connectionString;
    private readonly ITenantRepository _tenants;

    public HangfireBackgroundJobMonitor(JobStorage storage, string? connectionString, ITenantRepository tenants)
    {
        _storage = storage;
        _connectionString = connectionString;
        _tenants = tenants;
    }

    public async Task<PageResult<BackgroundJobListItem>> SearchAsync(BackgroundJobSearch search, CancellationToken cancellationToken)
    {
        var page = new PageQuery(search.Page, search.PageSize);
        var (ids, total) = string.IsNullOrWhiteSpace(_connectionString)
            ? MemoryPage(search, page)
            : await SqlPageAsync(search, page, cancellationToken).ConfigureAwait(false);
        var items = new List<BackgroundJobListItem>();
        foreach (var id in ids)
        {
            var details = await GetAsync(id, cancellationToken).ConfigureAwait(false);
            if (details is not null)
            {
                items.Add(details.Job);
            }
        }

        return new PageResult<BackgroundJobListItem>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public async Task<BackgroundJobDetails?> GetAsync(string jobId, CancellationToken cancellationToken)
    {
        var details = _storage.GetMonitoringApi().JobDetails(jobId);
        if (details is null)
        {
            return null;
        }

        var parameters = details.Properties ?? new Dictionary<string, string>();
        var history = (details.History ?? []).OrderBy(item => item.CreatedAt).ToList();
        var status = BackgroundJobSafety.Normalize(history.LastOrDefault()?.StateName);
        var started = history.FirstOrDefault(item => item.StateName == "Processing")?.CreatedAt;
        var completed = history.LastOrDefault(item => item.StateName is "Succeeded" or "Failed" or "Deleted")?.CreatedAt;
        var scheduled = history.FirstOrDefault(item => item.StateName == "Scheduled")?.CreatedAt;
        var error = history.LastOrDefault(item => item.StateName == "Failed");
        var tenantId = ParseGuid(Read(parameters, "TenantId"));
        var item = new BackgroundJobListItem(
            jobId,
            Read(parameters, "JobType") ?? "background-job",
            Read(parameters, "Queue") ?? "default",
            status,
            tenantId,
            await TenantNameAsync(tenantId, cancellationToken).ConfigureAwait(false),
            Read(parameters, "CorrelationId"),
            details.CreatedAt ?? history.FirstOrDefault()?.CreatedAt ?? DateTime.UtcNow,
            scheduled,
            started,
            completed,
            started is DateTime start && completed is DateTime end ? Math.Max(0, (end - start).TotalSeconds) : null,
            Math.Max(history.Count(item => item.StateName == "Processing"), status == BackgroundJobStatus.Failed ? 1 : 0),
            BackgroundJobSafety.Redact(ReadData(error?.Data, "ExceptionMessage") ?? error?.Reason),
            ParseGuid(Read(parameters, "CreatedBy")),
            Read(parameters, "SourceType"),
            Read(parameters, "SourceId"));
        var changes = history.Select(state => new BackgroundJobStateChange(
            BackgroundJobSafety.Normalize(state.StateName),
            state.CreatedAt,
            BackgroundJobSafety.Redact(ReadData(state.Data, "ExceptionMessage") ?? state.Reason))).ToList();
        return new BackgroundJobDetails(item, changes);
    }

    public async Task<BackgroundJobSummary> SummaryAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        var search = new BackgroundJobSearch(null, null, null, null, tenantId, null, null, null, null, 1, 1, null, true);
        var counts = string.IsNullOrWhiteSpace(_connectionString)
            ? MemoryCounts(tenantId)
            : await SqlCountsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var today = DateTime.UtcNow.Date;
        var lastDay = await SearchAsync(search with { From = DateTime.UtcNow.AddHours(-24), Page = 1, PageSize = 1 }, cancellationToken).ConfigureAwait(false);
        var todays = await SearchAsync(search with { From = today, Page = 1, PageSize = 1 }, cancellationToken).ConfigureAwait(false);
        return new BackgroundJobSummary(
            counts.GetValueOrDefault(BackgroundJobStatus.Queued),
            counts.GetValueOrDefault(BackgroundJobStatus.Scheduled),
            counts.GetValueOrDefault(BackgroundJobStatus.Processing),
            counts.GetValueOrDefault(BackgroundJobStatus.Succeeded),
            counts.GetValueOrDefault(BackgroundJobStatus.Failed),
            todays.TotalCount,
            lastDay.TotalCount,
            counts.TryGetValue(BackgroundJobStatus.Succeeded, out var succeeded) && succeeded > 0 ? Average(tenantId) : null,
            await QueuesAsync(cancellationToken).ConfigureAwait(false));
    }

    public Task<IReadOnlyList<string>> QueuesAsync(CancellationToken cancellationToken)
    {
        var names = _storage.GetMonitoringApi().Queues().Select(queue => queue.Name).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
        return Task.FromResult<IReadOnlyList<string>>(names.Count == 0 ? ["default"] : names);
    }

    public Task<bool> RetryAsync(string jobId, CancellationToken cancellationToken) =>
        Task.FromResult(new BackgroundJobClient(_storage).Requeue(jobId));

    public Task<bool> CancelAsync(string jobId, CancellationToken cancellationToken) =>
        Task.FromResult(new BackgroundJobClient(_storage).Delete(jobId));

    private (IReadOnlyList<string> Ids, int Total) MemoryPage(BackgroundJobSearch search, PageQuery page)
    {
        var matched = MemoryJobs(search.TenantId).Where(job => Matches(job, search)).ToList();
        matched.Sort((left, right) => search.Descending ? right.CreatedAt.CompareTo(left.CreatedAt) : left.CreatedAt.CompareTo(right.CreatedAt));
        return (matched.Skip(page.Skip).Take(page.NormalizedSize).Select(job => job.JobId).ToList(), matched.Count);
    }

    private Dictionary<BackgroundJobStatus, int> MemoryCounts(Guid? tenantId)
    {
        return MemoryJobs(tenantId)
            .GroupBy(job => job.Status)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private List<BackgroundJobListItem> MemoryJobs(Guid? tenantId)
    {
        var api = _storage.GetMonitoringApi();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var queue in api.Queues())
        {
            foreach (var job in api.EnqueuedJobs(queue.Name, 0, 200))
            {
                ids.Add(job.Key);
            }
        }

        Collect(ids, api.ScheduledJobs(0, 200));
        Collect(ids, api.ProcessingJobs(0, 200));
        Collect(ids, api.SucceededJobs(0, 200));
        Collect(ids, api.FailedJobs(0, 200));
        Collect(ids, api.DeletedJobs(0, 200));
        var items = new List<BackgroundJobListItem>();
        foreach (var id in ids)
        {
            var details = GetAsync(id, CancellationToken.None).GetAwaiter().GetResult();
            if (details is not null && (tenantId is null || details.Job.TenantId == tenantId))
            {
                items.Add(details.Job);
            }
        }

        return items;
    }

    private static void Collect<T>(HashSet<string> ids, JobList<T> jobs)
    {
        foreach (var job in jobs)
        {
            ids.Add(job.Key);
        }
    }

    private static bool Matches(BackgroundJobListItem job, BackgroundJobSearch search)
    {
        if (search.Status is BackgroundJobStatus status && job.Status != status)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(search.JobType) && !string.Equals(job.JobType, search.JobType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(search.Queue) && !string.Equals(job.Queue, search.Queue, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(search.CorrelationId) && !string.Equals(job.CorrelationId, search.CorrelationId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(search.SourceType) && !string.Equals(job.SourceType, search.SourceType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (search.From is DateTime from && job.CreatedAt < from)
        {
            return false;
        }

        if (search.To is DateTime to && job.CreatedAt > to)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(search.Term)
            && !job.JobId.Contains(search.Term, StringComparison.OrdinalIgnoreCase)
            && !job.JobType.Contains(search.Term, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private double? Average(Guid? tenantId)
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            return null;
        }

        var durations = MemoryJobs(tenantId).Where(job => job.DurationSeconds is not null).Select(job => job.DurationSeconds!.Value).ToList();
        return durations.Count == 0 ? null : durations.Average();
    }

    private async Task<string?> TenantNameAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        if (tenantId is not Guid id)
        {
            return null;
        }

        var tenant = await _tenants.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return tenant?.Name;
    }

    private static string? ReadData(IDictionary<string, string>? data, string name) =>
        data is not null && data.TryGetValue(name, out var value) ? value : null;

    private static string? Read(IDictionary<string, string> parameters, string name) =>
        parameters.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim().Trim('"') : null;

    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var id) ? id : null;
}
