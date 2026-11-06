using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Crm;

public sealed record MarkLeadLostCommand(Guid Id, LeadLostReason Reason, string? Notes) : IRequest<Result<LeadDetails>>;

public sealed class MarkLeadLostCommandHandler : IRequestHandler<MarkLeadLostCommand, Result<LeadDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeadRepository _leads;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public MarkLeadLostCommandHandler(IOperationGuard guard, ICurrentActor actor, ILeadRepository leads, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _leads = leads;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<LeadDetails>> Handle(MarkLeadLostCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmLost, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<LeadDetails>(allowed.Error);
        }

        var lead = await _leads.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (lead is null || !lead.MarkLost(request.Reason, userId, _clock.UtcNow))
        {
            return Result.Failure<LeadDetails>(lead is null ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            lead.Update(lead.FullName, lead.Mobile, lead.Email, lead.Source, lead.InterestedPlanId, request.Notes);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "LeadMarkedLost", "Lead", lead.Id, null, request.Reason.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success((await _leads.ReadAsync(tenantId, lead.Id, cancellationToken).ConfigureAwait(false))!);
    }
}

public sealed record ReopenLeadCommand(Guid Id) : IRequest<Result<LeadDetails>>;

public sealed class ReopenLeadCommandHandler : IRequestHandler<ReopenLeadCommand, Result<LeadDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeadRepository _leads;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public ReopenLeadCommandHandler(IOperationGuard guard, ICurrentActor actor, ILeadRepository leads, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _leads = leads;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<LeadDetails>> Handle(ReopenLeadCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmLost, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<LeadDetails>(allowed.Error);
        }

        var lead = await _leads.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (lead is null || !lead.Reopen())
        {
            return Result.Failure<LeadDetails>(lead is null ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "LeadReopened", "Lead", lead.Id, LeadStatus.Lost.ToString(), lead.Status.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success((await _leads.ReadAsync(tenantId, lead.Id, cancellationToken).ConfigureAwait(false))!);
    }
}

