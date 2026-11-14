using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Fitness;

public sealed class FitnessAssessment : BaseEntity
{
    private FitnessAssessment()
    {
    }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    public string? Gender { get; private set; }

    public decimal? HeightCm { get; private set; }

    public decimal? CurrentWeightKg { get; private set; }

    public decimal? TargetWeightKg { get; private set; }

    public decimal? WaistCircumferenceCm { get; private set; }

    public decimal? BodyFatPercentage { get; private set; }

    public string? Country { get; private set; }

    public UnitSystem MeasurementUnit { get; private set; }

    public string? PrimaryGoal { get; private set; }

    public string? SecondaryGoal { get; private set; }

    public string? GoalTimeline { get; private set; }

    public int? CustomGoalTimelineDays { get; private set; }

    public FitnessLevel? FitnessLevel { get; private set; }

    public string? Priority { get; private set; }

    public string? GoalDescription { get; private set; }

    public string? HealthJson { get; private set; }

    public string? WorkoutJson { get; private set; }

    public string? DietJson { get; private set; }

    public string? LifestyleJson { get; private set; }

    public string? MeasurementsJson { get; private set; }

    public int CurrentStep { get; private set; }

    public int CompletionPercentage { get; private set; }

    public bool IsCompleted { get; private set; }

    public static FitnessAssessment Create(Guid id, Guid userId, Guid tenantId) =>
        new()
        {
            Id = id,
            UserId = userId,
            TenantId = tenantId,
            MeasurementUnit = UnitSystem.Metric,
            CurrentStep = 1,
            CompletionPercentage = 0,
            IsCompleted = false,
        };

    public void ApplyDraft(
        DateOnly? dateOfBirth,
        string? gender,
        decimal? heightCm,
        decimal? currentWeightKg,
        decimal? targetWeightKg,
        decimal? waistCircumferenceCm,
        decimal? bodyFatPercentage,
        string? country,
        UnitSystem measurementUnit,
        string? primaryGoal,
        string? secondaryGoal,
        string? goalTimeline,
        int? customGoalTimelineDays,
        FitnessLevel? fitnessLevel,
        string? priority,
        string? goalDescription,
        string? healthJson,
        string? workoutJson,
        string? dietJson,
        string? lifestyleJson,
        string? measurementsJson,
        int currentStep,
        int completionPercentage,
        bool submit)
    {
        DateOfBirth = dateOfBirth;
        Gender = Trim(gender, 32);
        HeightCm = heightCm;
        CurrentWeightKg = currentWeightKg;
        TargetWeightKg = targetWeightKg;
        WaistCircumferenceCm = waistCircumferenceCm;
        BodyFatPercentage = bodyFatPercentage;
        Country = Trim(country, 100);
        MeasurementUnit = measurementUnit;
        PrimaryGoal = Trim(primaryGoal, 64);
        SecondaryGoal = Trim(secondaryGoal, 64);
        GoalTimeline = Trim(goalTimeline, 32);
        CustomGoalTimelineDays = customGoalTimelineDays;
        FitnessLevel = fitnessLevel;
        Priority = Trim(priority, 32);
        GoalDescription = Trim(goalDescription, 1000);
        HealthJson = healthJson;
        WorkoutJson = workoutJson;
        DietJson = dietJson;
        LifestyleJson = lifestyleJson;
        MeasurementsJson = measurementsJson;
        CurrentStep = Math.Clamp(currentStep, 1, 8);
        CompletionPercentage = Math.Clamp(completionPercentage, 0, 100);
        if (submit)
        {
            IsCompleted = true;
            CurrentStep = 8;
            CompletionPercentage = 100;
        }
        else
        {
            IsCompleted = false;
        }
    }

    public decimal? BodyMassIndex()
    {
        if (HeightCm is not decimal height || height <= 0 || CurrentWeightKg is not decimal weight || weight <= 0)
        {
            return null;
        }

        var meters = height / 100m;
        return Math.Round(weight / (meters * meters), 1, MidpointRounding.AwayFromZero);
    }

    public int? AgeYears(DateOnly today)
    {
        if (DateOfBirth is not DateOnly birth || birth > today)
        {
            return null;
        }

        var age = today.Year - birth.Year;
        if (birth > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];
}
