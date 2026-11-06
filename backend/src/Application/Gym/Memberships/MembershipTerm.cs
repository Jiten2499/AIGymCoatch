using FluentValidation;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Memberships;

public sealed record MembershipTermResponse(DateOnly StartDate, DateOnly EndDate, GymMembershipStatus Status, decimal Price, string Currency);

public sealed record GetMembershipTermQuery(Guid PlanId, DateOnly StartDate) : IRequest<Result<MembershipTermResponse>>;

public sealed class GetMembershipTermQueryValidator : AbstractValidator<GetMembershipTermQuery>
{
    public GetMembershipTermQueryValidator()
    {
        RuleFor(query => query.PlanId).NotEmpty();
    }
}

public sealed class GetMembershipTermQueryHandler : IRequestHandler<GetMembershipTermQuery, Result<MembershipTermResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _plans;
    private readonly IDateTimeProvider _clock;

    public GetMembershipTermQueryHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository plans, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _plans = plans;
        _clock = clock;
    }

    public async Task<Result<MembershipTermResponse>> Handle(GetMembershipTermQuery request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.MembershipPlanRead, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<MembershipTermResponse>(allowed.Error);
        }

        var plan = await _plans.GetPlanAsync(tenantId, request.PlanId, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return Result.Failure<MembershipTermResponse>(GymErrors.NotFound);
        }

        if (!plan.IsActive)
        {
            return Result.Failure<MembershipTermResponse>(GymErrors.InvalidState);
        }

        var end = plan.InclusiveEnd(request.StartDate);
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var status = MembershipCalendar.EffectiveStatus(GymMembershipStatus.Active, request.StartDate, end, today);
        return Result.Success(new MembershipTermResponse(request.StartDate, end, status, plan.Price, plan.Currency));
    }
}
