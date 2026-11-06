using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Tenancy;

public sealed record WorkspaceUserResponse(Guid Id, string Email, string FirstName, string LastName, string Status, string Role, bool IsOwner, DateTime CreatedAt, string Plan, bool HasPhoto = false);

public sealed record WorkspaceUserCreated(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    string? TemporaryPassword,
    Guid? StaffProfileId = null,
    string? InvitationToken = null,
    string Status = "Active",
    string? EmployeeCode = null);

public static class WorkspaceRoleRules
{
    public static readonly string[] Assignable = RoleNames.BusinessTemplates;

    public static bool CanAssign(string roleName) =>
        !string.IsNullOrWhiteSpace(roleName)
        && !string.Equals(roleName, RoleNames.SuperAdmin, StringComparison.Ordinal)
        && !string.Equals(roleName, RoleNames.PersonalOwner, StringComparison.Ordinal)
        && !string.Equals(roleName, RoleNames.PersonalMember, StringComparison.Ordinal);

    public static bool IsAssignable(Role role, Guid tenantId) =>
        role.IsActive
        && (AccessRoleRules.IsManaged(role, tenantId)
            || (role.TenantId is null && string.Equals(role.Name, RoleNames.Member, StringComparison.Ordinal)));

    public static string Display(IReadOnlyList<string> names)
    {
        var custom = names.FirstOrDefault(name => CanAssign(name) && !IsPersonalRole(name) && !IsTemplate(name));
        if (custom is not null)
        {
            return custom;
        }

        return RoleNames.BusinessStaff.FirstOrDefault(names.Contains)
            ?? names.FirstOrDefault(name => CanAssign(name) && !IsPersonalRole(name))
            ?? names.FirstOrDefault()
            ?? string.Empty;
    }

    private static bool IsTemplate(string roleName) =>
        RoleNames.BusinessStaff.Contains(roleName, StringComparer.Ordinal);

    private static bool IsPersonalRole(string roleName) =>
        string.Equals(roleName, RoleNames.PersonalOwner, StringComparison.Ordinal)
        || string.Equals(roleName, RoleNames.PersonalMember, StringComparison.Ordinal)
        || string.Equals(roleName, RoleNames.Member, StringComparison.Ordinal);

    public static string Label(string roleName) => AccessRoleCatalog.Label(roleName);
}

internal sealed record WorkspaceContext(Guid TenantId, Guid ActorId, Guid? OwnerUserId);

internal static class WorkspaceAccess
{
    public static async Task<Result<WorkspaceContext>> OpenAsync(
        ICurrentActor actor,
        ITenantRepository tenants,
        IPermissionService permissions,
        string permission,
        CancellationToken cancellationToken)
    {
        if (actor.UserId is not Guid actorId || actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<WorkspaceContext>(IdentityErrors.InvalidCredentials);
        }

        var tenant = await tenants.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            return Result.Failure<WorkspaceContext>(IdentityErrors.TenantNotFound);
        }

        if (tenant.TenantType == TenantType.Platform)
        {
            return Result.Failure<WorkspaceContext>(IdentityErrors.PlatformWorkspaceRejected);
        }

        if (!await permissions.HasPermissionAsync(actorId, tenantId, permission, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<WorkspaceContext>(IdentityErrors.Forbidden);
        }

        return Result.Success(new WorkspaceContext(tenantId, actorId, tenant.OwnerUserId));
    }
}

public sealed record ListWorkspaceUsersQuery(int Page, int PageSize) : IRequest<Result<PageResult<WorkspaceUserResponse>>>;

public sealed class ListWorkspaceUsersQueryValidator : AbstractValidator<ListWorkspaceUsersQuery>
{
    public ListWorkspaceUsersQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class ListWorkspaceUsersQueryHandler : IRequestHandler<ListWorkspaceUsersQuery, Result<PageResult<WorkspaceUserResponse>>>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly IUserAccountRepository _users;
    private readonly IRoleRepository _roles;
    private readonly ILicenseAdminStore _licenses;

