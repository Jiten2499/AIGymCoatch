using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Authorization;

public sealed record ListAccessRolesQuery : IRequest<Result<AccessRoleList>>;

public sealed record GetAccessCatalogQuery : IRequest<Result<IReadOnlyList<AccessCatalogModule>>>;

public sealed record ListAccessAssignmentsQuery : IRequest<Result<IReadOnlyList<AccessAssignment>>>;

public sealed record ListAccessOverviewQuery : IRequest<Result<IReadOnlyList<AccessOverviewRow>>>;

public sealed record GetEffectiveAccessQuery(Guid UserId) : IRequest<Result<EffectiveAccess>>;

public sealed class AccessRoleQueryHandler :
    IRequestHandler<ListAccessRolesQuery, Result<AccessRoleList>>,
    IRequestHandler<GetAccessCatalogQuery, Result<IReadOnlyList<AccessCatalogModule>>>,
    IRequestHandler<ListAccessAssignmentsQuery, Result<IReadOnlyList<AccessAssignment>>>,
    IRequestHandler<ListAccessOverviewQuery, Result<IReadOnlyList<AccessOverviewRow>>>,
    IRequestHandler<GetEffectiveAccessQuery, Result<EffectiveAccess>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly IRoleRepository _roles;
    private readonly IAccessRoleStore _store;
    private readonly ITenantMembershipRepository _members;

    public AccessRoleQueryHandler(IOperationGuard guard, ICurrentActor actor, IPermissionService permissions, IRoleRepository roles, IAccessRoleStore store, ITenantMembershipRepository members)
    {
        _guard = guard;
        _actor = actor;
        _permissions = permissions;
        _roles = roles;
        _store = store;
        _members = members;
    }

    public async Task<Result<AccessRoleList>> Handle(ListAccessRolesQuery request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<AccessRoleList>(opened.Error);
        }

        var roles = await _store.ListVisibleAsync(opened.Value, cancellationToken).ConfigureAwait(false);
        var counts = await _store.AssigneeCountsAsync(opened.Value, cancellationToken).ConfigureAwait(false);
        var codes = await _store.PermissionCodesAsync(roles.Select(role => role.Id).ToArray(), cancellationToken).ConfigureAwait(false);
        var items = new List<AccessRoleSummary>(roles.Count);
        foreach (var role in roles)
        {
            var granted = codes.TryGetValue(role.Id, out var value) ? value : [];
            var scopes = await _store.ScopesAsync(role.Id, cancellationToken).ConfigureAwait(false);
            items.Add(new AccessRoleSummary(role.Id, role.Name, AccessRoleCatalog.Label(role.Name), role.Description, role.IsSystem, role.IsProtectedOwner, role.IsActive, counts.TryGetValue(role.Id, out var count) ? count : 0, AccessRoleCatalog.Summary(granted), granted, scopes));
        }

        var canManage = await _permissions.HasPermissionAsync(ActorId(), opened.Value, PermissionCodes.RoleAssign, cancellationToken).ConfigureAwait(false);
        return Result.Success(new AccessRoleList(canManage, items));
    }

    public async Task<Result<IReadOnlyList<AccessCatalogModule>>> Handle(GetAccessCatalogQuery request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AccessCatalogModule>>(opened.Error);
        }

        var known = await _store.PermissionIdsAsync(AccessRoleCatalog.Codes.ToArray(), cancellationToken).ConfigureAwait(false);
        var modules = AccessRoleCatalog.Modules
            .Select(module => new AccessCatalogModule(module.Key, module.Name, module.Scoped, module.Actions.Where(action => known.ContainsKey(action.Code)).ToArray()))
            .Where(module => module.Actions.Count > 0)
            .ToArray();
        return Result.Success<IReadOnlyList<AccessCatalogModule>>(modules);
    }

    public async Task<Result<IReadOnlyList<AccessAssignment>>> Handle(ListAccessAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        return opened.IsFailure
            ? Result.Failure<IReadOnlyList<AccessAssignment>>(opened.Error)
            : Result.Success(await _store.AssignmentsAsync(opened.Value, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<IReadOnlyList<AccessOverviewRow>>> Handle(ListAccessOverviewQuery request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AccessOverviewRow>>(opened.Error);
        }

        var assignments = await _store.AssignmentsAsync(opened.Value, cancellationToken).ConfigureAwait(false);
        var rows = new List<AccessOverviewRow>(assignments.Count);
        foreach (var assignment in assignments)
        {
            var codes = await _roles.GetPermissionCodesAsync(assignment.UserId, opened.Value, cancellationToken).ConfigureAwait(false);
            var excessive = AccessRoleCatalog.SensitiveCodes.Where(codes.Contains).ToArray();
            var restricted = AccessRoleCatalog.Modules
                .Where(module => module.Actions.Any(action => action.Action == "View") && module.Actions.Where(action => action.Action == "View").All(action => !codes.Contains(action.Code)))
                .Select(module => module.Name)
                .ToArray();
            rows.Add(new AccessOverviewRow(assignment.UserId, assignment.DisplayName, assignment.RoleLabel, codes.ToArray(), excessive, restricted, await MemberScopeAsync(assignment, cancellationToken).ConfigureAwait(false)));
        }

        return Result.Success<IReadOnlyList<AccessOverviewRow>>(rows);
    }

    public async Task<Result<EffectiveAccess>> Handle(GetEffectiveAccessQuery request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<EffectiveAccess>(opened.Error);
        }

        if (await _members.GetActiveAsync(opened.Value, request.UserId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<EffectiveAccess>(IdentityErrors.NotMember);
        }

        var assignments = await _store.AssignmentsAsync(opened.Value, cancellationToken).ConfigureAwait(false);
        var person = assignments.FirstOrDefault(row => row.UserId == request.UserId);
        var codes = await _roles.GetPermissionCodesAsync(request.UserId, opened.Value, cancellationToken).ConfigureAwait(false);
        var scopes = person?.RoleId is Guid roleId ? await _store.ScopesAsync(roleId, cancellationToken).ConfigureAwait(false) : [];
        return Result.Success(new EffectiveAccess(request.UserId, person?.DisplayName ?? string.Empty, person?.RoleLabel ?? string.Empty, codes.ToArray(), scopes));
    }

    private async Task<Result<Guid>> OpenAsync(CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.RoleRead, FeatureCodes.GymConfiguration, cancellationToken).ConfigureAwait(false);
        return gate.IsFailure || _actor.TenantId is not Guid tenantId
            ? Result.Failure<Guid>(gate.IsFailure ? gate.Error : IdentityErrors.InvalidCredentials)
            : Result.Success(tenantId);
    }

    private Guid ActorId() => _actor.UserId ?? Guid.Empty;

    private async Task<string> MemberScopeAsync(AccessAssignment assignment, CancellationToken cancellationToken)
    {
        if (assignment.RoleId is not Guid roleId)
        {
            return AccessScopes.Own;
        }

        var stored = await _store.ScopesAsync(roleId, cancellationToken).ConfigureAwait(false);
        return stored.FirstOrDefault(scope => scope.Resource == "members")?.Scope ?? AccessRoleCatalog.FallbackScope(assignment.RoleName);
    }
}
