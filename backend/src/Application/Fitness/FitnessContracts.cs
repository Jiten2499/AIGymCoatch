using LifeFitAI.Application.Gym;
using LifeFitAI.Domain.Fitness;

namespace LifeFitAI.Application.Fitness;

public sealed record FitnessProfileResponse(Guid Id, UnitSystem UnitSystem, decimal? Height, decimal? Weight, FitnessLevel FitnessLevel, FitnessGoalType PrimaryGoal, ActivityLevel ActivityLevel, string? TrainingExperience, int PreferredWorkoutDays, int PreferredWorkoutDuration, string? PreferredTrainingLocation);

public sealed record GoalResponse(Guid Id, FitnessGoalType GoalType, decimal? TargetValue, decimal? CurrentValue, DateOnly StartDate, DateOnly? TargetDate, GoalStatus Status);

public sealed record ExerciseResponse(Guid Id, string Name, string? Description, string? Instructions, MuscleGroup MuscleGroup, string? Equipment, ExerciseType ExerciseType, WorkoutDifficulty Difficulty, bool IsSystem);

public sealed record ExerciseOption(Guid Id, string Name, string ExerciseType);

public sealed record ProgramExerciseResponse(Guid Id, Guid ExerciseId, int Sequence, int? Sets, int? TargetReps, int? TargetDurationSeconds, decimal? TargetDistance, decimal? TargetWeight, int? RestSeconds, string? Notes);

public sealed record ProgramDayResponse(Guid Id, int DayNumber, string Name, string? Description, bool RestDay, IReadOnlyList<ProgramExerciseResponse> Exercises);

public sealed record ProgramResponse(Guid Id, string Name, string? Description, FitnessGoalType GoalType, WorkoutDifficulty Difficulty, int DurationWeeks, int FrequencyPerWeek, ProgramStatus Status, bool IsOwnedByUser, IReadOnlyList<ProgramDayResponse> Days);

public sealed record SetResponse(Guid Id, int SetNumber, int? Reps, decimal? Weight, int? DurationSeconds, decimal? Distance, decimal? Rpe, bool Completed, decimal? EstimatedOneRepMax);

public sealed record SessionExerciseResponse(Guid Id, Guid ExerciseId, int Sequence, string? Notes, IReadOnlyList<SetResponse> Sets);

public sealed record SessionResponse(Guid Id, Guid? WorkoutProgramId, Guid? WorkoutProgramDayId, DateTime? StartedAt, DateTime? CompletedAt, SessionStatus Status, int? DurationSeconds, string? Notes, IReadOnlyList<SessionExerciseResponse> Exercises);

public sealed record MeasurementResponse(Guid Id, UnitSystem UnitSystem, DateOnly MeasurementDate, decimal? Weight, decimal? BodyFatPercentage, decimal? Chest, decimal? Waist, decimal? Hip, decimal? Arm, decimal? Thigh, string? Notes);

public sealed record PersonalRecordResponse(Guid ExerciseId, string ExerciseName, decimal? MaxWeight, int? MaxReps, int? MaxDurationSeconds, decimal? MaxDistance, decimal? EstimatedOneRepMax);

public sealed record NutritionProfileResponse(Guid Id, decimal DailyCalorieTarget, decimal ProteinTarget, decimal CarbTarget, decimal FatTarget, DietaryPreference DietaryPreference, int MealFrequency);

public sealed record NutritionLogResponse(Guid Id, DateOnly LogDate, MealType MealType, string FoodName, decimal Quantity, string Unit, decimal Calories, decimal Protein, decimal Carbohydrates, decimal Fat);

public sealed record NutritionSummaryResponse(DateOnly Date, decimal Calories, decimal Protein, decimal Carbohydrates, decimal Fat, decimal CalorieTarget, decimal ProteinTarget, decimal CarbTarget, decimal FatTarget);

public sealed record ChallengeResponse(Guid Id, string Name, string? Description, ChallengeType ChallengeType, DateOnly StartDate, DateOnly EndDate, decimal TargetValue, ChallengeStatus Status, bool IsSystem, decimal? CurrentValue, DateTime? CompletedAt);

public sealed record NotificationResponse(Guid Id, NotificationType Type, string Title, string Message, bool IsRead, DateTime CreatedAt);

public sealed record DashboardResponse(GoalResponse? CurrentGoal, MeasurementResponse? LatestMeasurement, int WeeklyWorkouts, int MonthlyWorkouts, int StreakDays, IReadOnlyList<SessionResponse> RecentWorkouts, NutritionSummaryResponse NutritionToday, ChallengeResponse? ActiveChallenge, string? MembershipStatus);

public sealed record AiChatResponse(Guid ConversationId, string Message, int InputTokens, int OutputTokens);

public sealed record ConversationSummary(Guid Id, string Title, DateTime UpdatedAt);

public sealed record ConversationMessageResponse(string Role, string Content, DateTime CreatedAt);

public sealed record ConversationDetailResponse(Guid Id, string Title, IReadOnlyList<ConversationMessageResponse> Messages);

public static class FitnessMaps
{
    public static FitnessProfileResponse ToResponse(FitnessProfile profile) =>
        new(profile.Id, profile.UnitSystem, profile.Height, profile.Weight, profile.FitnessLevel, profile.PrimaryGoal, profile.ActivityLevel, profile.TrainingExperience, profile.PreferredWorkoutDays, profile.PreferredWorkoutDuration, profile.PreferredTrainingLocation);

    public static GoalResponse ToResponse(FitnessGoal goal) =>
        new(goal.Id, goal.GoalType, goal.TargetValue, goal.CurrentValue, goal.StartDate, goal.TargetDate, goal.Status);

    public static ExerciseResponse ToResponse(Exercise exercise) =>
        new(exercise.Id, exercise.Name, exercise.Description, exercise.Instructions, exercise.MuscleGroup, exercise.Equipment, exercise.ExerciseType, exercise.Difficulty, exercise.IsSystem);

    public static MeasurementResponse ToResponse(BodyMeasurement row) =>
        new(row.Id, row.UnitSystem, row.MeasurementDate, row.Weight, row.BodyFatPercentage, row.Chest, row.Waist, row.Hip, row.Arm, row.Thigh, row.Notes);

    public static SetResponse ToResponse(WorkoutSet set) =>
        new(set.Id, set.SetNumber, set.Reps, set.Weight, set.DurationSeconds, set.Distance, set.Rpe, set.Completed, PersonalRecords.EpleyOneRepMax(set.Weight, set.Reps));
}
