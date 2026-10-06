using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Application.Tenancy;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Licensing;

public sealed record WorkspacePlanFeature(string Code, bool Enabled);

public sealed record WorkspacePlanResponse(
    Guid SubscriptionId,
    string PlanCode,
    string PlanName,
    string? Description,
    string Status,
    DateTime StartsAt,
    DateTime? ExpiresAt,
    int Seats,
    int Assigned,
    int Available,
    bool Usable,
    IReadOnlyList<WorkspacePlanFeature> Features);

public sealed record UserPlanAssignment(Guid SeatId, string PlanName);

public sealed record ListWorkspacePlansQuery : IRequest<Result<IReadOnlyList<WorkspacePlanResponse>>>;

public sealed class ListWorkspacePlansQueryHandler : IRequestHandler<ListWorkspacePlansQuery, Result<IReadOnlyList<WorkspacePlanResponse>>>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly ILicenseAdminStore _store;
    private readonly IDateTimeProvider _clock;

    public ListWorkspacePlansQueryHandler(ICurrentActor actor, IPermissionService permissions, ITenantRepository tenants, ILicenseAdminStore store, IDateTimeProvider clock)
    {
        _actor = actor;
        _permissions = permissions;
        _tenants = tenants;
        _store = store;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<WorkspacePlanResponse>>> Handle(ListWorkspacePlansQuery request, CancellationToken cancellationToken)
    {
        var access = await WorkspaceAccess.OpenAsync(_actor, _tenants, _permissions, PermissionCodes.LicenseRead, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<IReadOnlyList<WorkspacePlanResponse>>(access.Error);
        }

        var plans = await _store.ListWorkspacePlansAsync(access.Value.TenantId, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        return Result.Success(plans);
    }
}

public sealed record AssignWorkspacePlanCommand(Guid UserId, Guid SubscriptionId) : IRequest<Result<LicenseAssignmentResponse>>;

public sealed class AssignWorkspacePlanCommandValidator : AbstractValidator<AssignWorkspacePlanCommand>
{
    public AssignWorkspacePlanCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.SubscriptionId).NotEmpty();
    }
}

public sealed class AssignWorkspacePlanCommandHandler : IRequestHandler<AssignWorkspacePlanCommand, Result<LicenseAssignmentResponse>>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly ITenantMembershipRepository _members;
    private readonly ILicenseAdminStore _store;
    private readonly IAuditLogRepository _audit;
    private readonly IPermissionCacheInvalidator _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public AssignWorkspacePlanCommandHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        ITenantRepository tenants,
        ITenantMembershipRepository members,
        ILicenseAdminStore store,
        IAuditLogRepository audit,
        IPermissionCacheInvalidator cache,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _actor = actor;
        _permissions = permissions;
        _tenants = tenants;
        _members = members;
        _store = store;
        _audit = audit;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<LicenseAssignmentResponse>> Handle(AssignWorkspacePlanCommand request, CancellationToken cancellationToken)
    {
        var access = await WorkspaceAccess.OpenAsync(_actor, _tenants, _permissions, PermissionCodes.LicenseManage, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<LicenseAssignmentResponse>(access.Error);
        }

        if (await _members.GetActiveAsync(access.Value.TenantId, request.UserId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<LicenseAssignmentResponse>(new Error("license.not_found", "That person is not a member of this workspace."));
        }

        var subscription = await _store.GetTenantSubscriptionAsync(access.Value.TenantId, request.SubscriptionId, cancellationToken).ConfigureAwait(false);
        if (subscription is null || !subscription.IsUsable(_clock.UtcNow))
        {
            return Result.Failure<LicenseAssignmentResponse>(new Error("license.not_found", "That plan is not assigned to this workspace."));
        }

        var current = await _store.ActiveSeatsForUserAsync(access.Value.TenantId, request.UserId, cancellationToken).ConfigureAwait(false);
        var same = current.FirstOrDefault(seat => seat.SubscriptionId == subscription.Id && seat.IsActive);
        if (same is not null)
        {
            return Result.Success(new LicenseAssignmentResponse(same.Id, same.UserId, same.Status.ToString(), same.AssignedAt, null));
        }

        SeatReservation? reservation = null;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var seat in current.Where(seat => seat.IsActive))
            {
                seat.Release(_clock.UtcNow, access.Value.ActorId);
            }

            reservation = await _store.ReserveSeatAsync(access.Value.TenantId, subscription.Id, request.UserId, access.Value.ActorId, _clock.UtcNow, ct).ConfigureAwait(false);
            if (reservation.Succeeded && reservation.Seat is not null)
            {
                _cache.ScheduleFeatures(access.Value.TenantId);
                await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), access.Value.TenantId, access.Value.ActorId, "SeatAssigned", "SubscriptionSeat", reservation.Seat.Id, null, request.UserId.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), ct).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);

        if (reservation is null || !reservation.Succeeded || reservation.Seat is null)
        {
            return Result.Failure<LicenseAssignmentResponse>(reservation?.Error ?? new Error("license.capacity_conflict", "No seats are available."));
        }

        var assigned = reservation.Seat;
        return Result.Success(new LicenseAssignmentResponse(assigned.Id, assigned.UserId, assigned.Status.ToString(), assigned.AssignedAt, null));
    }
}
