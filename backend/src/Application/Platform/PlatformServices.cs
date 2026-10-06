using System.Diagnostics.Metrics;

namespace LifeFitAI.Application.Platform;

public static class PlatformMetrics
{
    public const string MeterName = "LifeFitAI";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> AiRequests = Meter.CreateCounter<long>("lifefit.ai.requests");

    public static readonly Counter<long> RateLimited = Meter.CreateCounter<long>("lifefit.ratelimit.rejected");

    public static readonly Counter<long> Jobs = Meter.CreateCounter<long>("lifefit.jobs.executions");

    public static readonly Counter<long> AuthFailures = Meter.CreateCounter<long>("lifefit.auth.failures");
}

public static class TenantCache
{
    public static string Features(Guid tenantId) => $"tenant:{tenantId:N}:features";

    public static string Feature(Guid tenantId, string featureCode) => $"{Features(tenantId)}:{featureCode}";

    public static string Permissions(Guid tenantId, Guid userId) => $"tenant:{tenantId:N}:permissions:{userId:N}";

    public static string Status(Guid tenantId) => $"tenant:{tenantId:N}:status";
}

public interface IPermissionCacheInvalidator
{
    void Schedule(Guid tenantId, Guid userId);

    void ScheduleStatus(Guid tenantId);

    void ScheduleFeatures(Guid tenantId);

    void Discard();

    Task FlushAsync(CancellationToken cancellationToken);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken);

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken);

    Task RemoveAsync(string key, CancellationToken cancellationToken);
}

public interface ISecretProvider
{
    string? Get(string name);
}

public interface IFileStorage
{
    bool IsSafeKey(string key);
}

public interface IIdempotencyStore
{
    Task<bool> ExistsAsync(Guid tenantId, Guid userId, string operation, string key, CancellationToken cancellationToken);

    Task RememberAsync(Guid tenantId, Guid userId, string operation, string key, int statusCode, CancellationToken cancellationToken);
}

public interface IDataExportService
{
    Task<PersonalExport> ExportPersonalAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}

public sealed record PersonalExport(Guid TenantId, Guid UserId, string? Goal, string? FitnessLevel, int MeasurementCount);

public interface IBackgroundJob
{
    string Name { get; }

    Task ExecuteAsync(CancellationToken cancellationToken);
}
