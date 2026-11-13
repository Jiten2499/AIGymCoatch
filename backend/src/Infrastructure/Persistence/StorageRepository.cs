using LifeFitAI.Application.Storage;
using LifeFitAI.Domain.Storage;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class StorageRepository : ITenantStorageConnectionRepository
{
    private readonly ApplicationDbContext _db;

    public StorageRepository(ApplicationDbContext db) => _db = db;

    public Task<TenantStorageConnection?> GetAsync(Guid tenantId, StorageProviderKind provider, CancellationToken cancellationToken) =>
        _db.TenantStorageConnections.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Provider == provider, cancellationToken);

    public Task AddAsync(TenantStorageConnection connection, CancellationToken cancellationToken)
    {
        _db.TenantStorageConnections.Add(connection);
        return Task.CompletedTask;
    }

    public Task<PlatformStorageConnection?> GetPlatformAsync(StorageProviderKind provider, CancellationToken cancellationToken) =>
        _db.PlatformStorageConnections.FirstOrDefaultAsync(item => item.Provider == provider, cancellationToken);

    public Task AddPlatformAsync(PlatformStorageConnection connection, CancellationToken cancellationToken)
    {
        _db.PlatformStorageConnections.Add(connection);
        return Task.CompletedTask;
    }

    public Task<StorageOAuthState?> GetOAuthStateByHashAsync(string stateHash, CancellationToken cancellationToken) =>
        _db.StorageOAuthStates.FirstOrDefaultAsync(item => item.StateHash == stateHash, cancellationToken);

    public Task AddOAuthStateAsync(StorageOAuthState state, CancellationToken cancellationToken)
    {
        _db.StorageOAuthStates.Add(state);
        return Task.CompletedTask;
    }

    public Task<StoredFile?> GetFileAsync(Guid tenantId, Guid fileId, CancellationToken cancellationToken) =>
        _db.StoredFiles.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == fileId, cancellationToken);

    public Task<StoredFile?> GetFileByLogicalKeyAsync(Guid tenantId, string logicalKey, CancellationToken cancellationToken) =>
        _db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.LogicalKey == logicalKey, cancellationToken);

    public Task AddFileAsync(StoredFile file, CancellationToken cancellationToken)
    {
        _db.StoredFiles.Add(file);
        return Task.CompletedTask;
    }
}
