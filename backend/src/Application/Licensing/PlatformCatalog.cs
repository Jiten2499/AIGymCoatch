using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Licensing;

public sealed record FeatureCatalogResponse(string Code, string Name, string Category, string Scope, bool IsActive);

public sealed record PlanFeatureState(string FeatureCode, bool Enabled);

public sealed record PlanDetailResponse(Guid Id, string Code, string Name, string? Description, string Status, string AppliesTo, int SortOrder, int BaseCapacity, IReadOnlyList<PlanFeatureState> Features);

public sealed record TenantLicenseAdminResponse(Guid LicenseId, Guid TenantId, string TenantName, string PlanCode, string Status, DateTime StartsAt, DateTime? ExpiresAt, int BaseCapacity, int ExtraCapacity, int Assigned, int Available, bool Usable, DateTime? RevokedAt);

public sealed record AuditEntryResponse(Guid Id, Guid? TenantId, string Action, string Entity, Guid? EntityId, DateTime CreatedAt);

public sealed record TenantMemberOption(Guid UserId, string Email, string FirstName, string LastName);

public sealed record ListFeatureCatalogQuery : IRequest<Result<IReadOnlyList<FeatureCatalogResponse>>>;

public sealed record GetPlanDetailQuery(Guid PlanId) : IRequest<Result<PlanDetailResponse>>;

public sealed record ListTenantLicensesQuery(int Page, int PageSize) : IRequest<Result<PageResult<TenantLicenseAdminResponse>>>;

public sealed record GetTenantLicenseQuery(Guid LicenseId) : IRequest<Result<TenantLicenseAdminResponse>>;

public sealed record ListAuditQuery(int Page, int PageSize) : IRequest<Result<PageResult<AuditEntryResponse>>>;

public sealed record ListTenantMembersQuery(Guid TenantId) : IRequest<Result<IReadOnlyList<TenantMemberOption>>>;

public sealed record ListTenantAssignmentsQuery(Guid TenantId) : IRequest<Result<IReadOnlyList<LicenseAssignmentResponse>>>;

public sealed record AssignPlatformSeatCommand(Guid TenantId, Guid UserId) : IRequest<Result<LicenseAssignmentResponse>>;

public sealed record RevokePlatformSeatCommand(Guid AssignmentId) : IRequest<Result>;

