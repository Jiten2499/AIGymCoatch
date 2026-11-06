namespace LifeFitAI.Domain.Gym;

public sealed class MembershipFreeze
{
    private MembershipFreeze()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid GymMembershipId { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public string? Reason { get; private set; }

    public GymMembershipStatus PreviousStatus { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public int? PlannedDays { get; private set; }

    public bool IsOpen => EndDate is null;

    public static MembershipFreeze Open(Guid id, Guid tenantId, Guid membershipId, DateOnly start, GymMembershipStatus previousStatus, string? reason, DateTime createdAt, int? plannedDays = null) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            GymMembershipId = membershipId,
            StartDate = start,
            PreviousStatus = previousStatus,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            CreatedAt = createdAt,
            PlannedDays = plannedDays is > 0 ? plannedDays : null,
        };

    public bool Close(DateOnly end)
    {
        if (end < StartDate)
        {
            return false;
        }

        EndDate = end;
        return true;
    }
}
