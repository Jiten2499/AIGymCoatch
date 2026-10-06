using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Api.Middleware;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Attendance;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class AttendanceController : ApiControllerBase
{
    public AttendanceController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpPost("attendance/check-in")]
    [RequiresPermission(PermissionCodes.AttendanceCheckIn)]
    [EnableRateLimiting("checkin")]
    [ServiceFilter(typeof(IdempotencyFilter))]
    public async Task<IActionResult> CheckIn(CheckInRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CheckInCommand(request.MemberProfileId, request.BranchId, request.Method, request.QrToken), cancellationToken)).ToActionResult();

    [HttpPost("attendance/check-out")]
    [RequiresPermission(PermissionCodes.AttendanceCheckOut)]
    public async Task<IActionResult> CheckOut(CheckOutRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CheckOutCommand(request.AttendanceId), cancellationToken)).ToActionResult();

    [HttpGet("attendance")]
    [RequiresPermission(PermissionCodes.AttendanceRead)]
    public async Task<IActionResult> List(Guid? branchId, Guid? memberProfileId, DateTime? from, DateTime? to, int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListAttendanceQuery(branchId, memberProfileId, from, to, page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpPost("attendance/qr-tokens")]
    [RequiresPermission(PermissionCodes.AttendanceCheckIn)]
    public async Task<IActionResult> IssueQrToken(IssueQrTokenRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new IssueQrTokenCommand(request.MemberProfileId), cancellationToken)).ToActionResult();
}
