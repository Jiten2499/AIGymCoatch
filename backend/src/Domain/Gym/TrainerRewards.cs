using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public enum TrainerTargetDefinitionStatus
{
    Draft = 1,
    Active = 2,
    Inactive = 3,
}

public enum TrainerTargetAssignmentStatus
{
    Active = 1,
    Completed = 2,
    Cancelled = 3,
}

public enum TrainerRewardTransactionType
{
    Award = 1,
    Adjustment = 2,
    Reversal = 3,
}

public enum TrainerRewardApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}

public sealed class TrainerTargetDefinition : BaseEntity
{
    private TrainerTargetDefinition()
    {
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public TrainerTargetType Metric { get; private set; }

    public decimal DefaultGoal { get; private set; }

    public TrainerTargetPeriod Period { get; private set; }

    public int DefaultAwardPoints { get; private set; }

    public TrainerTargetDefinitionStatus Status { get; private set; }

    public static TrainerTargetDefinition? Create(
        Guid id,
        Guid tenantId,
        string name,
        string? description,
        TrainerTargetType metric,
        decimal defaultGoal,
        TrainerTargetPeriod period,
        int defaultAwardPoints,
        TrainerTargetDefinitionStatus status)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120 || defaultGoal <= 0 || defaultAwardPoints < 0)
        {
            return null;
        }

        if (status is not (TrainerTargetDefinitionStatus.Draft or TrainerTargetDefinitionStatus.Active))
        {
            return null;
        }

        return new()
        {
            Id = id,
            TenantId = tenantId,
            Name = name.Trim(),
            Description = Clean(description),
            Metric = metric,
            DefaultGoal = defaultGoal,
            Period = period,
            DefaultAwardPoints = defaultAwardPoints,
            Status = status,
        };
    }

    public bool Update(string name, string? description, TrainerTargetType metric, decimal defaultGoal, TrainerTargetPeriod period, int defaultAwardPoints)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120 || defaultGoal <= 0 || defaultAwardPoints < 0)
        {
            return false;
        }

        Name = name.Trim();
        Description = Clean(description);
        Metric = metric;
        DefaultGoal = defaultGoal;
        Period = period;
        DefaultAwardPoints = defaultAwardPoints;
        return true;
    }

    public bool SetStatus(TrainerTargetDefinitionStatus status)
    {
        if (status is not (TrainerTargetDefinitionStatus.Active or TrainerTargetDefinitionStatus.Inactive))
        {
            return false;
        }

        Status = status;
        return true;
    }

    public string RewardRuleSnapshot() =>
        $"metric={Metric};period={Period};goal={DefaultGoal};points={DefaultAwardPoints};recognitionOnly=true";

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class TrainerTargetAssignment : BaseEntity
{
    private TrainerTargetAssignment()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid TrainerProfileId { get; private set; }

    public Guid TargetDefinitionId { get; private set; }

    public decimal GoalValue { get; private set; }

    public TrainerTargetPeriod Period { get; private set; }

    public DateOnly EffectiveStart { get; private set; }

    public DateOnly? EffectiveEnd { get; private set; }

    public TrainerTargetAssignmentStatus Status { get; private set; }

    public string SnapshotName { get; private set; } = string.Empty;

    public TrainerTargetType SnapshotMetric { get; private set; }

    public TrainerTargetPeriod SnapshotPeriod { get; private set; }

    public decimal SnapshotDefaultGoal { get; private set; }

    public int SnapshotAwardPoints { get; private set; }

    public string SnapshotRewardRule { get; private set; } = string.Empty;

    public static TrainerTargetAssignment? Open(
        Guid id,
        Guid tenantId,
        Guid trainerProfileId,
        TrainerTargetDefinition definition,
        decimal? goalOverride,
        DateOnly effectiveStart,
        DateOnly? effectiveEnd)
    {
        if (definition.Status != TrainerTargetDefinitionStatus.Active || definition.TenantId != tenantId)
        {
            return null;
        }

        var goal = goalOverride ?? definition.DefaultGoal;
        if (goal <= 0 || (effectiveEnd is not null && effectiveEnd < effectiveStart))
        {
            return null;
        }

        return new()
        {
            Id = id,
            TenantId = tenantId,
            TrainerProfileId = trainerProfileId,
            TargetDefinitionId = definition.Id,
            GoalValue = goal,
            Period = definition.Period,
            EffectiveStart = effectiveStart,
            EffectiveEnd = effectiveEnd,
            Status = TrainerTargetAssignmentStatus.Active,
            SnapshotName = definition.Name,
            SnapshotMetric = definition.Metric,
            SnapshotPeriod = definition.Period,
            SnapshotDefaultGoal = definition.DefaultGoal,
            SnapshotAwardPoints = definition.DefaultAwardPoints,
            SnapshotRewardRule = definition.RewardRuleSnapshot(),
        };
    }

    public bool Complete() => Move(TrainerTargetAssignmentStatus.Completed);

    public bool Cancel() => Move(TrainerTargetAssignmentStatus.Cancelled);

    private bool Move(TrainerTargetAssignmentStatus status)
    {
        if (Status != TrainerTargetAssignmentStatus.Active)
        {
            return false;
        }

        Status = status;
        return true;
    }
}

