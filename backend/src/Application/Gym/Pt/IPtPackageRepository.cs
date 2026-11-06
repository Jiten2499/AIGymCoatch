using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym.Pt;

public interface IPtPackageRepository
{
    Task<PtPackage?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(Guid tenantId, string name, CancellationToken cancellationToken);

    Task<IReadOnlyList<PtPackage>> ListAsync(Guid tenantId, bool activeOnly, CancellationToken cancellationToken);

    Task<bool> HasActiveAssignmentAsync(Guid tenantId, Guid memberProfileId, Guid packageId, CancellationToken cancellationToken);

    Task AddAsync(PtPackage package, CancellationToken cancellationToken);

    Task AddAssignmentAsync(MemberPtAssignment assignment, CancellationToken cancellationToken);

    Task<MemberPtAssignment?> GetAssignmentAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<PtSessionConsumption?> GetConsumptionAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<bool> SessionAlreadyConsumedAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken);

    Task AddSessionAsync(PtSession session, CancellationToken cancellationToken);

    Task AddConsumptionAsync(PtSessionConsumption consumption, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberPtAssignment>> ListMemberAssignmentsAsync(Guid tenantId, Guid memberProfileId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PtSession>> ListMemberSessionsAsync(Guid tenantId, Guid memberProfileId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PtSessionConsumption>> ListMemberConsumptionsAsync(Guid tenantId, Guid memberProfileId, CancellationToken cancellationToken);
}
