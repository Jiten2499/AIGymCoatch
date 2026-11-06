using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Configuration;
using LifeFitAI.Application.Gym.Expenses;
using LifeFitAI.Application.Gym.Ledger;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Gym.Payments;
using LifeFitAI.Application.Gym.Pt;
using LifeFitAI.Application.Gym.Trainers;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Memberships;

public sealed record RenewMembershipCommand(
    Guid MembershipId,
    string? Reason,
    bool AutoRenew,
    Guid? PlanId,
    DateOnly? StartDate,
    Guid? TrainerId,
    bool ClearTrainer,
    Guid? PtPackageId,
    Guid? DiscountRuleId,
    InitialMembershipPayment? Payment) : IRequest<Result<GymMembershipResponse>>;

public sealed class RenewMembershipCommandHandler : IRequestHandler<RenewMembershipCommand, Result<GymMembershipResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _memberships;
    private readonly IBusinessConfigRepository _configs;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IPeopleRepository _people;
    private readonly ITrainerWorkspace _trainers;
    private readonly IPtPackageRepository _packages;
    private readonly IMembershipPaymentRepository _payments;
    private readonly ILedgerPostingService _ledger;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public RenewMembershipCommandHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository memberships, IBusinessConfigRepository configs, IBusinessProfileRepository profiles, IPeopleRepository people, ITrainerWorkspace trainers, IPtPackageRepository packages, IMembershipPaymentRepository payments, ILedgerPostingService ledger, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _memberships = memberships;
        _configs = configs;
        _profiles = profiles;
        _people = people;
        _trainers = trainers;
        _packages = packages;
        _payments = payments;
        _ledger = ledger;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<GymMembershipResponse>> Handle(RenewMembershipCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipRenew, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<GymMembershipResponse>(gate.IsFailure ? gate.Error : IdentityErrors.Forbidden);
        }

        if (request.Payment is not null)
        {
            var paymentGate = await _guard.AllowAsync(PermissionCodes.PaymentCreate, FeatureCodes.GymPaymentManagement, cancellationToken).ConfigureAwait(false);
            if (paymentGate.IsFailure)
            {
                return Result.Failure<GymMembershipResponse>(paymentGate.Error);
            }
        }

        var current = await _memberships.GetMembershipAsync(tenantId, request.MembershipId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.NotFound);
        }

        var planId = request.PlanId ?? current.MembershipPlanId;
        var plan = await _memberships.GetPlanAsync(tenantId, planId, cancellationToken).ConfigureAwait(false);
        if (plan is null || !plan.IsActive)
        {
            return Result.Failure<GymMembershipResponse>(plan is null ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        if (request.TrainerId is Guid trainerId && await _people.GetTrainerAsync(tenantId, trainerId, cancellationToken).ConfigureAwait(false) is not { Status: ProfileStatus.Active })
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.NotFound);
        }

        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var today = ExpenseCalendar.LocalDate(_clock.UtcNow, ExpenseCalendar.Zone(profile?.TimeZone));
        var config = await _configs.GetConfigAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var start = request.StartDate ?? MembershipRenewalRules.DefaultStart(current, today);
        if (start is null)
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.InvalidState);
        }

        var successor = MembershipRenewalRules.CreateSuccessor(current, plan, today, start.Value, config?.AllowEarlyRenewal ?? true, Guid.NewGuid(), $"G{Guid.NewGuid():N}"[..9].ToUpperInvariant(), request.AutoRenew, request.Reason);
        if (successor is null)
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.InvalidState);
        }

        var pricing = await MembershipPricingService.ResolveAsync(tenantId, plan, request.DiscountRuleId, today, _configs, cancellationToken).ConfigureAwait(false);
        if (pricing.IsFailure)
        {
            return Result.Failure<GymMembershipResponse>(pricing.Error);
        }

        successor.SetPricing(pricing.Value.PackagePrice, pricing.Value.DiscountAmount, pricing.Value.FinalAmount, pricing.Value.Currency, pricing.Value.DiscountRuleId, pricing.Value.DiscountName);
        if (request.Payment is { Amount: var amount } && (amount <= 0 || amount > successor.FinalAmount))
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.InvalidState);
        }

        Error? blocked = null;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (await _memberships.OverlapsMembershipAsync(tenantId, current.MemberProfileId, successor.StartDate, successor.EndDate, current.Id, ct).ConfigureAwait(false))
                {
                    blocked = GymErrors.Conflict;
                    return;
                }

                await _memberships.AddMembershipAsync(successor, ct).ConfigureAwait(false);
                await _memberships.AddHistoryAsync(MembershipHistoryEntry.Create(Guid.NewGuid(), tenantId, successor.Id, MembershipAction.Renewed, null, successor.Status, successor.StartDate, request.Reason, actorId, _clock.UtcNow), ct).ConfigureAwait(false);
                if (request.Payment is InitialMembershipPayment payment)
                {
                    var recorded = MembershipPayment.Create(Guid.NewGuid(), tenantId, successor.MemberProfileId, successor.Id, payment.Amount, successor.Currency, payment.PaymentMethod, payment.PaymentDate, actorId, payment.ReferenceNumber, payment.Notes);
                    await _payments.AddAsync(recorded, ct).ConfigureAwait(false);
                    var posted = await _ledger.TryPostMembershipPaymentAsync(tenantId, recorded, actorId, ct).ConfigureAwait(false);
                    if (posted.IsFailure)
                    {
                        throw new MemberOnboardingException(posted.Error);
                    }
                }

                var coachId = request.ClearTrainer ? null : request.TrainerId;
                if (request.ClearTrainer || request.TrainerId is not null)
                {
                    await _trainers.SyncMemberTrainerAsync(tenantId, successor.MemberProfileId, coachId, successor.StartDate, actorId, ct).ConfigureAwait(false);
                }

                if (request.PtPackageId is Guid packageId)
                {
                    await MemberPtOpening.OpenAsync(_packages, tenantId, successor.MemberProfileId, coachId, packageId, successor.StartDate, ct).ConfigureAwait(false);
                }

                await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "MembershipRenewed", "GymMembership", successor.Id, current.Id.ToString(), successor.MembershipNumber, _client.IpAddress, _client.UserAgent, _clock.UtcNow), ct).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (MemberOnboardingException exception)
        {
            return Result.Failure<GymMembershipResponse>(exception.Error);
        }
        catch (ArgumentException)
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.InvalidState);
        }

        return blocked is null
            ? Result.Success(CreateMembershipCommandHandler.Map(successor, today, request.Payment?.Amount ?? 0m))
            : Result.Failure<GymMembershipResponse>(blocked);
    }
}
