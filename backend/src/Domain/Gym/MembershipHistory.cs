namespace LifeFitAI.Domain.Gym;

public sealed class MembershipHistoryEntry
{
    private MembershipHistoryEntry()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid GymMembershipId { get; private set; }

    public MembershipAction Action { get; private set; }

    public GymMembershipStatus? PreviousStatus { get; private set; }

    public GymMembershipStatus NewStatus { get; private set; }

    public DateOnly EffectiveDate { get; private set; }

    public string? Reason { get; private set; }

    public Guid? PerformedByUserId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static MembershipHistoryEntry Create(
        Guid id,
        Guid tenantId,
        Guid gymMembershipId,
        MembershipAction action,
        GymMembershipStatus? previousStatus,
        GymMembershipStatus newStatus,
        DateOnly effectiveDate,
        string? reason,
        Guid? performedByUserId,
        DateTime createdAt)
    {
        return new MembershipHistoryEntry
        {
            Id = id,
            TenantId = tenantId,
            GymMembershipId = gymMembershipId,
            Action = action,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            EffectiveDate = effectiveDate,
            Reason = reason,
            PerformedByUserId = performedByUserId,
            CreatedAt = createdAt,
        };
    }
}
