namespace LifeFitAI.Domain.Gym;

public static class MembershipCalendar
{
    public static DateOnly InclusiveEnd(DateOnly start, int count, PackageDurationUnit unit)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        // A month ends on the anniversary. DateOnly.AddMonths clamps a missing day to the
        // last day of the target month, so 31 January becomes 28 February, or 29 in a leap year.
        return unit == PackageDurationUnit.Month ? start.AddMonths(count) : start.AddDays(count - 1);
    }

    public static bool RangesOverlap(DateOnly start, DateOnly end, DateOnly otherStart, DateOnly otherEnd) =>
        start <= otherEnd && otherStart <= end;

    public static GymMembershipStatus EffectiveStatus(GymMembershipStatus status, DateOnly start, DateOnly end, DateOnly today)
    {
        if (status is GymMembershipStatus.Cancelled or GymMembershipStatus.Suspended or GymMembershipStatus.Frozen or GymMembershipStatus.Expired)
        {
            return status;
        }

        if (end < today)
        {
            return GymMembershipStatus.Expired;
        }

        return start > today ? GymMembershipStatus.Pending : status;
    }

    public static (int Count, PackageDurationUnit Unit) Resolve(MembershipType type, int durationDays, PackageDurationUnit requestedUnit, int? requestedCount)
    {
        if (requestedCount is int count)
        {
            return (count, requestedUnit);
        }

        return type switch
        {
            MembershipType.Monthly => (1, PackageDurationUnit.Month),
            MembershipType.Quarterly => (3, PackageDurationUnit.Month),
            MembershipType.HalfYearly => (6, PackageDurationUnit.Month),
            MembershipType.Yearly => (12, PackageDurationUnit.Month),
            _ => (durationDays, PackageDurationUnit.Day),
        };
    }
}
