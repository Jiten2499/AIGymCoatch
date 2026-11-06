using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform.BackgroundJobs;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Platform.SystemJobs;

public sealed record ListSystemJobsQuery : IRequest<Result<IReadOnlyList<SystemJobListItem>>>;

public sealed record GetSystemJobQuery(string JobKey) : IRequest<Result<SystemJobDetails>>;

public sealed record EnableSystemJobCommand(string JobKey) : IRequest<Result>;

public sealed record DisableSystemJobCommand(string JobKey) : IRequest<Result>;

public sealed record RunSystemJobCommand(string JobKey) : IRequest<Result>;

public static class SystemJobErrors
{
    public static readonly Error NotFound = new("system_job.not_found", "The system job was not found.");

    public static readonly Error Disabled = new("system_job.disabled", "Enable the system job before running it.");
}

public sealed class SystemJobHandlers :
    IRequestHandler<ListSystemJobsQuery, Result<IReadOnlyList<SystemJobListItem>>>,
    IRequestHandler<GetSystemJobQuery, Result<SystemJobDetails>>,
    IRequestHandler<EnableSystemJobCommand, Result>,
    IRequestHandler<DisableSystemJobCommand, Result>,
    IRequestHandler<RunSystemJobCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IRoleRepository _roles;
    private readonly IPermissionService _permissions;
    private readonly ISystemJobCatalog _catalog;
    private readonly ISystemJobRuntime _runtime;
    private readonly IBackgroundJobScheduler _scheduler;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public SystemJobHandlers(
        ICurrentActor actor,
        IRoleRepository roles,
        IPermissionService permissions,
        ISystemJobCatalog catalog,
        ISystemJobRuntime runtime,
        IBackgroundJobScheduler scheduler,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _actor = actor;
        _roles = roles;
        _permissions = permissions;
        _catalog = catalog;
        _runtime = runtime;
        _scheduler = scheduler;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<IReadOnlyList<SystemJobListItem>>> Handle(ListSystemJobsQuery request, CancellationToken cancellationToken)
    {
        if ((await OpenAsync(false, cancellationToken).ConfigureAwait(false)).IsFailure)
        {
            return Result.Failure<IReadOnlyList<SystemJobListItem>>(IdentityErrors.Forbidden);
        }

        var jobs = await _catalog.ListAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<SystemJobListItem>>(jobs.Select(MapList).ToArray());
    }

    public async Task<Result<SystemJobDetails>> Handle(GetSystemJobQuery request, CancellationToken cancellationToken)
    {
        if ((await OpenAsync(false, cancellationToken).ConfigureAwait(false)).IsFailure)
        {
            return Result.Failure<SystemJobDetails>(IdentityErrors.Forbidden);
        }

        var job = await _catalog.FindAsync(request.JobKey, cancellationToken).ConfigureAwait(false);
        return job is null ? Result.Failure<SystemJobDetails>(SystemJobErrors.NotFound) : Result.Success(MapDetails(job));
    }

    public Task<Result> Handle(EnableSystemJobCommand request, CancellationToken cancellationToken) =>
        ChangeAsync(request.JobKey, true, "SystemJobEnabled", cancellationToken);

    public Task<Result> Handle(DisableSystemJobCommand request, CancellationToken cancellationToken) =>
        ChangeAsync(request.JobKey, false, "SystemJobDisabled", cancellationToken);

    public async Task<Result> Handle(RunSystemJobCommand request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(true, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return scope;
        }

        var job = await _catalog.FindAsync(request.JobKey, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return Result.Failure(SystemJobErrors.NotFound);
        }

        if (!job.IsEnabled)
        {
            return Result.Failure(SystemJobErrors.Disabled);
        }

        var correlationId = Guid.NewGuid().ToString("N");
        _scheduler.EnqueueSystemJob(
            SystemJobMapping.Registration(job),
            new BackgroundJobMetadata(job.JobType, job.Queue, null, correlationId, "SystemJob", job.JobKey, scope.Value));
        await AuditAsync(scope.Value, "SystemJobRunNow", job.JobKey, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result> ChangeAsync(string jobKey, bool enabled, string action, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(true, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return scope;
        }

        var job = await _catalog.FindAsync(jobKey, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return Result.Failure(SystemJobErrors.NotFound);
        }

        var changed = job.IsEnabled != enabled;
        job.SetEnabled(enabled, _clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _scheduler.SyncRecurring(SystemJobMapping.Registration(job));
        if (changed)
        {
            await AuditAsync(scope.Value, action, job.JobKey, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    private async Task<Result<Guid>> OpenAsync(bool manage, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<Guid>(IdentityErrors.Forbidden);
        }

        var permission = manage ? PermissionCodes.BackgroundJobManage : PermissionCodes.BackgroundJobRead;
        var names = await _roles.GetRoleNamesAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
        var allowed = names.Contains(RoleNames.SuperAdmin, StringComparer.Ordinal)
            && await _permissions.HasPermissionAsync(userId, tenantId, PermissionCodes.PlatformAdmin, cancellationToken).ConfigureAwait(false)
            && await _permissions.HasPermissionAsync(userId, tenantId, permission, cancellationToken).ConfigureAwait(false);
        return allowed ? Result.Success(userId) : Result.Failure<Guid>(IdentityErrors.Forbidden);
    }

    private SystemJobListItem MapList(SystemJobDefinition job)
    {
        var runtime = _runtime.Read(job.JobKey);
        return new SystemJobListItem(job.JobKey, job.Name, job.JobType, SystemJobCatalog.DescribeSchedule(job.CronExpression), job.Queue, job.IsEnabled, runtime.LastRunUtc, runtime.NextRunUtc, runtime.LastStatus);
    }

    private SystemJobDetails MapDetails(SystemJobDefinition job)
    {
        var runtime = _runtime.Read(job.JobKey);
        return new SystemJobDetails(
            job.JobKey,
            job.Name,
            job.Description,
            job.JobType,
            SystemJobCatalog.DescribeSchedule(job.CronExpression),
            job.CronExpression,
            job.Queue,
            job.IsEnabled,
            job.FeatureKey,
            runtime.LastRunUtc,
            runtime.LastStatus,
            runtime.LastDurationSeconds,
            runtime.NextRunUtc,
            runtime.LastError,
            SystemJobStatisticsReader.Read(runtime.LastResult));
    }

    private async Task AuditAsync(Guid userId, string action, string jobKey, CancellationToken cancellationToken)
    {
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), _actor.TenantId, userId, action, "SystemJob", null, null, jobKey, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
