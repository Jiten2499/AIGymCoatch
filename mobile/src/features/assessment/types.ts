import type { ActivityLevel, FitnessLevel, UnitSystem } from '../fitness/types';

export type AssessmentHealth = {
  hasMedicalConditions: boolean | null;
  medicalConditions: string[];
  hasInjuries: boolean | null;
  injuryDetails: string;
  physicalLimitations: string;
  foodAllergies: string[];
  foodIntolerances: string;
  medications: string;
  doctorRestrictions: string;
  additionalNotes: string;
};

export type AssessmentWorkout = {
  location: string;
  experience: string;
  daysPerWeek: number | null;
  preferredDays: string[];
  durationMinutes: number | null;
  preferredTime: string;
  equipment: string[];
  trainingStyles: string[];
  preferredMuscleGroups: string[];
  exercisesToAvoid: string;
  cardioPreference: string;
  restDayPreference: string;
};

export type AssessmentDiet = {
  dietType: string;
  eggsAllowed: boolean | null;
  dairyConsumption: string;
  preferredCuisines: string[];
  mealsPerDay: number | null;
  preferredMealTimings: string;
  foodsLiked: string;
  foodsDisliked: string;
  foodsToAvoid: string;
  cookingAvailability: string;
  dailyFoodBudget: string;
  customBudgetAmount: number | null;
  supplements: string;
  preferredProteinSources: string;
};

export type AssessmentLifestyle = {
  occupation: string;
  workType: string;
  activityLevel: ActivityLevel | '';
  averageDailySteps: number | null;
  averageSleepHours: number | null;
  sleepQuality: string;
  stressLevel: string;
  dailyWaterLitres: number | null;
  preferredWorkoutSchedule: string;
};

export type AssessmentMeasurements = {
  chestCm: number | null;
  waistCm: number | null;
  hipCm: number | null;
  leftArmCm: number | null;
  rightArmCm: number | null;
  thighCm: number | null;
  bodyFatPercentage: number | null;
  fitnessNotes: string;
};

export type FitnessAssessment = {
  id: string;
  dateOfBirth: string | null;
  ageYears: number | null;
  gender: string | null;
  heightCm: number | null;
  currentWeightKg: number | null;
  targetWeightKg: number | null;
  waistCircumferenceCm: number | null;
  bodyFatPercentage: number | null;
  country: string | null;
  measurementUnit: UnitSystem;
  bodyMassIndex: number | null;
  primaryGoal: string | null;
  secondaryGoal: string | null;
  goalTimeline: string | null;
  customGoalTimelineDays: number | null;
  fitnessLevel: FitnessLevel | null;
  priority: string | null;
  goalDescription: string | null;
  health: AssessmentHealth | null;
  workout: AssessmentWorkout | null;
  diet: AssessmentDiet | null;
  lifestyle: AssessmentLifestyle | null;
  measurements: AssessmentMeasurements | null;
  currentStep: number;
  completionPercentage: number;
  isCompleted: boolean;
  updatedAt?: string;
};

export type FitnessAssessmentCompletion = {
  exists: boolean;
  isCompleted: boolean;
  currentStep: number;
  completionPercentage: number;
};

export type AssessmentDraft = {
  dateOfBirth: string;
  gender: string;
  heightCm: string;
  currentWeightKg: string;
  targetWeightKg: string;
  waistCircumferenceCm: string;
  bodyFatPercentage: string;
  country: string;
  measurementUnit: UnitSystem;
  primaryGoal: string;
  secondaryGoal: string;
  goalTimeline: string;
  customGoalTimelineDays: string;
  fitnessLevel: FitnessLevel | '';
  priority: string;
  goalDescription: string;
  health: AssessmentHealth;
  workout: AssessmentWorkout;
  diet: AssessmentDiet;
  lifestyle: AssessmentLifestyle;
  measurements: AssessmentMeasurements;
  currentStep: number;
};

export const assessmentGoals = [
  'WeightLoss',
  'MuscleGain',
  'Recomposition',
  'WeightMaintenance',
  'Strength',
  'Endurance',
  'GeneralFitness',
  'AthleticPerformance',
] as const;

export const weekDays = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'] as const;
