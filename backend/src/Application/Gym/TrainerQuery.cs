using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym;

public sealed record GetTrainerQuery(Guid Id) : IRequest<Result<TrainerResponse>>;

public sealed class GetTrainerQueryHandler : IRequestHandler<GetTrainerQuery, Result<TrainerResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IUserAccountRepository _users;

    public GetTrainerQueryHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IUserAccountRepository users)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _users = users;
    }

    public async Task<Result<TrainerResponse>> Handle(GetTrainerQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.TrainerRead, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<TrainerResponse>(gate.Error);
        }

        var trainer = await _people.GetTrainerAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        var user = trainer is null ? null : await _users.GetByIdAsync(trainer.UserId, cancellationToken).ConfigureAwait(false);
        return trainer is null || user is null
            ? Result.Failure<TrainerResponse>(GymErrors.NotFound)
            : Result.Success(TrainerMaps.ToResponse(trainer, user));
    }
}
