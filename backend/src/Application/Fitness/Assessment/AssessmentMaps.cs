using LifeFitAI.Domain.Fitness;
using LifeFitAI.SharedKernel.Abstractions;

namespace LifeFitAI.Application.Fitness.Assessment;

public static class AssessmentMaps
{
    public static FitnessAssessmentResponse ToResponse(FitnessAssessment assessment, IDateTimeProvider clock)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        return new FitnessAssessmentResponse(
            assessment.Id,
            assessment.DateOfBirth,
            assessment.AgeYears(today),
            assessment.Gender,
            assessment.HeightCm,
            assessment.CurrentWeightKg,
            assessment.TargetWeightKg,
            assessment.WaistCircumferenceCm,
            assessment.BodyFatPercentage,
            assessment.Country,
            assessment.MeasurementUnit,
            assessment.BodyMassIndex(),
            assessment.PrimaryGoal,
            assessment.SecondaryGoal,
            assessment.GoalTimeline,
            assessment.CustomGoalTimelineDays,
            assessment.FitnessLevel,
            assessment.Priority,
            assessment.GoalDescription,
            AssessmentJson.Deserialize<AssessmentHealthSection>(assessment.HealthJson),
            AssessmentJson.Deserialize<AssessmentWorkoutSection>(assessment.WorkoutJson),
            AssessmentJson.Deserialize<AssessmentDietSection>(assessment.DietJson),
            AssessmentJson.Deserialize<AssessmentLifestyleSection>(assessment.LifestyleJson),
            AssessmentJson.Deserialize<AssessmentMeasurementsSection>(assessment.MeasurementsJson),
            assessment.CurrentStep,
            assessment.CompletionPercentage,
            assessment.IsCompleted,
            assessment.UpdatedAt);
    }

    public static FitnessGoalType MapGoal(string? goal) => goal switch
    {
        "WeightLoss" or "FatLoss" => FitnessGoalType.FatLoss,
        "MuscleGain" => FitnessGoalType.MuscleGain,
        "Recomposition" => FitnessGoalType.MuscleGain,
        "WeightMaintenance" => FitnessGoalType.WeightMaintenance,
        "Strength" or "AthleticPerformance" => FitnessGoalType.Strength,
        "Endurance" => FitnessGoalType.Endurance,
        "Mobility" => FitnessGoalType.Mobility,
        _ => FitnessGoalType.GeneralFitness,
    };

    public static ActivityLevel MapActivity(ActivityLevel? level) => level ?? ActivityLevel.Moderate;

    public static int CompletionPercentage(
        DateOnly? dateOfBirth,
        decimal? heightCm,
        decimal? currentWeightKg,
        string? primaryGoal,
        FitnessLevel? fitnessLevel,
        AssessmentHealthSection? health,
        AssessmentWorkoutSection? workout,
        AssessmentDietSection? diet,
        AssessmentLifestyleSection? lifestyle,
        AssessmentMeasurementsSection? measurements)
    {
        var score = 0;
        if (dateOfBirth is not null) score++;
        if (heightCm is not null) score++;
        if (currentWeightKg is not null) score++;
        if (!string.IsNullOrWhiteSpace(primaryGoal)) score++;
        if (fitnessLevel is not null) score++;
        if (health is not null) score++;
        if (workout?.DaysPerWeek is not null) score++;
        if (!string.IsNullOrWhiteSpace(diet?.DietType)) score++;
        if (lifestyle?.ActivityLevel is not null) score++;
        if (measurements is not null) score++;
        return (int)Math.Round(score / 10m * 100m, MidpointRounding.AwayFromZero);
    }
}
