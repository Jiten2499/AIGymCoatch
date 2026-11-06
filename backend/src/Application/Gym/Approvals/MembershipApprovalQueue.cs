using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Approvals;

public sealed class MembershipApprovalQueue : IMembershipApprovalQueue
{
    private readonly IApprovalRequestRepository _requests;
    private readonly IPeopleRepository _people;
    private readonly IUserAccountRepository _users;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public MembershipApprovalQueue(
        IApprovalRequestRepository requests,
        IPeopleRepository people,
        IUserAccountRepository users,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _requests = requests;
        _people = people;
        _users = users;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public Task<Result<Guid>> QueueFreezeAsync(Guid tenantId, Guid actorId, GymMembership membership, int days, string? reason, CancellationToken cancellationToken) =>
        QueueAsync(tenantId, actorId, membership, ApprovalRequestType.MembershipFreeze, PermissionCodes.MembershipFreeze, "Membership freeze", days, reason, cancellationToken);

    public Task<Result<Guid>> QueueExtensionAsync(Guid tenantId, Guid actorId, GymMembership membership, int days, string? reason, CancellationToken cancellationToken) =>
        QueueAsync(tenantId, actorId, membership, ApprovalRequestType.MembershipExtension, PermissionCodes.MembershipRenew, "Membership extension", days, reason, cancellationToken);

    private async Task<Result<Guid>> QueueAsync(
        Guid tenantId,
        Guid actorId,
        GymMembership membership,
        ApprovalRequestType requestType,
        string permission,
        string title,
        int days,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (await _requests.HasPendingAsync(tenantId, requestType, membership.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<Guid>(GymErrors.Conflict);
        }

        var requestedBy = await DisplayNameAsync(actorId, cancellationToken).ConfigureAwait(false);
        var member = await _people.GetMemberAsync(tenantId, membership.MemberProfileId, cancellationToken).ConfigureAwait(false);
        var related = member is null ? membership.MembershipNumber : $"{member.FirstName} {member.LastName}".Trim();
        var plannedDays = days > 0 ? days : (int?)null;
        var proposed = plannedDays is int count ? membership.EndDate.AddDays(count) : (DateOnly?)null;
        var summary = plannedDays is int length
            ? $"{length} days · {membership.MembershipNumber} · {membership.EndDate:yyyy-MM-dd} to {proposed:yyyy-MM-dd}"
            : membership.MembershipNumber;
        var request = ApprovalRequest.Create(
            Guid.NewGuid(),
            tenantId,
            requestType,
            membership.Id,
            permission,
            title,
            summary,
            reason,
            actorId,
            requestedBy,
            membership.MemberProfileId,
            related,
            plannedDays,
            membership.EndDate,
            proposed,
            null,
            membership.Currency);
        await _requests.AddAsync(request, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "ApprovalRequested", "ApprovalRequest", request.Id, null, request.Status.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(request.Id);
    }

    private async Task<string> DisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return user is null ? "Staff" : $"{user.FirstName} {user.LastName}".Trim();
    }
}
