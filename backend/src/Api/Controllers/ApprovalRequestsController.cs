using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym.Approvals;
using LifeFitAI.Domain.Gym;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class ApprovalRequestsController : ApiControllerBase
{
    public ApprovalRequestsController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("approval-requests/pending-count")]
    public async Task<IActionResult> PendingCount(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetApprovalInboxAccessQuery(), cancellationToken)).ToActionResult();

    [HttpGet("approval-requests")]
    public async Task<IActionResult> List(ApprovalRequestStatus? status, ApprovalRequestType? requestType, Guid? requestedByUserId, DateOnly? from, DateOnly? to, int? page, int? pageSize, string? sort, bool includeAllStatuses = false, CancellationToken cancellationToken = default)
    {
        var selected = includeAllStatuses ? status : status ?? ApprovalRequestStatus.Pending;
        return (await Sender.Send(new ListApprovalRequestsQuery(new ApprovalListQuery(selected, requestType, requestedByUserId, from, to, page ?? 1, pageSize ?? 20, sort)), cancellationToken)).ToActionResult();
    }

    [HttpGet("approval-requests/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetApprovalRequestQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("approval-requests/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, ApprovalDecisionRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new DecideApprovalRequestCommand(id, true, request.Comment), cancellationToken)).ToActionResult();

    [HttpPost("approval-requests/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, ApprovalDecisionRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new DecideApprovalRequestCommand(id, false, request.Comment), cancellationToken)).ToActionResult();
}

public sealed record ApprovalDecisionRequest(string? Comment);
