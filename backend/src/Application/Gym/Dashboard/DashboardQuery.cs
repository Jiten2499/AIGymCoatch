using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Dashboard;

public sealed record GetBusinessDashboardQuery(Guid? BranchId) : IRequest<Result<BusinessDashboard>>;

public sealed class GetBusinessDashboardQueryHandler : IRequestHandler<GetBusinessDashboardQuery, Result<BusinessDashboard>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IDashboardQuery _dashboard;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IDateTimeProvider _clock;

    public GetBusinessDashboardQueryHandler(IOperationGuard guard, ICurrentActor actor, IDashboardQuery dashboard, IBusinessProfileRepository profiles, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _dashboard = dashboard;
        _profiles = profiles;
        _clock = clock;
    }

    public async Task<Result<BusinessDashboard>> Handle(GetBusinessDashboardQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.ReportRead, FeatureCodes.BusinessReports, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<BusinessDashboard>(gate.Error);
        }

        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var zone = TimeZoneInfo.Utc;
        if (profile is not null && TimeZoneInfo.TryFindSystemTimeZoneById(profile.TimeZone, out var found))
        {
            zone = found;
        }

        var result = await _dashboard.GetAsync(tenantId, request.BranchId, _clock.UtcNow, zone, cancellationToken).ConfigureAwait(false);
        return Result.Success(result);
    }
}
