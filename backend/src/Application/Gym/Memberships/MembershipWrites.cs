using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym.Configuration;
using LifeFitAI.Application.Gym.Ledger;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Gym.Payments;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Memberships;

public sealed record CreateMembershipCommand(
    Guid MemberProfileId,
    Guid? BranchId,
    Guid PlanId,
    DateOnly StartDate,
    bool AutoRenew,
    string? Notes,
    bool AssignAppAccess = false,
    Guid? AppSubscriptionId = null,
    Guid? DiscountRuleId = null,
    InitialMembershipPayment? InitialPayment = null) : IRequest<Result<GymMembershipResponse>>;

public sealed class CreateMembershipCommandValidator : AbstractValidator<CreateMembershipCommand>
{
    public CreateMembershipCommandValidator()
    {
        RuleFor(command => command.MemberProfileId).NotEmpty();
        RuleFor(command => command.PlanId).NotEmpty();
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.AppSubscriptionId).NotEmpty().When(command => command.AssignAppAccess);
        RuleFor(command => command.InitialPayment!.Amount).Must(MembershipPaymentRules.Amount).When(command => command.InitialPayment is not null);
        RuleFor(command => command.InitialPayment!.ReferenceNumber)
            .NotEmpty()
            .When(command => command.InitialPayment is not null && MembershipPaymentRules.RequiresReference(command.InitialPayment.PaymentMethod));
    }
}

