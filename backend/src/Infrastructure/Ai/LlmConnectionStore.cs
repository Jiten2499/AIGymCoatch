using LifeFitAI.Application.Ai;
using LifeFitAI.Domain.Fitness;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Ai;

public sealed class LlmConnectionStore : ILlmConnectionStore
{
    private readonly Persistence.ApplicationDbContext _db;

    public LlmConnectionStore(Persistence.ApplicationDbContext db) => _db = db;

    public Task AddAsync(LlmConnection connection, CancellationToken cancellationToken) =>
        _db.LlmConnections.AddAsync(connection, cancellationToken).AsTask();

    public Task<LlmConnection?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _db.LlmConnections.FirstOrDefaultAsync(connection => connection.Id == id, cancellationToken);

    public Task<LlmConnection?> GetDefaultActiveAsync(CancellationToken cancellationToken) =>
        _db.LlmConnections.FirstOrDefaultAsync(connection => connection.IsDefault && connection.IsActive, cancellationToken);

    public async Task<IReadOnlyList<LlmConnection>> ListAsync(CancellationToken cancellationToken) =>
        await _db.LlmConnections.OrderBy(connection => connection.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, int>> ModelCountsAsync(CancellationToken cancellationToken) =>
        await _db.LlmModels.GroupBy(model => model.ConnectionId).Select(group => new { group.Key, Count = group.Count() }).ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken).ConfigureAwait(false);

    public async Task ClearOtherDefaultsAsync(Guid keepId, CancellationToken cancellationToken)
    {
        var others = await _db.LlmConnections.Where(connection => connection.IsDefault && connection.Id != keepId).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var other in others)
        {
            other.SetDefault(false);
        }
    }

    public Task AddModelAsync(LlmModel model, CancellationToken cancellationToken) =>
        _db.LlmModels.AddAsync(model, cancellationToken).AsTask();

    public Task<LlmModel?> GetModelAsync(Guid id, CancellationToken cancellationToken) =>
        _db.LlmModels.FirstOrDefaultAsync(model => model.Id == id, cancellationToken);

    public async Task<IReadOnlyList<LlmModel>> ListModelsAsync(Guid connectionId, CancellationToken cancellationToken) =>
        await _db.LlmModels.Where(model => model.ConnectionId == connectionId).OrderBy(model => model.DisplayName).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task ClearOtherModelDefaultsAsync(Guid connectionId, Guid keepId, CancellationToken cancellationToken)
    {
        var others = await _db.LlmModels.Where(model => model.ConnectionId == connectionId && model.IsDefault && model.Id != keepId).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var other in others)
        {
            other.SetDefault(false);
        }
    }

    public Task<bool> ModelIdentifierExistsAsync(Guid connectionId, string modelIdentifier, CancellationToken cancellationToken) =>
        _db.LlmModels.AnyAsync(model => model.ConnectionId == connectionId && model.ModelIdentifier == modelIdentifier, cancellationToken);
}
