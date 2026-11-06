namespace LifeFitAI.Domain.Gym;

public sealed class LeadFollowUp
{
    private LeadFollowUp()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid LeadId { get; private set; }

    public DateTime ScheduledAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public string? Notes { get; private set; }

    public string? Outcome { get; private set; }

    public Guid? AssignedUserId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static LeadFollowUp Create(Guid id, Guid tenantId, Guid leadId, DateTime scheduledAt, string? notes, Guid? assignedUserId, DateTime createdAt) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            LeadId = leadId,
            ScheduledAt = scheduledAt,
            Notes = notes,
            AssignedUserId = assignedUserId,
            CreatedAt = createdAt,
        };

    public bool Complete(DateTime utcNow, string? outcome)
    {
        if (CompletedAt is not null)
        {
            return false;
        }

        CompletedAt = utcNow;
        Outcome = outcome;
        return true;
    }
}

public sealed class LeadTrial
{
    private LeadTrial()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid LeadId { get; private set; }

    public DateTime ScheduledAt { get; private set; }

    public TrialStatus Status { get; private set; }

    public string? Notes { get; private set; }

    public string? Outcome { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static LeadTrial Create(Guid id, Guid tenantId, Guid leadId, DateTime scheduledAt, string? notes, DateTime createdAt) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            LeadId = leadId,
            ScheduledAt = scheduledAt,
            Status = TrialStatus.Scheduled,
            Notes = notes,
            CreatedAt = createdAt,
        };

    public void Change(TrialStatus status, string? notes, string? outcome)
    {
        Status = status;
        Notes = notes;
        Outcome = outcome;
    }
}
