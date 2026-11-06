using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym.Pt;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class PtPackagesController : ApiControllerBase
{
    public PtPackagesController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("pt-packages")]
    [RequiresPermission(PermissionCodes.MembershipPlanRead)]
    public async Task<IActionResult> List(bool? activeOnly, string? term, string? status, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListPtPackagesQuery(activeOnly ?? false, term, status), cancellationToken)).ToActionResult();

    [HttpGet("pt-packages/{id:guid}")]
    [RequiresPermission(PermissionCodes.MembershipPlanRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetPtPackageQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("pt-packages")]
    [RequiresPermission(PermissionCodes.MembershipPlanManage)]
    public async Task<IActionResult> Create(CreatePtPackageCommand request, CancellationToken cancellationToken) =>
        (await Sender.Send(request, cancellationToken)).ToActionResult();

    [HttpPut("pt-packages/{id:guid}")]
    [RequiresPermission(PermissionCodes.MembershipPlanManage)]
    public async Task<IActionResult> Update(Guid id, UpdatePtPackageCommand request, CancellationToken cancellationToken) =>
        (await Sender.Send(request with { Id = id }, cancellationToken)).ToActionResult();

    [HttpPost("pt-packages/{id:guid}/status")]
    [RequiresPermission(PermissionCodes.MembershipPlanManage)]
    public async Task<IActionResult> Status(Guid id, SetPtPackageStatusCommand request, CancellationToken cancellationToken) =>
        (await Sender.Send(request with { Id = id }, cancellationToken)).ToActionResult();
}
