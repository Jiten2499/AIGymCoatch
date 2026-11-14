using System.Text.Json;
using System.Text.Json.Serialization;
using LifeFitAI.Application.Fitness.Assessment;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Fitness.Ai;

public sealed class WorkoutBrief
{
    public Guid AssessmentId { get; init; }

    public DateOnly PlanStart { get; init; }

    public int? Age { get; init; }

    public string? Gender { get; init; }

    public decimal? HeightCm { get; init; }

    public decimal? WeightKg { get; init; }

    public decimal? TargetWeightKg { get; init; }

    public decimal? BodyFat { get; init; }

    public string? Country { get; init; }

    public decimal? Bmi { get; init; }

    public decimal? Bmr { get; init; }

    public decimal? Tdee { get; init; }

    public string Activity { get; init; } = ActivityLevel.Moderate.ToString();

    public decimal ActivityFactor { get; init; }

    public int WeeklyMinutes { get; init; }

    public string PrimaryGoal { get; init; } = FitnessGoalType.GeneralFitness.ToString();

    public string? SecondaryGoal { get; init; }

    public string? Timeline { get; init; }

    public string Level { get; init; } = FitnessLevel.Beginner.ToString();

    public string? Priority { get; init; }

    public string? GoalDescription { get; init; }

    public int? DaysPerWeek { get; init; }

    public string? Avoid { get; init; }

    public object? Health { get; init; }

    public object? Workout { get; init; }

    public object? Recovery { get; init; }

    public object? Measurements { get; init; }

    public string[] Unknown { get; init; } = [];
}

