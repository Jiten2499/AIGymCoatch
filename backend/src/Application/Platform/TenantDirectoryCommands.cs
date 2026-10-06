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

namespace LifeFitAI.Application.Platform;

public sealed record BusinessTenantCreated(Guid Id, string Name, string TenantType, string Status, string OwnerEmail, string? TemporaryPassword);

public sealed record CreateBusinessTenantCommand(string Name, TenantType TenantType, string OwnerEmail, string OwnerFirstName, string OwnerLastName) : IRequest<Result<BusinessTenantCreated>>;

public sealed class CreateBusinessTenantCommandValidator : AbstractValidator<CreateBusinessTenantCommand>
{
    public CreateBusinessTenantCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.TenantType).IsInEnum();
        RuleFor(command => command.OwnerEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(command => command.OwnerFirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.OwnerLastName).NotEmpty().MaximumLength(100);
    }
}

public sealed record UpdateBusinessTenantCommand(Guid TenantId, string Name, TenantType TenantType, TenantStatus Status) : IRequest<Result>;

public sealed class UpdateBusinessTenantCommandValidator : AbstractValidator<UpdateBusinessTenantCommand>
{
    public UpdateBusinessTenantCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.TenantType).IsInEnum();
        RuleFor(command => command.Status).IsInEnum();
    }
}

public sealed record DeleteBusinessTenantsCommand(IReadOnlyList<Guid> TenantIds) : IRequest<Result>;

public sealed class DeleteBusinessTenantsCommandValidator : AbstractValidator<DeleteBusinessTenantsCommand>
{
    public DeleteBusinessTenantsCommandValidator() => RuleFor(command => command.TenantIds).NotEmpty();
}

