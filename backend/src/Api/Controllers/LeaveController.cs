using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym.Leave;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class LeaveController : ApiControllerBase
{
    public LeaveController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("leaves/mine")]
    [RequiresPermission(PermissionCodes.LeaveApply)]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListMyStaffLeavesQuery(), cancellationToken)).ToActionResult();

    [HttpGet("leaves")]
    [RequiresPermission(PermissionCodes.LeaveRead)]
    public async Task<IActionResult> List(DateOnly from, DateOnly to, Guid? staffProfileId, LeaveType? leaveType, LeaveRequestStatus? status, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListStaffLeavesQuery(from, to, staffProfileId, leaveType, status), cancellationToken)).ToActionResult();

    [HttpGet("leaves/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetStaffLeaveQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("leaves")]
    [RequiresPermission(PermissionCodes.LeaveApply)]
    public async Task<IActionResult> Apply(ApplyLeaveRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ApplyStaffLeaveCommand(request.LeaveType, request.StartDate, request.EndDate, request.Reason, request.AttachmentFileId), cancellationToken)).ToActionResult();

    [HttpPost("leaves/{id:guid}/cancel")]
    [RequiresPermission(PermissionCodes.LeaveApply)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new CancelStaffLeaveCommand(id), cancellationToken)).ToActionResult();
}

public sealed record ApplyLeaveRequest(LeaveType LeaveType, DateOnly StartDate, DateOnly EndDate, string Reason, Guid? AttachmentFileId);
