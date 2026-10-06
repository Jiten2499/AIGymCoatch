using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Licensing;

public sealed record AssignTenantPlanCommand(Guid TenantId, Guid PlanId) : IRequest<Result>;

public sealed record AssignUserPlanCommand(Guid UserId, Guid PlanId) : IRequest<Result>;

public sealed class AssignTenantPlanCommandValidator : AbstractValidator<AssignTenantPlanCommand>
{
    public AssignTenantPlanCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.PlanId).NotEmpty();
    }
}

public sealed class AssignUserPlanCommandValidator : AbstractValidator<AssignUserPlanCommand>
{
    public AssignUserPlanCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.PlanId).NotEmpty();
    }
}

public sealed class AssignTenantPlanCommandHandler : IRequestHandler<AssignTenantPlanCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IRoleRepository _roles;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly ILicenseAdminStore _store;
    private readonly IAuditLogRepository _audit;
    private readonly IPermissionCacheInvalidator _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public AssignTenantPlanCommandHandler(ICurrentActor actor, IRoleRepository roles, IPermissionService permissions, ITenantRepository tenants, ILicenseAdminStore store, IAuditLogRepository audit, IPermissionCacheInvalidator cache, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _actor = actor;
        _roles = roles;
        _permissions = permissions;
        _tenants = tenants;
        _store = store;
        _audit = audit;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(AssignTenantPlanCommand request, CancellationToken cancellationToken)
    {
        if (await PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return denied;
        }

        var tenant = await _tenants.GetAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            return Result.Failure(new Error("platform.not_found", "The workspace was not found."));
        }

        if (tenant.TenantType == TenantType.Platform)
        {
            return Result.Failure(new Error("tenant.not_business", "A plan is assigned to a business workspace."));
        }

        var plan = await _store.GetPlanAsync(request.PlanId, cancellationToken).ConfigureAwait(false);
        if (plan is null || plan.Status != PlanStatus.Active || plan.AppliesTo is not ("Business" or "Any"))
        {
            return Result.Failure(new Error("license.plan_missing", "Choose an active plan that applies to a business."));
        }

        var subscription = await _store.GetActiveSubscriptionAsync(tenant.Id, cancellationToken).ConfigureAwait(false);
        if (subscription is not null && await _store.ActiveSeatCountAsync(subscription.Id, cancellationToken).ConfigureAwait(false) > plan.BaseCapacity)
        {
            return Result.Failure(new Error("license.capacity_conflict", "That plan has fewer seats than the people already assigned."));
        }

        if (subscription is null)
        {
            subscription = Subscription.ForTenant(Guid.NewGuid(), tenant.Id, plan.Id, _clock.UtcNow, plan.BaseCapacity);
            await _store.AddSubscriptionAsync(subscription, cancellationToken).ConfigureAwait(false);
        }
        else if (subscription.PlanId != plan.Id || subscription.Seats != plan.BaseCapacity)
        {
            subscription.ChangePlan(plan.Id);
            subscription.SetSeats(plan.BaseCapacity);
        }

        _cache.ScheduleFeatures(tenant.Id);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenant.Id, _actor.UserId, "SubscriptionPlanAssigned", "Subscription", subscription.Id, null, plan.Code, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}

public sealed class AssignUserPlanCommandHandler : IRequestHandler<AssignUserPlanCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IRoleRepository _roles;
    private readonly IPermissionService _permissions;
    private readonly IUserAccountRepository _users;
    private readonly ILicenseAdminStore _store;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public AssignUserPlanCommandHandler(ICurrentActor actor, IRoleRepository roles, IPermissionService permissions, IUserAccountRepository users, ILicenseAdminStore store, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _actor = actor;
        _roles = roles;
        _permissions = permissions;
        _users = users;
        _store = store;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(AssignUserPlanCommand request, CancellationToken cancellationToken)
    {
        if (await PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return denied;
        }

        var user = await _users.GetByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure(new Error("platform.not_found", "The user was not found."));
        }

        var plan = await _store.GetPlanAsync(request.PlanId, cancellationToken).ConfigureAwait(false);
        if (plan is null || plan.Status != PlanStatus.Active || plan.AppliesTo is not ("Personal" or "Any"))
        {
            return Result.Failure(new Error("license.plan_missing", "Choose an active plan that applies to a person."));
        }

        var subscription = await _store.GetActiveUserSubscriptionAsync(user.Id, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
        {
            subscription = Subscription.ForUser(Guid.NewGuid(), user.Id, plan.Id, _clock.UtcNow, plan.BaseCapacity);
            await _store.AddSubscriptionAsync(subscription, cancellationToken).ConfigureAwait(false);
        }
        else if (subscription.PlanId != plan.Id || subscription.Seats != plan.BaseCapacity)
        {
            subscription.ChangePlan(plan.Id);
            subscription.SetSeats(plan.BaseCapacity);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), null, _actor.UserId, "UserPlanAssigned", "Subscription", subscription.Id, null, plan.Code, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
