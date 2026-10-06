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

public sealed record WorkspaceUserResponse(Guid Id, string Email, string FirstName, string LastName, string Status, string Role, bool IsOwner, DateTime CreatedAt, string Plan);

public sealed record WorkspaceUserCreated(Guid Id, string Email, string FirstName, string LastName, string Role, string? TemporaryPassword);

public static class WorkspaceRoleRules
{
    public static readonly string[] Assignable =
    [
        RoleNames.GymOwner,
        RoleNames.GymManager,
        RoleNames.Trainer,
        RoleNames.Receptionist,
        RoleNames.Member,
    ];

    public static bool CanAssign(string roleName) => Assignable.Contains(roleName, StringComparer.Ordinal);

    public static string Display(IReadOnlyList<string> names) =>
        names.FirstOrDefault(CanAssign) ?? names.FirstOrDefault() ?? string.Empty;
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
        var scope = new DirectoryScope(false, access.Value.TenantId);
        var rows = await _users.ListPageAsync(scope, page.Skip, page.NormalizedSize, cancellationToken).ConfigureAwait(false);
        var plans = await _licenses.ActiveUserPlansAsync(access.Value.TenantId, cancellationToken).ConfigureAwait(false);
        var items = new List<WorkspaceUserResponse>(rows.Count);
        foreach (var row in rows)
        {
            var names = await _roles.GetRoleNamesAsync(row.Id, access.Value.TenantId, cancellationToken).ConfigureAwait(false);
            plans.TryGetValue(row.Id, out var plan);
            items.Add(new WorkspaceUserResponse(row.Id, row.Email, row.FirstName, row.LastName, row.Status, WorkspaceRoleRules.Display(names), row.Id == access.Value.OwnerUserId, row.CreatedAt, plan?.PlanName ?? string.Empty));
        }

        var total = await _users.CountAsync(scope, cancellationToken).ConfigureAwait(false);
        return Result.Success(new PageResult<WorkspaceUserResponse>(items, page.NormalizedPage, page.NormalizedSize, total));
    }
}
