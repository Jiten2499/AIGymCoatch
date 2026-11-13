using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym.Expenses;
using LifeFitAI.Application.Gym.Ledger;
using LifeFitAI.Application.Gym.Pt;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Members;

public sealed record MemberPtPaymentItem(
    Guid Id,
    decimal Amount,
    string Currency,
    MembershipPaymentMethod PaymentMethod,
    DateOnly PaymentDate,
    string Status,
    string? ReferenceNumber);

public sealed record ListMemberPtPaymentsQuery(Guid MemberId, Guid AssignmentId) : IRequest<Result<IReadOnlyList<MemberPtPaymentItem>>>;

public sealed record RecordMemberPtPaymentCommand(
    Guid MemberId,
    Guid AssignmentId,
    decimal Amount,
    MembershipPaymentMethod PaymentMethod,
    DateOnly PaymentDate,
    string? ReferenceNumber,
    string? Notes) : IRequest<Result<MemberPtPaymentItem>>;

public sealed class RecordMemberPtPaymentCommandValidator : AbstractValidator<RecordMemberPtPaymentCommand>
{
    public RecordMemberPtPaymentCommandValidator()
    {
        RuleFor(command => command.MemberId).NotEmpty();
        RuleFor(command => command.AssignmentId).NotEmpty();
        RuleFor(command => command.Amount).Must(MembershipPaymentRules.Amount);
        RuleFor(command => command.ReferenceNumber).MaximumLength(100);
        RuleFor(command => command.Notes).MaximumLength(2000);
        RuleFor(command => command.ReferenceNumber)
            .NotEmpty()
            .When(command => MembershipPaymentRules.RequiresReference(command.PaymentMethod));
    }
}

