using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Memberships;
using LifeFitAI.Application.Gym.Payments;
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
    public async Task<IActionResult> ListPlans(bool? activeOnly, string? term, PackageCategory? category, int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListPlansQuery(activeOnly, term, category, page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpGet("membership-plans/{id:guid}/term")]
    [RequiresPermission(PermissionCodes.MembershipPlanRead)]
    public async Task<IActionResult> Term(Guid id, DateOnly start, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMembershipTermQuery(id, start), cancellationToken)).ToActionResult();

    [HttpGet("membership-plans/{id:guid}")]
    [RequiresPermission(PermissionCodes.MembershipPlanRead)]
    public async Task<IActionResult> GetPlan(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetPlanQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("membership-plans")]
    [RequiresPermission(PermissionCodes.MembershipPlanManage)]
    public async Task<IActionResult> CreatePlan(PlanRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreatePlanCommand(request.Name, request.Description, request.DurationDays, request.MembershipType, request.MaxVisits, request.Price, request.Currency, request.Category, request.DurationUnit, request.DurationCount, request.IncludedPtSessions), cancellationToken)).ToActionResult();

    [HttpPut("membership-plans/{id:guid}")]
    [RequiresPermission(PermissionCodes.MembershipPlanManage)]
    public async Task<IActionResult> UpdatePlan(Guid id, PlanRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdatePlanCommand(id, request.Name, request.Description, request.DurationDays, request.MembershipType, request.MaxVisits, request.Price, request.Currency, request.Category, request.DurationUnit, request.DurationCount, request.IncludedPtSessions), cancellationToken)).ToActionResult();

    [HttpPost("membership-plans/{id:guid}/activate")]
    [RequiresPermission(PermissionCodes.MembershipPlanManage)]
    public async Task<IActionResult> ActivatePlan(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new ActivatePlanCommand(id), cancellationToken)).ToActionResult();

    [HttpDelete("membership-plans/{id:guid}")]
    [RequiresPermission(PermissionCodes.MembershipPlanManage)]
    public async Task<IActionResult> DeactivatePlan(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new DeactivatePlanCommand(id), cancellationToken)).ToActionResult();

    [HttpGet("memberships")]
    [RequiresPermission(PermissionCodes.MembershipRead)]
    public async Task<IActionResult> List(GymMembershipStatus? status, Guid? planId, Guid? branchId, DateOnly? expiresFrom, DateOnly? expiresTo, Guid? memberId, string? term, DateOnly? startsFrom, DateOnly? startsTo, string? sort, int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListMembershipsQuery(status, planId, branchId, expiresFrom, expiresTo, memberId, term, startsFrom, startsTo, sort, page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpPost("memberships")]
    [RequiresPermission(PermissionCodes.MembershipCreate)]
    public async Task<IActionResult> Create(CreateMembershipRequest request, CancellationToken cancellationToken)
    {
        InitialMembershipPayment? payment = null;
        if (request.PaymentAmount is decimal amount && request.PaymentMethod is MembershipPaymentMethod method && request.PaymentDate is DateOnly paymentDate)
        {
            payment = new InitialMembershipPayment(amount, method, paymentDate, request.PaymentReferenceNumber, request.PaymentNotes);
        }

        return (await Sender.Send(new CreateMembershipCommand(
            request.MemberProfileId,
            request.BranchId,
            request.PlanId,
            request.StartDate,
            request.AutoRenew,
            request.Notes,
            request.AssignAppAccess,
            request.AppSubscriptionId,
            request.DiscountRuleId,
            payment), cancellationToken)).ToActionResult();
    }

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
    public async Task<IActionResult> Renew(Guid id, RenewMembershipRequest request, CancellationToken cancellationToken)
    {
        InitialMembershipPayment? payment = null;
        if (request.PaymentAmount is decimal amount && request.PaymentMethod is MembershipPaymentMethod method && request.PaymentDate is DateOnly paymentDate)
        {
            payment = new InitialMembershipPayment(amount, method, paymentDate, request.PaymentReferenceNumber, request.PaymentNotes);
        }

        return (await Sender.Send(new RenewMembershipCommand(id, request.Reason, request.AutoRenew, request.PlanId, request.StartDate, request.TrainerId, request.ClearTrainer, request.PtPackageId, request.DiscountRuleId, payment), cancellationToken)).ToActionResult();
    }

    [HttpPost("memberships/{id:guid}/freeze")]
    [RequiresPermission(PermissionCodes.MembershipFreeze)]
    public Task<IActionResult> Freeze(Guid id, ChangeMembershipRequest request, CancellationToken cancellationToken) =>
        Change(id, "freeze", request, cancellationToken);

    [HttpPost("memberships/{id:guid}/unfreeze")]
    [RequiresPermission(PermissionCodes.MembershipFreeze)]
    public Task<IActionResult> Unfreeze(Guid id, ChangeMembershipRequest request, CancellationToken cancellationToken) =>
        Change(id, "unfreeze", request, cancellationToken);

    [HttpPost("memberships/{id:guid}/extend")]
    [RequiresPermission(PermissionCodes.MembershipRenew)]
    public Task<IActionResult> Extend(Guid id, ChangeMembershipRequest request, CancellationToken cancellationToken) =>
        Change(id, "extend", request, cancellationToken);

    [HttpPost("memberships/{id:guid}/cancel")]
    [RequiresPermission(PermissionCodes.MembershipCancel)]
    public Task<IActionResult> Cancel(Guid id, ChangeMembershipRequest request, CancellationToken cancellationToken) =>
        Change(id, "cancel", request, cancellationToken);

    [HttpPost("memberships/{id:guid}/expire")]
    [RequiresPermission(PermissionCodes.MembershipCancel)]
    public Task<IActionResult> Expire(Guid id, ChangeMembershipRequest request, CancellationToken cancellationToken) =>
        Change(id, "expire", request, cancellationToken);

    private async Task<IActionResult> Change(Guid id, string action, ChangeMembershipRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ChangeMembershipCommand(id, action, request.Reason, request.AutoRenew, request.Days), cancellationToken)).ToActionResult();
}
