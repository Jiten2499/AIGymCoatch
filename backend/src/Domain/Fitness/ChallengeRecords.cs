using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Fitness;

public sealed class Challenge : BaseEntity
{
    private Challenge()
    {
    }

    public Guid? TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public ChallengeType ChallengeType { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public decimal TargetValue { get; private set; }

    public ChallengeStatus Status { get; private set; }

    public bool IsSystem => TenantId is null;

    public static Challenge Create(Guid id, Guid tenantId, string name, string? description, ChallengeType type, DateOnly start, DateOnly end, decimal target) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description,
            ChallengeType = type,
            StartDate = start,
            EndDate = end,
            TargetValue = target,
            Status = ChallengeStatus.Active,
        };

    public bool VisibleTo(Guid tenantId) => TenantId is null || TenantId == tenantId;
}

public sealed class ChallengeParticipant
{
    private ChallengeParticipant()
    {
    }

    public Guid Id { get; private set; }

    public Guid ChallengeId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public decimal CurrentValue { get; private set; }

    public DateTime JoinedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public static ChallengeParticipant Join(Guid id, Guid challengeId, Guid userId, Guid tenantId, DateTime utcNow) =>
        new()
        {
            Id = id,
            ChallengeId = challengeId,
            UserId = userId,
            TenantId = tenantId,
            CurrentValue = 0,
            JoinedAt = utcNow,
        };

    public void Record(decimal value, decimal target, DateTime utcNow)
    {
        CurrentValue = value < 0 ? 0 : value;
        if (CompletedAt is null && CurrentValue >= target)
        {
            CompletedAt = utcNow;
        }
    }
}

public sealed class Notification
{
    private Notification()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public NotificationType Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public bool IsRead { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? ReadAt { get; private set; }

    public static Notification Create(Guid id, Guid userId, Guid tenantId, NotificationType type, string title, string message, DateTime utcNow) =>
        new()
        {
            Id = id,
            UserId = userId,
            TenantId = tenantId,
            Type = type,
            Title = title,
            Message = message,
            IsRead = false,
            CreatedAt = utcNow,
        };

    public void MarkRead(DateTime utcNow)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAt = utcNow;
    }
}
