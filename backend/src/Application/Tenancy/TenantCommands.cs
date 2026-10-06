using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Tenancy;

public sealed record GetMyTenantsQuery : IRequest<Result<IReadOnlyList<WorkspaceSummary>>>;

public sealed class GetMyTenantsQueryHandler : IRequestHandler<GetMyTenantsQuery, Result<IReadOnlyList<WorkspaceSummary>>>
{
    private readonly ICurrentActor _actor;
    private readonly ITenantMembershipRepository _members;

    public GetMyTenantsQueryHandler(ICurrentActor actor, ITenantMembershipRepository members)
    {
        _actor = actor;
        _members = members;
    }

    public async Task<Result<IReadOnlyList<WorkspaceSummary>>> Handle(GetMyTenantsQuery request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId)
        {
            return Result.Failure<IReadOnlyList<WorkspaceSummary>>(IdentityErrors.InvalidCredentials);
        }

        var workspaces = await _members.ListForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return Result.Success(workspaces);
    }
}

public sealed record SwitchTenantCommand(Guid TenantId) : IRequest<Result<AuthResponse>>;

public sealed class SwitchTenantCommandValidator : AbstractValidator<SwitchTenantCommand>
{
    public SwitchTenantCommandValidator() => RuleFor(command => command.TenantId).NotEmpty();
}

public sealed class SwitchTenantCommandHandler : IRequestHandler<SwitchTenantCommand, Result<AuthResponse>>
{
    private readonly ICurrentActor _actor;
    private readonly ITenantMembershipRepository _members;
    private readonly ITenantRepository _tenants;
    private readonly IUserAccountRepository _users;
    private readonly ISessionIssuer _sessions;
    private readonly IAuditLogRepository _audit;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public SwitchTenantCommandHandler(
        ICurrentActor actor,
        ITenantMembershipRepository members,
        ITenantRepository tenants,
        IUserAccountRepository users,
        ISessionIssuer sessions,
        IAuditLogRepository audit,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _actor = actor;
        _members = members;
        _tenants = tenants;
        _users = users;
        _sessions = sessions;
        _audit = audit;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<AuthResponse>> Handle(SwitchTenantCommand request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId)
        {
            return Result.Failure<AuthResponse>(IdentityErrors.InvalidCredentials);
        }

        if (await _members.GetActiveAsync(request.TenantId, userId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<AuthResponse>(IdentityErrors.NotMember);
        }

        var tenant = await _tenants.GetAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
        var user = await _users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (tenant is null || user is null || tenant.Status != TenantStatus.Active)
        {
            return Result.Failure<AuthResponse>(IdentityErrors.TenantUnavailable);
        }

        var now = _clock.UtcNow;
        await _audit.AddAsync(
            AuditLog.Create(Guid.NewGuid(), tenant.Id, userId, "SwitchTenant", "Tenant", tenant.Id, null, tenant.Name, _client.IpAddress, _client.UserAgent, now),
            cancellationToken).ConfigureAwait(false);
        return Result.Success(await _sessions.IssueAsync(user, tenant, null, cancellationToken).ConfigureAwait(false));
    }
}

public sealed record CreateTenantCommand(string Name, TenantType TenantType) : IRequest<Result<TenantSummary>>;

public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.TenantType).IsInEnum();
    }
}

public sealed class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, Result<TenantSummary>>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly ITenantMembershipRepository _members;
    private readonly IRoleRepository _roles;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IPlanDefinitionRepository _planDefinitions;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public CreateTenantCommandHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        ITenantRepository tenants,
        ITenantMembershipRepository members,
        IRoleRepository roles,
        ISubscriptionRepository subscriptions,
        IPlanDefinitionRepository planDefinitions,
        IBusinessProfileRepository profiles,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _actor = actor;
        _permissions = permissions;
        _tenants = tenants;
        _members = members;
        _roles = roles;
        _subscriptions = subscriptions;
        _planDefinitions = planDefinitions;
        _profiles = profiles;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<TenantSummary>> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId || _actor.TenantId is not Guid currentTenantId)
        {
            return Result.Failure<TenantSummary>(IdentityErrors.InvalidCredentials);
        }

        if (!RoleNames.IsBusiness(request.TenantType))
        {
            return Result.Failure<TenantSummary>(IdentityErrors.PersonalTypeRejected);
        }

        if (!await _permissions.HasPermissionAsync(userId, currentTenantId, PermissionCodes.TenantCreate, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<TenantSummary>(IdentityErrors.Forbidden);
        }

        var role = await _roles.GetByNameAsync(RoleNames.OwnerRoleFor(request.TenantType), cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return Result.Failure<TenantSummary>(IdentityErrors.RoleNotFound);
        }

        var businessPlan = await _planDefinitions.GetByCodeAsync(SubscriptionIssuance.BusinessFree, cancellationToken).ConfigureAwait(false);
        if (businessPlan is null || businessPlan.Status != PlanStatus.Active)
        {
            return Result.Failure<TenantSummary>(new Error("license.plan_missing", "The business plan is not configured."));
        }

        TenantSummary? summary = null;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = _clock.UtcNow;
            var tenant = Tenant.Create(Guid.NewGuid(), request.Name.Trim(), request.TenantType);
            tenant.AssignOwner(userId);
            await _tenants.AddAsync(tenant, ct).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            await _members.AddAsync(TenantMember.Create(Guid.NewGuid(), tenant.Id, userId, now), ct).ConfigureAwait(false);
            await _roles.AddAsync(UserRole.Create(Guid.NewGuid(), userId, role.Id, tenant.Id), ct).ConfigureAwait(false);
            var subscription = Subscription.ForTenant(Guid.NewGuid(), tenant.Id, businessPlan.Id, now, businessPlan.BaseCapacity);
            await _subscriptions.AddAsync(subscription, ct).ConfigureAwait(false);
            await _profiles.AddAsync(BusinessProfile.Create(Guid.NewGuid(), tenant.Id, "UTC", "USD"), ct).ConfigureAwait(false);
            await _audit.AddAsync(
                AuditLog.Create(Guid.NewGuid(), tenant.Id, userId, "CreateTenant", "Tenant", tenant.Id, null, tenant.Name, _client.IpAddress, _client.UserAgent, now),
                ct).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            summary = new TenantSummary(tenant.Id, tenant.Name, tenant.TenantType, tenant.Status);
        }, cancellationToken).ConfigureAwait(false);

        return Result.Success(summary!);
    }
}
