using LifeFitAI.Application.Email;
using LifeFitAI.Domain.Email;
using LifeFitAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LifeFitAI.Infrastructure.Email;

public sealed class EmailConfigStore : IEmailConfigStore
{
    private readonly ApplicationDbContext _db;

    public EmailConfigStore(ApplicationDbContext db) => _db = db;

    public Task<EmailProviderConfiguration?> GetActiveAsync(CancellationToken cancellationToken) =>
        _db.EmailProviderConfigurations.FirstOrDefaultAsync(item => item.IsActive, cancellationToken);

    public Task<EmailProviderConfiguration?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _db.EmailProviderConfigurations.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task AddAsync(EmailProviderConfiguration configuration, CancellationToken cancellationToken) =>
        _db.EmailProviderConfigurations.AddAsync(configuration, cancellationToken).AsTask();

    public async Task ClearOtherActiveAsync(Guid keepId, CancellationToken cancellationToken)
    {
        var others = await _db.EmailProviderConfigurations.Where(item => item.IsActive && item.Id != keepId).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var other in others)
        {
            other.Deactivate();
        }
    }
}

public sealed class EmailDeliveryStore : IEmailDeliveryStore
{
    private readonly ApplicationDbContext _db;

    public EmailDeliveryStore(ApplicationDbContext db) => _db = db;

    public Task AddAsync(EmailDeliveryLog log, CancellationToken cancellationToken) =>
        _db.EmailDeliveryLogs.AddAsync(log, cancellationToken).AsTask();

    public Task<EmailDeliveryLog?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _db.EmailDeliveryLogs.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<EmailDeliveryLog?> FindByIdempotencyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        _db.EmailDeliveryLogs.FirstOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<(IReadOnlyList<EmailDeliveryLog> Items, int Total)> ListAsync(
        string? search,
        string? status,
        string? templateKey,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _db.EmailDeliveryLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Recipient.Contains(term) || item.Subject.Contains(term) || (item.ProviderMessageId != null && item.ProviderMessageId.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(templateKey) && !string.Equals(templateKey, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(item => item.TemplateKey == templateKey);
        }

        if (fromUtc is DateTime from)
        {
            query = query.Where(item => item.CreatedAt >= from);
        }

        if (toUtc is DateTime to)
        {
            query = query.Where(item => item.CreatedAt <= to);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return (items, total);
    }
}

public sealed class MemoryEmailConfigCache : IEmailConfigCache
{
    private const string CacheKey = "email:active-config-version";
    private readonly IMemoryCache _cache;
    private readonly IEmailConfigStore _store;

    public MemoryEmailConfigCache(IMemoryCache cache, IEmailConfigStore store)
    {
        _cache = cache;
        _store = store;
    }

    public void Invalidate() => _cache.Remove(CacheKey);

    public async Task<EmailProviderConfiguration?> GetActiveAsync(CancellationToken cancellationToken)
    {
        // Always load from SQL so workers pick up saves without restart. The cache key is only used for invalidation signals.
        _ = _cache.TryGetValue(CacheKey, out int _);
        var configuration = await _store.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (configuration is not null)
        {
            _cache.Set(CacheKey, configuration.ConfigVersion, TimeSpan.FromMinutes(5));
        }

        return configuration;
    }
}