public sealed class PlatformCatalogHandler :
    IRequestHandler<ListFeatureCatalogQuery, Result<IReadOnlyList<FeatureCatalogResponse>>>,
    IRequestHandler<GetPlanDetailQuery, Result<PlanDetailResponse>>,
    IRequestHandler<ListTenantLicensesQuery, Result<PageResult<TenantLicenseAdminResponse>>>,
    IRequestHandler<GetTenantLicenseQuery, Result<TenantLicenseAdminResponse>>,
    IRequestHandler<ListAuditQuery, Result<PageResult<AuditEntryResponse>>>,
    IRequestHandler<ListTenantMembersQuery, Result<IReadOnlyList<TenantMemberOption>>>,
    IRequestHandler<ListTenantAssignmentsQuery, Result<IReadOnlyList<LicenseAssignmentResponse>>>,
    IRequestHandler<AssignPlatformSeatCommand, Result<LicenseAssignmentResponse>>,
    IRequestHandler<RevokePlatformSeatCommand, Result>
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

    public PlatformCatalogHandler(ICurrentActor actor, IRoleRepository roles, IPermissionService permissions, ITenantMembershipRepository members, ILicenseAdminStore store, IAuditLogRepository audit, IPermissionCacheInvalidator cache, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
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

    public Task<Result<IReadOnlyList<FeatureCatalogResponse>>> Handle(ListFeatureCatalogQuery request, CancellationToken cancellationToken) =>
        Read(cancellationToken, () => _store.ListFeaturesAsync(cancellationToken));

    public async Task<Result<PlanDetailResponse>> Handle(GetPlanDetailQuery request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<PlanDetailResponse>(denied.Error);
        }

        var plan = await _store.GetPlanDetailAsync(request.PlanId, cancellationToken).ConfigureAwait(false);
        return plan is null ? Result.Failure<PlanDetailResponse>(new Error("license.not_found", "The plan was not found.")) : Result.Success(plan);
    }

    public Task<Result<PageResult<TenantLicenseAdminResponse>>> Handle(ListTenantLicensesQuery request, CancellationToken cancellationToken) =>
        Read(cancellationToken, () => _store.ListLicensesAsync(new PageQuery(request.Page, request.PageSize), _clock.UtcNow, cancellationToken));

    public async Task<Result<TenantLicenseAdminResponse>> Handle(GetTenantLicenseQuery request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<TenantLicenseAdminResponse>(denied.Error);
        }

        var license = await _store.GetLicenseAdminAsync(request.LicenseId, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        return license is null
            ? Result.Failure<TenantLicenseAdminResponse>(new Error("license.not_found", "The subscription was not found."))
            : Result.Success(license);
    }

    public Task<Result<PageResult<AuditEntryResponse>>> Handle(ListAuditQuery request, CancellationToken cancellationToken) =>
        Read(cancellationToken, () => _store.ListAuditAsync(new PageQuery(request.Page, request.PageSize), cancellationToken));

    public Task<Result<IReadOnlyList<TenantMemberOption>>> Handle(ListTenantMembersQuery request, CancellationToken cancellationToken) =>
        Read(cancellationToken, () => _store.ListMembersAsync(request.TenantId, cancellationToken));

    public Task<Result<IReadOnlyList<LicenseAssignmentResponse>>> Handle(ListTenantAssignmentsQuery request, CancellationToken cancellationToken) =>
        Read(cancellationToken, () => _store.ListAssignmentsAsync(request.TenantId, cancellationToken));

    public async Task<Result<LicenseAssignmentResponse>> Handle(AssignPlatformSeatCommand request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<LicenseAssignmentResponse>(denied.Error);
        }

        if (await _members.GetActiveAsync(request.TenantId, request.UserId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<LicenseAssignmentResponse>(new Error("license.not_found", "That person is not a member of this workspace."));
        }

        var subscription = await _store.GetActiveSubscriptionAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
        if (subscription is null || !subscription.IsUsable(_clock.UtcNow))
        {
            return Result.Failure<LicenseAssignmentResponse>(new Error("license.not_found", "This workspace does not have a usable subscription."));
        }

        SeatReservation? reservation = null;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            reservation = await _store.ReserveSeatAsync(request.TenantId, subscription.Id, request.UserId, _actor.UserId, _clock.UtcNow, ct).ConfigureAwait(false);
            if (reservation.Succeeded && reservation.Seat is not null)
            {
                _cache.ScheduleFeatures(request.TenantId);
                await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), request.TenantId, _actor.UserId, "SeatAssigned", "SubscriptionSeat", reservation.Seat.Id, null, request.UserId.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), ct).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);

        if (reservation is null || !reservation.Succeeded || reservation.Seat is null)
        {
            return Result.Failure<LicenseAssignmentResponse>(reservation?.Error ?? new Error("license.capacity_conflict", "No seats are currently available."));
        }

        var seat = reservation.Seat;
        return Result.Success(new LicenseAssignmentResponse(seat.Id, seat.UserId, seat.Status.ToString(), seat.AssignedAt, null));
    }

    public async Task<Result> Handle(RevokePlatformSeatCommand request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return denied;
        }

        var seat = await _store.GetSeatAsync(request.AssignmentId, cancellationToken).ConfigureAwait(false);
        if (seat is null)
        {
            return Result.Failure(new Error("license.not_found", "The seat was not found."));
        }

        var tenantId = await _store.SubscriptionTenantAsync(seat.SubscriptionId, cancellationToken).ConfigureAwait(false);
        seat.Release(_clock.UtcNow, _actor.UserId);
        if (tenantId is Guid resolved)
        {
            _cache.ScheduleFeatures(resolved);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "SeatReleased", "SubscriptionSeat", seat.Id, null, null, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result<T>> Read<T>(CancellationToken cancellationToken, Func<Task<T>> load)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<T>(denied.Error);
        }

        return Result.Success(await load().ConfigureAwait(false));
    }

    private Task<Result> Gate(CancellationToken cancellationToken) =>
        PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken);
}
