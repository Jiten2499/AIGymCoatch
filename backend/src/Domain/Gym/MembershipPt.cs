namespace LifeFitAI.Domain.Gym;

public sealed partial class MembershipPlan
{
    public int IncludedPtSessions { get; private set; }

    public void SetIncludedPtSessions(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Included personal-training sessions cannot be negative.");
        }

        IncludedPtSessions = count;
    }
}

public sealed partial class GymMembership
{
    public int IncludedPtSessions { get; private set; }

    public int PtSessionsRemaining { get; private set; }

    public void ApplyIncludedSessions(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Included personal-training sessions cannot be negative.");
        }

        IncludedPtSessions = count;
        PtSessionsRemaining = count;
    }

    public bool TryConsumeIncludedSession(DateOnly day)
    {
        if (PtSessionsRemaining <= 0 || day < StartDate || day > EndDate || Status is GymMembershipStatus.Cancelled or GymMembershipStatus.Suspended)
        {
            return false;
        }

        PtSessionsRemaining--;
        return true;
    }

    public bool RestoreIncludedSession()
    {
        if (PtSessionsRemaining >= IncludedPtSessions)
        {
            return false;
        }

        PtSessionsRemaining++;
        return true;
    }
}
