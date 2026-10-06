using LifeFitAI.Application.Fitness;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class MemberDashboardQuery : IMemberDashboardQuery
{
    private readonly ApplicationDbContext _db;

    public MemberDashboardQuery(ApplicationDbContext db) => _db = db;

    public async Task<DashboardResponse> GetAsync(Guid tenantId, Guid userId, DateOnly today, CancellationToken cancellationToken)
    {
        var goal = await _db.FitnessGoals.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId && item.Status == GoalStatus.Active)
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var measurement = await _db.BodyMeasurements.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderByDescending(item => item.MeasurementDate)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)).ToDateTime(TimeOnly.MinValue);
        var monthStart = new DateOnly(today.Year, today.Month, 1).ToDateTime(TimeOnly.MinValue);
        var sessions = _db.WorkoutSessions.AsNoTracking().Where(item => item.TenantId == tenantId && item.UserId == userId && item.Status == SessionStatus.Completed);
        var weekly = await sessions.CountAsync(item => item.CompletedAt >= weekStart, cancellationToken).ConfigureAwait(false);
        var monthly = await sessions.CountAsync(item => item.CompletedAt >= monthStart, cancellationToken).ConfigureAwait(false);
        var recentInstants = await sessions.Where(item => item.CompletedAt >= today.AddDays(-90).ToDateTime(TimeOnly.MinValue)).Select(item => item.CompletedAt!.Value).ToListAsync(cancellationToken).ConfigureAwait(false);
        var recent = await _db.WorkoutSessions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderByDescending(item => item.StartedAt)
            .Take(5)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var nutrition = await new NutritionRepository(_db).SummarizeDayAsync(tenantId, userId, today, cancellationToken).ConfigureAwait(false);
        var challenge = await new ChallengeRepository(_db).ActiveChallengeAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        var membership = await MembershipStatusAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        return new DashboardResponse(
            goal is null ? null : FitnessMaps.ToResponse(goal),
            measurement is null ? null : FitnessMaps.ToResponse(measurement),
            weekly,
            monthly,
            Streak(recentInstants, today),
            recent.Select(item => new SessionResponse(item.Id, item.WorkoutProgramId, item.WorkoutProgramDayId, item.StartedAt, item.CompletedAt, item.Status, item.DurationSeconds, item.Notes, [])).ToArray(),
            nutrition,
            challenge,
            membership);
    }

    private async Task<string?> MembershipStatusAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var memberId = await _db.MemberProfiles.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (memberId is not Guid id)
        {
            return null;
        }

        var status = await _db.GymMemberships.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.MemberProfileId == id)
            .OrderByDescending(item => item.StartDate)
            .Select(item => (GymMembershipStatus?)item.Status)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return status?.ToString();
    }

    private static int Streak(IReadOnlyList<DateTime> completed, DateOnly today)
    {
        var dates = completed.Select(DateOnly.FromDateTime).ToHashSet();
        var cursor = dates.Contains(today) ? today : today.AddDays(-1);
        var count = 0;
        while (dates.Contains(cursor))
        {
            count++;
            cursor = cursor.AddDays(-1);
        }

        return count;
    }
}
