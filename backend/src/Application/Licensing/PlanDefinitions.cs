using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Licensing;

public interface IPlanDefinitionRepository
{
    Task<PlanDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanFeature>> GetFeaturesAsync(Guid planId, CancellationToken cancellationToken);
}

/// <summary>
/// Read-side entitlement lookups used by <see cref="FeatureFlagService"/>. Entitlements are
/// derived from the plan's features; features that consume a license also require a seat.
/// </summary>
public interface IEntitlementStore
{
    Task<bool?> PlanFeatureAsync(Guid planId, string featureCode, CancellationToken cancellationToken);

    Task<bool> RequiresSeatAsync(string featureCode, CancellationToken cancellationToken);

    Task<bool> HasSeatAsync(Guid subscriptionId, Guid userId, DateTime utcNow, CancellationToken cancellationToken);
}

public sealed class NullEntitlementStore : IEntitlementStore
{
    public Task<bool?> PlanFeatureAsync(Guid planId, string featureCode, CancellationToken cancellationToken) =>
        Task.FromResult<bool?>(null);

    public Task<bool> RequiresSeatAsync(string featureCode, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<bool> HasSeatAsync(Guid subscriptionId, Guid userId, DateTime utcNow, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}

/// <summary>
/// Creates subscriptions from a plan code. Seat capacity is seeded from the plan's base
/// capacity; entitlements are never copied.
/// </summary>
public static class SubscriptionIssuance
{
    public const string PersonalFree = "PERSONAL_FREE";

    public const string BusinessFree = "BUSINESS_FREE";

    public static async Task<Subscription?> TryIssueForTenantAsync(
        IPlanDefinitionRepository plans,
        Guid tenantId,
        string code,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var plan = await plans.GetByCodeAsync(code, cancellationToken).ConfigureAwait(false);
        if (plan is null || plan.Status != PlanStatus.Active)
        {
            return null;
        }

        return Subscription.ForTenant(Guid.NewGuid(), tenantId, plan.Id, utcNow, plan.BaseCapacity);
    }
}

public static class SeatAllocator
{
    public static bool HasCapacity(int seats, int assigned) => assigned < seats;
}

public sealed record SeatReservation(bool Succeeded, Error? Error, SubscriptionSeat? Seat)
{
    public static SeatReservation Ok(SubscriptionSeat seat) => new(true, null, seat);

    public static SeatReservation Fail(Error error) => new(false, error, null);
}
