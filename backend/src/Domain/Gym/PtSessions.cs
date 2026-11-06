using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public enum PtSessionStatus
{
    Scheduled = 1,
    Completed = 2,
    Cancelled = 3,
    Missed = 4,
}

public enum PtConsumptionStatus
{
    Consumed = 1,
    Reversed = 2,
}

public sealed class PtSession : BaseEntity
{
    private PtSession()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid MemberProfileId { get; private set; }

    public Guid TrainerProfileId { get; private set; }

    public DateTime ScheduledStartUtc { get; private set; }

    public DateTime ScheduledEndUtc { get; private set; }

    public DateOnly? CompletedOn { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    public int ElapsedSeconds { get; private set; }

    public string? Location { get; private set; }

    public string? SessionType { get; private set; }

    public string? Focus { get; private set; }

    public PtSessionStatus Status { get; private set; }

    public string? Notes { get; private set; }

    public static PtSession? Schedule(Guid id, Guid tenantId, Guid memberId, Guid trainerId, DateTime startUtc, DateTime endUtc, string? sessionType, string? location, string? focus)
    {
        startUtc = AsUtc(startUtc);
        endUtc = AsUtc(endUtc);
        if (endUtc < startUtc || endUtc - startUtc > TimeSpan.FromHours(8))
        {
            return null;
        }

        return new PtSession
        {
            Id = id,
            TenantId = tenantId,
            MemberProfileId = memberId,
            TrainerProfileId = trainerId,
            ScheduledStartUtc = startUtc,
            ScheduledEndUtc = endUtc,
            Status = PtSessionStatus.Scheduled,
            SessionType = Trim(sessionType, 80),
            Location = Trim(location, 120),
            Focus = Trim(focus, 500),
        };
    }

    public bool Reschedule(DateTime startUtc, DateTime endUtc)
    {
        startUtc = AsUtc(startUtc);
        endUtc = AsUtc(endUtc);
        if (Status != PtSessionStatus.Scheduled || endUtc < startUtc || endUtc - startUtc > TimeSpan.FromHours(8))
        {
            return false;
        }

        ScheduledStartUtc = startUtc;
        ScheduledEndUtc = endUtc;
        return true;
    }

    public bool Start(DateTime utcNow)
    {
        if (Status != PtSessionStatus.Scheduled)
        {
            return false;
        }

        StartedAtUtc ??= AsUtc(utcNow);
        return true;
    }

    public bool RememberElapsed(int seconds)
    {
        if (Status != PtSessionStatus.Scheduled || seconds < 0 || seconds > 8 * 60 * 60)
        {
            return false;
        }

        ElapsedSeconds = seconds;
        return true;
    }

    public bool Finish(DateOnly completedOn, DateTime completedAtUtc, string? notes, int elapsedSeconds)
    {
        if (Status == PtSessionStatus.Completed)
        {
            return true;
        }

        if (Status != PtSessionStatus.Scheduled)
        {
            return false;
        }

        Status = PtSessionStatus.Completed;
        CompletedOn = completedOn;
        CompletedAtUtc = AsUtc(completedAtUtc);
        if (!string.IsNullOrWhiteSpace(notes))
        {
            Notes = Trim(notes, 1000);
        }

        if (elapsedSeconds >= 0)
        {
            ElapsedSeconds = Math.Min(elapsedSeconds, 8 * 60 * 60);
        }

        return true;
    }

    public bool Cancel()
    {
        if (Status != PtSessionStatus.Scheduled)
        {
            return false;
        }

        Status = PtSessionStatus.Cancelled;
        return true;
    }

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    public static PtSession Complete(Guid id, Guid tenantId, Guid memberId, Guid trainerId, DateOnly completedOn, DateTime completedAtUtc, string? notes)
    {
        if (completedAtUtc.Kind != DateTimeKind.Utc)
        {
            completedAtUtc = DateTime.SpecifyKind(completedAtUtc, DateTimeKind.Utc);
        }

        var start = completedAtUtc;
        return new PtSession
        {
            Id = id,
            TenantId = tenantId,
            MemberProfileId = memberId,
            TrainerProfileId = trainerId,
            ScheduledStartUtc = start,
            ScheduledEndUtc = start.AddHours(1),
            CompletedOn = completedOn,
            CompletedAtUtc = start,
            Status = PtSessionStatus.Completed,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        };
    }
}

public sealed class PtSessionConsumption : BaseEntity
{
    private PtSessionConsumption()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid PtSessionId { get; private set; }

    public Guid? GymMembershipId { get; private set; }

    public Guid? MemberPtAssignmentId { get; private set; }

    public int SessionsConsumed { get; private set; }

    public PtConsumptionStatus Status { get; private set; }

    public DateTime ConsumedAtUtc { get; private set; }

    public DateTime? ReversedAtUtc { get; private set; }

    public string? ReversalReason { get; private set; }

    public static PtSessionConsumption ForMembership(Guid id, PtSession session, Guid membershipId, DateTime utcNow) =>
        Create(id, session, membershipId, null, utcNow);

    public static PtSessionConsumption ForPackage(Guid id, PtSession session, Guid assignmentId, DateTime utcNow) =>
        Create(id, session, null, assignmentId, utcNow);

    public bool Reverse(DateTime utcNow, string? reason)
    {
        if (Status != PtConsumptionStatus.Consumed)
        {
            return false;
        }

        Status = PtConsumptionStatus.Reversed;
        ReversedAtUtc = utcNow;
        ReversalReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        return true;
    }

    private static PtSessionConsumption Create(Guid id, PtSession session, Guid? membershipId, Guid? assignmentId, DateTime utcNow)
    {
        if (session.Status != PtSessionStatus.Completed || (membershipId is null) == (assignmentId is null))
        {
            throw new ArgumentException("A completed session must consume exactly one entitlement source.");
        }

        return new PtSessionConsumption
        {
            Id = id,
            TenantId = session.TenantId,
            PtSessionId = session.Id,
            GymMembershipId = membershipId,
            MemberPtAssignmentId = assignmentId,
            SessionsConsumed = 1,
            Status = PtConsumptionStatus.Consumed,
            ConsumedAtUtc = utcNow,
        };
    }
}
