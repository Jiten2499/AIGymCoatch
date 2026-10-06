using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Gym;
using LifeFitAI.Domain.Fitness;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class WorkoutRepository : IWorkoutRepository
{
    private readonly ApplicationDbContext _db;

    public WorkoutRepository(ApplicationDbContext db) => _db = db;

    public async Task<PageResult<ProgramResponse>> ListProgramsAsync(Guid tenantId, Guid userId, PageQuery page, CancellationToken cancellationToken)
    {
        var query = VisiblePrograms(tenantId, userId);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(program => program.Name).Skip(page.Skip).Take(page.NormalizedSize)
            .Select(program => new ProgramResponse(program.Id, program.Name, program.Description, program.GoalType, program.Difficulty, program.DurationWeeks, program.FrequencyPerWeek, program.Status, program.UserId == userId, Array.Empty<ProgramDayResponse>()))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<ProgramResponse>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public async Task<ProgramResponse?> GetProgramAsync(Guid tenantId, Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var program = await VisiblePrograms(tenantId, userId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken).ConfigureAwait(false);
        if (program is null)
        {
            return null;
        }

        var days = await _db.WorkoutProgramDays.AsNoTracking().Where(day => day.WorkoutProgramId == program.Id).OrderBy(day => day.DayNumber).ToListAsync(cancellationToken).ConfigureAwait(false);
        var dayIds = days.Select(day => day.Id).ToArray();
        var exercises = await _db.WorkoutProgramExercises.AsNoTracking().Where(item => dayIds.Contains(item.WorkoutProgramDayId)).OrderBy(item => item.Sequence).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new ProgramResponse(
            program.Id,
            program.Name,
            program.Description,
            program.GoalType,
            program.Difficulty,
            program.DurationWeeks,
            program.FrequencyPerWeek,
            program.Status,
            program.UserId == userId,
            days.Select(day => new ProgramDayResponse(
                day.Id,
                day.DayNumber,
                day.Name,
                day.Description,
                day.RestDay,
                exercises.Where(item => item.WorkoutProgramDayId == day.Id).Select(item => new ProgramExerciseResponse(item.Id, item.ExerciseId, item.Sequence, item.Sets, item.TargetReps, item.TargetDurationSeconds, item.TargetDistance, item.TargetWeight, item.RestSeconds, item.Notes)).ToArray())).ToArray());
    }

    public Task<bool> ProgramVisibleAsync(Guid tenantId, Guid userId, Guid id, CancellationToken cancellationToken) =>
        VisiblePrograms(tenantId, userId).AnyAsync(program => program.Id == id, cancellationToken);

    public Task AddProgramAsync(WorkoutProgram program, CancellationToken cancellationToken) =>
        _db.WorkoutPrograms.AddAsync(program, cancellationToken).AsTask();

    public Task AddDayAsync(WorkoutProgramDay day, CancellationToken cancellationToken) =>
        _db.WorkoutProgramDays.AddAsync(day, cancellationToken).AsTask();

    public Task AddProgramExerciseAsync(WorkoutProgramExercise exercise, CancellationToken cancellationToken) =>
        _db.WorkoutProgramExercises.AddAsync(exercise, cancellationToken).AsTask();

    public async Task<PageResult<SessionResponse>> ListSessionsAsync(Guid tenantId, Guid userId, SessionStatus? status, PageQuery page, CancellationToken cancellationToken)
    {
        var query = OwnedSessions(tenantId, userId);
        if (status is SessionStatus value)
        {
            query = query.Where(session => session.Status == value);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderByDescending(session => session.StartedAt).Skip(page.Skip).Take(page.NormalizedSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<SessionResponse>(items.Select(session => Map(session, [])).ToArray(), page.NormalizedPage, page.NormalizedSize, total);
    }

    public async Task<SessionResponse?> GetSessionAsync(Guid tenantId, Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var session = await OwnedSessions(tenantId, userId).AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return null;
        }

        var exercises = await _db.WorkoutSessionExercises.AsNoTracking().Where(item => item.WorkoutSessionId == session.Id).OrderBy(item => item.Sequence).ToListAsync(cancellationToken).ConfigureAwait(false);
        var ids = exercises.Select(item => item.Id).ToArray();
        var sets = await _db.WorkoutSets.AsNoTracking().Where(item => ids.Contains(item.WorkoutSessionExerciseId)).OrderBy(item => item.SetNumber).ToListAsync(cancellationToken).ConfigureAwait(false);
        var mapped = exercises.Select(item => new SessionExerciseResponse(item.Id, item.ExerciseId, item.Sequence, item.Notes, sets.Where(set => set.WorkoutSessionExerciseId == item.Id).Select(FitnessMaps.ToResponse).ToArray())).ToArray();
        return Map(session, mapped);
    }

    public Task<WorkoutSession?> GetSessionEntityAsync(Guid tenantId, Guid userId, Guid id, CancellationToken cancellationToken) =>
        OwnedSessions(tenantId, userId).FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

    public Task AddSessionAsync(WorkoutSession session, CancellationToken cancellationToken) =>
        _db.WorkoutSessions.AddAsync(session, cancellationToken).AsTask();

    public async Task<int> NextSessionSequenceAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var max = await _db.WorkoutSessionExercises.Where(item => item.WorkoutSessionId == sessionId).Select(item => (int?)item.Sequence).MaxAsync(cancellationToken).ConfigureAwait(false);
        return (max ?? 0) + 1;
    }

    public Task AddSessionExerciseAsync(WorkoutSessionExercise exercise, CancellationToken cancellationToken) =>
        _db.WorkoutSessionExercises.AddAsync(exercise, cancellationToken).AsTask();

    public Task<WorkoutSessionExercise?> GetOwnedSessionExerciseAsync(Guid tenantId, Guid userId, Guid sessionExerciseId, CancellationToken cancellationToken) =>
        (from item in _db.WorkoutSessionExercises
         join session in _db.WorkoutSessions on item.WorkoutSessionId equals session.Id
         where item.Id == sessionExerciseId && session.TenantId == tenantId && session.UserId == userId
         select item).FirstOrDefaultAsync(cancellationToken);

    public async Task<int> NextSetNumberAsync(Guid sessionExerciseId, CancellationToken cancellationToken)
    {
        var max = await _db.WorkoutSets.Where(item => item.WorkoutSessionExerciseId == sessionExerciseId).Select(item => (int?)item.SetNumber).MaxAsync(cancellationToken).ConfigureAwait(false);
        return (max ?? 0) + 1;
    }

    public Task AddSetAsync(WorkoutSet set, CancellationToken cancellationToken) =>
        _db.WorkoutSets.AddAsync(set, cancellationToken).AsTask();

    public async Task<IReadOnlyList<PersonalRecordResponse>> ListRecordsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var rows = await (
            from set in _db.WorkoutSets.AsNoTracking()
            join item in _db.WorkoutSessionExercises.AsNoTracking() on set.WorkoutSessionExerciseId equals item.Id
            join session in _db.WorkoutSessions.AsNoTracking() on item.WorkoutSessionId equals session.Id
            join exercise in _db.Exercises.AsNoTracking() on item.ExerciseId equals exercise.Id
            where session.TenantId == tenantId && session.UserId == userId && session.Status == SessionStatus.Completed && set.Completed
            group set by new { exercise.Id, exercise.Name }
            into grouped
            select new
            {
                grouped.Key.Id,
                grouped.Key.Name,
                MaxWeight = grouped.Max(set => set.Weight),
                MaxReps = grouped.Max(set => set.Reps),
                MaxDuration = grouped.Max(set => set.DurationSeconds),
                MaxDistance = grouped.Max(set => set.Distance),
                Estimated = grouped.Max(set => set.Weight > 0 && set.Reps > 0 ? set.Reps == 1 ? set.Weight : set.Weight * (1m + (set.Reps / 30m)) : null),
            }).ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows.Select(row => new PersonalRecordResponse(row.Id, row.Name, row.MaxWeight, row.MaxReps, row.MaxDuration, row.MaxDistance, row.Estimated is null ? null : Math.Round(row.Estimated.Value, 2, MidpointRounding.AwayFromZero))).ToArray();
    }

    public async Task<IReadOnlyList<string>> RecentWorkoutLabelsAsync(Guid tenantId, Guid userId, int take, CancellationToken cancellationToken)
    {
        var dates = await OwnedSessions(tenantId, userId).AsNoTracking()
            .Where(session => session.Status == SessionStatus.Completed && session.CompletedAt != null)
            .OrderByDescending(session => session.CompletedAt)
            .Take(take)
            .Select(session => session.CompletedAt!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return dates.Select(date => date.ToString("yyyy-MM-dd")).ToArray();
    }

    public Task<int> CompletedCountAsync(Guid tenantId, Guid userId, DateTime fromUtc, CancellationToken cancellationToken) =>
        OwnedSessions(tenantId, userId).CountAsync(session => session.Status == SessionStatus.Completed && session.CompletedAt >= fromUtc, cancellationToken);

    public async Task<IReadOnlyList<DateOnly>> RecentCompletedDatesAsync(Guid tenantId, Guid userId, DateOnly from, CancellationToken cancellationToken)
    {
        var start = from.ToDateTime(TimeOnly.MinValue);
        var dates = await OwnedSessions(tenantId, userId).AsNoTracking()
            .Where(session => session.Status == SessionStatus.Completed && session.CompletedAt >= start)
            .Select(session => session.CompletedAt!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return dates.Select(DateOnly.FromDateTime).Distinct().ToArray();
    }

    private IQueryable<WorkoutProgram> VisiblePrograms(Guid tenantId, Guid userId) =>
        _db.WorkoutPrograms.Where(program => program.TenantId == null || (program.TenantId == tenantId && (program.UserId == null || program.UserId == userId)));

    private IQueryable<WorkoutSession> OwnedSessions(Guid tenantId, Guid userId) =>
        _db.WorkoutSessions.Where(session => session.TenantId == tenantId && session.UserId == userId);

    private static SessionResponse Map(WorkoutSession session, IReadOnlyList<SessionExerciseResponse> exercises) =>
        new(session.Id, session.WorkoutProgramId, session.WorkoutProgramDayId, session.StartedAt, session.CompletedAt, session.Status, session.DurationSeconds, session.Notes, exercises);
}
