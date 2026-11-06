using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Crm;

public sealed record GetLeadQuery(Guid Id) : IRequest<Result<LeadDetails>>;

public sealed class GetLeadQueryHandler : IRequestHandler<GetLeadQuery, Result<LeadDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeadRepository _leads;

    public GetLeadQueryHandler(IOperationGuard guard, ICurrentActor actor, ILeadRepository leads)
    {
        _guard = guard;
        _actor = actor;
        _leads = leads;
    }

    public async Task<Result<LeadDetails>> Handle(GetLeadQuery request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmRead, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<LeadDetails>(allowed.Error);
        }

        var lead = await _leads.ReadAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        return lead is null ? Result.Failure<LeadDetails>(GymErrors.NotFound) : Result.Success(lead);
    }
}

public sealed record GetLeadsQuery(string? Term, LeadStatus? Status, LeadSource? Source, Guid? AssignedUserId, Guid? PlanId, string? FollowUp, TrialStatus? TrialStatus, int Page, int PageSize, string? Sort) : IRequest<Result<PageResult<LeadListItem>>>;

public sealed class GetLeadsQueryHandler : IRequestHandler<GetLeadsQuery, Result<PageResult<LeadListItem>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeadRepository _leads;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IDateTimeProvider _clock;

    public GetLeadsQueryHandler(IOperationGuard guard, ICurrentActor actor, ILeadRepository leads, IBusinessProfileRepository profiles, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _leads = leads;
        _profiles = profiles;
        _clock = clock;
    }

    public async Task<Result<PageResult<LeadListItem>>> Handle(GetLeadsQuery request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmRead, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<PageResult<LeadListItem>>(allowed.Error);
        }

        DateTime? from = null;
        DateTime? to = null;
        if (request.FollowUp is "today" or "overdue" or "upcoming")
        {
            var zone = await ZoneAsync(tenantId, cancellationToken).ConfigureAwait(false);
            var start = CrmCalendar.LocalDayStartUtc(_clock.UtcNow, zone);
            var next = CrmCalendar.NextLocalDayStartUtc(_clock.UtcNow, zone);
            if (request.FollowUp == "overdue")
            {
                to = start;
            }
            else if (request.FollowUp == "upcoming")
            {
                from = next;
            }
            else
            {
                from = start;
                to = next;
            }
        }

        var page = await _leads.SearchAsync(tenantId, new LeadSearch(request.Term, request.Status, request.Source, request.AssignedUserId, request.PlanId, from, to, request.TrialStatus, request.Sort, request.Page, request.PageSize), cancellationToken).ConfigureAwait(false);
        return Result.Success(page);
    }

    private async Task<TimeZoneInfo> ZoneAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return CrmCalendar.Zone(profile?.TimeZone);
    }
}

public sealed record GetLeadFollowUpsQuery(Guid LeadId) : IRequest<Result<IReadOnlyList<FollowUpItem>>>;

public sealed class GetLeadFollowUpsQueryHandler : IRequestHandler<GetLeadFollowUpsQuery, Result<IReadOnlyList<FollowUpItem>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeadRepository _leads;

    public GetLeadFollowUpsQueryHandler(IOperationGuard guard, ICurrentActor actor, ILeadRepository leads)
    {
        _guard = guard;
        _actor = actor;
        _leads = leads;
    }

    public async Task<Result<IReadOnlyList<FollowUpItem>>> Handle(GetLeadFollowUpsQuery request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmRead, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<IReadOnlyList<FollowUpItem>>(allowed.Error);
        }

        if (await _leads.GetAsync(tenantId, request.LeadId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<IReadOnlyList<FollowUpItem>>(GymErrors.NotFound);
        }

        return Result.Success(await _leads.ListFollowUpsAsync(tenantId, request.LeadId, cancellationToken).ConfigureAwait(false));
    }
}

public sealed record GetFollowUpQueueQuery(string Bucket, int Page, int PageSize) : IRequest<Result<PageResult<FollowUpItem>>>;

public sealed class GetFollowUpQueueQueryHandler : IRequestHandler<GetFollowUpQueueQuery, Result<PageResult<FollowUpItem>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeadRepository _leads;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IDateTimeProvider _clock;

    public GetFollowUpQueueQueryHandler(IOperationGuard guard, ICurrentActor actor, ILeadRepository leads, IBusinessProfileRepository profiles, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _leads = leads;
        _profiles = profiles;
        _clock = clock;
    }

    public async Task<Result<PageResult<FollowUpItem>>> Handle(GetFollowUpQueueQuery request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmRead, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<PageResult<FollowUpItem>>(allowed.Error);
        }

        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var zone = CrmCalendar.Zone(profile?.TimeZone);
        var start = CrmCalendar.LocalDayStartUtc(_clock.UtcNow, zone);
        var next = CrmCalendar.NextLocalDayStartUtc(_clock.UtcNow, zone);
        var page = await _leads.QueueAsync(tenantId, request.Bucket, start, next, new PageQuery(request.Page, request.PageSize), cancellationToken).ConfigureAwait(false);
        return Result.Success(page);
    }
}

public sealed record GetLeadTrialsQuery(Guid LeadId) : IRequest<Result<IReadOnlyList<TrialItem>>>;

public sealed class GetLeadTrialsQueryHandler : IRequestHandler<GetLeadTrialsQuery, Result<IReadOnlyList<TrialItem>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeadRepository _leads;

    public GetLeadTrialsQueryHandler(IOperationGuard guard, ICurrentActor actor, ILeadRepository leads)
    {
        _guard = guard;
        _actor = actor;
        _leads = leads;
    }

    public async Task<Result<IReadOnlyList<TrialItem>>> Handle(GetLeadTrialsQuery request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmRead, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<IReadOnlyList<TrialItem>>(allowed.Error);
        }

        if (await _leads.GetAsync(tenantId, request.LeadId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<IReadOnlyList<TrialItem>>(GymErrors.NotFound);
        }

        return Result.Success(await _leads.ListTrialsAsync(tenantId, request.LeadId, cancellationToken).ConfigureAwait(false));
    }
}

public sealed record GetCrmSummaryQuery : IRequest<Result<CrmSummary>>;

public sealed class GetCrmSummaryQueryHandler : IRequestHandler<GetCrmSummaryQuery, Result<CrmSummary>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeadRepository _leads;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IDateTimeProvider _clock;

    public GetCrmSummaryQueryHandler(IOperationGuard guard, ICurrentActor actor, ILeadRepository leads, IBusinessProfileRepository profiles, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _leads = leads;
        _profiles = profiles;
        _clock = clock;
    }

    public async Task<Result<CrmSummary>> Handle(GetCrmSummaryQuery request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmRead, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<CrmSummary>(allowed.Error);
        }

        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var zone = CrmCalendar.Zone(profile?.TimeZone);
        var summary = await _leads.SummarizeAsync(tenantId, CrmCalendar.LocalDayStartUtc(_clock.UtcNow, zone), CrmCalendar.NextLocalDayStartUtc(_clock.UtcNow, zone), CrmCalendar.LocalMonthStartUtc(_clock.UtcNow, zone), cancellationToken).ConfigureAwait(false);
        return Result.Success(summary);
    }
}