    public ListWorkspaceUsersQueryHandler(ICurrentActor actor, IPermissionService permissions, ITenantRepository tenants, IUserAccountRepository users, IRoleRepository roles, ILicenseAdminStore licenses)
    {
        _actor = actor;
        _permissions = permissions;
        _tenants = tenants;
        _users = users;
        _roles = roles;
        _licenses = licenses;
    }

    public async Task<Result<PageResult<WorkspaceUserResponse>>> Handle(ListWorkspaceUsersQuery request, CancellationToken cancellationToken)
    {
        var access = await WorkspaceAccess.OpenAsync(_actor, _tenants, _permissions, PermissionCodes.TenantMemberRead, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<PageResult<WorkspaceUserResponse>>(access.Error);
        }

        var page = new PageQuery(request.Page, request.PageSize);
        var scope = new DirectoryScope(false, access.Value.TenantId, true);
        var rows = await _users.ListPageAsync(scope, page.Skip, page.NormalizedSize, cancellationToken).ConfigureAwait(false);
        var plans = await _licenses.ActiveUserPlansAsync(access.Value.TenantId, cancellationToken).ConfigureAwait(false);
        var items = new List<WorkspaceUserResponse>(rows.Count);
        foreach (var row in rows)
        {
            var names = await _roles.GetRoleNamesAsync(row.Id, access.Value.TenantId, cancellationToken).ConfigureAwait(false);
            plans.TryGetValue(row.Id, out var plan);
            items.Add(new WorkspaceUserResponse(row.Id, row.Email, row.FirstName, row.LastName, row.Status, WorkspaceRoleRules.Display(names), row.Id == access.Value.OwnerUserId, row.CreatedAt, plan?.PlanName ?? string.Empty, row.HasPhoto));
        }

        var total = await _users.CountAsync(scope, cancellationToken).ConfigureAwait(false);
        return Result.Success(new PageResult<WorkspaceUserResponse>(items, page.NormalizedPage, page.NormalizedSize, total));
    }
}

public sealed record AssignableStaffRole(string Name, string Label, bool Custom);

public sealed record ListAssignableStaffRolesQuery : IRequest<Result<IReadOnlyList<AssignableStaffRole>>>;

public sealed class ListAssignableStaffRolesQueryHandler : IRequestHandler<ListAssignableStaffRolesQuery, Result<IReadOnlyList<AssignableStaffRole>>>
{
    private static readonly string[] Hidden = [RoleNames.Trainer, RoleNames.Member, RoleNames.PersonalMember, RoleNames.PersonalOwner, RoleNames.SuperAdmin];

    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly IAccessRoleStore _roles;

    public ListAssignableStaffRolesQueryHandler(ICurrentActor actor, IPermissionService permissions, ITenantRepository tenants, IAccessRoleStore roles)
    {
        _actor = actor;
        _permissions = permissions;
        _tenants = tenants;
        _roles = roles;
    }

    public async Task<Result<IReadOnlyList<AssignableStaffRole>>> Handle(ListAssignableStaffRolesQuery request, CancellationToken cancellationToken)
    {
        var access = await WorkspaceAccess.OpenAsync(_actor, _tenants, _permissions, PermissionCodes.TenantMemberRead, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AssignableStaffRole>>(access.Error);
        }

        var roles = await _roles.ListVisibleAsync(access.Value.TenantId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<AssignableStaffRole> items = roles
            .Where(role => role.IsActive && !Hidden.Contains(role.Name, StringComparer.Ordinal))
            .OrderBy(role => role.IsSystem ? 0 : 1)
            .ThenBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .Select(role => new AssignableStaffRole(role.Name, AccessRoleCatalog.Label(role.Name), !role.IsSystem))
            .ToArray();
        return Result.Success(items);
    }
}
