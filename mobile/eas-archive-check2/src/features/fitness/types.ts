export type UnitSystem = 'Metric' | 'Imperial';
export type FitnessLevel = 'Beginner' | 'Intermediate' | 'Advanced';
export type FitnessGoalType = 'FatLoss' | 'MuscleGain' | 'Strength' | 'Endurance' | 'GeneralFitness' | 'Mobility' | 'WeightMaintenance';
export type ActivityLevel = 'Sedentary' | 'Light' | 'Moderate' | 'Active' | 'VeryActive';
export type GoalStatus = 'Active' | 'Completed' | 'Cancelled';
export type SessionStatus = 'Planned' | 'InProgress' | 'Completed' | 'Cancelled';

export type FitnessProfile = {
  id: string;
  unitSystem: UnitSystem;
  height: number | null;
  weight: number | null;
  fitnessLevel: FitnessLevel;
  primaryGoal: FitnessGoalType;
  activityLevel: ActivityLevel;
  trainingExperience: string | null;
  preferredWorkoutDays: number;
  preferredWorkoutDuration: number;
  preferredTrainingLocation: string | null;
};

export type FitnessGoal = {
  id: string;
  goalType: FitnessGoalType;
  targetValue: number | null;
  currentValue: number | null;
  startDate: string;
  targetDate: string | null;
  status: GoalStatus;
};

export type ExerciseDetail = {
  id: string;
  name: string;
  description: string | null;
  instructions: string | null;
  muscleGroup: string;
  equipment: string | null;
  exerciseType: string;
  difficulty: string;
  isSystem: boolean;
};

export type ProgramExercise = {
  id: string;
  exerciseId: string;
  sequence: number;
  sets: number | null;
  targetReps: number | null;
  targetDurationSeconds: number | null;
  targetDistance: number | null;
  targetWeight: number | null;
  restSeconds: number | null;
  notes: string | null;
};

export type ProgramDay = {
  id: string;
  dayNumber: number;
  name: string;
  description: string | null;
  restDay: boolean;
  exercises: ProgramExercise[];
};

export type WorkoutProgram = {
  id: string;
  name: string;
  description: string | null;
  goalType: FitnessGoalType;
  difficulty: string;
  durationWeeks: number;
  frequencyPerWeek: number;
  status: string;
  isOwnedByUser: boolean;
  days: ProgramDay[];
};

export type WorkoutSet = {
  id: string;
  setNumber: number;
  reps: number | null;
  weight: number | null;
  durationSeconds: number | null;
  distance: number | null;
  rpe: number | null;
  completed: boolean;
  estimatedOneRepMax: number | null;
};

export type SessionExercise = {
  id: string;
  exerciseId: string;
  sequence: number;
  notes: string | null;
  sets: WorkoutSet[];
};

export type WorkoutSession = {
  id: string;
  workoutProgramId: string | null;
  workoutProgramDayId: string | null;
  startedAt: string | null;
  completedAt: string | null;
  status: SessionStatus;
  durationSeconds: number | null;
  notes: string | null;
  exercises: SessionExercise[];
};

export type Measurement = {
  id: string;
  unitSystem: UnitSystem;
  measurementDate: string;
  weight: number | null;
  bodyFatPercentage: number | null;
  chest: number | null;
  waist: number | null;
  hip: number | null;
  arm: number | null;
  thigh: number | null;
  notes: string | null;
};

export type PersonalRecord = {
  exerciseId: string;
  exerciseName: string;
  maxWeight: number | null;
  maxReps: number | null;
  maxDurationSeconds: number | null;
  maxDistance: number | null;
  estimatedOneRepMax: number | null;
};

export type NutritionToday = {
  date: string;
  calories: number;
  protein: number;
  carbohydrates: number;
  fat: number;
  calorieTarget: number;
  proteinTarget: number;
  carbTarget: number;
  fatTarget: number;
};

export type MemberDashboard = {
  currentGoal: FitnessGoal | null;
  latestMeasurement: Measurement | null;
  weeklyWorkouts: number;
  monthlyWorkouts: number;
  streakDays: number;
  recentWorkouts: WorkoutSession[];
  nutritionToday: NutritionToday;
  activeChallenge: { id: string; name: string; currentValue: number | null; targetValue: number } | null;
  membershipStatus: string | null;
};

export type AiChat = {
  conversationId: string;
  message: string;
  inputTokens: number;
  outputTokens: number;
};

export const goalTypes: FitnessGoalType[] = ['FatLoss', 'MuscleGain', 'Strength', 'Endurance', 'GeneralFitness', 'Mobility', 'WeightMaintenance'];
export const fitnessLevels: FitnessLevel[] = ['Beginner', 'Intermediate', 'Advanced'];
export const activityLevels: ActivityLevel[] = ['Sedentary', 'Light', 'Moderate', 'Active', 'VeryActive'];
