using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym.Payments;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class MembershipPaymentsController : ApiControllerBase
{
    public MembershipPaymentsController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("memberships/{membershipId:guid}/payments")]
    [RequiresPermission(PermissionCodes.PaymentRead)]
    public async Task<IActionResult> List(Guid membershipId, int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListMembershipPaymentsQuery(membershipId, page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpGet("memberships/{membershipId:guid}/payment-summary")]
    [RequiresPermission(PermissionCodes.PaymentRead)]
    public async Task<IActionResult> Summary(Guid membershipId, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMembershipPaymentSummaryQuery(membershipId), cancellationToken)).ToActionResult();

    [HttpPost("memberships/{membershipId:guid}/payments")]
    [RequiresPermission(PermissionCodes.PaymentCreate)]
    public async Task<IActionResult> Collect(Guid membershipId, CollectPaymentRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new RecordMembershipPaymentCommand(
            membershipId,
            request.Amount,
            request.PaymentMethod,
            request.PaymentDate,
            request.ReferenceNumber,
            request.Notes), cancellationToken)).ToActionResult();

    [HttpGet("membership-payments/{id:guid}")]
    [RequiresPermission(PermissionCodes.PaymentRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMembershipPaymentQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("membership-payments/{id:guid}/void")]
    [RequiresPermission(PermissionCodes.PaymentVoid)]
    public async Task<IActionResult> Void(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new VoidMembershipPaymentCommand(id), cancellationToken)).ToActionResult();
}

public sealed record CollectPaymentRequest(
    decimal Amount,
    MembershipPaymentMethod PaymentMethod,
    DateOnly PaymentDate,
    string? ReferenceNumber,
    string? Notes);
