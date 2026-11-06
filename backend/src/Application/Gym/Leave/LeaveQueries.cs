using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Leave;

public sealed record ListMyStaffLeavesQuery : IRequest<Result<IReadOnlyList<StaffLeaveListItem>>>;

public sealed record GetStaffLeaveQuery(Guid Id) : IRequest<Result<StaffLeaveDetails>>;

public sealed record ListStaffLeavesQuery(DateOnly From, DateOnly To, Guid? StaffProfileId, LeaveType? LeaveType, LeaveRequestStatus? Status) : IRequest<Result<IReadOnlyList<StaffLeaveListItem>>>;

public sealed class LeaveQueryHandler :
    IRequestHandler<ListMyStaffLeavesQuery, Result<IReadOnlyList<StaffLeaveListItem>>>,
    IRequestHandler<GetStaffLeaveQuery, Result<StaffLeaveDetails>>,
    IRequestHandler<ListStaffLeavesQuery, Result<IReadOnlyList<StaffLeaveListItem>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeaveStore _store;
    private readonly IPeopleRepository _people;
    private readonly IUserAccountRepository _users;
    private readonly IPermissionService _permissions;

    public LeaveQueryHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        ILeaveStore store,
        IPeopleRepository people,
        IUserAccountRepository users,
        IPermissionService permissions)
    {
        _guard = guard;
        _actor = actor;
        _store = store;
        _people = people;
        _users = users;
        _permissions = permissions;
    }

    public async Task<Result<IReadOnlyList<StaffLeaveListItem>>> Handle(ListMyStaffLeavesQuery request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.LeaveApply, FeatureCodes.StaffLeave, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<IReadOnlyList<StaffLeaveListItem>>(allowed.IsFailure ? allowed.Error : IdentityErrors.InvalidCredentials);
        }

        var rows = await _store.ListForUserAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        var staff = await _people.GetStaffByUserAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        var name = await NameAsync(userId, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<StaffLeaveListItem>>(rows.Select(row => LeaveMaps.ToListItem(row, name, staff?.EmployeeCode ?? string.Empty)).ToArray());
    }

    public async Task<Result<StaffLeaveDetails>> Handle(GetStaffLeaveQuery request, CancellationToken cancellationToken)
    {
        if (_actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<StaffLeaveDetails>(IdentityErrors.InvalidCredentials);
        }

        var applyGate = await _guard.AllowAsync(PermissionCodes.LeaveApply, FeatureCodes.StaffLeave, cancellationToken).ConfigureAwait(false);
        var readGate = await _guard.AllowAsync(PermissionCodes.LeaveRead, FeatureCodes.StaffLeave, cancellationToken).ConfigureAwait(false);
        var canReadAll = readGate.IsSuccess && await _permissions.HasPermissionAsync(userId, tenantId, PermissionCodes.LeaveRead, cancellationToken).ConfigureAwait(false);
        if (applyGate.IsFailure && !canReadAll)
        {
            return Result.Failure<StaffLeaveDetails>(applyGate.IsFailure ? applyGate.Error : IdentityErrors.Forbidden);
        }

        var leave = await _store.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (leave is null || (!canReadAll && leave.UserId != userId))
        {
            return Result.Failure<StaffLeaveDetails>(GymErrors.NotFound);
        }

        var staff = await _people.GetStaffAsync(tenantId, leave.StaffProfileId, cancellationToken).ConfigureAwait(false);
        var name = await NameAsync(leave.UserId, cancellationToken).ConfigureAwait(false);
        return Result.Success(LeaveMaps.ToDetails(leave, name, staff?.EmployeeCode ?? string.Empty, leave.UserId == userId && leave.Status == LeaveRequestStatus.Pending));
    }

    public async Task<Result<IReadOnlyList<StaffLeaveListItem>>> Handle(ListStaffLeavesQuery request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.LeaveRead, FeatureCodes.StaffLeave, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<IReadOnlyList<StaffLeaveListItem>>(allowed.IsFailure ? allowed.Error : IdentityErrors.InvalidCredentials);
        }

        if (request.To < request.From)
        {
            return Result.Failure<IReadOnlyList<StaffLeaveListItem>>(GymErrors.LeaveDateRange);
        }

        var rows = await _store.SearchAsync(tenantId, new StaffLeaveCalendarQuery(request.From, request.To, request.StaffProfileId, request.LeaveType, request.Status), cancellationToken).ConfigureAwait(false);
        var staffIds = rows.Select(row => row.StaffProfileId).Distinct().ToArray();
        var userIds = rows.Select(row => row.UserId).Distinct().ToArray();
        var names = await NamesAsync(userIds, cancellationToken).ConfigureAwait(false);
        var codes = new Dictionary<Guid, string>();
        foreach (var staffId in staffIds)
        {
            var staff = await _people.GetStaffAsync(tenantId, staffId, cancellationToken).ConfigureAwait(false);
            if (staff is not null)
            {
                codes[staff.Id] = staff.EmployeeCode;
            }
        }

        return Result.Success<IReadOnlyList<StaffLeaveListItem>>(
            rows.Select(row => LeaveMaps.ToListItem(row, names.GetValueOrDefault(row.UserId, "Staff"), codes.GetValueOrDefault(row.StaffProfileId, string.Empty))).ToArray());
    }

    private async Task<string> NameAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return user is null ? "Staff" : $"{user.FirstName} {user.LastName}".Trim();
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(IReadOnlyList<Guid> userIds, CancellationToken cancellationToken)
    {
        var map = new Dictionary<Guid, string>();
        foreach (var userId in userIds)
        {
            map[userId] = await NameAsync(userId, cancellationToken).ConfigureAwait(false);
        }

        return map;
    }
}
