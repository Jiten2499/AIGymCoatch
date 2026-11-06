using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Pt;

public sealed record CompletePtSessionCommand(Guid MemberId, Guid TrainerId, DateOnly CompletedOn, Guid? MembershipId, Guid? AssignmentId, Guid? SessionId) : IRequest<Result<Guid>>;

public sealed class CompletePtSessionCommandHandler : IRequestHandler<CompletePtSessionCommand, Result<Guid>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _memberships;
    private readonly IPeopleRepository _people;
    private readonly IPtPackageRepository _packages;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public CompletePtSessionCommandHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository memberships, IPeopleRepository people, IPtPackageRepository packages, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _memberships = memberships;
        _people = people;
        _packages = packages;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(CompletePtSessionCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.TrainerUpdate, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<Guid>(gate.Error);
        }

        if ((request.MembershipId is null) == (request.AssignmentId is null))
        {
            return Result.Failure<Guid>(GymErrors.InvalidState);
        }

        var sessionId = request.SessionId ?? Guid.NewGuid();
        if (await _packages.SessionAlreadyConsumedAsync(tenantId, sessionId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success(sessionId);
        }

        var member = await _people.GetMemberAsync(tenantId, request.MemberId, cancellationToken).ConfigureAwait(false);
        var trainer = await _people.GetTrainerAsync(tenantId, request.TrainerId, cancellationToken).ConfigureAwait(false);
        if (member is null || trainer is null)
        {
            return Result.Failure<Guid>(GymErrors.NotFound);
        }

        var session = PtSession.Complete(sessionId, tenantId, request.MemberId, request.TrainerId, request.CompletedOn, _clock.UtcNow, null);
        PtSessionConsumption consumption;
        if (request.MembershipId is Guid membershipId)
        {
            var membership = await _memberships.GetMembershipAsync(tenantId, membershipId, cancellationToken).ConfigureAwait(false);
            if (membership is null || membership.MemberProfileId != request.MemberId || !membership.TryConsumeIncludedSession(request.CompletedOn))
            {
                return Result.Failure<Guid>(membership is null ? GymErrors.NotFound : GymErrors.InvalidState);
            }

            consumption = PtSessionConsumption.ForMembership(Guid.NewGuid(), session, membership.Id, _clock.UtcNow);
        }
        else
        {
            var assignment = await _packages.GetAssignmentAsync(tenantId, request.AssignmentId!.Value, cancellationToken).ConfigureAwait(false);
            if (assignment is null || assignment.MemberProfileId != request.MemberId || !assignment.TryConsume(request.CompletedOn))
            {
                return Result.Failure<Guid>(assignment is null ? GymErrors.NotFound : GymErrors.InvalidState);
            }

            consumption = PtSessionConsumption.ForPackage(Guid.NewGuid(), session, assignment.Id, _clock.UtcNow);
        }

        await _packages.AddSessionAsync(session, cancellationToken).ConfigureAwait(false);
        await _packages.AddConsumptionAsync(consumption, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(session.Id);
    }
}

public sealed record ReversePtSessionCommand(Guid ConsumptionId, string? Reason) : IRequest<Result<Guid>>;

public sealed class ReversePtSessionCommandHandler : IRequestHandler<ReversePtSessionCommand, Result<Guid>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _memberships;
    private readonly IPtPackageRepository _packages;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public ReversePtSessionCommandHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository memberships, IPtPackageRepository packages, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _memberships = memberships;
        _packages = packages;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(ReversePtSessionCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.TrainerUpdate, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<Guid>(gate.Error);
        }

        var consumption = await _packages.GetConsumptionAsync(tenantId, request.ConsumptionId, cancellationToken).ConfigureAwait(false);
        if (consumption is null || !consumption.Reverse(_clock.UtcNow, request.Reason))
        {
            return Result.Failure<Guid>(consumption is null ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        if (consumption.GymMembershipId is Guid membershipId)
        {
            var membership = await _memberships.GetMembershipAsync(tenantId, membershipId, cancellationToken).ConfigureAwait(false);
            if (membership is null || !membership.RestoreIncludedSession())
            {
                return Result.Failure<Guid>(GymErrors.InvalidState);
            }
        }
        else if (consumption.MemberPtAssignmentId is Guid assignmentId)
        {
            var assignment = await _packages.GetAssignmentAsync(tenantId, assignmentId, cancellationToken).ConfigureAwait(false);
            if (assignment is null || !assignment.Restore())
            {
                return Result.Failure<Guid>(GymErrors.InvalidState);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(consumption.Id);
    }
}
