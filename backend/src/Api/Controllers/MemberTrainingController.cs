using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Gym.Pt;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class MemberTrainingController : ApiControllerBase
{
    public MemberTrainingController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("members/{id:guid}/pt-assignments")]
    [RequiresPermission(PermissionCodes.MemberRead)]
    public async Task<IActionResult> Assignments(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListMemberPtAssignmentsQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("members/{id:guid}/pt-assignments")]
    [RequiresPermission(PermissionCodes.MemberUpdate)]
    public async Task<IActionResult> Assign(Guid id, AssignMemberPtRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AssignMemberPtPackageCommand(id, request.PackageId, request.TrainerId, request.StartDate, request.PaymentAmount, request.PaymentMethod, request.PaymentDate, request.PaymentReferenceNumber, request.PaymentNotes), cancellationToken)).ToActionResult();

    [HttpGet("members/{id:guid}/pt-assignments/{assignmentId:guid}/payments")]
    [RequiresPermission(PermissionCodes.PaymentRead)]
    public async Task<IActionResult> Payments(Guid id, Guid assignmentId, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListMemberPtPaymentsQuery(id, assignmentId), cancellationToken)).ToActionResult();

    [HttpPost("members/{id:guid}/pt-assignments/{assignmentId:guid}/payments")]
    [RequiresPermission(PermissionCodes.PaymentCreate)]
    public async Task<IActionResult> Collect(Guid id, Guid assignmentId, CollectPaymentRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new RecordMemberPtPaymentCommand(id, assignmentId, request.Amount, request.PaymentMethod, request.PaymentDate, request.ReferenceNumber, request.Notes), cancellationToken)).ToActionResult();

    [HttpGet("members/{id:guid}/pt-sessions")]
    [RequiresPermission(PermissionCodes.MemberRead)]
    public async Task<IActionResult> Sessions(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListMemberPtSessionsQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("members/{id:guid}/pt-sessions")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> Record(Guid id, RecordMemberPtSessionRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CompletePtSessionCommand(id, request.TrainerId, request.CompletedOn, request.MembershipId, request.AssignmentId, request.SessionId), cancellationToken)).ToActionResult();

    [HttpPost("members/{id:guid}/pt-sessions/{consumptionId:guid}/reverse")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> Reverse(Guid id, Guid consumptionId, ReverseMemberPtSessionRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ReverseMemberPtSessionCommand(id, consumptionId, request.Reason), cancellationToken)).ToActionResult();

    [HttpGet("members/{id:guid}/progress")]
    [RequiresPermission(PermissionCodes.MemberRead)]
    public async Task<IActionResult> Progress(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListMemberProgressQuery(id), cancellationToken)).ToActionResult();
}

public sealed record AssignMemberPtRequest(Guid PackageId, Guid? TrainerId, DateOnly StartDate, decimal? PaymentAmount, MembershipPaymentMethod? PaymentMethod, DateOnly? PaymentDate, string? PaymentReferenceNumber, string? PaymentNotes);

public sealed record RecordMemberPtSessionRequest(Guid TrainerId, DateOnly CompletedOn, Guid? MembershipId, Guid? AssignmentId, Guid? SessionId);

public sealed record ReverseMemberPtSessionRequest(string? Reason);
