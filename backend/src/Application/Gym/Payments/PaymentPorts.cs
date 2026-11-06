using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Payments;

public interface IMembershipPaymentRepository
{
    Task AddAsync(MembershipPayment payment, CancellationToken cancellationToken);

    Task<MembershipPayment?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<MembershipPaymentItem?> ReadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<decimal> SumPostedAsync(Guid tenantId, Guid membershipId, CancellationToken cancellationToken);

    Task<PageResult<MembershipPaymentItem>> ListForMembershipAsync(
        Guid tenantId,
        Guid membershipId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
