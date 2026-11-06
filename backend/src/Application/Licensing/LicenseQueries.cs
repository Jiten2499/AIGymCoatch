using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Licensing;

public sealed record GetCurrentLicenseQuery : IRequest<Result<LicenseSummary>>;

public sealed class GetCurrentLicenseQueryHandler : IRequestHandler<GetCurrentLicenseQuery, Result<LicenseSummary>>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ISubscriptionService _subscriptions;
    private readonly ILicenseAdminStore _store;
    private readonly IDateTimeProvider _clock;

    public GetCurrentLicenseQueryHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        ISubscriptionService subscriptions,
        ILicenseAdminStore store,
        IDateTimeProvider clock)
    {
        _actor = actor;
        _permissions = permissions;
        _subscriptions = subscriptions;
        _store = store;
        _clock = clock;
    }

    public async Task<Result<LicenseSummary>> Handle(GetCurrentLicenseQuery request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<LicenseSummary>(IdentityErrors.InvalidCredentials);
        }

        if (!await _permissions.HasPermissionAsync(userId, tenantId, PermissionCodes.LicenseRead, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<LicenseSummary>(IdentityErrors.Forbidden);
        }

        var subscription = await _subscriptions.GetActiveAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
        {
            return Result.Failure<LicenseSummary>(IdentityErrors.LicenseNotFound);
        }

        var code = await _store.PlanCodeAsync(subscription.PlanId, cancellationToken).ConfigureAwait(false) ?? string.Empty;
        return Result.Success(new LicenseSummary(
            subscription.Id,
            code,
            subscription.Status.ToString(),
            subscription.StartsAt,
            subscription.ExpiresAt,
            subscription.IsUsable(_clock.UtcNow)));
    }
}

public sealed record GetFeaturesQuery : IRequest<Result<IReadOnlyList<FeatureSummary>>>;

public sealed class GetFeaturesQueryHandler : IRequestHandler<GetFeaturesQuery, Result<IReadOnlyList<FeatureSummary>>>
{
    private readonly ICurrentActor _actor;
    private readonly ITenantMembershipRepository _members;
    private readonly ISubscriptionService _subscriptions;
    private readonly IFeatureFlagService _features;
    private readonly IDateTimeProvider _clock;

    public GetFeaturesQueryHandler(
        ICurrentActor actor,
        ITenantMembershipRepository members,
        ISubscriptionService subscriptions,
        IFeatureFlagService features,
        IDateTimeProvider clock)
    {
        _actor = actor;
        _members = members;
        _subscriptions = subscriptions;
        _features = features;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<FeatureSummary>>> Handle(GetFeaturesQuery request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<IReadOnlyList<FeatureSummary>>(IdentityErrors.InvalidCredentials);
        }

        if (await _members.GetActiveAsync(tenantId, userId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<IReadOnlyList<FeatureSummary>>(IdentityErrors.NotMember);
        }

        var subscription = await _subscriptions.GetActiveAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (subscription is null || !subscription.IsUsable(_clock.UtcNow))
        {
            return Result.Success<IReadOnlyList<FeatureSummary>>(FeatureCodes.All.Select(code => new FeatureSummary(code, false)).ToArray());
        }

        var summaries = new List<FeatureSummary>();
        foreach (var code in FeatureCodes.All)
        {
            summaries.Add(new FeatureSummary(code, await _features.IsEnabledAsync(tenantId, code, cancellationToken).ConfigureAwait(false)));
        }

        return Result.Success<IReadOnlyList<FeatureSummary>>(summaries);
    }
}
