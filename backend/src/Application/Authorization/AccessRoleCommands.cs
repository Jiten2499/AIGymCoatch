using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Authorization;

public sealed record CreateAccessRoleCommand(string Name, string? Description, IReadOnlyList<string> PermissionCodes, IReadOnlyList<AccessScopeInput> Scopes) : IRequest<Result<AccessRoleSummary>>;

public sealed class CreateAccessRoleCommandValidator : AbstractValidator<CreateAccessRoleCommand>
{
    public CreateAccessRoleCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(64);
        RuleFor(command => command.Description).MaximumLength(256);
    }
}

public sealed record UpdateAccessRoleCommand(Guid RoleId, string Name, string? Description, IReadOnlyList<string> PermissionCodes, IReadOnlyList<AccessScopeInput> Scopes) : IRequest<Result<AccessRoleSummary>>;

public sealed class UpdateAccessRoleCommandValidator : AbstractValidator<UpdateAccessRoleCommand>
{
    public UpdateAccessRoleCommandValidator()
    {
        RuleFor(command => command.RoleId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(64);
        RuleFor(command => command.Description).MaximumLength(256);
    }
}

public sealed class AccessRoleCommandHandler :
    IRequestHandler<CreateAccessRoleCommand, Result<AccessRoleSummary>>,
    IRequestHandler<UpdateAccessRoleCommand, Result<AccessRoleSummary>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IRoleRepository _roles;
    private readonly IAccessRoleStore _store;
    private readonly IPermissionCacheInvalidator _cache;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public AccessRoleCommandHandler(IOperationGuard guard, ICurrentActor actor, IRoleRepository roles, IAccessRoleStore store, IPermissionCacheInvalidator cache, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _roles = roles;
        _store = store;
        _cache = cache;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<AccessRoleSummary>> Handle(CreateAccessRoleCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<AccessRoleSummary>(opened.Error);
        }

        var prepared = await PrepareAsync(opened.Value.TenantId, opened.Value.ActorId, null, request.Name, request.PermissionCodes, request.Scopes, cancellationToken).ConfigureAwait(false);
        if (prepared.IsFailure)
        {
            return Result.Failure<AccessRoleSummary>(prepared.Error);
        }

        var role = Role.CreateCustom(Guid.NewGuid(), opened.Value.TenantId, request.Name, request.Description);
        await _store.AddAsync(role, cancellationToken).ConfigureAwait(false);
        await _store.ReplaceGrantsAsync(role.Id, prepared.Value.PermissionIds, prepared.Value.Scopes, cancellationToken).ConfigureAwait(false);
        await AuditAsync(opened.Value, "RoleCreated", role.Id, role.Name, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await SummaryAsync(role, prepared.Value.Codes, prepared.Value.Scopes, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<AccessRoleSummary>> Handle(UpdateAccessRoleCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<AccessRoleSummary>(opened.Error);
        }

        var role = await _store.FindVisibleAsync(opened.Value.TenantId, request.RoleId, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return Result.Failure<AccessRoleSummary>(IdentityErrors.RoleNotFound);
        }

        if (role.IsProtectedOwner)
        {
            return Result.Failure<AccessRoleSummary>(IdentityErrors.RoleProtected);
        }

        if (role.IsSystem)
        {
            return Result.Failure<AccessRoleSummary>(IdentityErrors.RoleSystemLocked);
        }

        var prepared = await PrepareAsync(opened.Value.TenantId, opened.Value.ActorId, role.Id, request.Name, request.PermissionCodes, request.Scopes, cancellationToken).ConfigureAwait(false);
        if (prepared.IsFailure || !role.TryRename(request.Name, request.Description))
        {
            return Result.Failure<AccessRoleSummary>(prepared.IsFailure ? prepared.Error : IdentityErrors.RoleProtected);
        }

        await _store.ReplaceGrantsAsync(role.Id, prepared.Value.PermissionIds, prepared.Value.Scopes, cancellationToken).ConfigureAwait(false);
        foreach (var userId in await _store.AssigneeIdsAsync(opened.Value.TenantId, role.Id, cancellationToken).ConfigureAwait(false))
        {
            _cache.Schedule(opened.Value.TenantId, userId);
        }

        await AuditAsync(opened.Value, "RolePermissionsChanged", role.Id, role.Name, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await SummaryAsync(role, prepared.Value.Codes, prepared.Value.Scopes, cancellationToken).ConfigureAwait(false));
    }

    private async Task<Result<(Guid TenantId, Guid ActorId)>> OpenAsync(CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.RoleAssign, FeatureCodes.GymConfiguration, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.UserId is not Guid actorId || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<(Guid, Guid)>(gate.IsFailure ? gate.Error : IdentityErrors.InvalidCredentials);
        }

        return Result.Success((tenantId, actorId));
    }

    private async Task<Result<(string[] Codes, Guid[] PermissionIds, IReadOnlyList<AccessScopeInput> Scopes)>> PrepareAsync(Guid tenantId, Guid actorId, Guid? roleId, string name, IReadOnlyList<string> codes, IReadOnlyList<AccessScopeInput> scopes, CancellationToken cancellationToken)
    {
        if (await _store.NameTakenAsync(tenantId, name, roleId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<(string[], Guid[], IReadOnlyList<AccessScopeInput>)>(IdentityErrors.RoleNameConflict);
        }

        var normalized = Normalize(scopes);
        if (normalized.IsFailure)
        {
            return Result.Failure<(string[], Guid[], IReadOnlyList<AccessScopeInput>)>(normalized.Error);
        }

        var distinct = codes.Distinct(StringComparer.Ordinal).ToArray();
        var held = await _roles.GetPermissionCodesAsync(actorId, tenantId, cancellationToken).ConfigureAwait(false);
        if (!AccessRoleRules.CanGrant(held, distinct))
        {
            return Result.Failure<(string[], Guid[], IReadOnlyList<AccessScopeInput>)>(IdentityErrors.RoleEscalation);
        }

        var ids = await _store.PermissionIdsAsync(distinct, cancellationToken).ConfigureAwait(false);
        if (ids.Count != distinct.Length)
        {
            return Result.Failure<(string[], Guid[], IReadOnlyList<AccessScopeInput>)>(IdentityErrors.RoleEscalation);
        }

        return Result.Success((distinct, distinct.Select(code => ids[code]).ToArray(), normalized.Value));
    }

    private async Task AuditAsync((Guid TenantId, Guid ActorId) opened, string action, Guid roleId, string name, CancellationToken cancellationToken) =>
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), opened.TenantId, opened.ActorId, action, "Role", roleId, null, name, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);

