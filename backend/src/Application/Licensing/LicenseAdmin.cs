using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Licensing;

public sealed record LicensePoolResponse(Guid LicenseId, string PlanCode, string Status, DateTime StartsAt, DateTime? ExpiresAt, int BaseCapacity, int ExtraCapacity, int Assigned, int Available, bool Usable);

public sealed record LicenseAssignmentResponse(Guid Id, Guid UserId, string Status, DateTime AssignedAt, DateTime? RevokedAt);

public sealed record PlanResponse(Guid Id, string Code, string Name, string Status, string AppliesTo, int BaseCapacity);

public sealed record ExpiringLicenseResponse(Guid LicenseId, Guid TenantId, string PlanCode, DateTime? ExpiresAt);

public interface ILicenseAdminStore
{
    Task<bool> PlanCodeExistsAsync(string code, CancellationToken cancellationToken);

    Task AddPlanAsync(PlanDefinition plan, CancellationToken cancellationToken);

    Task<PlanDefinition?> GetPlanAsync(Guid id, CancellationToken cancellationToken);

    Task SetFeatureAsync(Guid planId, string featureCode, bool enabled, DateTime utcNow, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> TenantsForPlanAsync(Guid planId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanResponse>> ListPlansAsync(CancellationToken cancellationToken);

    // Subscriptions (the former "tenant licenses").
    Task<Subscription?> GetSubscriptionAsync(Guid id, CancellationToken cancellationToken);

    Task<Subscription?> GetActiveSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<Guid?> SubscriptionTenantAsync(Guid subscriptionId, CancellationToken cancellationToken);

    Task<int> ActiveSeatCountAsync(Guid subscriptionId, CancellationToken cancellationToken);

    Task<SubscriptionSeat?> ActiveSeatForUserAsync(Guid subscriptionId, Guid userId, CancellationToken cancellationToken);

    Task AddSeatAsync(SubscriptionSeat seat, CancellationToken cancellationToken);

    Task<SubscriptionSeat?> GetSeatAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<LicenseAssignmentResponse>> ListAssignmentsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExpiringLicenseResponse>> ExpiringAsync(DateTime until, CancellationToken cancellationToken);

    Task<string?> PlanCodeAsync(Guid planId, CancellationToken cancellationToken);

    Task LockSubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken);

    Task<SeatReservation> ReserveSeatAsync(Guid tenantId, Guid subscriptionId, Guid userId, Guid? assignedBy, DateTime utcNow, CancellationToken cancellationToken);

    Task<IReadOnlyList<FeatureCatalogResponse>> ListFeaturesAsync(CancellationToken cancellationToken);

    Task<PlanDetailResponse?> GetPlanDetailAsync(Guid id, CancellationToken cancellationToken);

    Task<PageResult<TenantLicenseAdminResponse>> ListLicensesAsync(PageQuery page, DateTime utcNow, CancellationToken cancellationToken);

    Task<TenantLicenseAdminResponse?> GetLicenseAdminAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken);

