using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Gym;
using LifeFitAI.Domain.Fitness;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class FitnessRepository : IFitnessRepository
{
    private readonly ApplicationDbContext _db;

    public FitnessRepository(ApplicationDbContext db) => _db = db;

    public Task<FitnessProfile?> GetProfileAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        _db.FitnessProfiles.FirstOrDefaultAsync(profile => profile.TenantId == tenantId && profile.UserId == userId, cancellationToken);

    public Task AddProfileAsync(FitnessProfile profile, CancellationToken cancellationToken) =>
        _db.FitnessProfiles.AddAsync(profile, cancellationToken).AsTask();

    public async Task<IReadOnlyList<FitnessGoal>> ListGoalsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        await _db.FitnessGoals.AsNoTracking().Where(goal => goal.TenantId == tenantId && goal.UserId == userId).OrderByDescending(goal => goal.StartDate).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<FitnessGoal?> GetGoalAsync(Guid tenantId, Guid userId, Guid id, CancellationToken cancellationToken) =>
        _db.FitnessGoals.FirstOrDefaultAsync(goal => goal.TenantId == tenantId && goal.UserId == userId && goal.Id == id, cancellationToken);

    public Task AddGoalAsync(FitnessGoal goal, CancellationToken cancellationToken) =>
        _db.FitnessGoals.AddAsync(goal, cancellationToken).AsTask();

    public async Task<PageResult<Exercise>> ListExercisesAsync(Guid tenantId, string? search, PageQuery page, CancellationToken cancellationToken)
    {
        var query = _db.Exercises.AsNoTracking().Where(exercise => exercise.IsActive && (exercise.TenantId == null || exercise.TenantId == tenantId));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(exercise => exercise.Name.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(exercise => exercise.Name).Skip(page.Skip).Take(page.NormalizedSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<Exercise>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public Task<Exercise?> GetExerciseAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.Exercises.FirstOrDefaultAsync(exercise => exercise.Id == id && (exercise.TenantId == null || exercise.TenantId == tenantId), cancellationToken);

    public Task AddExerciseAsync(Exercise exercise, CancellationToken cancellationToken) =>
        _db.Exercises.AddAsync(exercise, cancellationToken).AsTask();

    public async Task<IReadOnlyList<ExerciseOption>> ListExerciseOptionsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _db.Exercises.AsNoTracking()
            .Where(exercise => exercise.IsActive && (exercise.TenantId == null || exercise.TenantId == tenantId))
            .OrderBy(exercise => exercise.Name)
            .Take(40)
            .Select(exercise => new { exercise.Id, exercise.Name, exercise.ExerciseType })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(exercise => new ExerciseOption(exercise.Id, exercise.Name, exercise.ExerciseType.ToString())).ToArray();
    }

    public async Task<PageResult<BodyMeasurement>> ListMeasurementsAsync(Guid tenantId, Guid userId, PageQuery page, CancellationToken cancellationToken)
    {
        var query = _db.BodyMeasurements.AsNoTracking().Where(row => row.TenantId == tenantId && row.UserId == userId);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderByDescending(row => row.MeasurementDate).Skip(page.Skip).Take(page.NormalizedSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<BodyMeasurement>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public Task AddMeasurementAsync(BodyMeasurement measurement, CancellationToken cancellationToken) =>
        _db.BodyMeasurements.AddAsync(measurement, cancellationToken).AsTask();

    public Task AddScanAsync(BodyScanRecord scan, CancellationToken cancellationToken) =>
        _db.BodyScanRecords.AddAsync(scan, cancellationToken).AsTask();
}

public sealed class NutritionRepository : INutritionRepository
{
    private readonly ApplicationDbContext _db;

    public NutritionRepository(ApplicationDbContext db) => _db = db;

    public Task<NutritionProfile?> GetProfileAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        _db.NutritionProfiles.FirstOrDefaultAsync(profile => profile.TenantId == tenantId && profile.UserId == userId, cancellationToken);

    public Task AddProfileAsync(NutritionProfile profile, CancellationToken cancellationToken) =>
        _db.NutritionProfiles.AddAsync(profile, cancellationToken).AsTask();

    public async Task<PageResult<NutritionLog>> ListLogsAsync(Guid tenantId, Guid userId, DateOnly? date, PageQuery page, CancellationToken cancellationToken)
    {
        var query = _db.NutritionLogs.AsNoTracking().Where(log => log.TenantId == tenantId && log.UserId == userId);
        if (date is DateOnly day)
        {
            query = query.Where(log => log.LogDate == day);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderByDescending(log => log.LogDate).Skip(page.Skip).Take(page.NormalizedSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<NutritionLog>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public Task AddLogAsync(NutritionLog log, CancellationToken cancellationToken) =>
        _db.NutritionLogs.AddAsync(log, cancellationToken).AsTask();

    public async Task<NutritionSummaryResponse> SummarizeDayAsync(Guid tenantId, Guid userId, DateOnly date, CancellationToken cancellationToken)
    {
        var totals = await _db.NutritionLogs.AsNoTracking()
            .Where(log => log.TenantId == tenantId && log.UserId == userId && log.LogDate == date)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Calories = group.Sum(log => log.Calories),
                Protein = group.Sum(log => log.Protein),
                Carbohydrates = group.Sum(log => log.Carbohydrates),
                Fat = group.Sum(log => log.Fat),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var profile = await _db.NutritionProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId, cancellationToken).ConfigureAwait(false);
        return new NutritionSummaryResponse(date, totals?.Calories ?? 0, totals?.Protein ?? 0, totals?.Carbohydrates ?? 0, totals?.Fat ?? 0, profile?.DailyCalorieTarget ?? 0, profile?.ProteinTarget ?? 0, profile?.CarbTarget ?? 0, profile?.FatTarget ?? 0);
    }
}
