namespace LifeFitAI.Domain.Fitness;

public enum UnitSystem
{
    Metric = 1,
    Imperial = 2,
}

public enum FitnessLevel
{
    Beginner = 1,
    Intermediate = 2,
    Advanced = 3,
}

public enum FitnessGoalType
{
    FatLoss = 1,
    MuscleGain = 2,
    Strength = 3,
    Endurance = 4,
    GeneralFitness = 5,
    Mobility = 6,
    WeightMaintenance = 7,
}

public enum ActivityLevel
{
    Sedentary = 1,
    Light = 2,
    Moderate = 3,
    Active = 4,
    VeryActive = 5,
}

public enum GoalStatus
{
    Active = 1,
    Completed = 2,
    Cancelled = 3,
}

public enum MuscleGroup
{
    Chest = 1,
    Back = 2,
    Shoulders = 3,
    Biceps = 4,
    Triceps = 5,
    Legs = 6,
    Glutes = 7,
    Core = 8,
    FullBody = 9,
    Cardio = 10,
    Mobility = 11,
}

public enum ExerciseType
{
    Strength = 1,
    Cardio = 2,
    Mobility = 3,
    Stretching = 4,
    Bodyweight = 5,
}

public enum WorkoutDifficulty
{
    Beginner = 1,
    Intermediate = 2,
    Advanced = 3,
}

public enum ProgramStatus
{
    Active = 1,
    Archived = 2,
}

public enum SessionStatus
{
    Planned = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
}

public enum DietaryPreference
{
    Vegetarian = 1,
    Vegan = 2,
    NonVegetarian = 3,
    Custom = 4,
}

public enum MealType
{
    Breakfast = 1,
    Lunch = 2,
    Dinner = 3,
    Snack = 4,
}

public enum ChallengeType
{
    WorkoutCount = 1,
    ConsistencyDays = 2,
    Custom = 3,
}

public enum ChallengeStatus
{
    Draft = 1,
    Active = 2,
    Completed = 3,
    Cancelled = 4,
}

public enum NotificationType
{
    WorkoutReminder = 1,
    MembershipReminder = 2,
    ChallengeReminder = 3,
    AiRecommendation = 4,
    WorkoutCompleted = 5,
    StaffLeaveUpdate = 6,
    ChatMessage = 7,
}

public enum AiMessageRole
{
    User = 1,
    Assistant = 2,
}

public enum AiRequestType
{
    Coach = 1,
    WorkoutGeneration = 2,
    NutritionAdvice = 3,
    ProgressInsight = 4,
    BusinessInsights = 5,
}

public enum BodyScanSource
{
    Manual = 1,
    Device = 2,
    Import = 3,
}

public enum BodyScanStatus
{
    Pending = 1,
    Stored = 2,
    Failed = 3,
}
