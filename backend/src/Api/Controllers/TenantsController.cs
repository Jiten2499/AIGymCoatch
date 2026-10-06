using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Tenancy;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class TenantsController : ApiControllerBase
{
    public TenantsController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("tenants/me")]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMyTenantsQuery(), cancellationToken)).ToActionResult();

    [HttpPost("tenants/switch")]
    public async Task<IActionResult> Switch(SwitchTenantRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new SwitchTenantCommand(request.TenantId), cancellationToken)).ToActionResult();

    [HttpPost("tenants")]
    [RequiresPermission(PermissionCodes.TenantCreate)]
    public async Task<IActionResult> Create(CreateTenantRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateTenantCommand(request.Name, request.TenantType), cancellationToken)).ToActionResult();

    [HttpPost("tenants/{tenantId:guid}/members")]
    [RequiresPermission(PermissionCodes.TenantMemberInvite)]
    public async Task<IActionResult> AddMember(Guid tenantId, AddMemberRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AddTenantMemberCommand(tenantId, request.Email, request.RoleName), cancellationToken)).ToActionResult();

    [HttpDelete("tenants/{tenantId:guid}/members/{userId:guid}")]
    [RequiresPermission(PermissionCodes.TenantMemberRemove)]
    public async Task<IActionResult> RemoveMember(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        (await Sender.Send(new RemoveTenantMemberCommand(tenantId, userId), cancellationToken)).ToActionResult();

    [HttpPost("tenants/{tenantId:guid}/roles")]
    [RequiresPermission(PermissionCodes.RoleAssign)]
    public async Task<IActionResult> AssignRole(Guid tenantId, AssignRoleRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AssignRoleCommand(tenantId, request.UserId, request.RoleName), cancellationToken)).ToActionResult();
}
