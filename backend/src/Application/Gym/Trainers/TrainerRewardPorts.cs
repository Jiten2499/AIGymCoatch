using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed record TrainerRewardSettingsDto(bool PointsEnabled, int DefaultAwardPoints, bool RequireApprovalForAwards, bool AllowManualAdjustments);

public sealed record TrainerTargetDefinitionItem(
    Guid Id,
    string Name,
    string? Description,
    string Metric,
    decimal DefaultGoal,
    string Period,
    int DefaultAwardPoints,
    string Status);

public sealed record TrainerTargetAssignmentItem(
    Guid Id,
    Guid TrainerProfileId,
    string TrainerName,
    Guid TargetDefinitionId,
    string SnapshotName,
    string SnapshotMetric,
    string Period,
    decimal GoalValue,
    int SnapshotAwardPoints,
    string SnapshotRewardRule,
    DateOnly EffectiveStart,
    DateOnly? EffectiveEnd,
    string Status);

public sealed record TrainerRewardTransactionItem(
    Guid Id,
    Guid TrainerProfileId,
    int Points,
    string TransactionType,
    string ApprovalStatus,
    Guid? SourceAssignmentId,
    Guid? SourceTransactionId,
    string IdempotencyKey,
    string? Reason,
    DateTime CreatedAt,
    DateTime? PostedAt);

public sealed record TrainerRewardBalanceItem(Guid TrainerProfileId, string TrainerName, int Balance);

public interface ITrainerRewardStore
{
    Task<TrainerRewardSettings> GetOrCreateSettingsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task SaveSettingsAsync(TrainerRewardSettings settings, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainerTargetDefinition>> ListDefinitionsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<TrainerTargetDefinition?> GetDefinitionAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<bool> DefinitionNameExistsAsync(Guid tenantId, string name, Guid? excludingId, CancellationToken cancellationToken);

    Task AddDefinitionAsync(TrainerTargetDefinition definition, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainerTargetAssignment>> ListAssignmentsAsync(Guid tenantId, Guid? trainerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainerTargetAssignment>> ListOpenAssignmentsForUpdateAsync(Guid tenantId, Guid trainerId, Guid definitionId, CancellationToken cancellationToken);

    Task<TrainerTargetAssignment?> GetAssignmentAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<bool> HasOpenAssignmentAsync(Guid tenantId, Guid trainerId, Guid definitionId, CancellationToken cancellationToken);

    Task AddAssignmentAsync(TrainerTargetAssignment assignment, CancellationToken cancellationToken);

    Task<TrainerProfile?> GetTrainerAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken);

    Task<TrainerProfile?> GetTrainerByUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<string?> TrainerNameAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken);

    Task<TrainerRewardTransaction?> GetTransactionAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<TrainerRewardTransaction?> FindByIdempotencyAsync(Guid tenantId, string key, CancellationToken cancellationToken);

    Task<bool> HasOpenAwardAsync(Guid tenantId, Guid assignmentId, CancellationToken cancellationToken);

    Task AddTransactionAsync(TrainerRewardTransaction transaction, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainerRewardTransaction>> ListTransactionsAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken);

    Task<int> BalanceAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken);
}
