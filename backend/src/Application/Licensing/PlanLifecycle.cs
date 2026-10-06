using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Licensing;

public sealed record UpdatePlanCommand(Guid PlanId, string Name, string? Description, string AppliesTo, int SortOrder, int BaseCapacity) : IRequest<Result>;

public sealed record ActivatePlanCommand(Guid PlanId) : IRequest<Result>;

public sealed record DeactivatePlanCommand(Guid PlanId) : IRequest<Result>;

public sealed record RetirePlanCommand(Guid PlanId) : IRequest<Result>;

public sealed class PlanLifecycleHandler :
    IRequestHandler<UpdatePlanCommand, Result>,
    IRequestHandler<ActivatePlanCommand, Result>,
    IRequestHandler<DeactivatePlanCommand, Result>,
    IRequestHandler<RetirePlanCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IRoleRepository _roles;
    private readonly IPermissionService _permissions;
    private readonly ILicenseAdminStore _store;
    private readonly IAuditLogRepository _audit;
    private readonly IPermissionCacheInvalidator _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public PlanLifecycleHandler(ICurrentActor actor, IRoleRepository roles, IPermissionService permissions, ILicenseAdminStore store, IAuditLogRepository audit, IPermissionCacheInvalidator cache, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _actor = actor;
        _roles = roles;
        _permissions = permissions;
        _store = store;
        _audit = audit;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public Task<Result> Handle(UpdatePlanCommand request, CancellationToken cancellationToken) =>
        ChangePlanAsync(request.PlanId, "PlanUpdated", plan =>
        {
            plan.Update(request.Name, request.Description, request.AppliesTo, request.SortOrder, request.BaseCapacity);
            return true;
        }, cancellationToken);

    public Task<Result> Handle(ActivatePlanCommand request, CancellationToken cancellationToken) =>
        ChangePlanAsync(request.PlanId, "PlanActivated", plan => plan.Activate(), cancellationToken);

    public Task<Result> Handle(DeactivatePlanCommand request, CancellationToken cancellationToken) =>
        ChangePlanAsync(request.PlanId, "PlanDeactivated", plan => plan.Deactivate(), cancellationToken);

    public Task<Result> Handle(RetirePlanCommand request, CancellationToken cancellationToken) =>
        ChangePlanAsync(request.PlanId, "PlanRetired", plan =>
        {
            plan.Retire();
            return true;
        }, cancellationToken);

    private async Task<Result> ChangePlanAsync(Guid planId, string action, Func<PlanDefinition, bool> change, CancellationToken cancellationToken)
    {
        if (await SuperAdminAsync(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return denied;
        }

        var plan = await _store.GetPlanAsync(planId, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return Result.Failure(new Error("license.not_found", "The plan was not found."));
        }

        if (!change(plan))
        {
            return Result.Failure(new Error("license.plan_conflict", "That plan status change is not allowed."));
        }

        foreach (var tenantId in await _store.TenantsForPlanAsync(plan.Id, cancellationToken).ConfigureAwait(false))
        {
            _cache.ScheduleFeatures(tenantId);
        }

        await AuditAsync(null, action, "Plan", plan.Id, plan.Status.ToString(), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result> SuperAdminAsync(CancellationToken cancellationToken)
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

    private Task AuditAsync(Guid? tenantId, string action, string entity, Guid entityId, string? value, CancellationToken cancellationToken) =>
        _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, action, entity, entityId, null, value, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken);
}