public sealed class MemberPtPaymentHandlers :
    IRequestHandler<ListMemberPtPaymentsQuery, Result<IReadOnlyList<MemberPtPaymentItem>>>,
    IRequestHandler<RecordMemberPtPaymentCommand, Result<MemberPtPaymentItem>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IPtPackageRepository _packages;
    private readonly ILedgerPostingService _ledger;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public MemberPtPaymentHandlers(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IPtPackageRepository packages, ILedgerPostingService ledger, IBusinessProfileRepository profiles, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _packages = packages;
        _ledger = ledger;
        _profiles = profiles;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<IReadOnlyList<MemberPtPaymentItem>>> Handle(ListMemberPtPaymentsQuery request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(request.MemberId, request.AssignmentId, PermissionCodes.PaymentRead, cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MemberPtPaymentItem>>(opened.Error);
        }

        var rows = await _packages.ListPaymentsAsync(opened.Value.TenantId, opened.Value.Assignment.Id, cancellationToken).ConfigureAwait(false);
        var items = rows.Select(Map).ToList();
        if (items.Count == 0 && Opening(opened.Value.Assignment) is MemberPtPaymentItem opening)
        {
            items.Add(opening);
        }

        return Result.Success<IReadOnlyList<MemberPtPaymentItem>>(items);
    }

    public async Task<Result<MemberPtPaymentItem>> Handle(RecordMemberPtPaymentCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(request.MemberId, request.AssignmentId, PermissionCodes.PaymentCreate, cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure || _actor.UserId is not Guid userId)
        {
            return Result.Failure<MemberPtPaymentItem>(opened.IsFailure ? opened.Error : GymErrors.InvalidState);
        }

        var assignment = opened.Value.Assignment;
        var tenantId = opened.Value.TenantId;
        if (assignment.Status == MemberPtAssignmentStatus.Cancelled || request.Amount > assignment.Price - assignment.AmountPaid)
        {
            return Result.Failure<MemberPtPaymentItem>(GymErrors.InvalidState);
        }

        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!MembershipPaymentRules.Date(request.PaymentDate, ExpenseCalendar.LocalDate(_clock.UtcNow, ExpenseCalendar.Zone(profile?.TimeZone))))
        {
            return Result.Failure<MemberPtPaymentItem>(GymErrors.InvalidState);
        }

        if (await KeepOpeningAsync(tenantId, assignment, userId, cancellationToken).ConfigureAwait(false) is Error openingError)
        {
            return Result.Failure<MemberPtPaymentItem>(openingError);
        }

        MemberPtPayment payment;
        try
        {
            payment = MemberPtPayment.Create(Guid.NewGuid(), tenantId, assignment.MemberProfileId, assignment.Id, request.Amount, assignment.Currency, request.PaymentMethod, request.PaymentDate, userId, request.ReferenceNumber, request.Notes);
            assignment.Collect(request.Amount, request.PaymentMethod, request.PaymentDate, request.ReferenceNumber, request.Notes);
        }
        catch (ArgumentException)
        {
            return Result.Failure<MemberPtPaymentItem>(GymErrors.InvalidState);
        }

        await _packages.AddPaymentAsync(payment, cancellationToken).ConfigureAwait(false);
        var posted = await _ledger.TryPostPtCollectionAsync(tenantId, assignment, payment, userId, cancellationToken).ConfigureAwait(false);
        if (posted.IsFailure)
        {
            return Result.Failure<MemberPtPaymentItem>(posted.Error);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "PtPackagePaymentRecorded", "MemberPtPayment", payment.Id, assignment.Id.ToString(), $"{payment.Amount} {payment.Currency} {payment.PaymentMethod}", _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(payment));
    }

    private async Task<Error?> KeepOpeningAsync(Guid tenantId, MemberPtAssignment assignment, Guid userId, CancellationToken cancellationToken)
    {
        var rows = await _packages.ListPaymentsAsync(tenantId, assignment.Id, cancellationToken).ConfigureAwait(false);
        if (rows.Count > 0 || Opening(assignment) is null)
        {
            return null;
        }

        try
        {
            var opening = MemberPtPayment.Create(Guid.NewGuid(), tenantId, assignment.MemberProfileId, assignment.Id, assignment.AmountPaid, assignment.Currency, assignment.PaymentMethod!.Value, assignment.PaymentDate!.Value, userId, assignment.PaymentReferenceNumber, assignment.PaymentNotes);
            await _packages.AddPaymentAsync(opening, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            return GymErrors.InvalidState;
        }

        var posted = await _ledger.TryPostPtPackagePaymentAsync(tenantId, assignment, userId, cancellationToken).ConfigureAwait(false);
        return posted.IsFailure ? posted.Error : null;
    }

    private async Task<Result<(Guid TenantId, MemberPtAssignment Assignment)>> OpenAsync(Guid memberId, Guid assignmentId, string permission, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(permission, FeatureCodes.GymPaymentManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<(Guid, MemberPtAssignment)>(gate.Error);
        }

        if (await _people.GetMemberAsync(tenantId, memberId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<(Guid, MemberPtAssignment)>(GymErrors.NotFound);
        }

        var assignment = await _packages.GetAssignmentAsync(tenantId, assignmentId, cancellationToken).ConfigureAwait(false);
        return assignment is null || assignment.MemberProfileId != memberId
            ? Result.Failure<(Guid, MemberPtAssignment)>(GymErrors.NotFound)
            : Result.Success((tenantId, assignment));
    }

    private static MemberPtPaymentItem? Opening(MemberPtAssignment assignment) =>
        assignment.AmountPaid > 0 && assignment.PaymentMethod is MembershipPaymentMethod method && assignment.PaymentDate is DateOnly date
            ? new MemberPtPaymentItem(assignment.Id, assignment.AmountPaid, assignment.Currency, method, date, nameof(MembershipPaymentStatus.Posted), assignment.PaymentReferenceNumber)
            : null;

    private static MemberPtPaymentItem Map(MemberPtPayment payment) =>
        new(payment.Id, payment.Amount, payment.Currency, payment.PaymentMethod, payment.PaymentDate, payment.Status.ToString(), payment.ReferenceNumber);
}
