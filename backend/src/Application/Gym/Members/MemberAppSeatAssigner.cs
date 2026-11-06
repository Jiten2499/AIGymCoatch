using LifeFitAI.Application.Email;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Members;

public sealed class MemberAppSeatAssigner : IMemberAppSeatAssigner
{
    private readonly IAccountLinker _accounts;
    private readonly ITenantMembershipRepository _memberships;
    private readonly IRoleRepository _roles;
    private readonly IPeopleRepository _people;
    private readonly ILicenseAdminStore _store;
    private readonly IAuditLogRepository _audit;
    private readonly IPermissionCacheInvalidator _cache;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;
    private readonly ITenantRepository? _tenants;
    private readonly IAccountActivationMailer? _activation;
    private readonly IUserAccountRepository? _users;

    public MemberAppSeatAssigner(
        IAccountLinker accounts,
        ITenantMembershipRepository memberships,
        IRoleRepository roles,
        IPeopleRepository people,
        ILicenseAdminStore store,
        IAuditLogRepository audit,
        IPermissionCacheInvalidator cache,
        IDateTimeProvider clock,
        IClientContext client,
        ITenantRepository? tenants = null,
        IAccountActivationMailer? activation = null,
        IUserAccountRepository? users = null)
    {
        _accounts = accounts;
        _memberships = memberships;
        _roles = roles;
        _people = people;
        _store = store;
        _audit = audit;
        _cache = cache;
        _clock = clock;
        _client = client;
        _tenants = tenants;
        _activation = activation;
        _users = users;
    }

