using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Identity;

/// <summary>
/// A subscriber (a tenant or a single user) attached to a plan. This is the single
/// source of truth for licensing: entitlements are computed from the plan's features,
/// never copied per subscription.
/// </summary>
public sealed class Subscription : BaseEntity
{
    private Subscription()
    {
    }

    public SubscriberType SubscriberType { get; private set; }

    public Guid? TenantId { get; private set; }

    public Guid? UserId { get; private set; }

    public Guid PlanId { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    public DateTime StartsAt { get; private set; }

    public DateTime? ExpiresAt { get; private set; }

    public int Seats { get; private set; }

    public string? Notes { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public Guid? RevokedBy { get; private set; }

    public static Subscription ForTenant(Guid id, Guid tenantId, Guid planId, DateTime startsAt, int seats, DateTime? expiresAt = null, string? notes = null) =>
        new()
        {
            Id = id,
            SubscriberType = SubscriberType.Tenant,
            TenantId = tenantId,
            PlanId = planId,
            Status = SubscriptionStatus.Active,
            StartsAt = startsAt,
            ExpiresAt = expiresAt,
            Seats = seats < 0 ? 0 : seats,
            Notes = notes,
        };

    public static Subscription ForUser(Guid id, Guid userId, Guid planId, DateTime startsAt, int seats, DateTime? expiresAt = null, string? notes = null) =>
        new()
        {
            Id = id,
            SubscriberType = SubscriberType.User,
            UserId = userId,
            PlanId = planId,
            Status = SubscriptionStatus.Active,
            StartsAt = startsAt,
            ExpiresAt = expiresAt,
            Seats = seats < 0 ? 0 : seats,
            Notes = notes,
        };

    public bool IsUsable(DateTime utcNow) =>
        Status == SubscriptionStatus.Active
        && DeletedAt is null
        && StartsAt <= utcNow
        && (ExpiresAt is null || ExpiresAt > utcNow);

    public void SetExpiresAt(DateTime? expiresAt) => ExpiresAt = expiresAt;

    public void SetSeats(int seats) => Seats = seats < 0 ? 0 : seats;

    public void ChangePlan(Guid planId) => PlanId = planId;

    public void Suspend() => Status = SubscriptionStatus.Suspended;

    public void MarkExpired(DateTime utcNow)
    {
        Status = SubscriptionStatus.Expired;
        ExpiresAt = utcNow;
    }

    public void Cancel(DateTime utcNow, Guid? userId)
    {
        Status = SubscriptionStatus.Cancelled;
        RevokedAt = utcNow;
        RevokedBy = userId;
    }
}

/// <summary>
/// A paid seat on a subscription assigned to a specific user. Required only for
/// features whose definition consumes a license (<see cref="FeatureScope.User"/>).
/// </summary>
public sealed class SubscriptionSeat : BaseEntity
{
    private SubscriptionSeat()
    {
    }

    public Guid SubscriptionId { get; private set; }

    public Guid UserId { get; private set; }

    public SeatStatus Status { get; private set; }

    public DateTime AssignedAt { get; private set; }

    public Guid? AssignedBy { get; private set; }

    public DateTime? ReleasedAt { get; private set; }

    public Guid? ReleasedBy { get; private set; }

    public static SubscriptionSeat Create(Guid id, Guid subscriptionId, Guid userId, Guid? assignedBy, DateTime utcNow) =>
        new()
        {
            Id = id,
            SubscriptionId = subscriptionId,
            UserId = userId,
            Status = SeatStatus.Active,
            AssignedAt = utcNow,
            AssignedBy = assignedBy,
        };

    public bool IsActive => Status == SeatStatus.Active && ReleasedAt is null && DeletedAt is null;

    public void Release(DateTime utcNow, Guid? userId)
    {
        Status = SeatStatus.Released;
        ReleasedAt = utcNow;
        ReleasedBy = userId;
    }
}
