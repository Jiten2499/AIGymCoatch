using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym.Branches;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class BranchesController : ApiControllerBase
{
    public BranchesController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("branches")]
    [RequiresPermission(PermissionCodes.BranchRead)]
    public async Task<IActionResult> List(int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListBranchesQuery(page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpPost("branches")]
    [RequiresPermission(PermissionCodes.BranchCreate)]
    public async Task<IActionResult> Create(CreateBranchRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateBranchCommand(request.Name, request.Code, request.Address, request.City, request.State, request.Country, request.PostalCode, request.Phone, request.Email, request.TimeZone), cancellationToken)).ToActionResult();

    [HttpGet("branches/{id:guid}")]
    [RequiresPermission(PermissionCodes.BranchRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetBranchQuery(id), cancellationToken)).ToActionResult();

    [HttpPut("branches/{id:guid}")]
    [RequiresPermission(PermissionCodes.BranchUpdate)]
    public async Task<IActionResult> Update(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateBranchCommand(id, request.Name, request.Address, request.City, request.State, request.Country, request.PostalCode, request.Phone, request.Email, request.TimeZone), cancellationToken)).ToActionResult();

    [HttpDelete("branches/{id:guid}")]
    [RequiresPermission(PermissionCodes.BranchDelete)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new DeactivateBranchCommand(id), cancellationToken)).ToActionResult();
}
