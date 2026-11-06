using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym.Trainers;

internal static class TrainerMobileMap
{
    public static DateTime StartOf(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    public static TrainerSessionItem Session(PtSession session, string name, MemberPtAssignment? package) =>
        new(
            session.Id,
            session.MemberProfileId,
            name,
            session.Status.ToString(),
            session.ScheduledStartUtc,
            session.ScheduledEndUtc,
            session.SessionType,
            session.Location,
            session.Focus,
            session.Notes,
            session.ElapsedSeconds,
            session.StartedAtUtc,
            package?.PackageName,
            package?.SessionsRemaining,
            package?.SessionCount);

    public static MemberPtAssignment? CurrentPackage(IEnumerable<MemberPtAssignment> packages, DateOnly day) =>
        packages
            .Where(item => item.Status == MemberPtAssignmentStatus.Active && item.StartDate <= day && item.EndDate >= day)
            .OrderBy(item => item.EndDate)
            .FirstOrDefault();

    public static int Percent(decimal actual, decimal goal) =>
        goal <= 0 ? 0 : (int)Math.Min(100m, Math.Round(actual / goal * 100m, MidpointRounding.AwayFromZero));

    public static bool Covers(DateOnly day, DateOnly start, DateOnly? end) =>
        day >= start && (end is null || day <= end);
}
