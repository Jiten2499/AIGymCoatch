using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Fitness;

public sealed class FitnessProfile : BaseEntity
{
    private FitnessProfile()
    {
    }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public UnitSystem UnitSystem { get; private set; }

    public decimal? Height { get; private set; }

    public decimal? Weight { get; private set; }

    public FitnessLevel FitnessLevel { get; private set; }

    public FitnessGoalType PrimaryGoal { get; private set; }

    public ActivityLevel ActivityLevel { get; private set; }

    public string? TrainingExperience { get; private set; }

    public int PreferredWorkoutDays { get; private set; }

    public int PreferredWorkoutDuration { get; private set; }

    public string? PreferredTrainingLocation { get; private set; }

    public static FitnessProfile Create(Guid id, Guid userId, Guid tenantId) =>
        new()
        {
            Id = id,
            UserId = userId,
            TenantId = tenantId,
            UnitSystem = UnitSystem.Metric,
            FitnessLevel = FitnessLevel.Beginner,
            PrimaryGoal = FitnessGoalType.GeneralFitness,
            ActivityLevel = ActivityLevel.Moderate,
            PreferredWorkoutDays = 3,
            PreferredWorkoutDuration = 45,
        };

    public void Update(UnitSystem unitSystem, decimal? height, decimal? weight, FitnessLevel level, FitnessGoalType goal, ActivityLevel activity, string? experience, int days, int duration, string? location)
    {
        UnitSystem = unitSystem;
        Height = height;
        Weight = weight;
        FitnessLevel = level;
        PrimaryGoal = goal;
        ActivityLevel = activity;
        TrainingExperience = experience;
        PreferredWorkoutDays = days;
        PreferredWorkoutDuration = duration;
        PreferredTrainingLocation = location;
    }
}

public sealed class FitnessGoal : BaseEntity
{
    private FitnessGoal()
    {
    }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public FitnessGoalType GoalType { get; private set; }

    public decimal? TargetValue { get; private set; }

    public decimal? CurrentValue { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? TargetDate { get; private set; }

    public GoalStatus Status { get; private set; }

    public static FitnessGoal Create(Guid id, Guid userId, Guid tenantId, FitnessGoalType type, decimal? target, decimal? current, DateOnly start, DateOnly? targetDate) =>
        new()
        {
            Id = id,
            UserId = userId,
            TenantId = tenantId,
            GoalType = type,
            TargetValue = target,
            CurrentValue = current,
            StartDate = start,
            TargetDate = targetDate,
            Status = GoalStatus.Active,
        };

    public bool Update(decimal? target, decimal? current, DateOnly? targetDate)
    {
        if (Status != GoalStatus.Active)
        {
            return false;
        }

        TargetValue = target;
        CurrentValue = current;
        TargetDate = targetDate;
        return true;
    }

    public bool Complete() => Transition(GoalStatus.Completed);

    public bool Cancel() => Transition(GoalStatus.Cancelled);

    private bool Transition(GoalStatus status)
    {
        if (Status != GoalStatus.Active)
        {
            return false;
        }

        Status = status;
        return true;
    }
}

public sealed class BodyMeasurement
{
    private BodyMeasurement()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public UnitSystem UnitSystem { get; private set; }

    public DateOnly MeasurementDate { get; private set; }

    public decimal? Weight { get; private set; }

    public decimal? BodyFatPercentage { get; private set; }

    public decimal? Chest { get; private set; }

    public decimal? Waist { get; private set; }

    public decimal? Hip { get; private set; }

    public decimal? Arm { get; private set; }

    public decimal? Thigh { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public static BodyMeasurement Record(Guid id, Guid userId, Guid tenantId, UnitSystem unitSystem, DateOnly date, decimal? weight, decimal? bodyFat, decimal? chest, decimal? waist, decimal? hip, decimal? arm, decimal? thigh, string? notes, DateTime utcNow) =>
        new()
        {
            Id = id,
            UserId = userId,
            TenantId = tenantId,
            UnitSystem = unitSystem,
            MeasurementDate = date,
            Weight = weight,
            BodyFatPercentage = bodyFat,
            Chest = chest,
            Waist = waist,
            Hip = hip,
            Arm = arm,
            Thigh = thigh,
            Notes = notes,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

    public void Apply(UnitSystem unitSystem, DateOnly date, decimal? weight, decimal? bodyFat, decimal? chest, decimal? waist, decimal? hip, decimal? arm, decimal? thigh, string? notes, DateTime utcNow)
    {
        UnitSystem = unitSystem;
        MeasurementDate = date;
        Weight = weight;
        BodyFatPercentage = bodyFat;
        Chest = chest;
        Waist = waist;
        Hip = hip;
        Arm = arm;
        Thigh = thigh;
        Notes = notes;
        UpdatedAt = utcNow;
    }
}

public sealed class BodyScanRecord
{
    private BodyScanRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public DateOnly ScanDate { get; private set; }

    public BodyScanSource Source { get; private set; }

    public string StorageReference { get; private set; } = string.Empty;

    public BodyScanStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static bool IsSafeReference(string reference) =>
        !string.IsNullOrWhiteSpace(reference)
        && reference.Length <= 200
        && !reference.Contains("://", StringComparison.Ordinal)
        && !reference.Contains("..", StringComparison.Ordinal)
        && !reference.StartsWith("http", StringComparison.OrdinalIgnoreCase);

    public static BodyScanRecord Create(Guid id, Guid userId, Guid tenantId, DateOnly scanDate, BodyScanSource source, string storageReference, DateTime utcNow) =>
        new()
        {
            Id = id,
            UserId = userId,
            TenantId = tenantId,
            ScanDate = scanDate,
            Source = source,
            StorageReference = storageReference.Trim(),
            Status = BodyScanStatus.Pending,
            CreatedAt = utcNow,
        };
}
