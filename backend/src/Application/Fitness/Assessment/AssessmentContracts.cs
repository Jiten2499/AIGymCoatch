using LifeFitAI.Domain.Fitness;

namespace LifeFitAI.Application.Fitness.Assessment;

public sealed record AssessmentHealthSection(
    bool? HasMedicalConditions,
    IReadOnlyList<string>? MedicalConditions,
    bool? HasInjuries,
    string? InjuryDetails,
    string? PhysicalLimitations,
    IReadOnlyList<string>? FoodAllergies,
    string? FoodIntolerances,
    string? Medications,
    string? DoctorRestrictions,
    string? AdditionalNotes);

public sealed record AssessmentWorkoutSection(
    string? Location,
    string? Experience,
    int? DaysPerWeek,
    IReadOnlyList<string>? PreferredDays,
    int? DurationMinutes,
    string? PreferredTime,
    IReadOnlyList<string>? Equipment,
    IReadOnlyList<string>? TrainingStyles,
    IReadOnlyList<string>? PreferredMuscleGroups,
    string? ExercisesToAvoid,
    string? CardioPreference,
    string? RestDayPreference);

public sealed record AssessmentDietSection(
    string? DietType,
    bool? EggsAllowed,
    string? DairyConsumption,
    IReadOnlyList<string>? PreferredCuisines,
    int? MealsPerDay,
    string? PreferredMealTimings,
    string? FoodsLiked,
    string? FoodsDisliked,
    string? FoodsToAvoid,
    string? CookingAvailability,
    string? DailyFoodBudget,
    decimal? CustomBudgetAmount,
    string? Supplements,
    string? PreferredProteinSources);

public sealed record AssessmentLifestyleSection(
    string? Occupation,
    string? WorkType,
    ActivityLevel? ActivityLevel,
    int? AverageDailySteps,
    decimal? AverageSleepHours,
    string? SleepQuality,
    string? StressLevel,
    decimal? DailyWaterLitres,
    string? PreferredWorkoutSchedule);

public sealed record AssessmentMeasurementsSection(
    decimal? ChestCm,
    decimal? WaistCm,
    decimal? HipCm,
    decimal? LeftArmCm,
    decimal? RightArmCm,
    decimal? ThighCm,
    decimal? BodyFatPercentage,
    string? FitnessNotes);

public sealed record FitnessAssessmentResponse(
    Guid Id,
    DateOnly? DateOfBirth,
    int? AgeYears,
    string? Gender,
    decimal? HeightCm,
    decimal? CurrentWeightKg,
    decimal? TargetWeightKg,
    decimal? WaistCircumferenceCm,
    decimal? BodyFatPercentage,
    string? Country,
    UnitSystem MeasurementUnit,
    decimal? BodyMassIndex,
    string? PrimaryGoal,
    string? SecondaryGoal,
    string? GoalTimeline,
    int? CustomGoalTimelineDays,
    FitnessLevel? FitnessLevel,
    string? Priority,
    string? GoalDescription,
    AssessmentHealthSection? Health,
    AssessmentWorkoutSection? Workout,
    AssessmentDietSection? Diet,
    AssessmentLifestyleSection? Lifestyle,
    AssessmentMeasurementsSection? Measurements,
    int CurrentStep,
    int CompletionPercentage,
    bool IsCompleted,
    DateTime UpdatedAt);

public sealed record FitnessAssessmentCompletionResponse(bool Exists, bool IsCompleted, int CurrentStep, int CompletionPercentage);
