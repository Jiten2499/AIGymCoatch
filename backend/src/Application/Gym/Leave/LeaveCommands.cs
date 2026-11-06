using FluentValidation;
using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Gym.Approvals;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Leave;

public sealed record ApplyStaffLeaveCommand(LeaveType LeaveType, DateOnly StartDate, DateOnly EndDate, string Reason, Guid? AttachmentFileId) : IRequest<Result<StaffLeaveDetails>>;

public sealed class ApplyStaffLeaveCommandValidator : AbstractValidator<ApplyStaffLeaveCommand>
{
    public ApplyStaffLeaveCommandValidator()
    {
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.EndDate).GreaterThanOrEqualTo(command => command.StartDate).WithMessage("Leave end date must be on or after the start date.");
    }
}

public sealed record CancelStaffLeaveCommand(Guid Id) : IRequest<Result<StaffLeaveDetails>>;

public sealed class LeaveCommandHandler :
    IRequestHandler<ApplyStaffLeaveCommand, Result<StaffLeaveDetails>>,
    IRequestHandler<CancelStaffLeaveCommand, Result<StaffLeaveDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeaveStore _store;
    private readonly IPeopleRepository _people;
    private readonly IApprovalRequestRepository _approvals;
    private readonly IUserAccountRepository _users;
    private readonly IChallengeRepository _notifications;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public LeaveCommandHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        ILeaveStore store,
        IPeopleRepository people,
        IApprovalRequestRepository approvals,
        IUserAccountRepository users,
        IChallengeRepository notifications,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _store = store;
        _people = people;
        _approvals = approvals;
        _users = users;
        _notifications = notifications;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<StaffLeaveDetails>> Handle(ApplyStaffLeaveCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenApply(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<StaffLeaveDetails>(opened.Error);
        }

        var (tenantId, userId) = opened.Value;
        if (request.EndDate < request.StartDate)
        {
            return Result.Failure<StaffLeaveDetails>(GymErrors.LeaveDateRange);
        }

        var staff = await _people.GetStaffByUserAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        if (staff is null)
        {
            return Result.Failure<StaffLeaveDetails>(GymErrors.StaffProfileRequired);
        }

        if (await _store.HasOverlapAsync(tenantId, staff.Id, request.StartDate, request.EndDate, null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<StaffLeaveDetails>(GymErrors.LeaveOverlap);
        }

        var name = await DisplayNameAsync(userId, cancellationToken).ConfigureAwait(false);
        var leave = StaffLeaveRequest.Create(Guid.NewGuid(), tenantId, staff.Id, userId, request.LeaveType, request.StartDate, request.EndDate, request.Reason, request.AttachmentFileId, _clock.UtcNow);
        var approval = ApprovalRequest.Create(
            Guid.NewGuid(),
            tenantId,
            ApprovalRequestType.StaffLeave,
            leave.Id,
            PermissionCodes.LeaveApprove,
            "Staff leave",
            $"{request.LeaveType} · {leave.DurationDays} day(s) · {leave.StartDate:yyyy-MM-dd} to {leave.EndDate:yyyy-MM-dd}",
            request.Reason,
            userId,
            name,
            null,
            name,
            leave.DurationDays,
            leave.StartDate,
            leave.EndDate,
            null,
            null);
        leave.LinkApproval(approval.Id);
        await _store.AddAsync(leave, cancellationToken).ConfigureAwait(false);
        await _approvals.AddAsync(approval, cancellationToken).ConfigureAwait(false);
        await _notifications.AddNotificationAsync(
            Notification.Create(Guid.NewGuid(), userId, tenantId, NotificationType.StaffLeaveUpdate, "Leave submitted", $"{request.LeaveType} leave {leave.StartDate:yyyy-MM-dd} to {leave.EndDate:yyyy-MM-dd} is pending review.", _clock.UtcNow),
            cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "StaffLeaveSubmitted", "StaffLeaveRequest", leave.Id, null, leave.Status.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(LeaveMaps.ToDetails(leave, name, staff.EmployeeCode, true));
    }

    public async Task<Result<StaffLeaveDetails>> Handle(CancelStaffLeaveCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenApply(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<StaffLeaveDetails>(opened.Error);
        }

        var (tenantId, userId) = opened.Value;
        var leave = await _store.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (leave is null || leave.UserId != userId)
        {
            return Result.Failure<StaffLeaveDetails>(GymErrors.NotFound);
        }

        if (!leave.Cancel(_clock.UtcNow))
        {
            return Result.Failure<StaffLeaveDetails>(GymErrors.InvalidState);
        }

        if (leave.ApprovalRequestId is Guid approvalId)
        {
            var approval = await _approvals.GetAsync(tenantId, approvalId, cancellationToken).ConfigureAwait(false);
            approval?.SoftDelete(_clock.UtcNow);
        }

        var staff = await _people.GetStaffByUserAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        var name = await DisplayNameAsync(userId, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "StaffLeaveCancelled", "StaffLeaveRequest", leave.Id, LeaveRequestStatus.Pending.ToString(), leave.Status.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(LeaveMaps.ToDetails(leave, name, staff?.EmployeeCode ?? string.Empty, false));
    }

    private async Task<Result<(Guid TenantId, Guid UserId)>> OpenApply(CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.LeaveApply, FeatureCodes.StaffLeave, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<(Guid, Guid)>(allowed.IsFailure ? allowed.Error : IdentityErrors.InvalidCredentials);
        }

        return Result.Success((tenantId, userId));
    }

    private async Task<string> DisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return user is null ? "Staff" : $"{user.FirstName} {user.LastName}".Trim();
    }
}

internal static class LeaveMaps
{
    public static StaffLeaveDetails ToDetails(StaffLeaveRequest leave, string staffName, string employeeCode, bool canCancel) =>
        new(
            leave.Id,
            leave.StaffProfileId,
            leave.UserId,
            staffName,
            employeeCode,
            leave.LeaveType.ToString(),
            leave.StartDate,
            leave.EndDate,
            leave.DurationDays,
            leave.Status.ToString(),
            leave.Reason,
            leave.AttachmentFileId,
            leave.ApprovalRequestId,
            leave.SubmittedAt,
            leave.DecisionComment,
            leave.DecidedByUserId,
            leave.DecidedAt,
            canCancel && leave.Status == LeaveRequestStatus.Pending);

    public static StaffLeaveListItem ToListItem(StaffLeaveRequest leave, string staffName, string employeeCode) =>
        new(
            leave.Id,
            leave.StaffProfileId,
            leave.UserId,
            staffName,
            employeeCode,
            leave.LeaveType.ToString(),
            leave.StartDate,
            leave.EndDate,
            leave.DurationDays,
            leave.Status.ToString(),
            leave.Reason,
            leave.ApprovalRequestId,
            leave.SubmittedAt);
}
