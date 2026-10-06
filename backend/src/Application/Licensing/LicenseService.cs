using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;

namespace LifeFitAI.Application.Licensing;

public interface ISubscriptionService
{
    Task<Subscription?> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<bool> IsUsableAsync(Guid tenantId, CancellationToken cancellationToken);
}

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly ITenantRepository _tenants;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IDateTimeProvider _clock;

    public SubscriptionService(ITenantRepository tenants, ISubscriptionRepository subscriptions, IDateTimeProvider clock)
    {
        _tenants = tenants;
        _subscriptions = subscriptions;
        _clock = clock;
    }

    public Task<Subscription?> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _subscriptions.GetActiveForTenantAsync(tenantId, cancellationToken);

    public async Task<bool> IsUsableAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _tenants.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null || tenant.Status != TenantStatus.Active)
        {
            return false;
        }

        var subscription = await _subscriptions.GetActiveForTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return subscription is not null && subscription.IsUsable(_clock.UtcNow);
    }
}
