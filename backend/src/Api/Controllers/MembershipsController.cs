using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Memberships;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class MembershipsController : ApiControllerBase
{
    public MembershipsController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("membership-plans")]
    [RequiresPermission(PermissionCodes.MembershipPlanRead)]
    public async Task<IActionResult> ListPlans(bool? activeOnly, int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListPlansQuery(activeOnly, page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpPost("membership-plans")]
    [RequiresPermission(PermissionCodes.MembershipPlanManage)]
    public async Task<IActionResult> CreatePlan(PlanRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreatePlanCommand(request.Name, request.Description, request.DurationDays, request.MembershipType, request.MaxVisits, request.Price, request.Currency), cancellationToken)).ToActionResult();

    [HttpPut("membership-plans/{id:guid}")]
    [RequiresPermission(PermissionCodes.MembershipPlanManage)]
    public async Task<IActionResult> UpdatePlan(Guid id, PlanRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdatePlanCommand(id, request.Name, request.Description, request.DurationDays, request.MembershipType, request.MaxVisits, request.Price, request.Currency), cancellationToken)).ToActionResult();

    [HttpDelete("membership-plans/{id:guid}")]
    [RequiresPermission(PermissionCodes.MembershipPlanManage)]
    public async Task<IActionResult> DeactivatePlan(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new DeactivatePlanCommand(id), cancellationToken)).ToActionResult();

    [HttpGet("memberships")]
    [RequiresPermission(PermissionCodes.MembershipRead)]
    public async Task<IActionResult> List(GymMembershipStatus? status, Guid? planId, Guid? branchId, DateOnly? expiresFrom, DateOnly? expiresTo, int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListMembershipsQuery(status, planId, branchId, expiresFrom, expiresTo, page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpPost("memberships")]
    [RequiresPermission(PermissionCodes.MembershipCreate)]
    public async Task<IActionResult> Create(CreateMembershipRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateMembershipCommand(request.MemberProfileId, request.BranchId, request.PlanId, request.StartDate, request.AutoRenew, request.Notes), cancellationToken)).ToActionResult();

    [HttpGet("memberships/{id:guid}")]
    [RequiresPermission(PermissionCodes.MembershipRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMembershipQuery(id), cancellationToken)).ToActionResult();

    [HttpGet("memberships/{id:guid}/history")]
    [RequiresPermission(PermissionCodes.MembershipRead)]
    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListMembershipHistoryQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("memberships/{id:guid}/renew")]
    [RequiresPermission(PermissionCodes.MembershipRenew)]
    public Task<IActionResult> Renew(Guid id, ChangeMembershipRequest request, CancellationToken cancellationToken) =>
        Change(id, "renew", request, cancellationToken);

    [HttpPost("memberships/{id:guid}/freeze")]
    [RequiresPermission(PermissionCodes.MembershipFreeze)]
    public Task<IActionResult> Freeze(Guid id, ChangeMembershipRequest request, CancellationToken cancellationToken) =>
        Change(id, "freeze", request, cancellationToken);

    [HttpPost("memberships/{id:guid}/unfreeze")]
    [RequiresPermission(PermissionCodes.MembershipFreeze)]
    public Task<IActionResult> Unfreeze(Guid id, ChangeMembershipRequest request, CancellationToken cancellationToken) =>
        Change(id, "unfreeze", request, cancellationToken);

    [HttpPost("memberships/{id:guid}/cancel")]
    [RequiresPermission(PermissionCodes.MembershipCancel)]
    public Task<IActionResult> Cancel(Guid id, ChangeMembershipRequest request, CancellationToken cancellationToken) =>
        Change(id, "cancel", request, cancellationToken);

    [HttpPost("memberships/{id:guid}/expire")]
    [RequiresPermission(PermissionCodes.MembershipCancel)]
    public Task<IActionResult> Expire(Guid id, ChangeMembershipRequest request, CancellationToken cancellationToken) =>
        Change(id, "expire", request, cancellationToken);

    private async Task<IActionResult> Change(Guid id, string action, ChangeMembershipRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ChangeMembershipCommand(id, action, request.Reason, request.AutoRenew), cancellationToken)).ToActionResult();
}
