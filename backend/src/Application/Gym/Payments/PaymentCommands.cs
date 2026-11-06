using FluentValidation;
using LifeFitAI.Application.Gym.Expenses;
using LifeFitAI.Application.Gym.Ledger;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Payments;

public sealed record RecordMembershipPaymentCommand(
    Guid MembershipId,
    decimal Amount,
    MembershipPaymentMethod PaymentMethod,
    DateOnly PaymentDate,
    string? ReferenceNumber,
    string? Notes) : IRequest<Result<MembershipPaymentItem>>;

public sealed record VoidMembershipPaymentCommand(Guid Id) : IRequest<Result<MembershipPaymentItem>>;

public sealed class RecordMembershipPaymentCommandValidator : AbstractValidator<RecordMembershipPaymentCommand>
{
    public RecordMembershipPaymentCommandValidator()
    {
        RuleFor(command => command.MembershipId).NotEmpty();
        RuleFor(command => command.Amount).Must(MembershipPaymentRules.Amount);
        RuleFor(command => command.Notes).MaximumLength(2000);
        RuleFor(command => command.ReferenceNumber).MaximumLength(100);
        RuleFor(command => command.ReferenceNumber)
            .NotEmpty()
            .When(command => MembershipPaymentRules.RequiresReference(command.PaymentMethod));
    }
}

public sealed class PaymentCommandHandler :
    IRequestHandler<RecordMembershipPaymentCommand, Result<MembershipPaymentItem>>,
    IRequestHandler<VoidMembershipPaymentCommand, Result<MembershipPaymentItem>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _memberships;
    private readonly IMembershipPaymentRepository _payments;
    private readonly ILedgerPostingService _ledger;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public PaymentCommandHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        IMembershipRepository memberships,
        IMembershipPaymentRepository payments,
        ILedgerPostingService ledger,
        IBusinessProfileRepository profiles,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _memberships = memberships;
        _payments = payments;
        _ledger = ledger;
        _profiles = profiles;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<MembershipPaymentItem>> Handle(RecordMembershipPaymentCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.PaymentCreate, FeatureCodes.GymPaymentManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<MembershipPaymentItem>(gate.Error);
        }

        var membership = await _memberships.GetMembershipAsync(tenantId, request.MembershipId, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return Result.Failure<MembershipPaymentItem>(GymErrors.NotFound);
        }

        if (!membership.AcceptsPayments)
        {
            return Result.Failure<MembershipPaymentItem>(GymErrors.InvalidState);
        }

        var today = await LocalTodayAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!MembershipPaymentRules.Date(request.PaymentDate, today))
        {
            return Result.Failure<MembershipPaymentItem>(GymErrors.InvalidState);
        }

        var paid = await _payments.SumPostedAsync(tenantId, membership.Id, cancellationToken).ConfigureAwait(false);
        var outstanding = MembershipPaymentRules.Outstanding(membership.FinalAmount, paid);
        if (request.Amount > outstanding)
        {
            return Result.Failure<MembershipPaymentItem>(GymErrors.InvalidState);
        }

        MembershipPayment payment;
        try
        {
            payment = MembershipPayment.Create(
                Guid.NewGuid(),
                tenantId,
                membership.MemberProfileId,
                membership.Id,
                request.Amount,
                membership.Currency,
                request.PaymentMethod,
                request.PaymentDate,
                userId,
                request.ReferenceNumber,
                request.Notes);
        }
        catch (ArgumentException)
        {
            return Result.Failure<MembershipPaymentItem>(GymErrors.InvalidState);
        }

        await _payments.AddAsync(payment, cancellationToken).ConfigureAwait(false);
        var ledger = await _ledger.TryPostMembershipPaymentAsync(tenantId, payment, userId, cancellationToken).ConfigureAwait(false);
        if (ledger.IsFailure)
        {
            return Result.Failure<MembershipPaymentItem>(ledger.Error);
        }

        await _audit.AddAsync(AuditLog.Create(
            Guid.NewGuid(),
            tenantId,
            userId,
            "MembershipPaymentRecorded",
            "MembershipPayment",
            payment.Id,
            membership.Id.ToString(),
            $"{payment.Amount} {payment.Currency} {payment.PaymentMethod} {payment.ReferenceNumber}",
            _client.IpAddress,
            _client.UserAgent,
            _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success((await _payments.ReadAsync(tenantId, payment.Id, cancellationToken).ConfigureAwait(false))!);
    }

    public async Task<Result<MembershipPaymentItem>> Handle(VoidMembershipPaymentCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.PaymentVoid, FeatureCodes.GymPaymentManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<MembershipPaymentItem>(gate.Error);
        }

        var payment = await _payments.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (payment is null)
        {
            return Result.Failure<MembershipPaymentItem>(GymErrors.NotFound);
        }

        if (!payment.Void(userId, _clock.UtcNow))
        {
            return Result.Failure<MembershipPaymentItem>(GymErrors.InvalidState);
        }

        var ledger = await _ledger.TryReverseMembershipPaymentAsync(tenantId, payment, userId, cancellationToken).ConfigureAwait(false);
        if (ledger.IsFailure)
        {
            return Result.Failure<MembershipPaymentItem>(ledger.Error);
        }

        await _audit.AddAsync(AuditLog.Create(
            Guid.NewGuid(),
            tenantId,
            userId,
            "MembershipPaymentVoided",
            "MembershipPayment",
            payment.Id,
            payment.GymMembershipId.ToString(),
            $"{payment.Amount} {payment.Currency}",
            _client.IpAddress,
            _client.UserAgent,
            _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success((await _payments.ReadAsync(tenantId, payment.Id, cancellationToken).ConfigureAwait(false))!);
    }

    private async Task<DateOnly> LocalTodayAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return ExpenseCalendar.LocalDate(_clock.UtcNow, ExpenseCalendar.Zone(profile?.TimeZone));
    }
}
