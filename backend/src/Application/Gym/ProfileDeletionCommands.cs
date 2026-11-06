using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym;

public sealed record DeleteMemberProfileCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteMemberProfileCommandHandler : IRequestHandler<DeleteMemberProfileCommand, Result>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IBusinessProfileSoftDelete _profiles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public DeleteMemberProfileCommandHandler(IOperationGuard guard, ICurrentActor actor, IBusinessProfileSoftDelete profiles, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public Task<Result> Handle(DeleteMemberProfileCommand request, CancellationToken cancellationToken) =>
        DeleteAsync(PermissionCodes.MemberDelete, FeatureCodes.MemberManagement, (tenantId, actorId, token) => _profiles.DeleteMemberAsync(tenantId, actorId, request.Id, _clock.UtcNow, _client.IpAddress, _client.UserAgent, token), cancellationToken);

    private async Task<Result> DeleteAsync(string permission, string feature, Func<Guid, Guid, CancellationToken, Task<Result>> delete, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(permission, feature, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return gate.IsFailure ? gate : Result.Failure(IdentityErrors.InvalidCredentials);
        }

        return await BusinessProfileDeletion.Execute(_unitOfWork, token => delete(tenantId, actorId, token), cancellationToken).ConfigureAwait(false);
    }
}

public sealed record DeleteTrainerProfileCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteTrainerProfileCommandHandler : IRequestHandler<DeleteTrainerProfileCommand, Result>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IBusinessProfileSoftDelete _profiles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public DeleteTrainerProfileCommandHandler(IOperationGuard guard, ICurrentActor actor, IBusinessProfileSoftDelete profiles, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(DeleteTrainerProfileCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.TrainerUpdate, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return gate.IsFailure ? gate : Result.Failure(IdentityErrors.InvalidCredentials);
        }

        return await BusinessProfileDeletion.Execute(
            _unitOfWork,
            token => _profiles.DeleteTrainerAsync(tenantId, actorId, request.Id, _clock.UtcNow, _client.IpAddress, _client.UserAgent, token),
            cancellationToken).ConfigureAwait(false);
    }
}

internal static class BusinessProfileDeletion
{
    public static async Task<Result> Execute(IUnitOfWork unitOfWork, Func<CancellationToken, Task<Result>> delete, CancellationToken cancellationToken)
    {
        try
        {
            var outcome = Result.Success();
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                outcome = await delete(token).ConfigureAwait(false);
                if (outcome.IsFailure)
                {
                    throw new BusinessProfileDeletionException(outcome.Error);
                }
            }, cancellationToken).ConfigureAwait(false);
            return outcome;
        }
        catch (BusinessProfileDeletionException exception)
        {
            return Result.Failure(exception.Error);
        }
    }

    private sealed class BusinessProfileDeletionException(Error error) : Exception
    {
        public Error Error { get; } = error;
    }
}
