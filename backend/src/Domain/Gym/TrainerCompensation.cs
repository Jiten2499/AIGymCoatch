using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public enum CommissionRuleKind
{
    PercentOfEligiblePtRevenue = 1,
    FixedPerPtPackage = 2,
}

public enum IncentiveRewardKind
{
    FixedOnAchievement = 1,
    PercentOfEligibleRevenue = 2,
}

public enum CompensationKind
{
    Commission = 1,
    Incentive = 2,
}

public enum CompensationStatus
{
    Calculated = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    Cancelled = 5,
}

public sealed class TrainerCommissionRule : BaseEntity
{
    private TrainerCommissionRule()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid TrainerProfileId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public CommissionRuleKind Kind { get; private set; }

    public decimal? Percent { get; private set; }

    public decimal? FixedAmount { get; private set; }

    public DateOnly EffectiveStart { get; private set; }

    public DateOnly? EffectiveEnd { get; private set; }

    public bool IsActive { get; private set; }

    public bool RequiresApproval { get; private set; }

    public static TrainerCommissionRule Create(Guid id, Guid tenantId, Guid trainerProfileId, string name, CommissionRuleKind kind, decimal? percent, decimal? fixedAmount, DateOnly start, DateOnly? end, bool requiresApproval) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            TrainerProfileId = trainerProfileId,
            Name = name.Trim(),
            Kind = kind,
            Percent = percent,
            FixedAmount = fixedAmount,
            EffectiveStart = start,
            EffectiveEnd = end,
            IsActive = true,
            RequiresApproval = requiresApproval,
        };

    public bool Covers(DateOnly day) => IsActive && day >= EffectiveStart && (EffectiveEnd is null || day <= EffectiveEnd);
}

public sealed class TrainerPerformanceIncentive : BaseEntity
{
    private TrainerPerformanceIncentive()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid TrainerProfileId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public TrainerTargetType Metric { get; private set; }

    public decimal Threshold { get; private set; }

    public IncentiveRewardKind RewardKind { get; private set; }

    public decimal? FixedAmount { get; private set; }

    public decimal? Percent { get; private set; }

    public DateOnly EffectiveStart { get; private set; }

    public DateOnly? EffectiveEnd { get; private set; }

    public bool IsActive { get; private set; }

    public bool RequiresApproval { get; private set; }

    public static TrainerPerformanceIncentive Create(Guid id, Guid tenantId, Guid trainerProfileId, string name, TrainerTargetType metric, decimal threshold, IncentiveRewardKind rewardKind, decimal? fixedAmount, decimal? percent, DateOnly start, DateOnly? end, bool requiresApproval) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            TrainerProfileId = trainerProfileId,
            Name = name.Trim(),
            Metric = metric,
            Threshold = threshold,
            RewardKind = rewardKind,
            FixedAmount = fixedAmount,
            Percent = percent,
            EffectiveStart = start,
            EffectiveEnd = end,
            IsActive = true,
            RequiresApproval = requiresApproval,
        };

    public bool Covers(DateOnly day) => IsActive && day >= EffectiveStart && (EffectiveEnd is null || day <= EffectiveEnd);
}

public sealed class TrainerCompensationCalculation : BaseEntity
{
    private TrainerCompensationCalculation()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid TrainerProfileId { get; private set; }

    public CompensationKind Kind { get; private set; }

    public Guid RuleId { get; private set; }

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    public string SourceKey { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public CompensationStatus Status { get; private set; }

    public string Basis { get; private set; } = string.Empty;

    public static TrainerCompensationCalculation Create(Guid id, Guid tenantId, Guid trainerProfileId, CompensationKind kind, Guid ruleId, DateOnly start, DateOnly end, decimal amount, string basis) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            TrainerProfileId = trainerProfileId,
            Kind = kind,
            RuleId = ruleId,
            PeriodStart = start,
            PeriodEnd = end,
            SourceKey = $"{kind}:{ruleId:N}:{start:yyyyMMdd}:{end:yyyyMMdd}",
            Amount = amount,
            Status = CompensationStatus.Calculated,
            Basis = basis,
        };

    public bool MoveTo(CompensationStatus status)
    {
        var allowed = Status switch
        {
            CompensationStatus.Calculated => status is CompensationStatus.PendingApproval or CompensationStatus.Approved or CompensationStatus.Cancelled,
            CompensationStatus.PendingApproval => status is CompensationStatus.Approved or CompensationStatus.Rejected or CompensationStatus.Cancelled,
            _ => false,
        };
        if (!allowed)
        {
            return false;
        }

        Status = status;
        return true;
    }
}

public static class CoachCompensationMath
{
    public static decimal? Commission(CommissionRuleKind kind, decimal? percent, decimal? fixedAmount, decimal? eligibleRevenue, int? packagesSold)
    {
        if (kind == CommissionRuleKind.PercentOfEligiblePtRevenue && percent is not null && eligibleRevenue is not null)
        {
            return Round(eligibleRevenue.Value * percent.Value / 100m);
        }

        if (kind == CommissionRuleKind.FixedPerPtPackage && fixedAmount is not null && packagesSold is not null)
        {
            return Round(fixedAmount.Value * packagesSold.Value);
        }

        return null;
    }

    public static decimal? Incentive(IncentiveRewardKind kind, decimal? actual, decimal threshold, decimal? fixedAmount, decimal? percent, decimal? eligibleRevenue)
    {
        if (actual is null)
        {
            return null;
        }

        if (actual.Value < threshold)
        {
            return 0m;
        }

        if (kind == IncentiveRewardKind.FixedOnAchievement && fixedAmount is not null)
        {
            return Round(fixedAmount.Value);
        }

        return kind == IncentiveRewardKind.PercentOfEligibleRevenue && percent is not null && eligibleRevenue is not null
            ? Round(eligibleRevenue.Value * percent.Value / 100m)
            : null;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
