using LifeFitAI.Application.Gym.Pt;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class PtPackageRepository : IPtPackageRepository
{
    private readonly ApplicationDbContext _db;

    public PtPackageRepository(ApplicationDbContext db) => _db = db;

    public Task<PtPackage?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.PtPackages.SingleOrDefaultAsync(package => package.TenantId == tenantId && package.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(Guid tenantId, string name, CancellationToken cancellationToken) =>
        _db.PtPackages.AnyAsync(package => package.TenantId == tenantId && package.Name == name, cancellationToken);

    public async Task<IReadOnlyList<PtPackage>> ListAsync(Guid tenantId, bool activeOnly, CancellationToken cancellationToken)
    {
        var query = _db.PtPackages.Where(package => package.TenantId == tenantId);
        if (activeOnly)
        {
            query = query.Where(package => package.IsActive);
        }

        return await query.OrderBy(package => package.Name).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> HasActiveAssignmentAsync(Guid tenantId, Guid memberProfileId, Guid packageId, CancellationToken cancellationToken) =>
        _db.MemberPtAssignments.AnyAsync(item => item.TenantId == tenantId && item.MemberProfileId == memberProfileId && item.PtPackageId == packageId && item.Status == MemberPtAssignmentStatus.Active, cancellationToken);

    public async Task AddAsync(PtPackage package, CancellationToken cancellationToken) =>
        await _db.PtPackages.AddAsync(package, cancellationToken).ConfigureAwait(false);

    public async Task AddAssignmentAsync(MemberPtAssignment assignment, CancellationToken cancellationToken) =>
        await _db.MemberPtAssignments.AddAsync(assignment, cancellationToken).ConfigureAwait(false);

    public Task<MemberPtAssignment?> GetAssignmentAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.MemberPtAssignments.SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);

    public Task<PtSessionConsumption?> GetConsumptionAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.PtSessionConsumptions.SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);

    public Task<bool> SessionAlreadyConsumedAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken) =>
        _db.PtSessionConsumptions.AnyAsync(item => item.TenantId == tenantId && item.PtSessionId == sessionId, cancellationToken);

    public async Task AddSessionAsync(PtSession session, CancellationToken cancellationToken) =>
        await _db.PtSessions.AddAsync(session, cancellationToken).ConfigureAwait(false);

    public async Task AddConsumptionAsync(PtSessionConsumption consumption, CancellationToken cancellationToken) =>
        await _db.PtSessionConsumptions.AddAsync(consumption, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<MemberPtAssignment>> ListMemberAssignmentsAsync(Guid tenantId, Guid memberProfileId, CancellationToken cancellationToken) =>
        await _db.MemberPtAssignments.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.MemberProfileId == memberProfileId)
            .OrderByDescending(item => item.StartDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<PtSession>> ListMemberSessionsAsync(Guid tenantId, Guid memberProfileId, CancellationToken cancellationToken) =>
        await _db.PtSessions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.MemberProfileId == memberProfileId)
            .OrderByDescending(item => item.ScheduledStartUtc)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<PtSessionConsumption>> ListMemberConsumptionsAsync(Guid tenantId, Guid memberProfileId, CancellationToken cancellationToken)
    {
        var sessionIds = _db.PtSessions.Where(item => item.TenantId == tenantId && item.MemberProfileId == memberProfileId).Select(item => item.Id);
        return await _db.PtSessionConsumptions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && sessionIds.Contains(item.PtSessionId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
