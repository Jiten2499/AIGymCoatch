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

public sealed record WorkspacePlanCandidate(Guid UserId, string Email, string FirstName, string LastName, string? CurrentPlan);

public sealed record ListWorkspacePlanCandidatesQuery(Guid SubscriptionId) : IRequest<Result<IReadOnlyList<WorkspacePlanCandidate>>>;

public sealed class ListWorkspacePlanCandidatesQueryValidator : AbstractValidator<ListWorkspacePlanCandidatesQuery>
{
    public ListWorkspacePlanCandidatesQueryValidator() => RuleFor(query => query.SubscriptionId).NotEmpty();
}

public sealed class ListWorkspacePlanCandidatesQueryHandler : IRequestHandler<ListWorkspacePlanCandidatesQuery, Result<IReadOnlyList<WorkspacePlanCandidate>>>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly ILicenseAdminStore _store;

    public ListWorkspacePlanCandidatesQueryHandler(ICurrentActor actor, IPermissionService permissions, ITenantRepository tenants, ILicenseAdminStore store)
    {
        _actor = actor;
        _permissions = permissions;
        _tenants = tenants;
        _store = store;
    }

    public async Task<Result<IReadOnlyList<WorkspacePlanCandidate>>> Handle(ListWorkspacePlanCandidatesQuery request, CancellationToken cancellationToken)
    {
        var access = await WorkspaceAccess.OpenAsync(_actor, _tenants, _permissions, PermissionCodes.LicenseRead, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<IReadOnlyList<WorkspacePlanCandidate>>(access.Error);
        }

        if (await _store.GetTenantSubscriptionAsync(access.Value.TenantId, request.SubscriptionId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<IReadOnlyList<WorkspacePlanCandidate>>(new Error("license.not_found", "That plan is not assigned to this workspace."));
        }

        var members = await _store.ListMembersAsync(access.Value.TenantId, cancellationToken).ConfigureAwait(false);
        var plans = await _store.ActiveUserPlansAsync(access.Value.TenantId, cancellationToken).ConfigureAwait(false);
        var assigned = await _store.ListActivePlanAssigneesAsync(access.Value.TenantId, request.SubscriptionId, cancellationToken).ConfigureAwait(false);
        var held = assigned.Select(item => item.UserId).ToHashSet();
        var candidates = members
            .Where(member => !held.Contains(member.UserId))
            .Select(member => new WorkspacePlanCandidate(member.UserId, member.Email, member.FirstName, member.LastName, plans.TryGetValue(member.UserId, out var plan) ? plan.PlanName : null))
            .ToArray();
        return Result.Success<IReadOnlyList<WorkspacePlanCandidate>>(candidates);
    }
}

public sealed record AssignWorkspacePlanUsersCommand(Guid SubscriptionId, IReadOnlyList<Guid> UserIds) : IRequest<Result>;

public sealed class AssignWorkspacePlanUsersCommandValidator : AbstractValidator<AssignWorkspacePlanUsersCommand>
{
    public AssignWorkspacePlanUsersCommandValidator()
    {
        RuleFor(command => command.SubscriptionId).NotEmpty();
        RuleFor(command => command.UserIds).NotEmpty();
        RuleForEach(command => command.UserIds).NotEmpty();
    }
}

public sealed class AssignWorkspacePlanUsersCommandHandler : IRequestHandler<AssignWorkspacePlanUsersCommand, Result>
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

    public AssignWorkspacePlanUsersCommandHandler(
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

    public async Task<Result> Handle(AssignWorkspacePlanUsersCommand request, CancellationToken cancellationToken)
    {
        var access = await WorkspaceAccess.OpenAsync(_actor, _tenants, _permissions, PermissionCodes.LicenseManage, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return access;
        }

        var subscription = await _store.GetTenantSubscriptionAsync(access.Value.TenantId, request.SubscriptionId, cancellationToken).ConfigureAwait(false);
        if (subscription is null || !subscription.IsUsable(_clock.UtcNow))
        {
            return Result.Failure(new Error("license.not_found", "That plan is not assigned to this workspace."));
        }

        var pending = new List<Guid>();
        foreach (var userId in request.UserIds.Distinct())
        {
            if (await _members.GetActiveAsync(access.Value.TenantId, userId, cancellationToken).ConfigureAwait(false) is null)
            {
                return Result.Failure(new Error("license.not_found", "That person is not a member of this workspace."));
            }

            var current = await _store.ActiveSeatsForUserAsync(access.Value.TenantId, userId, cancellationToken).ConfigureAwait(false);
            if (current.Any(seat => seat.SubscriptionId == subscription.Id && seat.IsActive))
            {
                continue;
            }

            pending.Add(userId);
        }

        if (pending.Count == 0)
        {
            return Result.Success();
        }

        var used = await _store.ActiveSeatCountAsync(subscription.Id, cancellationToken).ConfigureAwait(false);
        if (used + pending.Count > subscription.Seats)
        {
            return Result.Failure(new Error("license.capacity_conflict", "No seats are available."));
        }

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                foreach (var userId in pending)
                {
                    var current = await _store.ActiveSeatsForUserAsync(access.Value.TenantId, userId, ct).ConfigureAwait(false);
                    foreach (var seat in current.Where(seat => seat.IsActive))
                    {
                        seat.Release(_clock.UtcNow, access.Value.ActorId);
                    }

                    var reservation = await _store.ReserveSeatAsync(access.Value.TenantId, subscription.Id, userId, access.Value.ActorId, _clock.UtcNow, ct).ConfigureAwait(false);
                    if (!reservation.Succeeded || reservation.Seat is null)
                    {
                        throw new PlanAssignException(reservation.Error ?? new Error("license.capacity_conflict", "No seats are available."));
                    }

                    await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), access.Value.TenantId, access.Value.ActorId, "SeatAssigned", "SubscriptionSeat", reservation.Seat.Id, null, userId.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), ct).ConfigureAwait(false);
                }

                _cache.ScheduleFeatures(access.Value.TenantId);
                await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (PlanAssignException exception)
        {
            return Result.Failure(exception.Error);
        }

        return Result.Success();
    }

    private sealed class PlanAssignException(Error error) : Exception
    {
        public Error Error { get; } = error;
    }
}
