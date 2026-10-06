using LifeFitAI.Domain.Fitness;

namespace LifeFitAI.Api.Contracts;

public sealed record SaveFitnessProfileRequest(UnitSystem UnitSystem, decimal? Height, decimal? Weight, FitnessLevel FitnessLevel, FitnessGoalType PrimaryGoal, ActivityLevel ActivityLevel, string? TrainingExperience, int PreferredWorkoutDays, int PreferredWorkoutDuration, string? PreferredTrainingLocation);

public sealed record CreateGoalRequest(FitnessGoalType GoalType, decimal? TargetValue, decimal? CurrentValue, DateOnly StartDate, DateOnly? TargetDate);

public sealed record UpdateGoalRequest(decimal? TargetValue, decimal? CurrentValue, DateOnly? TargetDate);

public sealed record CreateExerciseRequest(string Name, string? Description, string? Instructions, MuscleGroup MuscleGroup, string? Equipment, ExerciseType ExerciseType, WorkoutDifficulty Difficulty);

public sealed record CreateProgramRequest(string Name, string? Description, FitnessGoalType GoalType, WorkoutDifficulty Difficulty, int DurationWeeks, int FrequencyPerWeek, IReadOnlyList<ProgramDayRequest> Days);

public sealed record ProgramDayRequest(int DayNumber, string Name, string? Description, bool RestDay, IReadOnlyList<ProgramExerciseRequest> Exercises);

public sealed record ProgramExerciseRequest(Guid ExerciseId, int? Sets, int? TargetReps, int? TargetDurationSeconds, decimal? TargetDistance, decimal? TargetWeight, int? RestSeconds, string? Notes);

public sealed record CreateSessionRequest(Guid? ProgramId, Guid? DayId, string? Notes);

public sealed record AddSessionExerciseRequest(Guid ExerciseId, string? Notes);

public sealed record AddSetRequest(int? Reps, decimal? Weight, int? DurationSeconds, decimal? Distance, decimal? Rpe, bool Completed);

public sealed record RecordMeasurementRequest(UnitSystem UnitSystem, DateOnly MeasurementDate, decimal? Weight, decimal? BodyFatPercentage, decimal? Chest, decimal? Waist, decimal? Hip, decimal? Arm, decimal? Thigh, string? Notes);

public sealed record RecordBodyScanRequest(DateOnly ScanDate, BodyScanSource Source, string StorageReference);

public sealed record SaveNutritionProfileRequest(decimal DailyCalorieTarget, decimal ProteinTarget, decimal CarbTarget, decimal FatTarget, DietaryPreference DietaryPreference, int MealFrequency);

public sealed record CreateNutritionLogRequest(DateOnly LogDate, MealType MealType, string FoodName, decimal Quantity, string Unit, decimal Calories, decimal Protein, decimal Carbohydrates, decimal Fat);

public sealed record CreateChallengeRequest(string Name, string? Description, ChallengeType ChallengeType, DateOnly StartDate, DateOnly EndDate, decimal TargetValue);

public sealed record RecordChallengeProgressRequest(decimal Value);

public sealed record CoachChatRequest(string Message, Guid? ConversationId);

public sealed record GenerateWorkoutRequest(string? Notes);

public sealed record NutritionAdviceRequest(string? Question);