    public async Task<Result<MemberAppAccessResponse>> EnableBasicAccessAsync(
        Guid tenantId,
        Guid actorId,
        MemberProfile member,
        string email,
        CancellationToken cancellationToken)
    {
        if (member.UserId is not null)
        {
            return Result.Failure<MemberAppAccessResponse>(GymErrors.BasicAccessAlreadyEnabled);
        }

        var linked = await LinkLoginAsync(tenantId, member, email, cancellationToken).ConfigureAwait(false);
        if (linked.IsFailure)
        {
            return Result.Failure<MemberAppAccessResponse>(linked.Error);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "MemberBasicAppAccessEnabled", "MemberProfile", member.Id, null, linked.Value.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        return Result.Success(await ReadAsync(tenantId, member, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<MemberAppAccessResponse>> AssignAsync(
        Guid tenantId,
        Guid actorId,
        MemberProfile member,
        Guid subscriptionId,
        string? emailToLink,
        CancellationToken cancellationToken)
    {
        var subscription = await _store.GetTenantSubscriptionAsync(tenantId, subscriptionId, cancellationToken).ConfigureAwait(false);
        if (subscription is null || !subscription.IsUsable(_clock.UtcNow))
        {
            return Result.Failure<MemberAppAccessResponse>(new Error("license.not_found", "That plan is not assigned to this workspace."));
        }

        var userId = member.UserId;
        if (userId is null)
        {
            if (string.IsNullOrWhiteSpace(emailToLink))
            {
                return Result.Failure<MemberAppAccessResponse>(GymErrors.AppLoginRequired);
            }

            var linked = await LinkLoginAsync(tenantId, member, emailToLink, cancellationToken).ConfigureAwait(false);
            if (linked.IsFailure)
            {
                return Result.Failure<MemberAppAccessResponse>(linked.Error);
            }

            userId = linked.Value;
        }

        var current = await _store.ActiveSeatsForUserAsync(tenantId, userId.Value, cancellationToken).ConfigureAwait(false);
        var same = current.FirstOrDefault(seat => seat.SubscriptionId == subscription.Id && seat.IsActive);
        if (same is not null)
        {
            return Result.Success(await ReadAsync(tenantId, member, cancellationToken).ConfigureAwait(false));
        }

        foreach (var seat in current.Where(seat => seat.IsActive))
        {
            seat.Release(_clock.UtcNow, actorId);
            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "SeatReleased", "SubscriptionSeat", seat.Id, null, userId.Value.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        }

        var reservation = await _store.ReserveSeatAsync(tenantId, subscription.Id, userId.Value, actorId, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        if (!reservation.Succeeded || reservation.Seat is null)
        {
            return Result.Failure<MemberAppAccessResponse>(reservation.Error ?? new Error("license.capacity_conflict", "No seats are available."));
        }

        _cache.ScheduleFeatures(tenantId);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "MemberAppPlanAssigned", "SubscriptionSeat", reservation.Seat.Id, null, $"{member.Id}:{userId}", _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        return Result.Success(await ReadAsync(tenantId, member, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result> ReleaseAsync(Guid tenantId, Guid actorId, MemberProfile member, CancellationToken cancellationToken)
    {
        if (member.UserId is not Guid userId)
        {
            return Result.Success();
        }

        var seats = await _store.ActiveSeatsForUserAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        var released = false;
        foreach (var seat in seats.Where(seat => seat.IsActive))
        {
            seat.Release(_clock.UtcNow, actorId);
            released = true;
            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "MemberAppPlanRemoved", "SubscriptionSeat", seat.Id, null, member.Id.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        }

        if (released)
        {
            _cache.ScheduleFeatures(tenantId);
        }

        return Result.Success();
    }

    public async Task<MemberAppAccessResponse> ReadAsync(Guid tenantId, MemberProfile member, CancellationToken cancellationToken)
    {
        if (member.UserId is not Guid userId)
        {
            return new MemberAppAccessResponse(member.Id, null, false, false, "None", "None", null, null, null, null, null, null);
        }

        var seats = await _store.ActiveSeatsForUserAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        var active = seats.FirstOrDefault(seat => seat.IsActive);
        if (active is null)
        {
            return new MemberAppAccessResponse(member.Id, userId, true, false, "Active", "None", null, null, null, null, null, null);
        }

        var subscription = await _store.GetSubscriptionAsync(active.SubscriptionId, cancellationToken).ConfigureAwait(false);
        var code = subscription is null ? null : await _store.PlanCodeAsync(subscription.PlanId, cancellationToken).ConfigureAwait(false);
        var plan = subscription is null ? null : await _store.GetPlanAsync(subscription.PlanId, cancellationToken).ConfigureAwait(false);
        var assigned = subscription is null ? 0 : await _store.ActiveSeatCountAsync(subscription.Id, cancellationToken).ConfigureAwait(false);
        var available = subscription is null ? (int?)null : Math.Max(0, subscription.Seats - assigned);
        return new MemberAppAccessResponse(
            member.Id,
            userId,
            true,
            true,
            "Active",
            active.Status.ToString(),
            active.Id,
            active.SubscriptionId,
            code,
            plan?.Name,
            active.AssignedAt,
            available);
    }

    private async Task<Result<Guid>> LinkLoginAsync(Guid tenantId, MemberProfile member, string email, CancellationToken cancellationToken)
    {
        var existingAccount = _users is not null && await _users.GetByEmailAsync(email.Trim().ToLowerInvariant(), cancellationToken).ConfigureAwait(false) is not null;
        var password = existingAccount ? null : TemporaryPassword.Create();
        var linked = await _accounts.ResolveAsync(email, password, member.FirstName, member.LastName, member.Phone, cancellationToken).ConfigureAwait(false);
        if (linked.IsFailure)
        {
            return Result.Failure<Guid>(linked.Error);
        }

        var user = linked.Value;
        if (await _people.GetMemberByUserAsync(tenantId, user.Id, cancellationToken).ConfigureAwait(false) is { } other && other.Id != member.Id)
        {
            return Result.Failure<Guid>(GymErrors.Conflict);
        }

        if (!member.LinkLogin(user.Id))
        {
            return Result.Failure<Guid>(GymErrors.Conflict);
        }

        if (await _memberships.GetActiveAsync(tenantId, user.Id, cancellationToken).ConfigureAwait(false) is null)
        {
            await _memberships.AddAsync(TenantMember.Create(Guid.NewGuid(), tenantId, user.Id, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        }

        var role = await _roles.GetByNameAsync(RoleNames.Member, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return Result.Failure<Guid>(IdentityErrors.RoleNotFound);
        }

        var names = await _roles.GetRoleNamesAsync(user.Id, tenantId, cancellationToken).ConfigureAwait(false);
        if (!names.Contains(RoleNames.Member))
        {
            await _roles.AddAsync(UserRole.Create(Guid.NewGuid(), user.Id, role.Id, tenantId), cancellationToken).ConfigureAwait(false);
        }

        if (_activation is not null)
        {
            var business = _tenants is null ? null : await _tenants.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
            await _activation.SendAsync(new AccountActivationNotice(user.Id, user.Email, user.FirstName, user.LastName, RoleNames.Member, tenantId, business?.Name ?? "LifeFit AI", existingAccount), cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(user.Id);
    }
}