public sealed class TrainerRewardTransaction : BaseEntity
{
    private TrainerRewardTransaction()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid TrainerProfileId { get; private set; }

    public int Points { get; private set; }

    public TrainerRewardTransactionType TransactionType { get; private set; }

    public TrainerRewardApprovalStatus ApprovalStatus { get; private set; }

    public Guid? SourceAssignmentId { get; private set; }

    public Guid? SourceTransactionId { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string? Reason { get; private set; }

    public DateTime? PostedAt { get; private set; }

    public Guid? ApprovedByUserId { get; private set; }

    public Guid? RejectedByUserId { get; private set; }

    public static TrainerRewardTransaction? Award(
        Guid id,
        Guid tenantId,
        Guid trainerProfileId,
        int points,
        Guid assignmentId,
        string idempotencyKey,
        string? reason,
        bool requireApproval,
        DateTime utcNow,
        Guid actorId)
    {
        if (points <= 0 || string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 100)
        {
            return null;
        }

        return Create(id, tenantId, trainerProfileId, points, TrainerRewardTransactionType.Award, assignmentId, null, idempotencyKey, reason, requireApproval, utcNow, actorId);
    }

    public static TrainerRewardTransaction? Adjust(
        Guid id,
        Guid tenantId,
        Guid trainerProfileId,
        int points,
        string idempotencyKey,
        string? reason,
        bool requireApproval,
        DateTime utcNow,
        Guid actorId)
    {
        if (points == 0 || string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 100)
        {
            return null;
        }

        return Create(id, tenantId, trainerProfileId, points, TrainerRewardTransactionType.Adjustment, null, null, idempotencyKey, reason, requireApproval, utcNow, actorId);
    }

    public static TrainerRewardTransaction? Reverse(
        Guid id,
        TrainerRewardTransaction source,
        string idempotencyKey,
        string? reason,
        DateTime utcNow,
        Guid actorId)
    {
        if (source.ApprovalStatus != TrainerRewardApprovalStatus.Approved || source.TransactionType == TrainerRewardTransactionType.Reversal)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 100)
        {
            return null;
        }

        return Create(id, source.TenantId, source.TrainerProfileId, -source.Points, TrainerRewardTransactionType.Reversal, source.SourceAssignmentId, source.Id, idempotencyKey, reason, requireApproval: false, utcNow, actorId);
    }

    public bool Approve(Guid userId, DateTime utcNow)
    {
        if (ApprovalStatus != TrainerRewardApprovalStatus.Pending)
        {
            return false;
        }

        ApprovalStatus = TrainerRewardApprovalStatus.Approved;
        ApprovedByUserId = userId;
        PostedAt = utcNow;
        return true;
    }

    public bool Reject(Guid userId)
    {
        if (ApprovalStatus != TrainerRewardApprovalStatus.Pending)
        {
            return false;
        }

        ApprovalStatus = TrainerRewardApprovalStatus.Rejected;
        RejectedByUserId = userId;
        PostedAt = null;
        return true;
    }

    private static TrainerRewardTransaction Create(
        Guid id,
        Guid tenantId,
        Guid trainerProfileId,
        int points,
        TrainerRewardTransactionType type,
        Guid? assignmentId,
        Guid? sourceTransactionId,
        string idempotencyKey,
        string? reason,
        bool requireApproval,
        DateTime utcNow,
        Guid actorId) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            TrainerProfileId = trainerProfileId,
            Points = points,
            TransactionType = type,
            ApprovalStatus = requireApproval ? TrainerRewardApprovalStatus.Pending : TrainerRewardApprovalStatus.Approved,
            SourceAssignmentId = assignmentId,
            SourceTransactionId = sourceTransactionId,
            IdempotencyKey = idempotencyKey.Trim(),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            PostedAt = requireApproval ? null : utcNow,
            ApprovedByUserId = requireApproval ? null : actorId,
        };
}
