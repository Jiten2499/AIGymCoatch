namespace LifeFitAI.Domain.Gym;

public static class MembershipRenewalRules
{
    public static DateOnly? DefaultStart(GymMembership membership, DateOnly today)
    {
        if (!CanRenew(membership, today, out var expired))
        {
            return null;
        }

        return expired ? today : membership.EndDate.AddDays(1);
    }

    public static GymMembership? CreateSuccessor(GymMembership current, MembershipPlan plan, DateOnly today, DateOnly start, bool allowEarly, Guid id, string membershipNumber, bool autoRenew, string? notes)
    {
        if (!CanRenew(current, today, out var expired) || !plan.IsActive || plan.TenantId != current.TenantId)
        {
            return null;
        }

        if (!allowEarly && !expired)
        {
            return null;
        }

        var earliest = expired ? today : current.EndDate.AddDays(1);
        if (start < earliest)
        {
            return null;
        }

        var successor = GymMembership.Create(id, current.TenantId, current.MemberProfileId, current.BranchId, plan.Id, membershipNumber, start, plan.InclusiveEnd(start), today, autoRenew, notes);
        successor.ApplyIncludedSessions(plan.IncludedPtSessions);
        return successor;
    }

    private static bool CanRenew(GymMembership membership, DateOnly today, out bool expired)
    {
        expired = membership.Status == GymMembershipStatus.Expired || membership.EndDate < today;
        if (membership.Status is GymMembershipStatus.Cancelled or GymMembershipStatus.Suspended or GymMembershipStatus.Pending)
        {
            return false;
        }

        return expired || membership.Status is GymMembershipStatus.Active or GymMembershipStatus.Frozen;
    }
}
