using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class AccessRolesController : ApiControllerBase
{
    public AccessRolesController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("access-roles")]
    [RequiresPermission(PermissionCodes.RoleRead)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListAccessRolesQuery(), cancellationToken)).ToActionResult();

    [HttpGet("access-roles/catalog")]
    [RequiresPermission(PermissionCodes.RoleRead)]
    public async Task<IActionResult> Catalog(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetAccessCatalogQuery(), cancellationToken)).ToActionResult();

    [HttpGet("access-roles/assignments")]
    [RequiresPermission(PermissionCodes.RoleRead)]
    public async Task<IActionResult> Assignments(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListAccessAssignmentsQuery(), cancellationToken)).ToActionResult();

    [HttpGet("access-roles/overview")]
    [RequiresPermission(PermissionCodes.RoleRead)]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListAccessOverviewQuery(), cancellationToken)).ToActionResult();

    [HttpGet("access-roles/users/{userId:guid}")]
    [RequiresPermission(PermissionCodes.RoleRead)]
    public async Task<IActionResult> Effective(Guid userId, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetEffectiveAccessQuery(userId), cancellationToken)).ToActionResult();

    [HttpPost("access-roles")]
    [RequiresPermission(PermissionCodes.RoleAssign)]
    public async Task<IActionResult> Create(SaveAccessRoleRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateAccessRoleCommand(request.Name, request.Description, request.PermissionCodes, request.Scopes), cancellationToken)).ToActionResult();

    [HttpPut("access-roles/{roleId:guid}")]
    [RequiresPermission(PermissionCodes.RoleAssign)]
    public async Task<IActionResult> Update(Guid roleId, SaveAccessRoleRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateAccessRoleCommand(roleId, request.Name, request.Description, request.PermissionCodes, request.Scopes), cancellationToken)).ToActionResult();

    [HttpPost("access-roles/{roleId:guid}/duplicate")]
    [RequiresPermission(PermissionCodes.RoleAssign)]
    public async Task<IActionResult> Duplicate(Guid roleId, DuplicateAccessRoleRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new DuplicateAccessRoleCommand(roleId, request.Name), cancellationToken)).ToActionResult();

    [HttpPost("access-roles/{roleId:guid}/status")]
    [RequiresPermission(PermissionCodes.RoleAssign)]
    public async Task<IActionResult> Status(Guid roleId, AccessRoleStatusRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new SetAccessRoleStatusCommand(roleId, request.Active), cancellationToken)).ToActionResult();

    [HttpPost("access-roles/assignments")]
    [RequiresPermission(PermissionCodes.RoleAssign)]
    public async Task<IActionResult> Assign(AssignAccessRoleRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AssignAccessRoleCommand(request.UserId, request.RoleId), cancellationToken)).ToActionResult();
}

public sealed record SaveAccessRoleRequest(string Name, string? Description, IReadOnlyList<string> PermissionCodes, IReadOnlyList<AccessScopeInput> Scopes);

public sealed record DuplicateAccessRoleRequest(string Name);

public sealed record AccessRoleStatusRequest(bool Active);

public sealed record AssignAccessRoleRequest(Guid UserId, Guid RoleId);
