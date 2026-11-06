using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Authorization;

public sealed record MyAccessResponse(IReadOnlyList<string> Permissions, IReadOnlyList<FeatureSummary> Features);

public sealed record GetMyAccessQuery : IRequest<Result<MyAccessResponse>>;

public sealed class GetMyAccessQueryHandler : IRequestHandler<GetMyAccessQuery, Result<MyAccessResponse>>
{
    private readonly ICurrentActor _actor;
    private readonly ITenantMembershipRepository _members;
    private readonly IRoleRepository _roles;
    private readonly ISubscriptionService _subscriptions;
    private readonly IFeatureFlagService _features;
    private readonly IDateTimeProvider _clock;

    public GetMyAccessQueryHandler(
        ICurrentActor actor,
        ITenantMembershipRepository members,
        IRoleRepository roles,
        ISubscriptionService subscriptions,
        IFeatureFlagService features,
        IDateTimeProvider clock)
    {
        _actor = actor;
        _members = members;
        _roles = roles;
        _subscriptions = subscriptions;
        _features = features;
        _clock = clock;
    }

    public async Task<Result<MyAccessResponse>> Handle(GetMyAccessQuery request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<MyAccessResponse>(IdentityErrors.InvalidCredentials);
        }

        if (await _members.GetActiveAsync(tenantId, userId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<MyAccessResponse>(IdentityErrors.NotMember);
        }

        var permissions = await _roles.GetPermissionCodesAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
        var subscription = await _subscriptions.GetActiveAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (subscription is null || !subscription.IsUsable(_clock.UtcNow))
        {
            return Result.Success(new MyAccessResponse(permissions, FeatureCodes.All.Select(code => new FeatureSummary(code, false)).ToArray()));
        }

        var features = new List<FeatureSummary>(FeatureCodes.All.Length);
        foreach (var code in FeatureCodes.All)
        {
            features.Add(new FeatureSummary(code, await _features.IsEnabledAsync(tenantId, code, cancellationToken).ConfigureAwait(false)));
        }

        return Result.Success(new MyAccessResponse(permissions, features));
    }
}
