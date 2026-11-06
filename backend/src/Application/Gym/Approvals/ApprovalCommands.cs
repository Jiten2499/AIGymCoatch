using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Gym.Configuration;
using LifeFitAI.Application.Gym.Leave;
using LifeFitAI.Application.Gym.Trainers;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Approvals;

public sealed record DecideApprovalRequestCommand(Guid Id, bool Approve, string? Comment) : IRequest<Result<ApprovalRequestDetails>>;

public sealed class DecideApprovalRequestCommandValidator : AbstractValidator<DecideApprovalRequestCommand>
{
    public DecideApprovalRequestCommandValidator()
    {
        RuleFor(command => command.Comment).NotEmpty().MaximumLength(500).When(command => !command.Approve);
        RuleFor(command => command.Comment).MaximumLength(500);
    }
}

public sealed class DecideApprovalRequestCommandHandler : IRequestHandler<DecideApprovalRequestCommand, Result<ApprovalRequestDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly IApprovalRequestRepository _requests;
    private readonly IMembershipRepository _memberships;
    private readonly IBusinessConfigRepository _configs;
    private readonly ITrainerRewardStore _rewards;
    private readonly ILeaveStore _leaves;
    private readonly IChallengeRepository _notifications;
    private readonly IUserAccountRepository _users;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public DecideApprovalRequestCommandHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        IPermissionService permissions,
        IApprovalRequestRepository requests,
        IMembershipRepository memberships,
        IBusinessConfigRepository configs,
        ITrainerRewardStore rewards,
        ILeaveStore leaves,
        IChallengeRepository notifications,
        IUserAccountRepository users,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _permissions = permissions;
        _requests = requests;
        _memberships = memberships;
        _configs = configs;
        _rewards = rewards;
        _leaves = leaves;
        _notifications = notifications;
        _users = users;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<ApprovalRequestDetails>> Handle(DecideApprovalRequestCommand request, CancellationToken cancellationToken)
    {
        if (_actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<ApprovalRequestDetails>(IdentityErrors.Forbidden);
        }

        var item = await _requests.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return Result.Failure<ApprovalRequestDetails>(GymErrors.NotFound);
        }

        var allowed = await _permissions.HasPermissionAsync(actorId, tenantId, item.RequiredPermission, cancellationToken).ConfigureAwait(false);
        if (item.RequestType == ApprovalRequestType.StaffLeave)
        {
            var gate = await _guard.AllowAsync(PermissionCodes.LeaveApprove, FeatureCodes.StaffLeave, cancellationToken).ConfigureAwait(false);
            if (gate.IsFailure)
            {
                return Result.Failure<ApprovalRequestDetails>(gate.Error);
            }

            allowed = allowed && gate.IsSuccess;
        }
        else if (item.RequestType != ApprovalRequestType.TrainerRewardAward)
        {
            var gate = await _guard.AllowAsync(item.RequiredPermission, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
            allowed = allowed && gate.IsSuccess;
            if (gate.IsFailure)
            {
                return Result.Failure<ApprovalRequestDetails>(gate.Error);
            }
        }

        if (!allowed)
        {
            return Result.Failure<ApprovalRequestDetails>(IdentityErrors.Forbidden);
        }

        if (item.Status != ApprovalRequestStatus.Pending)
        {
            return Result.Failure<ApprovalRequestDetails>(GymErrors.ApprovalAlreadyDecided);
        }

        var name = await DisplayNameAsync(actorId, cancellationToken).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        Error? error = null;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var current = await _requests.GetAsync(tenantId, request.Id, ct).ConfigureAwait(false);
            if (current is null || current.Status != ApprovalRequestStatus.Pending)
            {
                error = GymErrors.ApprovalAlreadyDecided;
                return;
            }

            if (request.Approve)
            {
                error = await ApplyAsync(current, tenantId, actorId, today, request.Comment, ct).ConfigureAwait(false);
                if (error is not null || !current.Approve(actorId, name, _clock.UtcNow, request.Comment))
                {
                    error ??= GymErrors.ApprovalAlreadyDecided;
                    return;
                }
            }
            else
            {
                if (current.RequestType == ApprovalRequestType.TrainerRewardAward)
                {
                    var transaction = await _rewards.GetTransactionAsync(tenantId, current.SourceId, ct).ConfigureAwait(false);
                    if (transaction is null || !transaction.Reject(actorId))
                    {
                        error = transaction is null ? GymErrors.NotFound : GymErrors.ApprovalAlreadyDecided;
                        return;
                    }
                }

                if (current.RequestType == ApprovalRequestType.StaffLeave)
                {
                    error = await RejectLeaveAsync(current, tenantId, actorId, request.Comment ?? string.Empty, ct).ConfigureAwait(false);
                    if (error is not null)
                    {
                        return;
                    }
                }

                if (!current.Reject(actorId, name, _clock.UtcNow, request.Comment ?? string.Empty))
                {
                    error = string.IsNullOrWhiteSpace(request.Comment) ? GymErrors.ApprovalCommentRequired : GymErrors.ApprovalAlreadyDecided;
                    return;
                }
            }

            var action = request.Approve ? "ApprovalApproved" : "ApprovalRejected";
            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, action, "ApprovalRequest", current.Id, ApprovalRequestStatus.Pending.ToString(), current.Status.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), ct).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            item = current;
        }, cancellationToken).ConfigureAwait(false);

        return error is null ? Result.Success(ApprovalQueryHandler.Map(item)) : Result.Failure<ApprovalRequestDetails>(error);
    }

    private async Task<Error?> ApplyAsync(ApprovalRequest request, Guid tenantId, Guid actorId, DateOnly today, string? comment, CancellationToken cancellationToken)
    {
        if (request.RequestType == ApprovalRequestType.StaffLeave)
        {
            var leave = await _leaves.GetAsync(tenantId, request.SourceId, cancellationToken).ConfigureAwait(false);
            if (leave is null || !leave.Approve(actorId, comment, _clock.UtcNow))
            {
                return leave is null ? GymErrors.NotFound : GymErrors.ApprovalAlreadyDecided;
            }

            await _notifications.AddNotificationAsync(
                Notification.Create(Guid.NewGuid(), leave.UserId, tenantId, NotificationType.StaffLeaveUpdate, "Leave approved", $"{leave.LeaveType} leave {leave.StartDate:yyyy-MM-dd} to {leave.EndDate:yyyy-MM-dd} was approved.", _clock.UtcNow),
                cancellationToken).ConfigureAwait(false);
            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "StaffLeaveApproved", "StaffLeaveRequest", leave.Id, LeaveRequestStatus.Pending.ToString(), LeaveRequestStatus.Approved.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (request.RequestType == ApprovalRequestType.TrainerRewardAward)
        {
            var transaction = await _rewards.GetTransactionAsync(tenantId, request.SourceId, cancellationToken).ConfigureAwait(false);
            if (transaction is null || !transaction.Approve(actorId, _clock.UtcNow))
            {
                return transaction is null ? GymErrors.NotFound : GymErrors.ApprovalAlreadyDecided;
            }

            if (transaction.SourceAssignmentId is Guid assignmentId)
            {
                var assignment = await _rewards.GetAssignmentAsync(tenantId, assignmentId, cancellationToken).ConfigureAwait(false);
                assignment?.Complete();
            }

            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "TrainerRewardApproved", "TrainerRewardTransaction", transaction.Id, TrainerRewardApprovalStatus.Pending.ToString(), TrainerRewardApprovalStatus.Approved.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
            return null;
        }

        var membership = await _memberships.GetMembershipAsync(tenantId, request.SourceId, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return GymErrors.NotFound;
        }

        if (request.RequestType == ApprovalRequestType.MembershipExtension)
        {
            var config = await _configs.GetConfigAsync(tenantId, cancellationToken).ConfigureAwait(false);
            var days = request.Days ?? 0;
            if (days <= 0 || (config is { MaxExtensionDays: > 0 } && days > config.MaxExtensionDays) || !membership.Extend(days))
            {
                return GymErrors.ExtensionOutsideLimits;
            }

            await RecordMembershipAsync(tenantId, actorId, membership, MembershipAction.Extended, today, request.Reason, cancellationToken).ConfigureAwait(false);
            return null;
        }

        var previous = membership.Status;
        if (!membership.Freeze(today))
        {
            return GymErrors.InvalidState;
        }

        if (request.Days is int planned && !membership.ExtendForFreeze(planned))
        {
            return GymErrors.InvalidState;
        }

        await _memberships.AddFreezeAsync(MembershipFreeze.Open(Guid.NewGuid(), tenantId, membership.Id, today, previous, request.Reason, _clock.UtcNow, request.Days), cancellationToken).ConfigureAwait(false);
        await RecordMembershipAsync(tenantId, actorId, membership, MembershipAction.Frozen, today, request.Reason, cancellationToken).ConfigureAwait(false);
        return null;
    }

    private async Task<Error?> RejectLeaveAsync(ApprovalRequest request, Guid tenantId, Guid actorId, string comment, CancellationToken cancellationToken)
    {
        var leave = await _leaves.GetAsync(tenantId, request.SourceId, cancellationToken).ConfigureAwait(false);
        if (leave is null || !leave.Reject(actorId, comment, _clock.UtcNow))
        {
            return leave is null
                ? GymErrors.NotFound
                : string.IsNullOrWhiteSpace(comment) ? GymErrors.ApprovalCommentRequired : GymErrors.ApprovalAlreadyDecided;
        }

        await _notifications.AddNotificationAsync(
            Notification.Create(Guid.NewGuid(), leave.UserId, tenantId, NotificationType.StaffLeaveUpdate, "Leave rejected", $"{leave.LeaveType} leave {leave.StartDate:yyyy-MM-dd} to {leave.EndDate:yyyy-MM-dd} was rejected.", _clock.UtcNow),
            cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "StaffLeaveRejected", "StaffLeaveRequest", leave.Id, LeaveRequestStatus.Pending.ToString(), LeaveRequestStatus.Rejected.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        return null;
    }

    private async Task RecordMembershipAsync(Guid tenantId, Guid actorId, GymMembership membership, MembershipAction action, DateOnly today, string? reason, CancellationToken cancellationToken)
    {
        await _memberships.AddHistoryAsync(MembershipHistoryEntry.Create(Guid.NewGuid(), tenantId, membership.Id, action, null, membership.Status, today, reason, actorId, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        var audit = action == MembershipAction.Frozen ? "MembershipFrozen" : "MembershipExtended";
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, audit, "GymMembership", membership.Id, null, membership.Status.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> DisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return user is null ? "Staff" : $"{user.FirstName} {user.LastName}".Trim();
    }
}
