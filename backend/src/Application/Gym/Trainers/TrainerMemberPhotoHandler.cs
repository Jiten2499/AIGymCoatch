using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed record GetMyTrainerMemberPhotoQuery(Guid MemberId) : IRequest<Result<MemberPhotoFile>>;

public sealed class TrainerMemberPhotoHandler : IRequestHandler<GetMyTrainerMemberPhotoQuery, Result<MemberPhotoFile>>
{
    private readonly ICurrentActor _actor;
    private readonly ITrainerMobileStore _store;
    private readonly IFileStorage _storage;
    private readonly IDateTimeProvider _clock;

    public TrainerMemberPhotoHandler(ICurrentActor actor, ITrainerMobileStore store, IFileStorage storage, IDateTimeProvider clock)
    {
        _actor = actor;
        _store = store;
        _storage = storage;
        _clock = clock;
    }

    public async Task<Result<MemberPhotoFile>> Handle(GetMyTrainerMemberPhotoQuery request, CancellationToken cancellationToken)
    {
        if (_actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<MemberPhotoFile>(IdentityErrors.Forbidden);
        }

        var trainer = await _store.GetTrainerByUserAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        if (trainer is null || !await _store.IsAssignedAsync(tenantId, trainer.Id, request.MemberId, today, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<MemberPhotoFile>(trainer is null ? IdentityErrors.Forbidden : GymErrors.NotFound);
        }

        var member = await _store.GetMemberAsync(tenantId, request.MemberId, cancellationToken).ConfigureAwait(false);
        if (member is null || !MemberFieldRules.OwnsPhoto(tenantId, member.Id, member.PhotoStorageKey) || member.PhotoContentType is null)
        {
            return Result.Failure<MemberPhotoFile>(GymErrors.NotFound);
        }

        var content = await _storage.ReadAsync(member.PhotoStorageKey!, cancellationToken).ConfigureAwait(false);
        return content is null
            ? Result.Failure<MemberPhotoFile>(GymErrors.NotFound)
            : Result.Success(new MemberPhotoFile(content, member.PhotoContentType));
    }
}
