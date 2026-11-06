using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public sealed partial class MembershipPlan : BaseEntity
{
    private MembershipPlan()
    {
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int DurationDays { get; private set; }

    public int DurationCount { get; private set; }

    public PackageDurationUnit DurationUnit { get; private set; } = PackageDurationUnit.Day;

    public PackageCategory Category { get; private set; } = PackageCategory.Standard;

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
        string currency,
        PackageCategory category = PackageCategory.Standard,
        PackageDurationUnit durationUnit = PackageDurationUnit.Day,
        int? durationCount = null,
        int includedPtSessions = 0)
    {
        var count = durationCount ?? durationDays;
        var plan = new MembershipPlan
        {
            Id = id,
            TenantId = tenantId,
            Name = name,
            Description = description,
            DurationDays = durationDays,
            DurationCount = count,
            DurationUnit = durationUnit,
            Category = category,
            MembershipType = membershipType,
            MaxVisits = maxVisits,
            Price = price,
            Currency = currency,
            IsActive = true,
        };
        plan.SetIncludedPtSessions(includedPtSessions);
        return plan;
    }

    public DateOnly InclusiveEnd(DateOnly start) => MembershipCalendar.InclusiveEnd(start, DurationCount > 0 ? DurationCount : DurationDays, DurationUnit);

    public void Update(
        string name,
        string? description,
        int durationDays,
        MembershipType membershipType,
        int? maxVisits,
        decimal price,
        string currency,
        PackageCategory category = PackageCategory.Standard,
        PackageDurationUnit durationUnit = PackageDurationUnit.Day,
        int? durationCount = null,
        int includedPtSessions = 0)
    {
        Name = name;
        Description = description;
        DurationDays = durationDays;
        DurationCount = durationCount ?? durationDays;
        DurationUnit = durationUnit;
        Category = category;
        MembershipType = membershipType;
        MaxVisits = maxVisits;
        Price = price;
        Currency = currency;
        SetIncludedPtSessions(includedPtSessions);
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}

public sealed partial class GymMembership : BaseEntity
{
    private GymMembership()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid MemberProfileId { get; private set; }

    public Guid? BranchId { get; private set; }

    public Guid MembershipPlanId { get; private set; }

    public string MembershipNumber { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public GymMembershipStatus Status { get; private set; }

    public bool AutoRenew { get; private set; }

    public string? Notes { get; private set; }

    public decimal PackagePrice { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public decimal FinalAmount { get; private set; }

    public string Currency { get; private set; } = "USD";

    public Guid? DiscountRuleId { get; private set; }

    public string? DiscountName { get; private set; }

    public static GymMembership Create(
        Guid id,
        Guid tenantId,
        Guid memberProfileId,
        Guid? branchId,
        Guid membershipPlanId,
        string membershipNumber,
        DateOnly startDate,
        int durationDays,
        DateOnly today,
        bool autoRenew,
        string? notes) =>
        Create(id, tenantId, memberProfileId, branchId, membershipPlanId, membershipNumber, startDate, MembershipCalendar.InclusiveEnd(startDate, durationDays, PackageDurationUnit.Day), today, autoRenew, notes);

    public static GymMembership Create(
        Guid id,
        Guid tenantId,
        Guid memberProfileId,
        Guid? branchId,
        Guid membershipPlanId,
        string membershipNumber,
        DateOnly startDate,
        DateOnly endDate,
        DateOnly today,
        bool autoRenew,
        string? notes)
    {
        return new GymMembership
        {
            Id = id,
            TenantId = tenantId,
            MemberProfileId = memberProfileId,
            BranchId = branchId,
            MembershipPlanId = membershipPlanId,
            MembershipNumber = membershipNumber,
            StartDate = startDate,
            EndDate = endDate,
            Status = startDate > today ? GymMembershipStatus.Pending : GymMembershipStatus.Active,
            AutoRenew = autoRenew,
            Notes = notes,
            PackagePrice = 0m,
            DiscountAmount = 0m,
            FinalAmount = 0m,
            Currency = "USD",
        };
    }

    public void SetPricing(decimal packagePrice, decimal discountAmount, decimal finalAmount, string currency, Guid? discountRuleId, string? discountName)
    {
        if (packagePrice < 0 || discountAmount < 0 || finalAmount < 0 || discountAmount > packagePrice || finalAmount > packagePrice)
        {
            throw new ArgumentOutOfRangeException(nameof(finalAmount), "Membership pricing values are invalid.");
        }

        PackagePrice = packagePrice;
        DiscountAmount = discountAmount;
        FinalAmount = finalAmount;
        Currency = currency.ToUpperInvariant();
        DiscountRuleId = discountRuleId;
        DiscountName = string.IsNullOrWhiteSpace(discountName) ? null : discountName.Trim();
    }

    public bool AcceptsPayments =>
        Status is GymMembershipStatus.Pending or GymMembershipStatus.Active or GymMembershipStatus.Frozen or GymMembershipStatus.Expired;

    public GymMembership? ScheduleRenewal(DateOnly today, int count, PackageDurationUnit unit, Guid newId, string membershipNumber, bool autoRenew, string? notes)
    {
        if (Status is GymMembershipStatus.Cancelled or GymMembershipStatus.Suspended or GymMembershipStatus.Pending)
        {
            return null;
        }

        var expired = Status == GymMembershipStatus.Expired || EndDate < today;
        if (!expired && Status is not (GymMembershipStatus.Active or GymMembershipStatus.Frozen))
        {
            return null;
        }

        var start = expired ? today : EndDate.AddDays(1);
        var next = Create(newId, TenantId, MemberProfileId, BranchId, MembershipPlanId, membershipNumber, start, MembershipCalendar.InclusiveEnd(start, count, unit), today, autoRenew, notes);
        next.ApplyIncludedSessions(IncludedPtSessions);
        return next;
    }

    public bool CanCheckIn(DateOnly today) =>
        Status == GymMembershipStatus.Active && StartDate <= today && EndDate >= today;

    public bool Extend(int days)
    {
        if (days <= 0 || Status is GymMembershipStatus.Cancelled or GymMembershipStatus.Suspended or GymMembershipStatus.Expired)
        {
            return false;
        }

        EndDate = EndDate.AddDays(days);
        return true;
    }

    public bool ExtendForFreeze(int days)
    {
        if (days < 0 || Status != GymMembershipStatus.Frozen)
        {
            return false;
        }

        EndDate = EndDate.AddDays(days);
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
