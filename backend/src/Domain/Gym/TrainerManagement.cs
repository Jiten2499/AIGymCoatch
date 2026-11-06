using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public enum TrainerAssignmentStatus
{
    Active = 1,
    Ended = 2,
}

public enum TrainerTargetType
{
    PtRevenue = 1,
    PtSessions = 2,
    NewPtClients = 3,
}

public enum TrainerTargetPeriod
{
    Monthly = 1,
    Quarterly = 2,
    Custom = 3,
}

public sealed class TrainerMemberAssignment : BaseEntity
{
    private TrainerMemberAssignment()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid TrainerProfileId { get; private set; }

    public Guid MemberProfileId { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public TrainerAssignmentStatus Status { get; private set; }

    public Guid AssignedByUserId { get; private set; }

    public static TrainerMemberAssignment Start(Guid id, Guid tenantId, Guid trainerProfileId, Guid memberProfileId, DateOnly startDate, Guid assignedByUserId) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            TrainerProfileId = trainerProfileId,
            MemberProfileId = memberProfileId,
            StartDate = startDate,
            Status = TrainerAssignmentStatus.Active,
            AssignedByUserId = assignedByUserId,
        };

    public void End(DateOnly endDate)
    {
        if (Status != TrainerAssignmentStatus.Active)
        {
            return;
        }

        EndDate = endDate < StartDate ? StartDate : endDate;
        Status = TrainerAssignmentStatus.Ended;
    }
}

public sealed class TrainerTarget : BaseEntity
{
    private TrainerTarget()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid TrainerProfileId { get; private set; }

    public TrainerTargetType TargetType { get; private set; }

    public TrainerTargetPeriod Period { get; private set; }

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    public decimal TargetValue { get; private set; }

    public string? Name { get; private set; }

    public bool IsActive { get; private set; }

    public static TrainerTarget Create(Guid id, Guid tenantId, Guid trainerProfileId, TrainerTargetType targetType, TrainerTargetPeriod period, DateOnly periodStart, DateOnly periodEnd, decimal targetValue, string? name = null) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            TrainerProfileId = trainerProfileId,
            TargetType = targetType,
            Period = period,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            TargetValue = targetValue,
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            IsActive = true,
        };

    public void SetActive(bool active) => IsActive = active;
}

public sealed class TrainerIncentiveRule : BaseEntity
{
    private TrainerIncentiveRule()
    {
    }

    public Guid TenantId { get; private set; }

    public TrainerTargetType TargetType { get; private set; }

    public decimal Percent { get; private set; }

    public static TrainerIncentiveRule Create(Guid id, Guid tenantId, TrainerTargetType targetType, decimal percent) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            TargetType = targetType,
            Percent = percent,
        };

    public void ChangePercent(decimal percent) => Percent = percent;
}

public static class TrainerPerformanceMath
{
    public static decimal? Achievement(decimal? actual, decimal? target) =>
        actual is null || target is null or <= 0 ? null : Math.Round(actual.Value / target.Value * 100m, 2, MidpointRounding.AwayFromZero);

    public static decimal? IncentiveAmount(decimal actual, decimal target, decimal? percent) =>
        percent is null || target <= 0 || actual < target ? null : Math.Round(actual * percent.Value / 100m, 2, MidpointRounding.AwayFromZero);

    public static (DateOnly Start, DateOnly End) PeriodRange(TrainerTargetPeriod period, DateOnly start, DateOnly end) =>
        period switch
        {
            TrainerTargetPeriod.Monthly => (new DateOnly(start.Year, start.Month, 1), new DateOnly(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month))),
            TrainerTargetPeriod.Quarterly => Quarter(start),
            _ => end < start ? (start, start) : (start, end),
        };

    private static (DateOnly Start, DateOnly End) Quarter(DateOnly day)
    {
        var firstMonth = ((day.Month - 1) / 3 * 3) + 1;
        var start = new DateOnly(day.Year, firstMonth, 1);
        var endMonth = firstMonth + 2;
        return (start, new DateOnly(day.Year, endMonth, DateTime.DaysInMonth(day.Year, endMonth)));
    }
}
