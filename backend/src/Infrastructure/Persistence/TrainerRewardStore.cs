using LifeFitAI.Application.Gym.Trainers;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class TrainerRewardStore : ITrainerRewardStore
{
    private readonly ApplicationDbContext _db;

    public TrainerRewardStore(ApplicationDbContext db) => _db = db;

    public async Task<TrainerRewardSettings> GetOrCreateSettingsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var existing = await _db.TrainerRewardSettings.FirstOrDefaultAsync(item => item.TenantId == tenantId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var created = TrainerRewardSettings.CreateDefault(Guid.NewGuid(), tenantId);
        _db.TrainerRewardSettings.Add(created);
        return created;
    }

    public Task SaveSettingsAsync(TrainerRewardSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<IReadOnlyList<TrainerTargetDefinition>> ListDefinitionsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await _db.TrainerTargetDefinitions.AsNoTracking().Where(item => item.TenantId == tenantId).OrderBy(item => item.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<TrainerTargetDefinition?> GetDefinitionAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.TrainerTargetDefinitions.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);

    public Task<bool> DefinitionNameExistsAsync(Guid tenantId, string name, Guid? excludingId, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        return _db.TrainerTargetDefinitions.AnyAsync(
            item => item.TenantId == tenantId && item.Name == trimmed && (excludingId == null || item.Id != excludingId),
            cancellationToken);
    }

    public Task AddDefinitionAsync(TrainerTargetDefinition definition, CancellationToken cancellationToken)
    {
        _db.TrainerTargetDefinitions.Add(definition);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TrainerTargetAssignment>> ListAssignmentsAsync(Guid tenantId, Guid? trainerId, CancellationToken cancellationToken)
    {
        var query = _db.TrainerTargetAssignments.AsNoTracking().Where(item => item.TenantId == tenantId);
        if (trainerId is Guid id)
        {
            query = query.Where(item => item.TrainerProfileId == id);
        }

        return await query.OrderByDescending(item => item.EffectiveStart).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TrainerTargetAssignment>> ListOpenAssignmentsForUpdateAsync(Guid tenantId, Guid trainerId, Guid definitionId, CancellationToken cancellationToken) =>
        await _db.TrainerTargetAssignments
            .Where(item => item.TenantId == tenantId
                && item.TrainerProfileId == trainerId
                && item.TargetDefinitionId == definitionId
                && item.Status == TrainerTargetAssignmentStatus.Active)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<TrainerTargetAssignment?> GetAssignmentAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.TrainerTargetAssignments.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);

    public Task<bool> HasOpenAssignmentAsync(Guid tenantId, Guid trainerId, Guid definitionId, CancellationToken cancellationToken) =>
        _db.TrainerTargetAssignments.AnyAsync(
            item => item.TenantId == tenantId
                && item.TrainerProfileId == trainerId
                && item.TargetDefinitionId == definitionId
                && item.Status == TrainerTargetAssignmentStatus.Active,
            cancellationToken);

    public Task AddAssignmentAsync(TrainerTargetAssignment assignment, CancellationToken cancellationToken)
    {
        _db.TrainerTargetAssignments.Add(assignment);
        return Task.CompletedTask;
    }

    public Task<TrainerProfile?> GetTrainerAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken) =>
        _db.TrainerProfiles.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == trainerId, cancellationToken);

    public Task<TrainerProfile?> GetTrainerByUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        _db.TrainerProfiles.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId, cancellationToken);

    public async Task<string?> TrainerNameAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken)
    {
        var row = await (
            from trainer in _db.TrainerProfiles.AsNoTracking()
            join user in _db.Users.AsNoTracking() on trainer.UserId equals user.Id
            where trainer.TenantId == tenantId && trainer.Id == trainerId
            select new { user.FirstName, user.LastName }).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row is null ? null : $"{row.FirstName} {row.LastName}".Trim();
    }

    public Task<TrainerRewardTransaction?> GetTransactionAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.TrainerRewardTransactions.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);

    public Task<TrainerRewardTransaction?> FindByIdempotencyAsync(Guid tenantId, string key, CancellationToken cancellationToken) =>
        _db.TrainerRewardTransactions.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.IdempotencyKey == key.Trim(), cancellationToken);

    public Task<bool> HasOpenAwardAsync(Guid tenantId, Guid assignmentId, CancellationToken cancellationToken) =>
        _db.TrainerRewardTransactions.AnyAsync(
            item => item.TenantId == tenantId
                && item.SourceAssignmentId == assignmentId
                && item.TransactionType == TrainerRewardTransactionType.Award
                && (item.ApprovalStatus == TrainerRewardApprovalStatus.Pending || item.ApprovalStatus == TrainerRewardApprovalStatus.Approved),
            cancellationToken);

    public Task AddTransactionAsync(TrainerRewardTransaction transaction, CancellationToken cancellationToken)
    {
        _db.TrainerRewardTransactions.Add(transaction);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TrainerRewardTransaction>> ListTransactionsAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken) =>
        await _db.TrainerRewardTransactions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<int> BalanceAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken)
    {
        var rows = await _db.TrainerRewardTransactions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.ApprovalStatus == TrainerRewardApprovalStatus.Approved)
            .Select(item => item.Points)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Sum();
    }
}
