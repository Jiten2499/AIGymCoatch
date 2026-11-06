using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed partial class TrainerMobileStore
{
    public async Task<IReadOnlyList<MemberProfile>> ListAssignedMembersAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken)
    {
        var linked = _db.TrainerMemberAssignments.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.Status == TrainerAssignmentStatus.Active)
            .Select(item => item.MemberProfileId);
        return await _db.MemberProfiles.AsNoTracking()
            .Where(item => item.TenantId == tenantId && (item.AssignedTrainerId == trainerId || linked.Contains(item.Id)))
            .OrderBy(item => item.FirstName)
            .ThenBy(item => item.LastName)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<MemberProfile?> GetMemberAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken) =>
        _db.MemberProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == memberId, cancellationToken);

    public async Task<IReadOnlyList<MemberPtAssignment>> ListMemberPackagesAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken) =>
        await _db.MemberPtAssignments.AsNoTracking().Where(item => item.TenantId == tenantId && item.MemberProfileId == memberId).OrderByDescending(item => item.StartDate).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<MemberPtAssignment>> OpenPackagesAsync(Guid tenantId, Guid trainerId, Guid memberId, DateOnly day, CancellationToken cancellationToken) =>
        await _db.MemberPtAssignments
            .Where(item => item.TenantId == tenantId && item.MemberProfileId == memberId && (item.TrainerProfileId == null || item.TrainerProfileId == trainerId) && item.Status == MemberPtAssignmentStatus.Active && item.SessionsRemaining > 0 && item.StartDate <= day && item.EndDate >= day)
            .OrderBy(item => item.EndDate)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<GymMembership>> OpenMembershipsAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken) =>
        await _db.GymMemberships.Where(item => item.TenantId == tenantId && item.MemberProfileId == memberId && item.PtSessionsRemaining > 0).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<TrainerMemberAssignment>> RecentAssignmentsAsync(Guid tenantId, Guid trainerId, DateOnly since, CancellationToken cancellationToken) =>
        await _db.TrainerMemberAssignments.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.Status == TrainerAssignmentStatus.Active && item.StartDate >= since)
            .OrderByDescending(item => item.StartDate)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<TrainerTargetAssignment>> ListTargetsAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken) =>
        await _db.TrainerTargetAssignments.AsNoTracking().Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId).OrderByDescending(item => item.EffectiveStart).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<TrainerRewardTransaction>> ListRewardsAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken) =>
        await _db.TrainerRewardTransactions.AsNoTracking().Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId).OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<DateOnly>> CompletedDatesAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken) =>
        await _db.PtSessions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.Status == PtSessionStatus.Completed && item.CompletedOn != null)
            .Select(item => item.CompletedOn!.Value)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<MemberPtAssignment>> SoldPackagesAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken) =>
        await _db.MemberPtAssignments.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.Status != MemberPtAssignmentStatus.Cancelled)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<DateOnly>> ClientStartsAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken) =>
        await _db.TrainerMemberAssignments.AsNoTracking().Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId).Select(item => item.StartDate).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<BodyMeasurement>> ListMeasurementsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        await _db.BodyMeasurements.AsNoTracking().Where(item => item.TenantId == tenantId && item.UserId == userId).OrderByDescending(item => item.MeasurementDate).Take(8).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, string>> MemberNamesAsync(Guid tenantId, IReadOnlyCollection<Guid> memberIds, CancellationToken cancellationToken)
    {
        var rows = await _db.MemberProfiles.AsNoTracking()
            .Where(item => item.TenantId == tenantId && memberIds.Contains(item.Id))
            .Select(item => new { item.Id, item.FirstName, item.LastName })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.ToDictionary(item => item.Id, item => $"{item.FirstName} {item.LastName}".Trim());
    }
}
