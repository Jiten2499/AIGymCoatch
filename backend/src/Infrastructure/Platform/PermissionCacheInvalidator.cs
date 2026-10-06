using LifeFitAI.Application.Platform;
using Microsoft.Extensions.Logging;

namespace LifeFitAI.Infrastructure.Platform;

public sealed class PermissionCacheInvalidator : IPermissionCacheInvalidator
{
    private readonly ICacheService _cache;
    private readonly ILogger<PermissionCacheInvalidator> _logger;
    private readonly HashSet<(Guid TenantId, Guid UserId)> _pending = [];
    private readonly HashSet<Guid> _pendingStatus = [];
    private readonly HashSet<Guid> _pendingFeatures = [];

    public PermissionCacheInvalidator(ICacheService cache, ILogger<PermissionCacheInvalidator> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public void Schedule(Guid tenantId, Guid userId) => _pending.Add((tenantId, userId));

    public void ScheduleStatus(Guid tenantId) => _pendingStatus.Add(tenantId);

    public void ScheduleFeatures(Guid tenantId) => _pendingFeatures.Add(tenantId);

    public void Discard()
    {
        _pending.Clear();
        _pendingStatus.Clear();
        _pendingFeatures.Clear();
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        var pending = _pending.ToArray();
        _pending.Clear();
        foreach (var (tenantId, userId) in pending)
        {
            try
            {
                await _cache.RemoveAsync(TenantCache.Permissions(tenantId, userId), cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("PermissionCacheInvalidated {TenantId} {UserId}", tenantId, userId);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "PermissionCacheInvalidationFailed {TenantId} {UserId} {ExceptionType}", tenantId, userId, exception.GetType().Name);
            }
        }

        var features = _pendingFeatures.ToArray();
        _pendingFeatures.Clear();
        foreach (var tenantId in features)
        {
            foreach (var code in LifeFitAI.Domain.Identity.FeatureCodes.All)
            {
                try
                {
                    await _cache.RemoveAsync(TenantCache.Feature(tenantId, code), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "FeatureCacheInvalidationFailed {TenantId} {ExceptionType}", tenantId, exception.GetType().Name);
                }
            }

            _logger.LogInformation("FeatureCacheInvalidated {TenantId}", tenantId);
        }

        var statuses = _pendingStatus.ToArray();
        _pendingStatus.Clear();
        foreach (var tenantId in statuses)
        {
            try
            {
                await _cache.RemoveAsync(TenantCache.Status(tenantId), cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("TenantStatusCacheInvalidated {TenantId}", tenantId);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "TenantStatusCacheInvalidationFailed {TenantId} {ExceptionType}", tenantId, exception.GetType().Name);
            }
        }
    }
}
