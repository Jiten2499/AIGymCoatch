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

public sealed record UpdateLeadCommand(Guid Id, string FullName, string Mobile, string? Email, LeadSource Source, Guid? InterestedPlanId, string? Notes) : IRequest<Result<LeadDetails>>;

public sealed class UpdateLeadCommandValidator : AbstractValidator<UpdateLeadCommand>
{
    public UpdateLeadCommandValidator()
    {
        RuleFor(command => command.FullName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Mobile).Must(MemberFieldRules.Phone);
        RuleFor(command => command.Email).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.Notes).MaximumLength(2000);
    }
}

public sealed class UpdateLeadCommandHandler : IRequestHandler<UpdateLeadCommand, Result<LeadDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeadRepository _leads;
    private readonly IMembershipRepository _plans;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public UpdateLeadCommandHandler(IOperationGuard guard, ICurrentActor actor, ILeadRepository leads, IMembershipRepository plans, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _leads = leads;
        _plans = plans;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<LeadDetails>> Handle(UpdateLeadCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmUpdate, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<LeadDetails>(allowed.Error);
        }

        var lead = await _leads.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (lead is null)
        {
            return Result.Failure<LeadDetails>(GymErrors.NotFound);
        }

        if (request.InterestedPlanId is Guid planId && await _plans.GetPlanAsync(tenantId, planId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<LeadDetails>(GymErrors.NotFound);
        }

        if (await _leads.HasOpenMobileAsync(tenantId, request.Mobile.Trim(), lead.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<LeadDetails>(GymErrors.Conflict);
        }

        lead.Update(request.FullName, request.Mobile, request.Email, request.Source, request.InterestedPlanId, request.Notes);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "LeadUpdated", "Lead", lead.Id, null, lead.Status.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success((await _leads.ReadAsync(tenantId, lead.Id, cancellationToken).ConfigureAwait(false))!);
    }
}

public sealed record AssignLeadCommand(Guid Id, Guid? UserId) : IRequest<Result<LeadDetails>>;

public sealed class AssignLeadCommandHandler : IRequestHandler<AssignLeadCommand, Result<LeadDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeadRepository _leads;
    private readonly ITenantMembershipRepository _members;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public AssignLeadCommandHandler(IOperationGuard guard, ICurrentActor actor, ILeadRepository leads, ITenantMembershipRepository members, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _leads = leads;
        _members = members;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<LeadDetails>> Handle(AssignLeadCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmUpdate, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<LeadDetails>(allowed.Error);
        }

        var lead = await _leads.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (lead is null || (request.UserId is Guid assignee && await _members.GetActiveAsync(tenantId, assignee, cancellationToken).ConfigureAwait(false) is null))
        {
            return Result.Failure<LeadDetails>(GymErrors.NotFound);
        }

        lead.Assign(request.UserId);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "LeadAssigned", "Lead", lead.Id, null, request.UserId?.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success((await _leads.ReadAsync(tenantId, lead.Id, cancellationToken).ConfigureAwait(false))!);
    }
}

public sealed record ChangeLeadStatusCommand(Guid Id, LeadStatus Status) : IRequest<Result<LeadDetails>>;

public sealed class ChangeLeadStatusCommandHandler : IRequestHandler<ChangeLeadStatusCommand, Result<LeadDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeadRepository _leads;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public ChangeLeadStatusCommandHandler(IOperationGuard guard, ICurrentActor actor, ILeadRepository leads, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _leads = leads;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<LeadDetails>> Handle(ChangeLeadStatusCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmUpdate, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<LeadDetails>(allowed.Error);
        }

        var lead = await _leads.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (lead is null)
        {
            return Result.Failure<LeadDetails>(GymErrors.NotFound);
        }

        var previous = lead.Status;
        if (request.Status is LeadStatus.Lost or LeadStatus.Converted || !lead.ChangeStatus(request.Status))
        {
            return Result.Failure<LeadDetails>(GymErrors.InvalidState);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "LeadStatusChanged", "Lead", lead.Id, previous.ToString(), lead.Status.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success((await _leads.ReadAsync(tenantId, lead.Id, cancellationToken).ConfigureAwait(false))!);
    }
}
