using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Fitness;

public sealed class WorkoutProgram : BaseEntity
{
    private WorkoutProgram()
    {
    }

    public Guid? TenantId { get; private set; }

    public Guid? UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public FitnessGoalType GoalType { get; private set; }

    public WorkoutDifficulty Difficulty { get; private set; }

    public int DurationWeeks { get; private set; }

    public int FrequencyPerWeek { get; private set; }

    public ProgramStatus Status { get; private set; }

    public static WorkoutProgram Create(Guid id, Guid? tenantId, Guid? userId, string name, string? description, FitnessGoalType goal, WorkoutDifficulty difficulty, int durationWeeks, int frequency) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            UserId = userId,
            Name = name.Trim(),
            Description = description,
            GoalType = goal,
            Difficulty = difficulty,
            DurationWeeks = durationWeeks,
            FrequencyPerWeek = frequency,
            Status = ProgramStatus.Active,
        };

    public bool VisibleTo(Guid tenantId, Guid userId) =>
        TenantId is null || TenantId == tenantId && (UserId is null || UserId == userId);
}

public sealed class WorkoutProgramDay : BaseEntity
{
    private WorkoutProgramDay()
    {
    }

    public Guid WorkoutProgramId { get; private set; }

    public int DayNumber { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool RestDay { get; private set; }

    public static WorkoutProgramDay Create(Guid id, Guid programId, int dayNumber, string name, string? description, bool restDay) =>
        new()
        {
            Id = id,
            WorkoutProgramId = programId,
            DayNumber = dayNumber,
            Name = name.Trim(),
            Description = description,
            RestDay = restDay,
        };
}

public sealed class WorkoutProgramExercise : BaseEntity
{
    private WorkoutProgramExercise()
    {
    }

    public Guid WorkoutProgramDayId { get; private set; }

    public Guid ExerciseId { get; private set; }

    public int Sequence { get; private set; }

    public int? Sets { get; private set; }

    public int? TargetReps { get; private set; }

    public int? TargetDurationSeconds { get; private set; }

    public decimal? TargetDistance { get; private set; }

    public decimal? TargetWeight { get; private set; }

    public int? RestSeconds { get; private set; }

    public string? Notes { get; private set; }

    public static WorkoutProgramExercise Create(Guid id, Guid dayId, Guid exerciseId, int sequence, int? sets, int? reps, int? duration, decimal? distance, decimal? weight, int? rest, string? notes) =>
        new()
        {
            Id = id,
            WorkoutProgramDayId = dayId,
            ExerciseId = exerciseId,
            Sequence = sequence,
            Sets = sets,
            TargetReps = reps,
            TargetDurationSeconds = duration,
            TargetDistance = distance,
            TargetWeight = weight,
            RestSeconds = rest,
            Notes = notes,
        };
}

public sealed class WorkoutSession : BaseEntity
{
    private WorkoutSession()
    {
    }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid? WorkoutProgramId { get; private set; }

    public Guid? WorkoutProgramDayId { get; private set; }

    public DateTime? StartedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public SessionStatus Status { get; private set; }

    public int? DurationSeconds { get; private set; }

    public string? Notes { get; private set; }

    public static WorkoutSession Plan(Guid id, Guid userId, Guid tenantId, Guid? programId, Guid? dayId, string? notes) =>
        new()
        {
            Id = id,
            UserId = userId,
            TenantId = tenantId,
            WorkoutProgramId = programId,
            WorkoutProgramDayId = dayId,
            Status = SessionStatus.Planned,
            Notes = notes,
        };

    public bool Start(DateTime utcNow)
    {
        if (Status != SessionStatus.Planned)
        {
            return false;
        }

        Status = SessionStatus.InProgress;
        StartedAt = utcNow;
        return true;
    }

    public bool Complete(DateTime utcNow)
    {
        if (Status != SessionStatus.InProgress || StartedAt is null)
        {
            return false;
        }

        Status = SessionStatus.Completed;
        CompletedAt = utcNow;
        DurationSeconds = (int)Math.Max(0, (utcNow - StartedAt.Value).TotalSeconds);
        return true;
    }

    public bool Cancel()
    {
        if (Status is SessionStatus.Completed or SessionStatus.Cancelled)
        {
            return false;
        }

        Status = SessionStatus.Cancelled;
        return true;
    }
}

public sealed class WorkoutSessionExercise
{
    private WorkoutSessionExercise()
    {
    }

    public Guid Id { get; private set; }

    public Guid WorkoutSessionId { get; private set; }

    public Guid ExerciseId { get; private set; }

    public int Sequence { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public static WorkoutSessionExercise Create(Guid id, Guid sessionId, Guid exerciseId, int sequence, string? notes, DateTime utcNow) =>
        new()
        {
            Id = id,
            WorkoutSessionId = sessionId,
            ExerciseId = exerciseId,
            Sequence = sequence,
            Notes = notes,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
}

public sealed class WorkoutSet
{
    private WorkoutSet()
    {
    }

    public Guid Id { get; private set; }

    public Guid WorkoutSessionExerciseId { get; private set; }

    public int SetNumber { get; private set; }

    public int? Reps { get; private set; }

    public decimal? Weight { get; private set; }

    public int? DurationSeconds { get; private set; }

    public decimal? Distance { get; private set; }

    public decimal? Rpe { get; private set; }

    public bool Completed { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public static WorkoutSet Record(Guid id, Guid sessionExerciseId, int setNumber, int? reps, decimal? weight, int? duration, decimal? distance, decimal? rpe, bool completed, DateTime utcNow) =>
        new()
        {
            Id = id,
            WorkoutSessionExerciseId = sessionExerciseId,
            SetNumber = setNumber,
            Reps = reps,
            Weight = weight,
            DurationSeconds = duration,
            Distance = distance,
            Rpe = rpe,
            Completed = completed,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
}
