using System.Text.Json;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LifeFitAI.Infrastructure.Platform;

public sealed class EnvironmentSecretProvider : ISecretProvider
{
    private readonly IConfiguration _configuration;

    public EnvironmentSecretProvider(IConfiguration configuration) => _configuration = configuration;

    public string? Get(string name)
    {
        var value = _configuration[name];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage()
        : this(new StorageOptions())
    {
    }

    public LocalFileStorage(IOptions<StorageOptions> options)
        : this(options.Value)
    {
    }

    public LocalFileStorage(StorageOptions options)
    {
        var root = string.IsNullOrWhiteSpace(options.RootPath) ? "storage" : options.RootPath;
        _root = Path.GetFullPath(root);
    }

    public bool IsSafeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200)
        {
            return false;
        }

        if (key.Contains("..", StringComparison.Ordinal) || key.Contains(':', StringComparison.Ordinal) || key.StartsWith('/') || key.StartsWith('\\'))
        {
            return false;
        }

        return !key.Contains("://", StringComparison.Ordinal) && !key.StartsWith("http", StringComparison.OrdinalIgnoreCase);
    }

    public async Task SaveAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        if (!TryResolve(key, out var path) || !File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        if (TryResolve(key, out var path) && File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string Resolve(string key) => TryResolve(key, out var path) ? path : throw new InvalidOperationException("The storage key is not valid.");

    private bool TryResolve(string key, out string path)
    {
        path = string.Empty;
        if (!IsSafeKey(key))
        {
            return false;
        }

        var combined = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        var root = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        path = combined;
        return true;
    }
}

public sealed class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public MemoryCacheService(IMemoryCache cache) => _cache = cache;

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) =>
        Task.FromResult(_cache.TryGetValue(key, out T? value) ? value : default);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        _cache.Set(key, value, ttl);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
}

public sealed class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _connection;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer connection, ILogger<RedisCacheService> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        try
        {
            var value = await _connection.GetDatabase().StringGetAsync(key).ConfigureAwait(false);
            return value.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (RedisException)
        {
            _logger.LogWarning("Redis read failed for a tenant cache key");
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        try
        {
            await _connection.GetDatabase().StringSetAsync(key, JsonSerializer.Serialize(value), ttl).ConfigureAwait(false);
        }
        catch (RedisException)
        {
            _logger.LogWarning("Redis write failed. The request continues without cache");
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _connection.GetDatabase().KeyDeleteAsync(key).ConfigureAwait(false);
        }
        catch (RedisException)
        {
            _logger.LogWarning("Redis invalidation failed");
        }
    }
}

public sealed class IdempotencyStore : IIdempotencyStore
{
    private readonly ApplicationDbContext _db;

    public IdempotencyStore(ApplicationDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid tenantId, Guid userId, string operation, string key, CancellationToken cancellationToken) =>
        _db.IdempotencyRecords.AnyAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Operation == operation && item.IdempotencyKey == key, cancellationToken);

    public Task RememberAsync(Guid tenantId, Guid userId, string operation, string key, int statusCode, CancellationToken cancellationToken) =>
        _db.IdempotencyRecords.AddAsync(IdempotencyRecord.Create(Guid.NewGuid(), tenantId, userId, operation, key, statusCode, DateTime.UtcNow), cancellationToken).AsTask();
}

public sealed class PersonalDataExportService : IDataExportService
{
    private readonly ApplicationDbContext _db;

    public PersonalDataExportService(ApplicationDbContext db) => _db = db;

    public async Task<PersonalExport> ExportPersonalAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _db.FitnessProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId, cancellationToken).ConfigureAwait(false);
        var measurements = await _db.BodyMeasurements.AsNoTracking().CountAsync(item => item.TenantId == tenantId && item.UserId == userId, cancellationToken).ConfigureAwait(false);
        return new PersonalExport(tenantId, userId, profile?.PrimaryGoal.ToString(), profile?.FitnessLevel.ToString(), measurements);
    }
}

