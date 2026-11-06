using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Email;
using LifeFitAI.Application.Gym.Ledger;
using LifeFitAI.Application.Gym.Pt;
using LifeFitAI.Application.Gym.Payments;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Members;

public sealed class CreateMemberCommandHandler : IRequestHandler<CreateMemberCommand, Result<MemberResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly IAccountLinker _accounts;
    private readonly IUserAccountRepository _users;
    private readonly ITenantRepository _tenants;
    private readonly ITenantMembershipRepository _memberships;
    private readonly IRoleRepository _roles;
    private readonly IPeopleRepository _people;
    private readonly IBranchRepository _branches;
    private readonly IMembershipRepository _plans;
    private readonly IMembershipPaymentRepository _payments;
    private readonly ILedgerPostingService _ledger;
    private readonly IMemberAppSeatAssigner _appSeats;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;
    private readonly Trainers.ITrainerWorkspace? _trainers;
    private readonly IPtPackageRepository _ptPackages;
    private readonly IAccountActivationMailer? _activation;

    public CreateMemberCommandHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        IPermissionService permissions,
        IAccountLinker accounts,
        IUserAccountRepository users,
        ITenantRepository tenants,
        ITenantMembershipRepository memberships,
        IRoleRepository roles,
        IPeopleRepository people,
        IBranchRepository branches,
        IMembershipRepository plans,
        IMembershipPaymentRepository payments,
        ILedgerPostingService ledger,
        IMemberAppSeatAssigner appSeats,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client,
        IPtPackageRepository ptPackages,
        Trainers.ITrainerWorkspace? trainers = null,
        IAccountActivationMailer? activation = null)
    {
        _guard = guard;
        _actor = actor;
        _permissions = permissions;
        _accounts = accounts;
        _users = users;
        _tenants = tenants;
        _memberships = memberships;
        _roles = roles;
        _people = people;
        _branches = branches;
        _plans = plans;
        _payments = payments;
        _ledger = ledger;
        _appSeats = appSeats;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
        _ptPackages = ptPackages;
        _trainers = trainers;
        _activation = activation;
    }

    public async Task<Result<MemberResponse>> Handle(CreateMemberCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberCreate, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<MemberResponse>(gate.Error);
        }

        var tenant = await _tenants.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null || !RoleNames.IsBusiness(tenant.TenantType))
        {
            return Result.Failure<MemberResponse>(GymErrors.NotFound);
        }

        var branch = request.BranchId is Guid branchId ? await _branches.GetAsync(tenantId, branchId, cancellationToken).ConfigureAwait(false) : null;
        var plan = request.PlanId is Guid planId ? await _plans.GetPlanAsync(tenantId, planId, cancellationToken).ConfigureAwait(false) : null;
        if (request.PlanId.HasValue)
        {
            var membershipGate = await _guard.AllowAsync(PermissionCodes.MembershipCreate, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
            if (membershipGate.IsFailure)
            {
                return Result.Failure<MemberResponse>(membershipGate.Error);
            }
        }

        if (request.InitialPayment is not null)
        {
            var paymentGate = await _guard.AllowAsync(PermissionCodes.PaymentCreate, FeatureCodes.GymPaymentManagement, cancellationToken).ConfigureAwait(false);
            if (paymentGate.IsFailure)
            {
                return Result.Failure<MemberResponse>(paymentGate.Error);
            }

            if (plan is not null && request.InitialPayment.Amount > plan.Price)
            {
                return Result.Failure<MemberResponse>(GymErrors.InvalidState);
            }
        }

        if (request.PlanId.HasValue && (plan is null || !plan.IsActive))
        {
            return Result.Failure<MemberResponse>(plan is { IsActive: false } ? GymErrors.InvalidState : GymErrors.NotFound);
        }

        if (request.BranchId.HasValue && (branch is null || branch.Status != BranchStatus.Active))
        {
            return Result.Failure<MemberResponse>(GymErrors.NotFound);
        }

        if (request.AssignedTrainerId is Guid trainerId)
        {
            var trainer = await _people.GetTrainerAsync(tenantId, trainerId, cancellationToken).ConfigureAwait(false);
            if (trainer is null || trainer.Status != ProfileStatus.Active)
            {
                return Result.Failure<MemberResponse>(GymErrors.NotFound);
            }
        }

        if (!MemberMaps.TryGoal(request.FitnessGoal, out var goal))
        {
            return Result.Failure<MemberResponse>(GymErrors.InvalidState);
        }

        if (request.EnableBasicAppAccess && string.IsNullOrWhiteSpace(request.Email))
        {
            return Result.Failure<MemberResponse>(GymErrors.AppLoginRequired);
        }

        if (request.AssignAppAccess)
        {
            if (request.AppSubscriptionId is null)
            {
                return Result.Failure<MemberResponse>(GymErrors.AppSubscriptionRequired);
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Result.Failure<MemberResponse>(GymErrors.AppLoginRequired);
            }

            if (!await _permissions.HasPermissionAsync(actorId, tenantId, PermissionCodes.LicenseManage, cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure<MemberResponse>(IdentityErrors.Forbidden);
            }
        }

        MemberResponse? response = null;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                UserAccount? user = null;
                var existingAccount = false;
                if (!string.IsNullOrWhiteSpace(request.Email))
                {
                    var password = request.Password;
                    var existing = await _users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct).ConfigureAwait(false);
                    existingAccount = existing is not null;
                    if (existing is null && string.IsNullOrWhiteSpace(password))
                    {
                        password = TemporaryPassword.Create();
                    }

                    var linked = await _accounts.ResolveAsync(request.Email, password, request.FirstName, request.LastName, request.Phone, ct).ConfigureAwait(false);
                    if (linked.IsFailure)
                    {
                        throw new MemberOnboardingException(linked.Error);
                    }

                    user = linked.Value;
                    if (await _people.GetMemberByUserAsync(tenantId, user.Id, ct).ConfigureAwait(false) is not null)
                    {
                        throw new MemberOnboardingException(GymErrors.Conflict);
                    }

                    if (await _memberships.GetActiveAsync(tenantId, user.Id, ct).ConfigureAwait(false) is null)
                    {
                        await _memberships.AddAsync(TenantMember.Create(Guid.NewGuid(), tenantId, user.Id, _clock.UtcNow), ct).ConfigureAwait(false);
                    }

                    var role = await _roles.GetByNameAsync(RoleNames.Member, ct).ConfigureAwait(false) ?? throw new MemberOnboardingException(IdentityErrors.RoleNotFound);
                    var names = await _roles.GetRoleNamesAsync(user.Id, tenantId, ct).ConfigureAwait(false);
                    if (!names.Contains(RoleNames.Member))
                    {
                        await _roles.AddAsync(UserRole.Create(Guid.NewGuid(), user.Id, role.Id, tenantId), ct).ConfigureAwait(false);
                    }
                }

                var today = DateOnly.FromDateTime(_clock.UtcNow);
                var profile = MemberProfile.Create(Guid.NewGuid(), tenantId, user?.Id, $"M{Guid.NewGuid():N}"[..9].ToUpperInvariant(), request.JoinDate ?? today, request.FirstName, request.LastName, request.Phone);
                profile.Update(request.DateOfBirth, request.Gender, request.EmergencyContactName, request.EmergencyContactPhone, request.Address, request.City, request.State, request.PostalCode, request.Status, request.EmergencyContactRelationship, goal, request.AssignedTrainerId, request.JoinDate);
                await _people.AddMemberAsync(profile, ct).ConfigureAwait(false);
                if (_trainers is not null)
                    await _trainers.SyncMemberTrainerAsync(tenantId, profile.Id, request.AssignedTrainerId, request.JoinDate ?? today, actorId, ct).ConfigureAwait(false);
                if (plan is not null)
                {
                    var start = request.StartDate ?? request.JoinDate ?? today;
                    await MemberMembershipOpening.OpenAsync(_plans, _payments, _ledger, _audit, _client, _clock, tenantId, actorId, profile.Id, branch?.Id, plan, start, today, request.AutoRenew, request.InitialPayment, ct).ConfigureAwait(false);
                }

                if (request.PtPackageId is Guid ptPackageId)
                {
                    var ptStart = request.StartDate ?? request.JoinDate ?? today;
                    await MemberPtOpening.OpenAsync(_ptPackages, tenantId, profile.Id, request.AssignedTrainerId, ptPackageId, ptStart, ct).ConfigureAwait(false);
                }

                if (request.AssignAppAccess && request.AppSubscriptionId is Guid appSubscriptionId)
                {
                    var appAccess = await _appSeats.AssignAsync(tenantId, actorId, profile, appSubscriptionId, request.Email, ct).ConfigureAwait(false);
                    if (appAccess.IsFailure)
                    {
                        throw new MemberOnboardingException(appAccess.Error);
                    }
                }

                await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "MemberCreated", "MemberProfile", profile.Id, null, profile.MemberCode, _client.IpAddress, _client.UserAgent, _clock.UtcNow), ct).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                if (user is not null && (request.AssignAppAccess || request.EnableBasicAppAccess) && _activation is not null)
                {
                    await _activation.SendAsync(new AccountActivationNotice(user.Id, user.Email, user.FirstName, user.LastName, RoleNames.Member, tenantId, tenant.Name, existingAccount), ct).ConfigureAwait(false);
                }

                response = await _people.ReadMemberAsync(tenantId, profile.Id, ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (MemberOnboardingException exception)
        {
            return Result.Failure<MemberResponse>(exception.Error);
        }
        return response is null ? Result.Failure<MemberResponse>(GymErrors.NotFound) : Result.Success(response);
    }
}
