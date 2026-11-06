using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Crm;

public sealed record CreateFollowUpCommand(Guid LeadId, DateTime ScheduledAt, string? Notes, string? Outcome) : IRequest<Result<FollowUpItem>>;

public sealed class CreateFollowUpCommandValidator : AbstractValidator<CreateFollowUpCommand>
{
    public CreateFollowUpCommandValidator() => RuleFor(command => command.Notes).MaximumLength(2000);
}

public sealed class CreateFollowUpCommandHandler : IRequestHandler<CreateFollowUpCommand, Result<FollowUpItem>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeadRepository _leads;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public CreateFollowUpCommandHandler(IOperationGuard guard, ICurrentActor actor, ILeadRepository leads, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _leads = leads;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<FollowUpItem>> Handle(CreateFollowUpCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmUpdate, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<FollowUpItem>(allowed.Error);
        }

        var lead = await _leads.GetAsync(tenantId, request.LeadId, cancellationToken).ConfigureAwait(false);
        if (lead is null || lead.Status is LeadStatus.Converted or LeadStatus.Lost)
        {
            return Result.Failure<FollowUpItem>(lead is null ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        var followUp = LeadFollowUp.Create(Guid.NewGuid(), tenantId, lead.Id, request.ScheduledAt, request.Notes, lead.AssignedUserId, _clock.UtcNow);
        await _leads.AddFollowUpAsync(followUp, cancellationToken).ConfigureAwait(false);
        lead.ScheduleFollowUp(request.ScheduledAt);
        if (lead.Status is LeadStatus.New or LeadStatus.Contacted)
        {
            lead.ChangeStatus(LeadStatus.FollowUp);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "FollowUpCreated", "LeadFollowUp", followUp.Id, null, request.ScheduledAt.ToString("O"), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new FollowUpItem(followUp.Id, followUp.LeadId, followUp.ScheduledAt, null, followUp.Notes, null, lead.FullName));
    }
}

public sealed record CompleteFollowUpCommand(Guid LeadId, Guid FollowUpId, string? Outcome) : IRequest<Result<FollowUpItem>>;

public sealed class CompleteFollowUpCommandHandler : IRequestHandler<CompleteFollowUpCommand, Result<FollowUpItem>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeadRepository _leads;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public CompleteFollowUpCommandHandler(IOperationGuard guard, ICurrentActor actor, ILeadRepository leads, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _leads = leads;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<FollowUpItem>> Handle(CompleteFollowUpCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmUpdate, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<FollowUpItem>(allowed.Error);
        }

        var lead = await _leads.GetAsync(tenantId, request.LeadId, cancellationToken).ConfigureAwait(false);
        var followUp = lead is null ? null : await _leads.GetFollowUpAsync(tenantId, lead.Id, request.FollowUpId, cancellationToken).ConfigureAwait(false);
        if (followUp is null || !followUp.Complete(_clock.UtcNow, request.Outcome))
        {
            return Result.Failure<FollowUpItem>(followUp is null ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        lead!.ScheduleFollowUp(await _leads.NextOpenFollowUpAsync(tenantId, lead.Id, cancellationToken).ConfigureAwait(false));
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "FollowUpCompleted", "LeadFollowUp", followUp.Id, null, request.Outcome, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new FollowUpItem(followUp.Id, followUp.LeadId, followUp.ScheduledAt, followUp.CompletedAt, followUp.Notes, followUp.Outcome, lead.FullName));
    }
}

public sealed record CreateTrialCommand(Guid LeadId, DateTime ScheduledAt, string? Notes) : IRequest<Result<TrialItem>>;

public sealed class CreateTrialCommandHandler : IRequestHandler<CreateTrialCommand, Result<TrialItem>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeadRepository _leads;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public CreateTrialCommandHandler(IOperationGuard guard, ICurrentActor actor, ILeadRepository leads, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _leads = leads;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<TrialItem>> Handle(CreateTrialCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmUpdate, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<TrialItem>(allowed.Error);
        }

        var lead = await _leads.GetAsync(tenantId, request.LeadId, cancellationToken).ConfigureAwait(false);
        if (lead is null || lead.Status is LeadStatus.Converted or LeadStatus.Lost)
        {
            return Result.Failure<TrialItem>(lead is null ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        var trial = LeadTrial.Create(Guid.NewGuid(), tenantId, lead.Id, request.ScheduledAt, request.Notes, _clock.UtcNow);
        await _leads.AddTrialAsync(trial, cancellationToken).ConfigureAwait(false);
        lead.ChangeStatus(LeadStatus.Trial);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "TrialScheduled", "LeadTrial", trial.Id, null, request.ScheduledAt.ToString("O"), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new TrialItem(trial.Id, trial.LeadId, trial.ScheduledAt, trial.Status, trial.Notes, trial.Outcome));
    }
}

public sealed record ChangeTrialStatusCommand(Guid LeadId, Guid TrialId, TrialStatus Status, string? Notes, string? Outcome) : IRequest<Result<TrialItem>>;

public sealed class ChangeTrialStatusCommandHandler : IRequestHandler<ChangeTrialStatusCommand, Result<TrialItem>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeadRepository _leads;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public ChangeTrialStatusCommandHandler(IOperationGuard guard, ICurrentActor actor, ILeadRepository leads, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _leads = leads;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<TrialItem>> Handle(ChangeTrialStatusCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmUpdate, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<TrialItem>(allowed.Error);
        }

        var trial = await _leads.GetTrialAsync(tenantId, request.LeadId, request.TrialId, cancellationToken).ConfigureAwait(false);
        if (trial is null)
        {
            return Result.Failure<TrialItem>(GymErrors.NotFound);
        }

        var previous = trial.Status;
        trial.Change(request.Status, request.Notes ?? trial.Notes, request.Outcome);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "TrialStatusChanged", "LeadTrial", trial.Id, previous.ToString(), trial.Status.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new TrialItem(trial.Id, trial.LeadId, trial.ScheduledAt, trial.Status, trial.Notes, trial.Outcome));
    }
}
