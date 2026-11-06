using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Platform.BackgroundJobs;

public sealed record ListBackgroundJobsQuery(
    string? Term,
    string? JobType,
    BackgroundJobStatus? Status,
    string? Queue,
    Guid? TenantId,
    DateTime? From,
    DateTime? To,
    string? CorrelationId,
    string? SourceType,
    int Page,
    int PageSize,
    string? Sort,
    bool Descending) : IRequest<Result<PageResult<BackgroundJobListItem>>>;

public sealed record GetBackgroundJobQuery(string JobId) : IRequest<Result<BackgroundJobDetails>>;

public sealed record BackgroundJobSummaryQuery(Guid? TenantId) : IRequest<Result<BackgroundJobSummary>>;

public sealed record ListBackgroundJobQueuesQuery : IRequest<Result<IReadOnlyList<string>>>;

public sealed record RetryBackgroundJobCommand(string JobId) : IRequest<Result>;

public sealed record CancelBackgroundJobCommand(string JobId) : IRequest<Result>;

public sealed class BackgroundJobHandlers :
    IRequestHandler<ListBackgroundJobsQuery, Result<PageResult<BackgroundJobListItem>>>,
    IRequestHandler<GetBackgroundJobQuery, Result<BackgroundJobDetails>>,
    IRequestHandler<BackgroundJobSummaryQuery, Result<BackgroundJobSummary>>,
    IRequestHandler<ListBackgroundJobQueuesQuery, Result<IReadOnlyList<string>>>,
    IRequestHandler<RetryBackgroundJobCommand, Result>,
    IRequestHandler<CancelBackgroundJobCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IRoleRepository _roles;
    private readonly IPermissionService _permissions;
    private readonly IBackgroundJobMonitor _monitor;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public BackgroundJobHandlers(
        ICurrentActor actor,
        IRoleRepository roles,
        IPermissionService permissions,
        IBackgroundJobMonitor monitor,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _actor = actor;
        _roles = roles;
        _permissions = permissions;
        _monitor = monitor;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<PageResult<BackgroundJobListItem>>> Handle(ListBackgroundJobsQuery request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(false, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<PageResult<BackgroundJobListItem>>(scope.Error);
        }

        var tenantId = scope.Value.CrossTenant ? request.TenantId : scope.Value.TenantId;
        var page = await _monitor.SearchAsync(new BackgroundJobSearch(request.Term, request.JobType, request.Status, request.Queue, tenantId, request.From, request.To, request.CorrelationId, request.SourceType, request.Page, request.PageSize, request.Sort, request.Descending), cancellationToken).ConfigureAwait(false);
        return Result.Success(page);
    }

    public async Task<Result<BackgroundJobDetails>> Handle(GetBackgroundJobQuery request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(false, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<BackgroundJobDetails>(scope.Error);
        }

        var details = await _monitor.GetAsync(request.JobId, cancellationToken).ConfigureAwait(false);
        if (details is null || !Visible(scope.Value, details.Job.TenantId))
        {
            return Result.Failure<BackgroundJobDetails>(BackgroundJobErrors.NotFound);
        }

        return Result.Success(details);
    }

    public async Task<Result<BackgroundJobSummary>> Handle(BackgroundJobSummaryQuery request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(false, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<BackgroundJobSummary>(scope.Error);
        }

        var tenantId = scope.Value.CrossTenant ? request.TenantId : scope.Value.TenantId;
        return Result.Success(await _monitor.SummaryAsync(tenantId, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<IReadOnlyList<string>>> Handle(ListBackgroundJobQueuesQuery request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(false, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(scope.Error);
        }

        return Result.Success(await _monitor.QueuesAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result> Handle(RetryBackgroundJobCommand request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(true, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return scope;
        }

        var details = await _monitor.GetAsync(request.JobId, cancellationToken).ConfigureAwait(false);
        if (details is null || !Visible(scope.Value, details.Job.TenantId))
        {
            return Result.Failure(BackgroundJobErrors.NotFound);
        }

        if (!BackgroundJobSafety.CanRetry(details.Job.Status) || !await _monitor.RetryAsync(request.JobId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(BackgroundJobErrors.InvalidState);
        }

        await AuditAsync(scope.Value.TenantId, "BackgroundJobRetried", request.JobId, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> Handle(CancelBackgroundJobCommand request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(true, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return scope;
        }

        var details = await _monitor.GetAsync(request.JobId, cancellationToken).ConfigureAwait(false);
        if (details is null || !Visible(scope.Value, details.Job.TenantId))
        {
            return Result.Failure(BackgroundJobErrors.NotFound);
        }

        if (!BackgroundJobSafety.CanCancel(details.Job.Status) || !await _monitor.CancelAsync(request.JobId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(BackgroundJobErrors.InvalidState);
        }

        await AuditAsync(scope.Value.TenantId, "BackgroundJobCancelled", request.JobId, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result<JobScope>> OpenAsync(bool manage, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<JobScope>(IdentityErrors.Forbidden);
        }

        var permission = manage ? PermissionCodes.BackgroundJobManage : PermissionCodes.BackgroundJobRead;
        if (!await _permissions.HasPermissionAsync(userId, tenantId, permission, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<JobScope>(IdentityErrors.Forbidden);
        }

        var names = await _roles.GetRoleNamesAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
        var crossTenant = names.Contains(RoleNames.SuperAdmin, StringComparer.Ordinal)
            && await _permissions.HasPermissionAsync(userId, tenantId, PermissionCodes.PlatformAdmin, cancellationToken).ConfigureAwait(false);
        return Result.Success(new JobScope(crossTenant, tenantId, userId));
    }

    private static bool Visible(JobScope scope, Guid? jobTenantId) =>
        scope.CrossTenant || (jobTenantId is Guid tenantId && tenantId == scope.TenantId);

    private async Task AuditAsync(Guid tenantId, string action, string jobId, CancellationToken cancellationToken)
    {
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, action, "BackgroundJob", null, null, jobId, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record JobScope(bool CrossTenant, Guid TenantId, Guid UserId);
}
