using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Approvals;

public sealed record GetApprovalInboxAccessQuery : IRequest<Result<ApprovalInboxAccess>>;

public sealed record ListApprovalRequestsQuery(ApprovalListQuery Query) : IRequest<Result<PageResult<ApprovalRequestListItem>>>;

public sealed record GetApprovalRequestQuery(Guid Id) : IRequest<Result<ApprovalRequestDetails>>;

public sealed class ApprovalQueryHandler :
    IRequestHandler<GetApprovalInboxAccessQuery, Result<ApprovalInboxAccess>>,
    IRequestHandler<ListApprovalRequestsQuery, Result<PageResult<ApprovalRequestListItem>>>,
    IRequestHandler<GetApprovalRequestQuery, Result<ApprovalRequestDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly IApprovalRequestRepository _requests;

    public ApprovalQueryHandler(IOperationGuard guard, ICurrentActor actor, IPermissionService permissions, IApprovalRequestRepository requests)
    {
        _guard = guard;
        _actor = actor;
        _permissions = permissions;
        _requests = requests;
    }

    public async Task<Result<ApprovalInboxAccess>> Handle(GetApprovalInboxAccessQuery request, CancellationToken cancellationToken)
    {
        var allowed = await AllowedAsync(cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<ApprovalInboxAccess>(allowed.Error);
        }

        var count = await _requests.CountPendingAsync(allowed.Value.TenantId, allowed.Value.Permissions, cancellationToken).ConfigureAwait(false);
        return Result.Success(new ApprovalInboxAccess(count));
    }

    public async Task<Result<PageResult<ApprovalRequestListItem>>> Handle(ListApprovalRequestsQuery request, CancellationToken cancellationToken)
    {
        var allowed = await AllowedAsync(cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<PageResult<ApprovalRequestListItem>>(allowed.Error);
        }

        var page = await _requests.SearchAsync(allowed.Value.TenantId, allowed.Value.Permissions, request.Query, cancellationToken).ConfigureAwait(false);
        return Result.Success(page);
    }

    public async Task<Result<ApprovalRequestDetails>> Handle(GetApprovalRequestQuery request, CancellationToken cancellationToken)
    {
        var allowed = await AllowedAsync(cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<ApprovalRequestDetails>(allowed.Error);
        }

        var item = await _requests.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return Result.Failure<ApprovalRequestDetails>(GymErrors.NotFound);
        }

        if (!allowed.Value.Permissions.Contains(item.RequiredPermission, StringComparer.Ordinal))
        {
            return Result.Failure<ApprovalRequestDetails>(IdentityErrors.Forbidden);
        }

        return Result.Success(Map(item));
    }

    private async Task<Result<(Guid TenantId, IReadOnlyList<string> Permissions)>> AllowedAsync(CancellationToken cancellationToken)
    {
        if (_actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<(Guid, IReadOnlyList<string>)>(IdentityErrors.Forbidden);
        }

        var permissions = new List<string>();
        if (await _permissions.HasPermissionAsync(userId, tenantId, PermissionCodes.MembershipFreeze, cancellationToken).ConfigureAwait(false))
        {
            permissions.Add(PermissionCodes.MembershipFreeze);
        }

        if (await _permissions.HasPermissionAsync(userId, tenantId, PermissionCodes.MembershipRenew, cancellationToken).ConfigureAwait(false))
        {
            permissions.Add(PermissionCodes.MembershipRenew);
        }

        if (await _permissions.HasPermissionAsync(userId, tenantId, PermissionCodes.TrainerUpdate, cancellationToken).ConfigureAwait(false))
        {
            permissions.Add(PermissionCodes.TrainerUpdate);
        }

        if (await _permissions.HasPermissionAsync(userId, tenantId, PermissionCodes.LeaveApprove, cancellationToken).ConfigureAwait(false))
        {
            permissions.Add(PermissionCodes.LeaveApprove);
        }

        if (permissions.Count == 0)
        {
            return Result.Failure<(Guid, IReadOnlyList<string>)>(IdentityErrors.Forbidden);
        }

        var membershipGate = permissions.Contains(PermissionCodes.MembershipFreeze, StringComparer.Ordinal) || permissions.Contains(PermissionCodes.MembershipRenew, StringComparer.Ordinal)
            ? await _guard.AllowAsync(permissions.Contains(PermissionCodes.MembershipFreeze, StringComparer.Ordinal) ? PermissionCodes.MembershipFreeze : PermissionCodes.MembershipRenew, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false)
            : Result.Success();
        var hasTrainerApproval = permissions.Contains(PermissionCodes.TrainerUpdate, StringComparer.Ordinal);
        var staffGate = hasTrainerApproval ? Result.Success() : Result.Failure(IdentityErrors.Forbidden);
        var leaveGate = permissions.Contains(PermissionCodes.LeaveApprove, StringComparer.Ordinal)
            ? await _guard.AllowAsync(PermissionCodes.LeaveApprove, FeatureCodes.StaffLeave, cancellationToken).ConfigureAwait(false)
            : Result.Failure(IdentityErrors.Forbidden);
        if (membershipGate.IsFailure && staffGate.IsFailure && leaveGate.IsFailure)
        {
            return Result.Failure<(Guid, IReadOnlyList<string>)>(membershipGate.IsFailure ? membershipGate.Error : staffGate.IsFailure ? staffGate.Error : leaveGate.Error);
        }

        return Result.Success<(Guid, IReadOnlyList<string>)>((tenantId, permissions));
    }

    internal static ApprovalRequestDetails Map(ApprovalRequest item)
    {
        var history = new List<ApprovalHistoryEntry>
        {
            new("Requested", item.CreatedAt, item.RequestedByName, item.Reason),
        };
        if (item.DecidedAt is DateTime decidedAt && item.DecidedByName is not null)
        {
            history.Add(new(item.Status.ToString(), decidedAt, item.DecidedByName, item.DecisionComment));
        }

        return new(
            item.Id,
            item.RequestType,
            item.Title,
            item.Summary,
            item.Reason,
            item.RequestedByName,
            item.CreatedAt,
            item.Status,
            item.RelatedLabel,
            item.MemberProfileId,
            item.SourceId,
            item.Days,
            item.OriginalEndDate,
            item.ProposedEndDate,
            item.Amount,
            item.Currency,
            item.DecisionComment,
            history);
    }
}
