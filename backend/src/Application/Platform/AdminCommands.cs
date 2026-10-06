using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Platform;

public sealed record PlatformTenantResponse(Guid Id, string Name, string TenantType, string Status);

public sealed record ListPlatformTenantsQuery(int Page, int PageSize) : IRequest<Result<PageResult<PlatformTenantResponse>>>;

public sealed record SuspendTenantCommand(Guid TenantId) : IRequest<Result>;

public sealed record ExportPersonalDataQuery : IRequest<Result<PersonalExport>>;

public sealed class PlatformAdminHandler :
    IRequestHandler<ListPlatformTenantsQuery, Result<PageResult<PlatformTenantResponse>>>,
    IRequestHandler<SuspendTenantCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IRoleRepository _roles;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly IAuditLogRepository _audit;
    private readonly IPermissionCacheInvalidator _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public PlatformAdminHandler(ICurrentActor actor, IRoleRepository roles, IPermissionService permissions, ITenantRepository tenants, IAuditLogRepository audit, IPermissionCacheInvalidator cache, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _actor = actor;
        _roles = roles;
        _permissions = permissions;
        _tenants = tenants;
        _audit = audit;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<PageResult<PlatformTenantResponse>>> Handle(ListPlatformTenantsQuery request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<PageResult<PlatformTenantResponse>>(IdentityErrors.InvalidCredentials);
        }

        var platform = await AuthorizeAsync(cancellationToken).ConfigureAwait(false);
        var scope = platform.IsSuccess
            ? new DirectoryScope(true, null)
            : await _permissions.HasPermissionAsync(userId, tenantId, PermissionCodes.TenantMemberRead, cancellationToken).ConfigureAwait(false)
                ? new DirectoryScope(false, tenantId)
                : null;
        if (scope is null)
        {
            return Result.Failure<PageResult<PlatformTenantResponse>>(IdentityErrors.Forbidden);
        }

        var page = new PageQuery(request.Page, request.PageSize);
        var tenants = await _tenants.ListAsync(scope, page.Skip, page.NormalizedSize, cancellationToken).ConfigureAwait(false);
        var total = await _tenants.CountAsync(scope, cancellationToken).ConfigureAwait(false);
        var items = tenants.Select(tenant => new PlatformTenantResponse(tenant.Id, tenant.Name, tenant.TenantType.ToString(), tenant.Status.ToString())).ToArray();
        return Result.Success(new PageResult<PlatformTenantResponse>(items, page.NormalizedPage, page.NormalizedSize, total));
    }

    public async Task<Result> Handle(SuspendTenantCommand request, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return denied;
        }

        var tenant = await _tenants.GetAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            return Result.Failure(new Error("platform.not_found", "The workspace was not found."));
        }

        tenant.Suspend();
        _cache.ScheduleStatus(tenant.Id);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenant.Id, _actor.UserId, "tenant.suspended", "Tenant", tenant.Id, null, null, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result> AuthorizeAsync(CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure(IdentityErrors.InvalidCredentials);
        }

        var roles = await _roles.GetRoleNamesAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
        if (!roles.Contains(RoleNames.SuperAdmin, StringComparer.Ordinal)
            || !await _permissions.HasPermissionAsync(userId, tenantId, PermissionCodes.PlatformAdmin, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(IdentityErrors.Forbidden);
        }

        return Result.Success();
    }
}

public sealed class ExportPersonalDataQueryHandler : IRequestHandler<ExportPersonalDataQuery, Result<PersonalExport>>
{
    private readonly ICurrentActor _actor;
    private readonly IDataExportService _exports;

    public ExportPersonalDataQueryHandler(ICurrentActor actor, IDataExportService exports)
    {
        _actor = actor;
        _exports = exports;
    }

    public async Task<Result<PersonalExport>> Handle(ExportPersonalDataQuery request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<PersonalExport>(IdentityErrors.InvalidCredentials);
        }

        return Result.Success(await _exports.ExportPersonalAsync(tenantId, userId, cancellationToken).ConfigureAwait(false));
    }
}
