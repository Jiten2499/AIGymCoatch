using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Crm;

public sealed record CreateLeadCommand(string FullName, string Mobile, string? Email, LeadSource Source, Guid? InterestedPlanId, Guid? AssignedUserId, DateTime? NextFollowUpAt, string? Notes) : IRequest<Result<LeadDetails>>;

public sealed class CreateLeadCommandValidator : AbstractValidator<CreateLeadCommand>
{
    public CreateLeadCommandValidator()
    {
        RuleFor(command => command.FullName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Mobile).Must(MemberFieldRules.Phone).WithMessage("Enter a valid mobile number.");
        RuleFor(command => command.Email).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.Notes).MaximumLength(2000);
    }
}

public sealed class CreateLeadCommandHandler : IRequestHandler<CreateLeadCommand, Result<LeadDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeadRepository _leads;
    private readonly IMembershipRepository _plans;
    private readonly ITenantMembershipRepository _members;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public CreateLeadCommandHandler(IOperationGuard guard, ICurrentActor actor, ILeadRepository leads, IMembershipRepository plans, ITenantMembershipRepository members, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _leads = leads;
        _plans = plans;
        _members = members;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<LeadDetails>> Handle(CreateLeadCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmCreate, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<LeadDetails>(allowed.Error);
        }

        if (request.InterestedPlanId is Guid planId && await _plans.GetPlanAsync(tenantId, planId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<LeadDetails>(GymErrors.NotFound);
        }

        if (request.AssignedUserId is Guid assignee && await _members.GetActiveAsync(tenantId, assignee, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<LeadDetails>(GymErrors.NotFound);
        }

        var mobile = request.Mobile.Trim();
        if (await _leads.HasOpenMobileAsync(tenantId, mobile, null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<LeadDetails>(GymErrors.Conflict);
        }

        var lead = Lead.Create(Guid.NewGuid(), tenantId, request.FullName, mobile, request.Email, request.Source, request.Notes);
        lead.Update(lead.FullName, lead.Mobile, lead.Email, lead.Source, request.InterestedPlanId, request.Notes);
        lead.Assign(request.AssignedUserId);
        if (request.NextFollowUpAt is DateTime followUpAt)
        {
            var followUp = LeadFollowUp.Create(Guid.NewGuid(), tenantId, lead.Id, followUpAt, request.Notes, request.AssignedUserId, _clock.UtcNow);
            lead.ScheduleFollowUp(followUpAt);
            lead.ChangeStatus(LeadStatus.FollowUp);
            await _leads.AddFollowUpAsync(followUp, cancellationToken).ConfigureAwait(false);
        }

        await _leads.AddAsync(lead, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "LeadCreated", "Lead", lead.Id, null, lead.FullName, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var details = await _leads.ReadAsync(tenantId, lead.Id, cancellationToken).ConfigureAwait(false);
        return Result.Success(details!);
    }
}
