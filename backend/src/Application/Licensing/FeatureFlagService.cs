using LifeFitAI.Application.Identity;
using LifeFitAI.SharedKernel.Abstractions;

namespace LifeFitAI.Application.Licensing;

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(Guid tenantId, string featureCode, CancellationToken cancellationToken);
}

/// <summary>
/// Computes entitlements purely from the subscriber's active subscription and its plan
/// features. There are no per-tenant feature copies or overrides: customisation happens
/// by assigning a different plan. Features that consume a license additionally require an
/// active seat for the current user.
/// </summary>
public sealed class FeatureFlagService : IFeatureFlagService
{
    private readonly ISubscriptionService _subscriptions;
    private readonly ICurrentActor _actor;
    private readonly IDateTimeProvider _clock;
    private readonly IEntitlementStore _entitlements;

    public FeatureFlagService(
        ISubscriptionService subscriptions,
        ICurrentActor actor,
        IDateTimeProvider clock,
        IEntitlementStore entitlements)
    {
        _subscriptions = subscriptions;
        _actor = actor;
        _clock = clock;
        _entitlements = entitlements;
    }

    public async Task<bool> IsEnabledAsync(Guid tenantId, string featureCode, CancellationToken cancellationToken)
    {
        if (!await _subscriptions.IsUsableAsync(tenantId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var subscription = await _subscriptions.GetActiveAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
        {
            return false;
        }

        var now = _clock.UtcNow;
        var enabled = await _entitlements.PlanFeatureAsync(subscription.PlanId, featureCode, cancellationToken).ConfigureAwait(false);
        if (enabled != true)
        {
            return false;
        }

        if (!await _entitlements.RequiresSeatAsync(featureCode, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        return _actor.UserId is Guid userId
            && await _entitlements.HasSeatAsync(subscription.Id, userId, now, cancellationToken).ConfigureAwait(false);
    }
}
