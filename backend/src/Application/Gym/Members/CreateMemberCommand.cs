using FluentValidation;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Members;

public sealed record CreateMemberCommand(
    string Email,
    string? Password,
    string FirstName,
    string LastName,
    string? Phone,
    Guid BranchId,
    Guid PlanId,
    DateOnly StartDate,
    bool AutoRenew) : IRequest<Result<MemberResponse>>;

public sealed class CreateMemberCommandValidator : AbstractValidator<CreateMemberCommand>
{
    public CreateMemberCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress();
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.BranchId).NotEmpty();
        RuleFor(command => command.PlanId).NotEmpty();
    }
}

public sealed class CreateMemberCommandHandler : IRequestHandler<CreateMemberCommand, Result<MemberResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IAccountLinker _accounts;
    private readonly ITenantRepository _tenants;
    private readonly ITenantMembershipRepository _memberships;
    private readonly IRoleRepository _roles;
    private readonly IPeopleRepository _people;
    private readonly IBranchRepository _branches;
    private readonly IMembershipRepository _plans;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public CreateMemberCommandHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        IAccountLinker accounts,
        ITenantRepository tenants,
        ITenantMembershipRepository memberships,
        IRoleRepository roles,
        IPeopleRepository people,
        IBranchRepository branches,
        IMembershipRepository plans,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _accounts = accounts;
        _tenants = tenants;
        _memberships = memberships;
        _roles = roles;
        _people = people;
        _branches = branches;
        _plans = plans;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
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

        var branch = await _branches.GetAsync(tenantId, request.BranchId, cancellationToken).ConfigureAwait(false);
        var plan = await _plans.GetPlanAsync(tenantId, request.PlanId, cancellationToken).ConfigureAwait(false);
        if (branch is null || branch.Status != BranchStatus.Active || plan is null || !plan.IsActive)
        {
            return Result.Failure<MemberResponse>(GymErrors.NotFound);
        }

        MemberResponse? response = null;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var linked = await _accounts.ResolveAsync(request.Email, request.Password, request.FirstName, request.LastName, request.Phone, ct).ConfigureAwait(false);
                if (linked.IsFailure)
                {
                    throw new MemberOnboardingException(linked.Error);
                }

                var user = linked.Value;
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

                var today = DateOnly.FromDateTime(_clock.UtcNow);
                var profile = MemberProfile.Create(Guid.NewGuid(), tenantId, user.Id, $"M{Guid.NewGuid():N}"[..9].ToUpperInvariant(), today);
                await _people.AddMemberAsync(profile, ct).ConfigureAwait(false);
                var membership = GymMembership.Create(Guid.NewGuid(), tenantId, profile.Id, branch.Id, plan.Id, $"G{Guid.NewGuid():N}"[..9].ToUpperInvariant(), request.StartDate, plan.DurationDays, today, request.AutoRenew, null);
                await _plans.AddMembershipAsync(membership, ct).ConfigureAwait(false);
                await _plans.AddHistoryAsync(MembershipHistoryEntry.Create(Guid.NewGuid(), tenantId, membership.Id, MembershipAction.Created, null, membership.Status, request.StartDate, null, actorId, _clock.UtcNow), ct).ConfigureAwait(false);
                await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "CreateMember", "MemberProfile", profile.Id, null, profile.MemberCode, _client.IpAddress, _client.UserAgent, _clock.UtcNow), ct).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                response = MemberMaps.From(profile, user);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (MemberOnboardingException exception)
        {
            return Result.Failure<MemberResponse>(exception.Error);
        }

        return Result.Success(response!);
    }
}

public sealed class MemberOnboardingException : Exception
{
    public MemberOnboardingException(Error error) => Error = error;

    public Error Error { get; }
}
