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

public sealed record ListWorkspacePlanAssigneesQuery(Guid SubscriptionId) : IRequest<Result<IReadOnlyList<WorkspacePlanAssignee>>>;

public sealed class ListWorkspacePlanAssigneesQueryValidator : AbstractValidator<ListWorkspacePlanAssigneesQuery>
{
    public ListWorkspacePlanAssigneesQueryValidator() => RuleFor(query => query.SubscriptionId).NotEmpty();
}

public sealed class ListWorkspacePlanAssigneesQueryHandler : IRequestHandler<ListWorkspacePlanAssigneesQuery, Result<IReadOnlyList<WorkspacePlanAssignee>>>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly ILicenseAdminStore _store;

    public ListWorkspacePlanAssigneesQueryHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        ITenantRepository tenants,
        ILicenseAdminStore store)
    {
        _actor = actor;
        _permissions = permissions;
        _tenants = tenants;
        _store = store;
    }

    public async Task<Result<IReadOnlyList<WorkspacePlanAssignee>>> Handle(ListWorkspacePlanAssigneesQuery request, CancellationToken cancellationToken)
    {
        var access = await WorkspaceAccess.OpenAsync(_actor, _tenants, _permissions, PermissionCodes.LicenseRead, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<IReadOnlyList<WorkspacePlanAssignee>>(access.Error);
        }

        if (await _store.GetTenantSubscriptionAsync(access.Value.TenantId, request.SubscriptionId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<IReadOnlyList<WorkspacePlanAssignee>>(new Error("license.not_found", "That plan is not assigned to this workspace."));
        }

        var assignees = await _store.ListActivePlanAssigneesAsync(access.Value.TenantId, request.SubscriptionId, cancellationToken).ConfigureAwait(false);
        return Result.Success(assignees);
    }
}

public sealed record RevokeWorkspacePlanSeatsCommand(Guid SubscriptionId, IReadOnlyList<Guid> SeatIds) : IRequest<Result>;

public sealed class RevokeWorkspacePlanSeatsCommandValidator : AbstractValidator<RevokeWorkspacePlanSeatsCommand>
{
    public RevokeWorkspacePlanSeatsCommandValidator()
    {
        RuleFor(command => command.SubscriptionId).NotEmpty();
        RuleFor(command => command.SeatIds).NotEmpty();
        RuleForEach(command => command.SeatIds).NotEmpty();
    }
}

public sealed class RevokeWorkspacePlanSeatsCommandHandler : IRequestHandler<RevokeWorkspacePlanSeatsCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly ILicenseAdminStore _store;
    private readonly IAuditLogRepository _audit;
    private readonly IPermissionCacheInvalidator _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public RevokeWorkspacePlanSeatsCommandHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        ITenantRepository tenants,
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
        _store = store;
        _audit = audit;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(RevokeWorkspacePlanSeatsCommand request, CancellationToken cancellationToken)
    {
        var access = await WorkspaceAccess.OpenAsync(_actor, _tenants, _permissions, PermissionCodes.LicenseManage, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return access;
        }

        if (await _store.GetTenantSubscriptionAsync(access.Value.TenantId, request.SubscriptionId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure(new Error("license.not_found", "That plan is not assigned to this workspace."));
        }

        var seatIds = request.SeatIds.Distinct().ToArray();
        var seats = new List<SubscriptionSeat>(seatIds.Length);
        foreach (var seatId in seatIds)
        {
            var seat = await _store.GetSeatAsync(seatId, cancellationToken).ConfigureAwait(false);
            if (seat is null
                || seat.SubscriptionId != request.SubscriptionId
                || !seat.IsActive
                || await _store.SubscriptionTenantAsync(seat.SubscriptionId, cancellationToken).ConfigureAwait(false) != access.Value.TenantId)
            {
                return Result.Failure(new Error("license.not_found", "One or more seats are not active on that workspace plan."));
            }

            seats.Add(seat);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var seat in seats)
            {
                seat.Release(_clock.UtcNow, access.Value.ActorId);
                await _audit.AddAsync(
                    AuditLog.Create(
                        Guid.NewGuid(),
                        access.Value.TenantId,
                        access.Value.ActorId,
                        "SeatReleased",
                        "SubscriptionSeat",
                        seat.Id,
                        null,
                        seat.UserId.ToString(),
                        _client.IpAddress,
                        _client.UserAgent,
                        _clock.UtcNow),
                    ct).ConfigureAwait(false);
            }

            _cache.ScheduleFeatures(access.Value.TenantId);
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