public static class AssessmentWorkoutBrief
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static Result<WorkoutBrief> Create(FitnessAssessment? assessment, DateOnly today)
    {
        if (assessment is null || !assessment.IsCompleted)
        {
            return Result.Failure<WorkoutBrief>(AiErrors.AssessmentRequired);
        }

        var health = Read<AssessmentHealthSection>(assessment.HealthJson);
        var restriction = string.Join(' ', new[] { health?.DoctorRestrictions, health?.PhysicalLimitations, health?.AdditionalNotes }.Where(item => !string.IsNullOrWhiteSpace(item)));
        if (RequiresClearance(restriction))
        {
            return Result.Failure<WorkoutBrief>(AiErrors.MedicalClearance);
        }

        var workout = Read<AssessmentWorkoutSection>(assessment.WorkoutJson);
        var diet = Read<AssessmentDietSection>(assessment.DietJson);
        var lifestyle = Read<AssessmentLifestyleSection>(assessment.LifestyleJson);
        var measurements = Read<AssessmentMeasurementsSection>(assessment.MeasurementsJson);
        var age = assessment.AgeYears(today);
        var activity = lifestyle?.ActivityLevel ?? ActivityLevel.Moderate;
        var factor = ActivityFactor(activity);
        var bmr = EstimateBmr(assessment.CurrentWeightKg, assessment.HeightCm, age, assessment.Gender);
        var days = workout?.DaysPerWeek is >= 1 and <= 7 ? workout.DaysPerWeek : null;
        var minutes = workout?.DurationMinutes is >= 10 and <= 180 ? workout.DurationMinutes.Value : 45;
        var unknown = UnknownFields(assessment, workout, measurements);
        return Result.Success(new WorkoutBrief
        {
            AssessmentId = assessment.Id,
            PlanStart = today,
            Age = age,
            Gender = assessment.Gender,
            HeightCm = assessment.HeightCm,
            WeightKg = assessment.CurrentWeightKg,
            TargetWeightKg = assessment.TargetWeightKg,
            BodyFat = assessment.BodyFatPercentage ?? measurements?.BodyFatPercentage,
            Country = assessment.Country,
            Bmi = assessment.BodyMassIndex(),
            Bmr = bmr,
            Tdee = bmr is null ? null : Math.Round(bmr.Value * factor, 0, MidpointRounding.AwayFromZero),
            Activity = activity.ToString(),
            ActivityFactor = factor,
            WeeklyMinutes = (days ?? 3) * minutes,
            PrimaryGoal = assessment.PrimaryGoal ?? FitnessGoalType.GeneralFitness.ToString(),
            SecondaryGoal = assessment.SecondaryGoal,
            Timeline = assessment.GoalTimeline,
            Level = (workout?.Experience ?? assessment.FitnessLevel?.ToString()) ?? FitnessLevel.Beginner.ToString(),
            Priority = assessment.Priority,
            GoalDescription = assessment.GoalDescription,
            DaysPerWeek = days,
            Avoid = workout?.ExercisesToAvoid,
            Health = new
            {
                health?.HasMedicalConditions,
                health?.MedicalConditions,
                health?.HasInjuries,
                health?.InjuryDetails,
                health?.PhysicalLimitations,
                health?.DoctorRestrictions,
            },
            Workout = new
            {
                workout?.Location,
                DaysPerWeek = days ?? 3,
                workout?.PreferredDays,
                DurationMinutes = minutes,
                workout?.PreferredTime,
                workout?.Equipment,
                workout?.TrainingStyles,
                workout?.PreferredMuscleGroups,
                workout?.ExercisesToAvoid,
                workout?.CardioPreference,
                workout?.RestDayPreference,
            },
            Recovery = new
            {
                diet?.DietType,
                diet?.FoodsToAvoid,
                RecoveryAids = diet?.Supplements,
                lifestyle?.Occupation,
                lifestyle?.WorkType,
                lifestyle?.AverageDailySteps,
                lifestyle?.AverageSleepHours,
                lifestyle?.SleepQuality,
                lifestyle?.StressLevel,
                lifestyle?.DailyWaterLitres,
            },
            Measurements = measurements,
            Unknown = unknown,
        });
    }

    public static string ToJson(WorkoutBrief brief, IReadOnlyList<string> catalog) =>
        JsonSerializer.Serialize(new
        {
            brief.AssessmentId,
            planStart = brief.PlanStart.ToString("yyyy-MM-dd"),
            durationDays = 30,
            personal = new { brief.Age, brief.Gender, brief.HeightCm, brief.WeightKg, brief.TargetWeightKg, brief.BodyFat, brief.Country, brief.Bmi, brief.Bmr, brief.Tdee, brief.Activity, brief.ActivityFactor, brief.WeeklyMinutes },
            goals = new { brief.PrimaryGoal, brief.SecondaryGoal, brief.Timeline, brief.Level, brief.Priority, brief.GoalDescription },
            brief.Health,
            brief.Workout,
            brief.Recovery,
            brief.Measurements,
            brief.Unknown,
            catalog,
            calculations = "BMI, BMR, and TDEE are informational. Do not choose intensity from BMI alone. Do not invent missing values.",
        }, Options);

    public static IReadOnlyList<GenerationExercise> MatchEquipment(IReadOnlyList<GenerationExercise> catalog, IReadOnlyList<string>? equipment, string? location)
    {
        var tokens = equipment?.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).ToArray() ?? [];
        if (tokens.Any(token => token.Equals("FullGym", StringComparison.OrdinalIgnoreCase)) || (tokens.Length == 0 && string.Equals(location, "Gym", StringComparison.OrdinalIgnoreCase)))
        {
            return catalog.Take(60).ToArray();
        }

        var bodyweightOnly = tokens.Length == 0 || tokens.All(token => token.Equals("BodyweightOnly", StringComparison.OrdinalIgnoreCase) || token.Equals("Other", StringComparison.OrdinalIgnoreCase));
        return catalog.Where(exercise => GearFits(exercise, tokens, bodyweightOnly)).Take(60).ToArray();
    }

    public static decimal? EstimateBmr(decimal? weightKg, decimal? heightCm, int? age, string? gender)
    {
        if (weightKg is not > 0 || heightCm is not > 0 || age is < 15 or > 100 or null)
        {
            return null;
        }

        var sex = gender?.Trim().ToLowerInvariant();
        var offset = sex is "male" ? 5m : sex is "female" ? -161m : -78m;
        return Math.Round((10m * weightKg.Value) + (6.25m * heightCm.Value) - (5m * age.Value) + offset, 0, MidpointRounding.AwayFromZero);
    }

    public static decimal ActivityFactor(ActivityLevel level) => level switch
    {
        ActivityLevel.Sedentary => 1.2m,
        ActivityLevel.Light => 1.375m,
        ActivityLevel.Active => 1.725m,
        ActivityLevel.VeryActive => 1.9m,
        _ => 1.55m,
    };

    public static bool RequiresClearance(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.ToLowerInvariant();
        return value.Contains("medical clearance", StringComparison.Ordinal)
            || value.Contains("clearance before", StringComparison.Ordinal)
            || value.Contains("not cleared", StringComparison.Ordinal)
            || value.Contains("do not exercise", StringComparison.Ordinal)
            || value.Contains("don't exercise", StringComparison.Ordinal)
            || value.Contains("no exercise allowed", StringComparison.Ordinal)
            || value.Contains("avoid all exercise", StringComparison.Ordinal);
    }

    private static bool GearFits(GenerationExercise exercise, IReadOnlyList<string> tokens, bool bodyweightOnly)
    {
        var gear = exercise.Equipment?.ToLowerInvariant() ?? string.Empty;
        var bodyweight = gear.Length == 0 || gear.Contains("body", StringComparison.Ordinal) || gear.Contains("none", StringComparison.Ordinal) || exercise.ExerciseType is "Bodyweight" or "Mobility" or "Stretching";
        if (bodyweightOnly || bodyweight)
        {
            return bodyweight;
        }

        return tokens.Any(token => gear.Contains(TokenText(token), StringComparison.Ordinal));
    }

    private static string TokenText(string token) => token.ToLowerInvariant() switch
    {
        "dumbbells" => "dumbbell",
        "barbells" => "barbell",
        "resistancebands" => "band",
        "treadmill" => "tread",
        "exercisebike" => "bike",
        "pullupbar" => "pull",
        _ => token.ToLowerInvariant(),
    };

    internal static T? Read<T>(string? json) where T : class
    {
        try
        {
            return AssessmentJson.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string[] UnknownFields(FitnessAssessment assessment, AssessmentWorkoutSection? workout, AssessmentMeasurementsSection? measurements)
    {
        var missing = new List<string>();
        if (assessment.BodyFatPercentage is null && measurements?.BodyFatPercentage is null) missing.Add("bodyFat");
        if (workout?.Equipment is null || workout.Equipment.Count == 0) missing.Add("equipment");
        if (workout?.DaysPerWeek is null) missing.Add("daysPerWeek");
        if (string.IsNullOrWhiteSpace(workout?.Location)) missing.Add("location");
        return missing.ToArray();
    }
}
