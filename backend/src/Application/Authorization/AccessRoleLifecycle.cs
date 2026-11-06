using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Authorization;

public sealed record DuplicateAccessRoleCommand(Guid RoleId, string Name) : IRequest<Result<AccessRoleSummary>>;

public sealed class DuplicateAccessRoleCommandValidator : AbstractValidator<DuplicateAccessRoleCommand>
{
    public DuplicateAccessRoleCommandValidator()
    {
        RuleFor(command => command.RoleId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(64);
    }
}

public sealed record SetAccessRoleStatusCommand(Guid RoleId, bool Active) : IRequest<Result>;

public sealed class SetAccessRoleStatusCommandValidator : AbstractValidator<SetAccessRoleStatusCommand>
{
    public SetAccessRoleStatusCommandValidator() => RuleFor(command => command.RoleId).NotEmpty();
}

public sealed record AssignAccessRoleCommand(Guid UserId, Guid RoleId) : IRequest<Result>;

public sealed class AssignAccessRoleCommandValidator : AbstractValidator<AssignAccessRoleCommand>
{
    public AssignAccessRoleCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.RoleId).NotEmpty();
    }
}

public sealed class AccessRoleLifecycleHandler :
    IRequestHandler<DuplicateAccessRoleCommand, Result<AccessRoleSummary>>,
    IRequestHandler<SetAccessRoleStatusCommand, Result>,
    IRequestHandler<AssignAccessRoleCommand, Result>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IRoleRepository _roles;
    private readonly IAccessRoleStore _store;
    private readonly ITenantRepository _tenants;
    private readonly ITenantMembershipRepository _members;
    private readonly IPermissionCacheInvalidator _cache;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public AccessRoleLifecycleHandler(IOperationGuard guard, ICurrentActor actor, IRoleRepository roles, IAccessRoleStore store, ITenantRepository tenants, ITenantMembershipRepository members, IPermissionCacheInvalidator cache, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _roles = roles;
        _store = store;
        _tenants = tenants;
        _members = members;
        _cache = cache;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<AccessRoleSummary>> Handle(DuplicateAccessRoleCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<AccessRoleSummary>(opened.Error);
        }

        var source = await _store.FindVisibleAsync(opened.Value.TenantId, request.RoleId, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return Result.Failure<AccessRoleSummary>(IdentityErrors.RoleNotFound);
        }

        if (source.IsProtectedOwner)
        {
            return Result.Failure<AccessRoleSummary>(IdentityErrors.RoleProtected);
        }

        if (await _store.NameTakenAsync(opened.Value.TenantId, request.Name, null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<AccessRoleSummary>(IdentityErrors.RoleNameConflict);
        }

        var codes = (await _store.PermissionCodesAsync(source.Id, cancellationToken).ConfigureAwait(false)).Where(AccessRoleCatalog.Codes.Contains).Distinct(StringComparer.Ordinal).ToArray();
        var held = await _roles.GetPermissionCodesAsync(opened.Value.ActorId, opened.Value.TenantId, cancellationToken).ConfigureAwait(false);
        if (!AccessRoleRules.CanGrant(held, codes))
        {
            return Result.Failure<AccessRoleSummary>(IdentityErrors.RoleEscalation);
        }

        var ids = await _store.PermissionIdsAsync(codes, cancellationToken).ConfigureAwait(false);
        var scopes = await _store.ScopesAsync(source.Id, cancellationToken).ConfigureAwait(false);
        var normalized = AccessRoleCommandHandler.Normalize(scopes);
        var copy = Role.CreateCustom(Guid.NewGuid(), opened.Value.TenantId, request.Name, source.Description);
        await _store.AddAsync(copy, cancellationToken).ConfigureAwait(false);
        await _store.ReplaceGrantsAsync(copy.Id, codes.Select(code => ids[code]).ToArray(), normalized.IsSuccess ? normalized.Value : [], cancellationToken).ConfigureAwait(false);
        await WriteAsync(opened.Value, "RoleDuplicated", copy.Id, copy.Name, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new AccessRoleSummary(copy.Id, copy.Name, copy.Name, copy.Description, false, false, true, 0, AccessRoleCatalog.Summary(codes), codes, normalized.IsSuccess ? normalized.Value : []));
    }

    public async Task<Result> Handle(SetAccessRoleStatusCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return opened;
        }

        var role = await _store.FindVisibleAsync(opened.Value.TenantId, request.RoleId, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return Result.Failure(IdentityErrors.RoleNotFound);
        }

        if (role.IsSystem || role.IsProtectedOwner || !role.TrySetActive(request.Active))
        {
            return Result.Failure(role.IsProtectedOwner ? IdentityErrors.RoleProtected : IdentityErrors.RoleSystemLocked);
        }

        if (!request.Active && await _store.AssigneeCountAsync(opened.Value.TenantId, role.Id, cancellationToken).ConfigureAwait(false) > 0)
        {
            return Result.Failure(IdentityErrors.RoleStillAssigned);
        }

        foreach (var userId in await _store.AssigneeIdsAsync(opened.Value.TenantId, role.Id, cancellationToken).ConfigureAwait(false))
        {
            _cache.Schedule(opened.Value.TenantId, userId);
        }

        await WriteAsync(opened.Value, request.Active ? "RoleActivated" : "RoleDeactivated", role.Id, role.Name, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> Handle(AssignAccessRoleCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return opened;
        }

        var role = await _store.FindVisibleAsync(opened.Value.TenantId, request.RoleId, cancellationToken).ConfigureAwait(false);
        if (role is null || !role.IsActive)
        {
            return Result.Failure(role is null ? IdentityErrors.RoleNotFound : IdentityErrors.RoleInactive);
        }

        var actorRoles = await _roles.GetRoleNamesAsync(opened.Value.ActorId, opened.Value.TenantId, cancellationToken).ConfigureAwait(false);
        if (!AccessRoleRules.OwnerAssignmentAllowed(role.Name, actorRoles))
        {
            return Result.Failure(IdentityErrors.OwnerAssignmentRejected);
        }

        if (await _members.GetActiveAsync(opened.Value.TenantId, request.UserId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure(IdentityErrors.NotMember);
        }

        var tenant = await _tenants.GetAsync(opened.Value.TenantId, cancellationToken).ConfigureAwait(false);
        if (tenant?.OwnerUserId == request.UserId && role.Name != RoleNames.GymOwner)
        {
            return Result.Failure(IdentityErrors.OwnerRoleRejected);
        }

        await _store.ReplaceManagedRoleAsync(opened.Value.TenantId, request.UserId, role, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        await WriteAsync(opened.Value, "RoleAssigned", request.UserId, role.Name, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result<(Guid TenantId, Guid ActorId)>> OpenAsync(CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.RoleAssign, FeatureCodes.GymConfiguration, cancellationToken).ConfigureAwait(false);
        return gate.IsFailure || _actor.UserId is not Guid actorId || _actor.TenantId is not Guid tenantId
            ? Result.Failure<(Guid, Guid)>(gate.IsFailure ? gate.Error : IdentityErrors.InvalidCredentials)
            : Result.Success((tenantId, actorId));
    }

    private async Task WriteAsync((Guid TenantId, Guid ActorId) opened, string action, Guid entityId, string name, CancellationToken cancellationToken) =>
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), opened.TenantId, opened.ActorId, action, "Role", entityId, null, name, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
}
