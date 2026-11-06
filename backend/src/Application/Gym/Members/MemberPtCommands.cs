using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym.Pt;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Members;

public sealed record AssignMemberPtPackageCommand(Guid MemberId, Guid PackageId, Guid? TrainerId, DateOnly StartDate) : IRequest<Result<Guid>>;

public sealed class AssignMemberPtPackageCommandValidator : AbstractValidator<AssignMemberPtPackageCommand>
{
    public AssignMemberPtPackageCommandValidator()
    {
        RuleFor(command => command.MemberId).NotEmpty();
        RuleFor(command => command.PackageId).NotEmpty();
        RuleFor(command => command.StartDate).NotEmpty();
    }
}

public sealed class AssignMemberPtPackageCommandHandler : IRequestHandler<AssignMemberPtPackageCommand, Result<Guid>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IPtPackageRepository _packages;
    private readonly IUnitOfWork _unitOfWork;

    public AssignMemberPtPackageCommandHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IPtPackageRepository packages, IUnitOfWork unitOfWork)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _packages = packages;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(AssignMemberPtPackageCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberUpdate, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<Guid>(gate.Error);
        }

        var member = await _people.GetMemberAsync(tenantId, request.MemberId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<Guid>(GymErrors.NotFound);
        }

        if (request.TrainerId is Guid trainerId && await _people.GetTrainerAsync(tenantId, trainerId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<Guid>(GymErrors.NotFound);
        }

        try
        {
            var assignmentId = await MemberPtOpening.OpenAsync(_packages, tenantId, member.Id, request.TrainerId, request.PackageId, request.StartDate, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(assignmentId);
        }
        catch (MemberOnboardingException exception)
        {
            return Result.Failure<Guid>(exception.Error);
        }
    }
}

public sealed record ReverseMemberPtSessionCommand(Guid MemberId, Guid ConsumptionId, string? Reason) : IRequest<Result<Guid>>;

public sealed class ReverseMemberPtSessionCommandHandler : IRequestHandler<ReverseMemberPtSessionCommand, Result<Guid>>
{
    private readonly ISender _sender;
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IPtPackageRepository _packages;

    public ReverseMemberPtSessionCommandHandler(ISender sender, IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IPtPackageRepository packages)
    {
        _sender = sender;
        _guard = guard;
        _actor = actor;
        _people = people;
        _packages = packages;
    }

    public async Task<Result<Guid>> Handle(ReverseMemberPtSessionCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberRead, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<Guid>(gate.Error);
        }

        if (await _people.GetMemberAsync(tenantId, request.MemberId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<Guid>(GymErrors.NotFound);
        }

        var consumption = await _packages.GetConsumptionAsync(tenantId, request.ConsumptionId, cancellationToken).ConfigureAwait(false);
        if (consumption is null)
        {
            return Result.Failure<Guid>(GymErrors.NotFound);
        }

        var sessions = await _packages.ListMemberSessionsAsync(tenantId, request.MemberId, cancellationToken).ConfigureAwait(false);
        if (!sessions.Any(session => session.Id == consumption.PtSessionId))
        {
            return Result.Failure<Guid>(GymErrors.NotFound);
        }

        return await _sender.Send(new ReversePtSessionCommand(consumption.Id, request.Reason), cancellationToken).ConfigureAwait(false);
    }
}
