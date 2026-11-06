namespace LifeFitAI.Domain.Gym;

public enum LeadSource
{
    Instagram = 1,
    Google = 2,
    WalkIn = 3,
    Referral = 4,
    Facebook = 5,
    Website = 6,
    Phone = 7,
    Advertisement = 8,
    Other = 9,
}

public enum LeadStatus
{
    New = 1,
    Contacted = 2,
    FollowUp = 3,
    Trial = 4,
    Converted = 5,
    Lost = 6,
}

public enum LeadLostReason
{
    TooExpensive = 1,
    JoinedAnotherGym = 2,
    NotInterested = 3,
    NoResponse = 4,
    Location = 5,
    Timing = 6,
    Other = 7,
}

public enum TrialStatus
{
    Scheduled = 1,
    Completed = 2,
    NoShow = 3,
    Cancelled = 4,
}

public static class LeadRules
{
    public static bool CanChange(LeadStatus current, LeadStatus next)
    {
        if (current == LeadStatus.Converted || next == LeadStatus.Converted)
        {
            return false;
        }

        if (current == next)
        {
            return true;
        }

        if (current == LeadStatus.Lost)
        {
            return next == LeadStatus.New;
        }

        return next is LeadStatus.Contacted or LeadStatus.FollowUp or LeadStatus.Trial or LeadStatus.Lost;
    }

    public static bool CanConvert(LeadStatus current) =>
        current is LeadStatus.New or LeadStatus.Contacted or LeadStatus.FollowUp or LeadStatus.Trial;
}
