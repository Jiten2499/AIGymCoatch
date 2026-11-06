using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Members;

public sealed record ListMemberProgressQuery(Guid MemberId) : IRequest<Result<IReadOnlyList<MeasurementResponse>>>;

public sealed class ListMemberProgressQueryHandler : IRequestHandler<ListMemberProgressQuery, Result<IReadOnlyList<MeasurementResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IFitnessRepository _fitness;

    public ListMemberProgressQueryHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IFitnessRepository fitness)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _fitness = fitness;
    }

    public async Task<Result<IReadOnlyList<MeasurementResponse>>> Handle(ListMemberProgressQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberRead, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<IReadOnlyList<MeasurementResponse>>(gate.Error);
        }

        var member = await _people.GetMemberAsync(tenantId, request.MemberId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<IReadOnlyList<MeasurementResponse>>(GymErrors.NotFound);
        }

        if (member.UserId is not Guid userId)
        {
            return Result.Success<IReadOnlyList<MeasurementResponse>>([]);
        }

        var page = await _fitness.ListMeasurementsAsync(tenantId, userId, new PageQuery(1, 50, "date"), cancellationToken).ConfigureAwait(false);
        IReadOnlyList<MeasurementResponse> items = page.Items.Select(FitnessMaps.ToResponse).ToArray();
        return Result.Success(items);
    }
}
