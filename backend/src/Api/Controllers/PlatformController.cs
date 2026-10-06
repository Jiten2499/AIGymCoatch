using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

public sealed class PlatformController : ApiControllerBase
{
    public PlatformController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("platform")]
    [AllowAnonymous]
    public IActionResult Get() => new HttpResultAction(Results.Ok(new PlatformInfo("LifeFit AI", "1.0", "foundation")));

    [HttpGet("platform/tenants")]
    [Authorize]
    public async Task<IActionResult> Tenants(int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListPlatformTenantsQuery(page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpPost("platform/tenants")]
    [Authorize]
    public async Task<IActionResult> CreateTenant(CreateBusinessTenantRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateBusinessTenantCommand(request.Name, request.TenantType, request.OwnerEmail, request.OwnerFirstName, request.OwnerLastName), cancellationToken)).ToActionResult();

    [HttpPut("platform/tenants/{tenantId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateTenant(Guid tenantId, UpdateBusinessTenantRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateBusinessTenantCommand(tenantId, request.Name, request.TenantType, request.Status), cancellationToken)).ToActionResult();

    [HttpPost("platform/tenants/delete")]
    [Authorize]
    public async Task<IActionResult> DeleteTenants(DeleteBusinessTenantsRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new DeleteBusinessTenantsCommand(request.TenantIds), cancellationToken)).ToActionResult();

    [HttpGet("platform/users")]
    [Authorize]
    public async Task<IActionResult> Users(int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListPlatformUsersQuery(page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpPost("platform/tenants/{tenantId:guid}/suspend")]
    [Authorize]
    public async Task<IActionResult> Suspend(Guid tenantId, CancellationToken cancellationToken) =>
        (await Sender.Send(new SuspendTenantCommand(tenantId), cancellationToken)).ToActionResult();
}