public sealed class CreateMembershipCommandHandler : IRequestHandler<CreateMembershipCommand, Result<GymMembershipResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly IPeopleRepository _people;
    private readonly IBranchRepository _branches;
    private readonly IMembershipRepository _memberships;
    private readonly IMembershipPaymentRepository _payments;
    private readonly ILedgerPostingService _ledger;
    private readonly IBusinessConfigRepository _configs;
    private readonly IMemberAppSeatAssigner _appSeats;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public CreateMembershipCommandHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        IPermissionService permissions,
        IPeopleRepository people,
        IBranchRepository branches,
        IMembershipRepository memberships,
        IMembershipPaymentRepository payments,
        ILedgerPostingService ledger,
        IBusinessConfigRepository configs,
        IMemberAppSeatAssigner appSeats,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _permissions = permissions;
        _people = people;
        _branches = branches;
        _memberships = memberships;
        _payments = payments;
        _ledger = ledger;
        _configs = configs;
        _appSeats = appSeats;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<GymMembershipResponse>> Handle(CreateMembershipCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipCreate, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<GymMembershipResponse>(gate.Error);
        }

        if (request.InitialPayment is not null)
        {
            var paymentGate = await _guard.AllowAsync(PermissionCodes.PaymentCreate, FeatureCodes.GymPaymentManagement, cancellationToken).ConfigureAwait(false);
            if (paymentGate.IsFailure)
            {
                return Result.Failure<GymMembershipResponse>(paymentGate.Error);
            }
        }

        var member = await _people.GetMemberAsync(tenantId, request.MemberProfileId, cancellationToken).ConfigureAwait(false);
        var branch = request.BranchId is Guid branchId ? await _branches.GetAsync(tenantId, branchId, cancellationToken).ConfigureAwait(false) : null;
        var plan = await _memberships.GetPlanAsync(tenantId, request.PlanId, cancellationToken).ConfigureAwait(false);
        if (member is null || member.Status != ProfileStatus.Active || plan is null || (request.BranchId.HasValue && (branch is null || branch.Status != BranchStatus.Active)))
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.NotFound);
        }

        if (!plan.IsActive)
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.InvalidState);
        }

        if (request.AssignAppAccess)
        {
            if (request.AppSubscriptionId is null)
            {
                return Result.Failure<GymMembershipResponse>(GymErrors.AppSubscriptionRequired);
            }

            if (member.UserId is null)
            {
                return Result.Failure<GymMembershipResponse>(GymErrors.AppLoginRequired);
            }

            if (!await _permissions.HasPermissionAsync(actorId, tenantId, PermissionCodes.LicenseManage, cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure<GymMembershipResponse>(IdentityErrors.Forbidden);
            }
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var pricing = await MembershipPricingService.ResolveAsync(tenantId, plan, request.DiscountRuleId, today, _configs, cancellationToken).ConfigureAwait(false);
        if (pricing.IsFailure)
        {
            return Result.Failure<GymMembershipResponse>(pricing.Error);
        }

        if (request.InitialPayment is { Amount: var amount } && amount > pricing.Value.FinalAmount)
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.InvalidState);
        }

        GymMembership? created = null;
        decimal totalPaid = 0m;
        Error? error = null;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var membership = GymMembership.Create(Guid.NewGuid(), tenantId, member.Id, branch?.Id, plan.Id, $"G{Guid.NewGuid():N}"[..9].ToUpperInvariant(), request.StartDate, plan.InclusiveEnd(request.StartDate), today, request.AutoRenew, request.Notes);
            membership.SetPricing(pricing.Value.PackagePrice, pricing.Value.DiscountAmount, pricing.Value.FinalAmount, pricing.Value.Currency, pricing.Value.DiscountRuleId, pricing.Value.DiscountName);
            membership.ApplyIncludedSessions(plan.IncludedPtSessions);
            if (await _memberships.OverlapsMembershipAsync(tenantId, member.Id, membership.StartDate, membership.EndDate, null, ct).ConfigureAwait(false))
            {
                error = GymErrors.Conflict;
                return;
            }

            await _memberships.AddMembershipAsync(membership, ct).ConfigureAwait(false);
            await _memberships.AddHistoryAsync(MembershipHistoryEntry.Create(Guid.NewGuid(), tenantId, membership.Id, MembershipAction.Created, null, membership.Status, request.StartDate, request.Notes, actorId, _clock.UtcNow), ct).ConfigureAwait(false);
            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "MembershipCreated", "GymMembership", membership.Id, null, membership.MembershipNumber, _client.IpAddress, _client.UserAgent, _clock.UtcNow), ct).ConfigureAwait(false);

            if (request.InitialPayment is InitialMembershipPayment paymentRequest)
            {
                MembershipPayment payment;
                try
                {
                    payment = MembershipPayment.Create(
                        Guid.NewGuid(),
                        tenantId,
                        member.Id,
                        membership.Id,
                        paymentRequest.Amount,
                        membership.Currency,
                        paymentRequest.PaymentMethod,
                        paymentRequest.PaymentDate,
                        actorId,
                        paymentRequest.ReferenceNumber,
                        paymentRequest.Notes);
                }
                catch (ArgumentException)
                {
                    error = GymErrors.InvalidState;
                    return;
                }

                await _payments.AddAsync(payment, ct).ConfigureAwait(false);
                var ledgerPost = await _ledger.TryPostMembershipPaymentAsync(tenantId, payment, actorId, ct).ConfigureAwait(false);
                if (ledgerPost.IsFailure)
                {
                    error = ledgerPost.Error;
                    return;
                }

                await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "MembershipPaymentRecorded", "MembershipPayment", payment.Id, membership.Id.ToString(), $"{payment.Amount} {payment.Currency} {payment.PaymentMethod}", _client.IpAddress, _client.UserAgent, _clock.UtcNow), ct).ConfigureAwait(false);
                totalPaid = payment.Amount;
            }

            if (request.AssignAppAccess && request.AppSubscriptionId is Guid appSubscriptionId)
            {
                var appAccess = await _appSeats.AssignAsync(tenantId, actorId, member, appSubscriptionId, null, ct).ConfigureAwait(false);
                if (appAccess.IsFailure)
                {
                    error = appAccess.Error;
                    return;
                }
            }

            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            created = membership;
        }, cancellationToken).ConfigureAwait(false);

        if (error is not null)
        {
            return Result.Failure<GymMembershipResponse>(error);
        }

        return Result.Success(Map(created!, today, totalPaid));
    }

    internal static GymMembershipResponse Map(GymMembership membership, DateOnly today, decimal totalPaid = 0m) =>
        new(
            membership.Id,
            membership.MemberProfileId,
            membership.BranchId,
            membership.MembershipPlanId,
            membership.MembershipNumber,
            membership.StartDate,
            membership.EndDate,
            MembershipCalendar.EffectiveStatus(membership.Status, membership.StartDate, membership.EndDate, today),
            membership.AutoRenew,
            membership.Notes,
            null,
            null,
            membership.PackagePrice,
            membership.DiscountAmount,
            membership.FinalAmount,
            membership.Currency,
            membership.DiscountRuleId,
            membership.DiscountName,
            totalPaid,
            MembershipPaymentRules.Outstanding(membership.FinalAmount, totalPaid),
            MembershipPaymentRules.Settlement(membership.FinalAmount, totalPaid));
}

