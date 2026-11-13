using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym.Ledger;
using LifeFitAI.Application.Gym.Pt;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Members;

public sealed record AssignMemberPtPackageCommand(
    Guid MemberId,
    Guid PackageId,
    Guid? TrainerId,
    DateOnly StartDate,
    decimal? PaymentAmount,
    MembershipPaymentMethod? PaymentMethod,
    DateOnly? PaymentDate,
    string? PaymentReferenceNumber,
    string? PaymentNotes) : IRequest<Result<Guid>>;

public sealed class AssignMemberPtPackageCommandValidator : AbstractValidator<AssignMemberPtPackageCommand>
{
    public AssignMemberPtPackageCommandValidator()
    {
        RuleFor(command => command.MemberId).NotEmpty();
        RuleFor(command => command.PackageId).NotEmpty();
        RuleFor(command => command.StartDate).NotEmpty();
        RuleFor(command => command.PaymentAmount).GreaterThan(0).When(command => command.PaymentAmount is not null);
        RuleFor(command => command.PaymentMethod).NotNull().When(command => command.PaymentAmount is not null);
        RuleFor(command => command.PaymentDate).NotNull().When(command => command.PaymentAmount is not null);
        RuleFor(command => command.PaymentReferenceNumber).MaximumLength(100);
        RuleFor(command => command.PaymentNotes).MaximumLength(2000);
        RuleFor(command => command.PaymentReferenceNumber)
            .NotEmpty()
            .When(command => command.PaymentAmount is not null && command.PaymentMethod is MembershipPaymentMethod method && MembershipPaymentRules.RequiresReference(method));
    }
}

public sealed class AssignMemberPtPackageCommandHandler : IRequestHandler<AssignMemberPtPackageCommand, Result<Guid>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IPtPackageRepository _packages;
    private readonly ILedgerPostingService _ledger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public AssignMemberPtPackageCommandHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IPtPackageRepository packages, ILedgerPostingService ledger, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _packages = packages;
        _ledger = ledger;
        _unitOfWork = unitOfWork;
        _clock = clock;
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

        PtOpeningPayment? payment = null;
        if (request.PaymentAmount is decimal amount)
        {
            if (request.PaymentMethod is not MembershipPaymentMethod method || request.PaymentDate is not DateOnly paymentDate || !MembershipPaymentRules.Date(paymentDate, DateOnly.FromDateTime(_clock.UtcNow)))
            {
                return Result.Failure<Guid>(GymErrors.InvalidState);
            }

            payment = new PtOpeningPayment(amount, method, paymentDate, request.PaymentReferenceNumber, request.PaymentNotes);
        }

        if (_actor.UserId is not Guid actorId)
        {
            return Result.Failure<Guid>(GymErrors.InvalidState);
        }

        try
        {
            var assignment = await MemberPtOpening.OpenAsync(_packages, tenantId, member.Id, request.TrainerId, request.PackageId, request.StartDate, cancellationToken, payment).ConfigureAwait(false);
            if (payment is not null)
            {
                var posted = await _ledger.TryPostPtPackagePaymentAsync(tenantId, assignment, actorId, cancellationToken).ConfigureAwait(false);
                if (posted.IsFailure)
                {
                    return Result.Failure<Guid>(posted.Error);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(assignment.Id);
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