    Task<PageResult<AuditEntryResponse>> ListAuditAsync(PageQuery page, CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantMemberOption>> ListMembersAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspacePlanResponse>> ListWorkspacePlansAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, UserPlanAssignment>> ActiveUserPlansAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<Subscription?> GetTenantSubscriptionAsync(Guid tenantId, Guid subscriptionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SubscriptionSeat>> ActiveSeatsForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task AddSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken);

    Task<Subscription?> GetActiveUserSubscriptionAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record CreatePlanCommand(string Code, string Name, string? Description, string AppliesTo, int SortOrder, int BaseCapacity) : IRequest<Result<PlanResponse>>;

public sealed class CreatePlanCommandValidator : AbstractValidator<CreatePlanCommand>
{
    public CreatePlanCommandValidator()
    {
        RuleFor(command => command.Code).NotEmpty().MaximumLength(64);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.AppliesTo).Must(value => value is "Personal" or "Business" or "Any");
        RuleFor(command => command.BaseCapacity).GreaterThanOrEqualTo(0);
    }
}

public sealed record SetPlanFeatureCommand(Guid PlanId, string FeatureCode, bool Enabled) : IRequest<Result>;

public sealed record ExtendTenantLicenseCommand(Guid LicenseId, DateTime? ExpiresAt) : IRequest<Result>;

public sealed record RevokeTenantLicenseCommand(Guid LicenseId) : IRequest<Result>;

public sealed record AssignUserLicenseCommand(Guid UserId) : IRequest<Result<LicenseAssignmentResponse>>;

public sealed record RevokeUserLicenseCommand(Guid AssignmentId) : IRequest<Result>;

public sealed record GetLicensePoolQuery : IRequest<Result<LicensePoolResponse>>;

public sealed record ListLicenseAssignmentsQuery : IRequest<Result<IReadOnlyList<LicenseAssignmentResponse>>>;

public sealed record ListPlansQuery : IRequest<Result<IReadOnlyList<PlanResponse>>>;

public sealed record ListExpiringLicensesQuery(int WithinDays) : IRequest<Result<IReadOnlyList<ExpiringLicenseResponse>>>;

public sealed class LicenseAdminHandler :
    IRequestHandler<CreatePlanCommand, Result<PlanResponse>>,
    IRequestHandler<SetPlanFeatureCommand, Result>,
    IRequestHandler<ExtendTenantLicenseCommand, Result>,
    IRequestHandler<RevokeTenantLicenseCommand, Result>,
    IRequestHandler<AssignUserLicenseCommand, Result<LicenseAssignmentResponse>>,
    IRequestHandler<RevokeUserLicenseCommand, Result>,
    IRequestHandler<GetLicensePoolQuery, Result<LicensePoolResponse>>,
    IRequestHandler<ListLicenseAssignmentsQuery, Result<IReadOnlyList<LicenseAssignmentResponse>>>,
    IRequestHandler<ListPlansQuery, Result<IReadOnlyList<PlanResponse>>>,
    IRequestHandler<ListExpiringLicensesQuery, Result<IReadOnlyList<ExpiringLicenseResponse>>>
{
    private readonly ICurrentActor _actor;
    private readonly IRoleRepository _roles;
    private readonly IPermissionService _permissions;
    private readonly ITenantMembershipRepository _members;
    private readonly ILicenseAdminStore _store;
    private readonly IAuditLogRepository _audit;
    private readonly IPermissionCacheInvalidator _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public LicenseAdminHandler(ICurrentActor actor, IRoleRepository roles, IPermissionService permissions, ITenantMembershipRepository members, ILicenseAdminStore store, IAuditLogRepository audit, IPermissionCacheInvalidator cache, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _actor = actor;
        _roles = roles;
        _permissions = permissions;
        _members = members;
        _store = store;
        _audit = audit;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<PlanResponse>> Handle(CreatePlanCommand request, CancellationToken cancellationToken)
    {
        if (await SuperAdminAsync(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<PlanResponse>(denied.Error);
        }

        if (await _store.PlanCodeExistsAsync(request.Code.Trim().ToUpperInvariant(), cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<PlanResponse>(new Error("license.plan_conflict", "A plan with that code already exists."));
        }

        var plan = PlanDefinition.Create(Guid.NewGuid(), request.Code, request.Name, request.Description, request.AppliesTo, request.SortOrder, request.BaseCapacity);
        plan.Activate();
        await _store.AddPlanAsync(plan, cancellationToken).ConfigureAwait(false);
        await AuditAsync(null, "PlanCreated", "Plan", plan.Id, plan.Code, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new PlanResponse(plan.Id, plan.Code, plan.Name, plan.Status.ToString(), plan.AppliesTo, plan.BaseCapacity));
    }

    public async Task<Result> Handle(SetPlanFeatureCommand request, CancellationToken cancellationToken)
    {
        if (await SuperAdminAsync(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return denied;
        }

        var plan = await _store.GetPlanAsync(request.PlanId, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return Result.Failure(new Error("license.not_found", "The plan was not found."));
        }

        await _store.SetFeatureAsync(plan.Id, request.FeatureCode.Trim().ToUpperInvariant(), request.Enabled, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        foreach (var tenantId in await _store.TenantsForPlanAsync(plan.Id, cancellationToken).ConfigureAwait(false))
        {
            _cache.ScheduleFeatures(tenantId);
        }

        await AuditAsync(null, request.Enabled ? "PlanFeatureEnabled" : "PlanFeatureDisabled", "Plan", plan.Id, request.FeatureCode, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> Handle(ExtendTenantLicenseCommand request, CancellationToken cancellationToken)
    {
        if (await SuperAdminAsync(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return denied;
        }

        var subscription = await _store.GetSubscriptionAsync(request.LicenseId, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
        {
            return Result.Failure(new Error("license.not_found", "The subscription was not found."));
        }

        if (request.ExpiresAt is DateTime expires && expires <= subscription.StartsAt)
        {
            return Result.Failure(new Error("license.invalid_dates", "Expiry must be after the subscription starts."));
        }

        subscription.SetExpiresAt(request.ExpiresAt);
        if (subscription.TenantId is Guid tenantId)
        {
            _cache.ScheduleFeatures(tenantId);
        }

        await AuditAsync(subscription.TenantId, "SubscriptionExtended", "Subscription", subscription.Id, request.ExpiresAt?.ToString("o"), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> Handle(RevokeTenantLicenseCommand request, CancellationToken cancellationToken)
    {
        if (await SuperAdminAsync(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return denied;
        }

        var subscription = await _store.GetSubscriptionAsync(request.LicenseId, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
        {
            return Result.Failure(new Error("license.not_found", "The subscription was not found."));
        }

        subscription.Cancel(_clock.UtcNow, _actor.UserId);
        if (subscription.TenantId is Guid tenantId)
        {
            _cache.ScheduleFeatures(tenantId);
        }

        await AuditAsync(subscription.TenantId, "SubscriptionCancelled", "Subscription", subscription.Id, null, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<LicenseAssignmentResponse>> Handle(AssignUserLicenseCommand request, CancellationToken cancellationToken)
    {
        var access = await ManageOwnAsync(cancellationToken).ConfigureAwait(false);
        if (access.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<LicenseAssignmentResponse>(access.IsFailure ? access.Error : IdentityErrors.InvalidCredentials);
        }

        if (await _members.GetActiveAsync(tenantId, request.UserId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<LicenseAssignmentResponse>(new Error("license.not_found", "That person is not a member of this workspace."));
        }

        var subscription = await _store.GetActiveSubscriptionAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (subscription is null || !subscription.IsUsable(_clock.UtcNow))
        {
            return Result.Failure<LicenseAssignmentResponse>(new Error("license.not_found", "This workspace does not have a usable subscription."));
        }

        SeatReservation? reservation = null;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            reservation = await _store.ReserveSeatAsync(tenantId, subscription.Id, request.UserId, _actor.UserId, _clock.UtcNow, ct).ConfigureAwait(false);
            if (!reservation.Succeeded || reservation.Seat is null)
            {
                return;
            }

            _cache.ScheduleFeatures(tenantId);
            await AuditAsync(tenantId, "SeatAssigned", "SubscriptionSeat", reservation.Seat.Id, request.UserId.ToString(), ct).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        if (reservation is null || !reservation.Succeeded || reservation.Seat is null)
        {
            return Result.Failure<LicenseAssignmentResponse>(reservation?.Error ?? new Error("license.capacity_conflict", "No seats are available."));
        }

        var seat = reservation.Seat;
        return Result.Success(new LicenseAssignmentResponse(seat.Id, seat.UserId, seat.Status.ToString(), seat.AssignedAt, null));
    }

    public async Task<Result> Handle(RevokeUserLicenseCommand request, CancellationToken cancellationToken)
    {
        var access = await ManageOwnAsync(cancellationToken).ConfigureAwait(false);
        if (access.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return access.IsFailure ? access : Result.Failure(IdentityErrors.InvalidCredentials);
        }

        var seat = await _store.GetSeatAsync(request.AssignmentId, cancellationToken).ConfigureAwait(false);
        if (seat is null || await _store.SubscriptionTenantAsync(seat.SubscriptionId, cancellationToken).ConfigureAwait(false) != tenantId)
        {
            return Result.Failure(new Error("license.not_found", "The seat was not found."));
        }

        seat.Release(_clock.UtcNow, _actor.UserId);
        _cache.ScheduleFeatures(tenantId);
        await AuditAsync(tenantId, "SeatReleased", "SubscriptionSeat", seat.Id, null, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<LicensePoolResponse>> Handle(GetLicensePoolQuery request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<LicensePoolResponse>(IdentityErrors.InvalidCredentials);
        }

        if (!await _permissions.HasPermissionAsync(userId, tenantId, PermissionCodes.LicenseRead, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<LicensePoolResponse>(IdentityErrors.Forbidden);
        }

        var subscription = await _store.GetActiveSubscriptionAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
        {
            return Result.Failure<LicensePoolResponse>(new Error("license.not_found", "This workspace does not have a subscription."));
        }

        var assigned = await _store.ActiveSeatCountAsync(subscription.Id, cancellationToken).ConfigureAwait(false);
        var code = await _store.PlanCodeAsync(subscription.PlanId, cancellationToken).ConfigureAwait(false) ?? string.Empty;
        return Result.Success(new LicensePoolResponse(subscription.Id, code, subscription.Status.ToString(), subscription.StartsAt, subscription.ExpiresAt, subscription.Seats, 0, assigned, Math.Max(0, subscription.Seats - assigned), subscription.IsUsable(_clock.UtcNow)));
    }

    public async Task<Result<IReadOnlyList<LicenseAssignmentResponse>>> Handle(ListLicenseAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var access = await ManageOwnAsync(cancellationToken).ConfigureAwait(false);
        if (access.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<IReadOnlyList<LicenseAssignmentResponse>>(access.IsFailure ? access.Error : IdentityErrors.InvalidCredentials);
        }

        return Result.Success(await _store.ListAssignmentsAsync(tenantId, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<IReadOnlyList<PlanResponse>>> Handle(ListPlansQuery request, CancellationToken cancellationToken)
    {
        if (await SuperAdminAsync(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<IReadOnlyList<PlanResponse>>(denied.Error);
        }

        return Result.Success(await _store.ListPlansAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<IReadOnlyList<ExpiringLicenseResponse>>> Handle(ListExpiringLicensesQuery request, CancellationToken cancellationToken)
    {
        if (await SuperAdminAsync(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<IReadOnlyList<ExpiringLicenseResponse>>(denied.Error);
        }

        var until = _clock.UtcNow.AddDays(request.WithinDays <= 0 ? 30 : request.WithinDays);
        return Result.Success(await _store.ExpiringAsync(until, cancellationToken).ConfigureAwait(false));
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

    private async Task<Result> ManageOwnAsync(CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure(IdentityErrors.InvalidCredentials);
        }

        if (!await _permissions.HasPermissionAsync(userId, tenantId, PermissionCodes.LicenseManage, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(IdentityErrors.Forbidden);
        }

        return Result.Success();
    }

    private Task AuditAsync(Guid? tenantId, string action, string entity, Guid entityId, string? value, CancellationToken cancellationToken) =>
        _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, action, entity, entityId, null, value, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken);
}
