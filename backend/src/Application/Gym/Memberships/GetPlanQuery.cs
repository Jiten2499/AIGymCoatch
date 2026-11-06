using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Memberships;

public sealed record GetPlanQuery(Guid Id) : IRequest<Result<PlanResponse>>;

public sealed class GetPlanQueryHandler : IRequestHandler<GetPlanQuery, Result<PlanResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _plans;

    public GetPlanQueryHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository plans)
    {
        _guard = guard;
        _actor = actor;
        _plans = plans;
    }

    public async Task<Result<PlanResponse>> Handle(GetPlanQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipPlanRead, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<PlanResponse>(gate.Error);
        }

        var plan = await _plans.GetPlanAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        return plan is null
            ? Result.Failure<PlanResponse>(GymErrors.NotFound)
            : Result.Success(CreatePlanCommandHandler.Map(plan));
    }
}
