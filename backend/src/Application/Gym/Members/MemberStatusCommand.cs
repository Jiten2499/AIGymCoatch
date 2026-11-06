using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Members;

public sealed record ChangeMemberStatusCommand(Guid Id, ProfileStatus Status) : IRequest<Result<MemberResponse>>;

public sealed class ChangeMemberStatusCommandHandler : IRequestHandler<ChangeMemberStatusCommand, Result<MemberResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public ChangeMemberStatusCommandHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<MemberResponse>> Handle(ChangeMemberStatusCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberUpdate, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<MemberResponse>(gate.Error);
        }

        if (request.Status is not ProfileStatus.Active and not ProfileStatus.Inactive)
        {
            return Result.Failure<MemberResponse>(GymErrors.InvalidState);
        }

        var profile = await _people.GetMemberAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Result.Failure<MemberResponse>(GymErrors.NotFound);
        }

        var previous = profile.Status;
        profile.ChangeStatus(request.Status);
        if (previous != profile.Status)
        {
            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "MemberStatusChanged", "MemberProfile", profile.Id, previous.ToString(), profile.Status.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var member = await _people.ReadMemberAsync(tenantId, profile.Id, cancellationToken).ConfigureAwait(false);
        return member is null ? Result.Failure<MemberResponse>(GymErrors.NotFound) : Result.Success(member);
    }
}
