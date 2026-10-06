using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public sealed class MembershipPlan : BaseEntity
{
    private MembershipPlan()
    {
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int DurationDays { get; private set; }

    public MembershipType MembershipType { get; private set; }

    public int? MaxVisits { get; private set; }

    public decimal Price { get; private set; }

    public string Currency { get; private set; } = "USD";

    public bool IsActive { get; private set; }

    public static MembershipPlan Create(
        Guid id,
        Guid tenantId,
        string name,
        string? description,
        int durationDays,
        MembershipType membershipType,
        int? maxVisits,
        decimal price,
        string currency)
    {
        return new MembershipPlan
        {
            Id = id,
            TenantId = tenantId,
            Name = name,
            Description = description,
            DurationDays = durationDays,
            MembershipType = membershipType,
            MaxVisits = maxVisits,
            Price = price,
            Currency = currency,
            IsActive = true,
        };
    }

    public void Update(
        string name,
        string? description,
        int durationDays,
        MembershipType membershipType,
        int? maxVisits,
        decimal price,
        string currency)
    {
        Name = name;
        Description = description;
        DurationDays = durationDays;
        MembershipType = membershipType;
        MaxVisits = maxVisits;
        Price = price;
        Currency = currency;
    }

    public void Deactivate() => IsActive = false;
}

public sealed class GymMembership : BaseEntity
{
    private GymMembership()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid MemberProfileId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid MembershipPlanId { get; private set; }

    public string MembershipNumber { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public GymMembershipStatus Status { get; private set; }

    public bool AutoRenew { get; private set; }

    public string? Notes { get; private set; }

    public static GymMembership Create(
        Guid id,
        Guid tenantId,
        Guid memberProfileId,
        Guid branchId,
        Guid membershipPlanId,
        string membershipNumber,
        DateOnly startDate,
        int durationDays,
        DateOnly today,
        bool autoRenew,
        string? notes)
    {
        var end = startDate.AddDays(durationDays - 1);
        return new GymMembership
        {
            Id = id,
            TenantId = tenantId,
            MemberProfileId = memberProfileId,
            BranchId = branchId,
            MembershipPlanId = membershipPlanId,
            MembershipNumber = membershipNumber,
            StartDate = startDate,
            EndDate = end,
            Status = startDate > today ? GymMembershipStatus.Pending : GymMembershipStatus.Active,
            AutoRenew = autoRenew,
            Notes = notes,
        };
    }

    public bool CanCheckIn(DateOnly today) =>
        Status == GymMembershipStatus.Active && StartDate <= today && EndDate >= today;

    public bool Renew(DateOnly today, int durationDays, bool autoRenew)
    {
        if (Status is GymMembershipStatus.Cancelled or GymMembershipStatus.Suspended or GymMembershipStatus.Pending)
        {
            return false;
        }

        var stillCurrent = (Status is GymMembershipStatus.Active or GymMembershipStatus.Frozen) && EndDate >= today;
        var baseDate = stillCurrent ? EndDate.AddDays(1) : today;
        if (Status == GymMembershipStatus.Expired)
        {
            StartDate = today;
        }

        EndDate = baseDate.AddDays(durationDays - 1);

        Status = GymMembershipStatus.Active;
        AutoRenew = autoRenew;
        return true;
    }

    public bool Freeze(DateOnly today)
    {
        if (Status != GymMembershipStatus.Active || EndDate < today)
        {
            return false;
        }

        Status = GymMembershipStatus.Frozen;
        return true;
    }

    public bool Unfreeze(DateOnly today)
    {
        if (Status != GymMembershipStatus.Frozen)
        {
            return false;
        }

        Status = EndDate < today ? GymMembershipStatus.Expired : GymMembershipStatus.Active;
        return true;
    }

    public bool Cancel()
    {
        if (Status is GymMembershipStatus.Cancelled or GymMembershipStatus.Expired)
        {
            return false;
        }

        Status = GymMembershipStatus.Cancelled;
        return true;
    }

    public bool Expire(DateOnly today)
    {
        if (Status is GymMembershipStatus.Cancelled or GymMembershipStatus.Expired or GymMembershipStatus.Suspended)
        {
            return false;
        }

        if (EndDate >= today && Status != GymMembershipStatus.Pending)
        {
            return false;
        }

        Status = GymMembershipStatus.Expired;
        return true;
    }
}

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