    private async Task<AccessRoleSummary> SummaryAsync(Role role, IReadOnlyList<string> codes, IReadOnlyList<AccessScopeInput> scopes, CancellationToken cancellationToken) =>
        new(role.Id, role.Name, AccessRoleCatalog.Label(role.Name), role.Description, role.IsSystem, role.IsProtectedOwner, role.IsActive, await _store.AssigneeCountAsync(role.TenantId ?? Guid.Empty, role.Id, cancellationToken).ConfigureAwait(false), AccessRoleCatalog.Summary(codes), codes, scopes);

    internal static Result<IReadOnlyList<AccessScopeInput>> Normalize(IReadOnlyList<AccessScopeInput> scopes)
    {
        if (scopes.Any(scope => AccessRoleCatalog.Modules.All(module => module.Key != scope.Resource) || !moduleScoped(scope.Resource)))
        {
            return Result.Failure<IReadOnlyList<AccessScopeInput>>(IdentityErrors.RoleEscalation);
        }

        var normalized = AccessRoleCatalog.Modules.Where(module => module.Scoped).Select(module =>
        {
            var supplied = scopes.FirstOrDefault(scope => scope.Resource == module.Key)?.Scope ?? AccessScopes.Own;
            return AccessScopes.IsKnown(supplied) ? new AccessScopeInput(module.Key, supplied) : null;
        }).ToArray();
        return normalized.Any(scope => scope is null)
            ? Result.Failure<IReadOnlyList<AccessScopeInput>>(IdentityErrors.RoleEscalation)
            : Result.Success<IReadOnlyList<AccessScopeInput>>(normalized!);

        static bool moduleScoped(string resource) => AccessRoleCatalog.Modules.Any(module => module.Key == resource && module.Scoped);
    }
}
