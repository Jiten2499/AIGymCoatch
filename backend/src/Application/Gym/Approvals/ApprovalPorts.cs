using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Approvals;

public interface IApprovalRequestRepository
{
    Task AddAsync(ApprovalRequest request, CancellationToken cancellationToken);

    Task<bool> HasPendingAsync(Guid tenantId, ApprovalRequestType requestType, Guid sourceId, CancellationToken cancellationToken);

    Task<ApprovalRequest?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<PageResult<ApprovalRequestListItem>> SearchAsync(Guid tenantId, IReadOnlyList<string> permissions, ApprovalListQuery query, CancellationToken cancellationToken);

    Task<int> CountPendingAsync(Guid tenantId, IReadOnlyList<string> permissions, CancellationToken cancellationToken);
}

public interface IMembershipApprovalQueue
{
    Task<Result<Guid>> QueueFreezeAsync(Guid tenantId, Guid actorId, GymMembership membership, int days, string? reason, CancellationToken cancellationToken);

    Task<Result<Guid>> QueueExtensionAsync(Guid tenantId, Guid actorId, GymMembership membership, int days, string? reason, CancellationToken cancellationToken);
}