public sealed class TenantDirectoryHandler :
    IRequestHandler<CreateBusinessTenantCommand, Result<BusinessTenantCreated>>,
    IRequestHandler<UpdateBusinessTenantCommand, Result>,
    IRequestHandler<DeleteBusinessTenantsCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IRoleRepository _roles;
    private readonly IPermissionService _permissions;
    private readonly IUserAccountRepository _users;
    private readonly IAccountLinker _accounts;
    private readonly ITenantRepository _tenants;
    private readonly ITenantMembershipRepository _members;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IPlanDefinitionRepository _plans;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public TenantDirectoryHandler(
        ICurrentActor actor,
        IRoleRepository roles,
        IPermissionService permissions,
        IUserAccountRepository users,
        IAccountLinker accounts,
        ITenantRepository tenants,
        ITenantMembershipRepository members,
        ISubscriptionRepository subscriptions,
        IPlanDefinitionRepository plans,
        IBusinessProfileRepository profiles,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _actor = actor;
        _roles = roles;
        _permissions = permissions;
        _users = users;
        _accounts = accounts;
        _tenants = tenants;
        _members = members;
        _subscriptions = subscriptions;
        _plans = plans;
        _profiles = profiles;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<BusinessTenantCreated>> Handle(CreateBusinessTenantCommand request, CancellationToken cancellationToken)
    {
        if (await GateAsync(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<BusinessTenantCreated>(denied.Error);
        }

        if (!RoleNames.IsBusiness(request.TenantType))
        {
            return Result.Failure<BusinessTenantCreated>(IdentityErrors.PersonalTypeRejected);
        }

        var role = await _roles.GetByNameAsync(RoleNames.OwnerRoleFor(request.TenantType), cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return Result.Failure<BusinessTenantCreated>(IdentityErrors.RoleNotFound);
        }

        var plan = await _plans.GetByCodeAsync(SubscriptionIssuance.BusinessFree, cancellationToken).ConfigureAwait(false);
        if (plan is null || plan.Status != PlanStatus.Active)
        {
            return Result.Failure<BusinessTenantCreated>(new Error("license.plan_missing", "The business plan is not configured."));
        }

        var email = request.OwnerEmail.Trim().ToLowerInvariant();
        var existing = await _users.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        var password = existing is null ? TemporaryPassword.Create() : null;
        BusinessTenantCreated? created = null;
        Result<UserAccount>? ownerFailure = null;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var owner = await _accounts.ResolveAsync(email, password, request.OwnerFirstName, request.OwnerLastName, null, ct).ConfigureAwait(false);
                if (owner.IsFailure)
                {
                    ownerFailure = owner;
                    throw new OwnerAccountException();
                }

                var now = _clock.UtcNow;
                var tenant = Tenant.Create(Guid.NewGuid(), request.Name.Trim(), request.TenantType);
                tenant.AssignOwner(owner.Value.Id);
                await _tenants.AddAsync(tenant, ct).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                await _members.AddAsync(TenantMember.Create(Guid.NewGuid(), tenant.Id, owner.Value.Id, now), ct).ConfigureAwait(false);
                await _roles.AddAsync(UserRole.Create(Guid.NewGuid(), owner.Value.Id, role.Id, tenant.Id), ct).ConfigureAwait(false);
                await _subscriptions.AddAsync(Subscription.ForTenant(Guid.NewGuid(), tenant.Id, plan.Id, now, plan.BaseCapacity), ct).ConfigureAwait(false);
                await _profiles.AddAsync(BusinessProfile.Create(Guid.NewGuid(), tenant.Id, "UTC", "USD"), ct).ConfigureAwait(false);
                await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenant.Id, _actor.UserId, "CreateBusinessTenant", "Tenant", tenant.Id, null, tenant.Name, _client.IpAddress, _client.UserAgent, now), ct).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                created = new BusinessTenantCreated(tenant.Id, tenant.Name, tenant.TenantType.ToString(), tenant.Status.ToString(), email, password);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OwnerAccountException)
        {
            return Result.Failure<BusinessTenantCreated>(ownerFailure?.Error ?? new Error("tenant.owner_invalid", "The owner account could not be created."));
        }

        return created is null
            ? Result.Failure<BusinessTenantCreated>(new Error("tenant.owner_invalid", "The owner account could not be created."))
            : Result.Success(created);
    }

    public async Task<Result> Handle(UpdateBusinessTenantCommand request, CancellationToken cancellationToken)
    {
        if (await GateAsync(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return denied;
        }

        var tenant = await _tenants.GetAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            return Result.Failure(new Error("platform.not_found", "The workspace was not found."));
        }

        tenant.Rename(request.Name);
        if (tenant.TenantType != TenantType.Platform && !tenant.ChangeType(request.TenantType))
        {
            return Result.Failure(IdentityErrors.PersonalTypeRejected);
        }

        tenant.SetStatus(request.Status);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenant.Id, _actor.UserId, "UpdateBusinessTenant", "Tenant", tenant.Id, null, tenant.Name, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> Handle(DeleteBusinessTenantsCommand request, CancellationToken cancellationToken)
    {
        if (await GateAsync(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return denied;
        }

        var ids = request.TenantIds.Distinct().ToArray();
        var tenants = new List<Tenant>();
        foreach (var id in ids)
        {
            var tenant = await _tenants.GetAsync(id, cancellationToken).ConfigureAwait(false);
            if (tenant is null)
            {
                return Result.Failure(new Error("platform.not_found", "A selected workspace was not found."));
            }

            if (tenant.Id == PlatformConstants.PlatformTenantId || tenant.TenantType == TenantType.Platform)
            {
                return Result.Failure(new Error("tenant.platform_protected", "The platform workspace cannot be deleted."));
            }

            tenants.Add(tenant);
        }

        var now = _clock.UtcNow;
        foreach (var tenant in tenants)
        {
            tenant.SoftDelete(now);
            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenant.Id, _actor.UserId, "DeleteBusinessTenant", "Tenant", tenant.Id, null, tenant.Name, _client.IpAddress, _client.UserAgent, now), cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result> GateAsync(CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure(IdentityErrors.InvalidCredentials);
        }

        var roles = await _roles.GetRoleNamesAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
        if (!roles.Contains(RoleNames.SuperAdmin, StringComparer.Ordinal)
            || !await _permissions.HasPermissionAsync(userId, tenantId, PermissionCodes.PlatformAdmin, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(IdentityErrors.Forbidden);
        }

        return Result.Success();
    }
}

sealed class OwnerAccountException : Exception
{
}
