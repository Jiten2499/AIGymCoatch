using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public sealed class Lead : BaseEntity
{
    private Lead()
    {
    }

    public Guid TenantId { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    public string Mobile { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public LeadSource Source { get; private set; }

    public Guid? InterestedPlanId { get; private set; }

    public Guid? AssignedUserId { get; private set; }

    public LeadStatus Status { get; private set; }

    public DateTime? NextFollowUpAt { get; private set; }

    public string? Notes { get; private set; }

    public Guid? ConvertedMemberId { get; private set; }

    public DateTime? ConvertedAt { get; private set; }

    public Guid? ConvertedByUserId { get; private set; }

    public LeadLostReason? LostReason { get; private set; }

    public DateTime? LostAt { get; private set; }

    public Guid? LostByUserId { get; private set; }

    public static Lead Create(Guid id, Guid tenantId, string fullName, string mobile, string? email, LeadSource source, string? notes)
    {
        return new Lead
        {
            Id = id,
            TenantId = tenantId,
            FullName = fullName.Trim(),
            Mobile = mobile.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Source = source,
            Status = LeadStatus.New,
            Notes = notes,
        };
    }

    public void Update(string fullName, string mobile, string? email, LeadSource source, Guid? interestedPlanId, string? notes)
    {
        FullName = fullName.Trim();
        Mobile = mobile.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Source = source;
        InterestedPlanId = interestedPlanId;
        Notes = notes;
    }

    public void Assign(Guid? userId) => AssignedUserId = userId;

    public bool ChangeStatus(LeadStatus next)
    {
        if (!LeadRules.CanChange(Status, next))
        {
            return false;
        }

        Status = next;
        return true;
    }

    public void ScheduleFollowUp(DateTime? nextFollowUpAt) => NextFollowUpAt = nextFollowUpAt;

    public bool Convert(Guid memberId, DateTime utcNow, Guid convertedByUserId)
    {
        if (!LeadRules.CanConvert(Status))
        {
            return false;
        }

        Status = LeadStatus.Converted;
        ConvertedMemberId = memberId;
        ConvertedAt = utcNow;
        ConvertedByUserId = convertedByUserId;
        return true;
    }

    public bool MarkLost(LeadLostReason reason, Guid userId, DateTime utcNow)
    {
        if (!ChangeStatus(LeadStatus.Lost))
        {
            return false;
        }

        LostReason = reason;
        LostAt = utcNow;
        LostByUserId = userId;
        return true;
    }

    public bool Reopen()
    {
        if (!ChangeStatus(LeadStatus.New))
        {
            return false;
        }

        return true;
    }
}
