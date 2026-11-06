using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym.Crm;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class LeadsController : ApiControllerBase
{
    public LeadsController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("leads")]
    [RequiresPermission(PermissionCodes.CrmRead)]
    public async Task<IActionResult> Search(string? term, LeadStatus? status, LeadSource? source, Guid? assignedUserId, Guid? planId, string? followUp, TrialStatus? trialStatus, int? page, int? pageSize, string? sort, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetLeadsQuery(term, status, source, assignedUserId, planId, followUp, trialStatus, page ?? 1, pageSize ?? 20, sort), cancellationToken)).ToActionResult();

    [HttpGet("leads/summary")]
    [RequiresPermission(PermissionCodes.CrmRead)]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetCrmSummaryQuery(), cancellationToken)).ToActionResult();

    [HttpGet("leads/follow-ups")]
    [RequiresPermission(PermissionCodes.CrmRead)]
    public async Task<IActionResult> Queue(string? bucket, int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetFollowUpQueueQuery(bucket ?? "today", page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpPost("leads")]
    [RequiresPermission(PermissionCodes.CrmCreate)]
    public async Task<IActionResult> Create(LeadRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateLeadCommand(request.FullName, request.Mobile, request.Email, request.Source, request.InterestedPlanId, request.AssignedUserId, request.NextFollowUpAt, request.Notes), cancellationToken)).ToActionResult();

    [HttpGet("leads/{id:guid}")]
    [RequiresPermission(PermissionCodes.CrmRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetLeadQuery(id), cancellationToken)).ToActionResult();

    [HttpPut("leads/{id:guid}")]
    [RequiresPermission(PermissionCodes.CrmUpdate)]
    public async Task<IActionResult> Update(Guid id, LeadRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateLeadCommand(id, request.FullName, request.Mobile, request.Email, request.Source, request.InterestedPlanId, request.Notes), cancellationToken)).ToActionResult();

    [HttpPost("leads/{id:guid}/assign")]
    [RequiresPermission(PermissionCodes.CrmUpdate)]
    public async Task<IActionResult> Assign(Guid id, AssignLeadRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AssignLeadCommand(id, request.UserId), cancellationToken)).ToActionResult();

    [HttpPost("leads/{id:guid}/status")]
    [RequiresPermission(PermissionCodes.CrmUpdate)]
    public async Task<IActionResult> Status(Guid id, LeadStatusRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ChangeLeadStatusCommand(id, request.Status), cancellationToken)).ToActionResult();

    [HttpGet("leads/{id:guid}/follow-ups")]
    [RequiresPermission(PermissionCodes.CrmRead)]
    public async Task<IActionResult> FollowUps(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetLeadFollowUpsQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("leads/{id:guid}/follow-ups")]
    [RequiresPermission(PermissionCodes.CrmUpdate)]
    public async Task<IActionResult> AddFollowUp(Guid id, FollowUpRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateFollowUpCommand(id, request.ScheduledAt, request.Notes, request.Outcome), cancellationToken)).ToActionResult();

    [HttpPost("leads/{id:guid}/follow-ups/{followUpId:guid}/complete")]
    [RequiresPermission(PermissionCodes.CrmUpdate)]
    public async Task<IActionResult> CompleteFollowUp(Guid id, Guid followUpId, FollowUpRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CompleteFollowUpCommand(id, followUpId, request.Outcome), cancellationToken)).ToActionResult();

    [HttpGet("leads/{id:guid}/trials")]
    [RequiresPermission(PermissionCodes.CrmRead)]
    public async Task<IActionResult> Trials(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetLeadTrialsQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("leads/{id:guid}/trials")]
    [RequiresPermission(PermissionCodes.CrmUpdate)]
    public async Task<IActionResult> AddTrial(Guid id, TrialRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateTrialCommand(id, request.ScheduledAt, request.Notes), cancellationToken)).ToActionResult();

    [HttpPost("leads/{id:guid}/trials/{trialId:guid}/status")]
    [RequiresPermission(PermissionCodes.CrmUpdate)]
    public async Task<IActionResult> TrialStatus(Guid id, Guid trialId, TrialStatusRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ChangeTrialStatusCommand(id, trialId, request.Status, request.Notes, request.Outcome), cancellationToken)).ToActionResult();

    [HttpGet("leads/{id:guid}/member-matches")]
    [RequiresPermission(PermissionCodes.CrmConvert)]
    public async Task<IActionResult> MemberMatches(Guid id, string? mobile, string? email, CancellationToken cancellationToken) =>
        (await Sender.Send(new MatchLeadMembersQuery(id, mobile, email), cancellationToken)).ToActionResult();

    [HttpPost("leads/{id:guid}/convert")]
    [RequiresPermission(PermissionCodes.CrmConvert)]
    public async Task<IActionResult> Convert(Guid id, ConvertLeadRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ConvertLeadCommand(id, request.MemberProfileId, request.FirstName, request.LastName, request.Phone, request.Email, request.DateOfBirth, request.Gender, request.Address, request.EmergencyContactName, request.EmergencyContactPhone, request.JoinDate, request.FitnessGoal, request.AssignedTrainerId, request.PlanId, request.BranchId, request.StartDate, request.AssignAppAccess, request.AppSubscriptionId, request.EnableBasicAppAccess), cancellationToken)).ToActionResult();

    [HttpPost("leads/{id:guid}/lost")]
    [RequiresPermission(PermissionCodes.CrmLost)]
    public async Task<IActionResult> Lost(Guid id, LostLeadRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new MarkLeadLostCommand(id, request.Reason, request.Notes), cancellationToken)).ToActionResult();

    [HttpPost("leads/{id:guid}/reopen")]
    [RequiresPermission(PermissionCodes.CrmLost)]
    public async Task<IActionResult> Reopen(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new ReopenLeadCommand(id), cancellationToken)).ToActionResult();
}

public sealed record LeadRequest(string FullName, string Mobile, string? Email, LeadSource Source, Guid? InterestedPlanId, Guid? AssignedUserId, DateTime? NextFollowUpAt, string? Notes);
public sealed record AssignLeadRequest(Guid? UserId);
public sealed record LeadStatusRequest(LeadStatus Status);
public sealed record FollowUpRequest(DateTime ScheduledAt, string? Notes, string? Outcome);
public sealed record TrialRequest(DateTime ScheduledAt, string? Notes);
public sealed record TrialStatusRequest(TrialStatus Status, string? Notes, string? Outcome);
public sealed record ConvertLeadRequest(
    Guid? MemberProfileId,
    string FirstName,
    string LastName,
    string Phone,
    string? Email,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Address,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    DateOnly JoinDate,
    string? FitnessGoal,
    Guid? AssignedTrainerId,
    Guid? PlanId,
    Guid? BranchId,
    DateOnly? StartDate,
    bool AssignAppAccess = false,
    Guid? AppSubscriptionId = null,
    bool EnableBasicAppAccess = false);
public sealed record LostLeadRequest(LeadLostReason Reason, string? Notes);
