using LifeFitAI.Application.Gym.Trainers;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed partial class TrainerMobileStore : ITrainerMobileStore
{
    private readonly ApplicationDbContext _db;

    public TrainerMobileStore(ApplicationDbContext db) => _db = db;

    public Task<TrainerProfile?> GetTrainerByUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        _db.TrainerProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Status == ProfileStatus.Active, cancellationToken);

    public async Task<(string FirstName, string LastName, string Email)?> GetUserNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId, cancellationToken).ConfigureAwait(false);
        return user is null ? null : (user.FirstName, user.LastName, user.Email);
    }

    public async Task<bool> IsAssignedAsync(Guid tenantId, Guid trainerId, Guid memberId, DateOnly day, CancellationToken cancellationToken)
    {
        var member = await _db.MemberProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == memberId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return false;
        }

        if (member.AssignedTrainerId == trainerId)
        {
            return true;
        }

        return await _db.TrainerMemberAssignments.AsNoTracking().AnyAsync(
            item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.MemberProfileId == memberId && item.Status == TrainerAssignmentStatus.Active && item.StartDate <= day && (item.EndDate == null || item.EndDate >= day),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PtSession>> ListSessionsAsync(Guid tenantId, Guid trainerId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) =>
        await _db.PtSessions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.ScheduledStartUtc >= fromUtc && item.ScheduledStartUtc < toUtc)
            .OrderBy(item => item.ScheduledStartUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<PtSession?> GetSessionAsync(Guid tenantId, Guid trainerId, Guid sessionId, CancellationToken cancellationToken) =>
        _db.PtSessions.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.TrainerProfileId == trainerId && item.Id == sessionId, cancellationToken);

    public async Task<IReadOnlyList<PtSessionExercise>> ListExercisesAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken) =>
        await _db.PtSessionExercises.AsNoTracking().Where(item => item.TenantId == tenantId && item.PtSessionId == sessionId).OrderBy(item => item.SortOrder).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AddSessionAsync(PtSession session, CancellationToken cancellationToken) =>
        await _db.PtSessions.AddAsync(session, cancellationToken).ConfigureAwait(false);

    public async Task AddExerciseAsync(PtSessionExercise exercise, CancellationToken cancellationToken) =>
        await _db.PtSessionExercises.AddAsync(exercise, cancellationToken).ConfigureAwait(false);

    public Task<PtSessionExercise?> GetExerciseAsync(Guid tenantId, Guid sessionId, Guid exerciseId, CancellationToken cancellationToken) =>
        _db.PtSessionExercises.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.PtSessionId == sessionId && item.Id == exerciseId, cancellationToken);

    public async Task<int> NextExerciseOrderAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken)
    {
        var max = await _db.PtSessionExercises.Where(item => item.TenantId == tenantId && item.PtSessionId == sessionId).Select(item => (int?)item.SortOrder).MaxAsync(cancellationToken).ConfigureAwait(false);
        return (max ?? 0) + 1;
    }

    public async Task AddConsumptionAsync(PtSessionConsumption consumption, CancellationToken cancellationToken) =>
        await _db.PtSessionConsumptions.AddAsync(consumption, cancellationToken).ConfigureAwait(false);

    public Task<bool> SessionConsumedAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken) =>
        _db.PtSessionConsumptions.AnyAsync(item => item.TenantId == tenantId && item.PtSessionId == sessionId, cancellationToken);
}