public sealed record UpdatePlanCommand(Guid Id, string Name, string? Description, int DurationDays, MembershipType MembershipType, int? MaxVisits, decimal Price, string Currency, PackageCategory Category = PackageCategory.Standard, PackageDurationUnit DurationUnit = PackageDurationUnit.Day, int? DurationCount = null, int IncludedPtSessions = 0) : IRequest<Result<PlanResponse>>;

public sealed class UpdatePlanCommandValidator : AbstractValidator<UpdatePlanCommand>
{
    public UpdatePlanCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.DurationDays).GreaterThan(0);
        RuleFor(command => command.Price).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Currency).NotEmpty().Length(3);
        RuleFor(command => command.IncludedPtSessions).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdatePlanCommandHandler : IRequestHandler<UpdatePlanCommand, Result<PlanResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _plans;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public UpdatePlanCommandHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository plans, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _plans = plans;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<PlanResponse>> Handle(UpdatePlanCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipPlanManage, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<PlanResponse>(gate.Error);
        }

        var plan = await _plans.GetPlanAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return Result.Failure<PlanResponse>(GymErrors.NotFound);
        }

        var duration = MembershipCalendar.Resolve(request.MembershipType, request.DurationDays, request.DurationUnit, request.DurationCount);
        plan.Update(request.Name.Trim(), request.Description, request.DurationDays, request.MembershipType, request.MaxVisits, request.Price, request.Currency.ToUpperInvariant(), request.Category, duration.Unit, duration.Count, request.IncludedPtSessions);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "PackageUpdated", "MembershipPlan", plan.Id, null, plan.Name, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(CreatePlanCommandHandler.Map(plan));
    }
}

public sealed record GetMembershipQuery(Guid Id) : IRequest<Result<GymMembershipResponse>>;

public sealed class GetMembershipQueryHandler : IRequestHandler<GetMembershipQuery, Result<GymMembershipResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _memberships;
    private readonly IMembershipPaymentRepository _payments;
    private readonly IDateTimeProvider _clock;

    public GetMembershipQueryHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository memberships, IMembershipPaymentRepository payments, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _memberships = memberships;
        _payments = payments;
        _clock = clock;
    }

    public async Task<Result<GymMembershipResponse>> Handle(GetMembershipQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipRead, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<GymMembershipResponse>(gate.Error);
        }

        var membership = await _memberships.GetMembershipAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.NotFound);
        }

        var paid = await _payments.SumPostedAsync(tenantId, membership.Id, cancellationToken).ConfigureAwait(false);
        return Result.Success(CreateMembershipCommandHandler.Map(membership, DateOnly.FromDateTime(_clock.UtcNow), paid));
    }
}

public sealed record ListMembershipHistoryQuery(Guid MembershipId) : IRequest<Result<IReadOnlyList<MembershipHistoryResponse>>>;

public sealed class ListMembershipHistoryQueryHandler : IRequestHandler<ListMembershipHistoryQuery, Result<IReadOnlyList<MembershipHistoryResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _memberships;

    public ListMembershipHistoryQueryHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository memberships)
    {
        _guard = guard;
        _actor = actor;
        _memberships = memberships;
    }

    public async Task<Result<IReadOnlyList<MembershipHistoryResponse>>> Handle(ListMembershipHistoryQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipRead, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<IReadOnlyList<MembershipHistoryResponse>>(gate.Error);
        }

        if (await _memberships.GetMembershipAsync(tenantId, request.MembershipId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<IReadOnlyList<MembershipHistoryResponse>>(GymErrors.NotFound);
        }

        return Result.Success(await _memberships.ListHistoryAsync(tenantId, request.MembershipId, cancellationToken).ConfigureAwait(false));
    }
}
