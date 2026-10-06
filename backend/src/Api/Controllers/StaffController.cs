using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Staffing;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class StaffController : ApiControllerBase
{
    public StaffController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("staff")]
    [RequiresPermission(PermissionCodes.StaffRead)]
    public async Task<IActionResult> List(int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListStaffQuery(page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpPost("staff")]
    [RequiresPermission(PermissionCodes.StaffCreate)]
    public async Task<IActionResult> Add(AddStaffRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AddStaffCommand(request.Email, request.Password, request.FirstName, request.LastName, request.Phone, request.StaffType, request.BranchId), cancellationToken)).ToActionResult();

    [HttpPut("staff/{id:guid}")]
    [RequiresPermission(PermissionCodes.StaffUpdate)]
    public async Task<IActionResult> Update(Guid id, UpdateStaffRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateStaffCommand(id, request.BranchId, request.StaffType, request.Status), cancellationToken)).ToActionResult();

    [HttpDelete("staff/{id:guid}")]
    [RequiresPermission(PermissionCodes.StaffRemove)]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new RemoveStaffCommand(id), cancellationToken)).ToActionResult();
}
