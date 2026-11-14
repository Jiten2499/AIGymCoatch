import type { AssessmentDraft, FitnessAssessment } from './types';

export function emptyDraft(): AssessmentDraft {
  return {
    dateOfBirth: '',
    gender: '',
    heightCm: '',
    currentWeightKg: '',
    targetWeightKg: '',
    waistCircumferenceCm: '',
    bodyFatPercentage: '',
    country: '',
    measurementUnit: 'Metric',
    primaryGoal: '',
    secondaryGoal: '',
    goalTimeline: '',
    customGoalTimelineDays: '',
    fitnessLevel: '',
    priority: '',
    goalDescription: '',
    health: {
      hasMedicalConditions: null,
      medicalConditions: [],
      hasInjuries: null,
      injuryDetails: '',
      physicalLimitations: '',
      foodAllergies: [],
      foodIntolerances: '',
      medications: '',
      doctorRestrictions: '',
      additionalNotes: '',
    },
    workout: {
      location: '',
      experience: '',
      daysPerWeek: null,
      preferredDays: [],
      durationMinutes: null,
      preferredTime: '',
      equipment: [],
      trainingStyles: [],
      preferredMuscleGroups: [],
      exercisesToAvoid: '',
      cardioPreference: '',
      restDayPreference: '',
    },
    diet: {
      dietType: '',
      eggsAllowed: null,
      dairyConsumption: '',
      preferredCuisines: [],
      mealsPerDay: null,
      preferredMealTimings: '',
      foodsLiked: '',
      foodsDisliked: '',
      foodsToAvoid: '',
      cookingAvailability: '',
      dailyFoodBudget: '',
      customBudgetAmount: null,
      supplements: '',
      preferredProteinSources: '',
    },
    lifestyle: {
      occupation: '',
      workType: '',
      activityLevel: '',
      averageDailySteps: null,
      averageSleepHours: null,
      sleepQuality: '',
      stressLevel: '',
      dailyWaterLitres: null,
      preferredWorkoutSchedule: '',
    },
    measurements: {
      chestCm: null,
      waistCm: null,
      hipCm: null,
      leftArmCm: null,
      rightArmCm: null,
      thighCm: null,
      bodyFatPercentage: null,
      fitnessNotes: '',
    },
    currentStep: 1,
  };
}

export function fromAssessment(data: FitnessAssessment): AssessmentDraft {
  const base = emptyDraft();
  return {
    ...base,
    dateOfBirth: data.dateOfBirth?.slice(0, 10) ?? '',
    gender: data.gender ?? '',
    heightCm: text(data.heightCm),
    currentWeightKg: text(data.currentWeightKg),
    targetWeightKg: text(data.targetWeightKg),
    waistCircumferenceCm: text(data.waistCircumferenceCm),
    bodyFatPercentage: text(data.bodyFatPercentage),
    country: data.country ?? '',
    measurementUnit: data.measurementUnit,
    primaryGoal: data.primaryGoal ?? '',
    secondaryGoal: data.secondaryGoal ?? '',
    goalTimeline: data.goalTimeline ?? '',
    customGoalTimelineDays: text(data.customGoalTimelineDays),
    fitnessLevel: data.fitnessLevel ?? '',
    priority: data.priority ?? '',
    goalDescription: data.goalDescription ?? '',
    health: { ...base.health, ...normalizeHealth(data.health) },
    workout: { ...base.workout, ...normalizeWorkout(data.workout) },
    diet: { ...base.diet, ...normalizeDiet(data.diet) },
    lifestyle: { ...base.lifestyle, ...normalizeLifestyle(data.lifestyle) },
    measurements: { ...base.measurements, ...normalizeMeasurements(data.measurements) },
    currentStep: data.currentStep || 1,
  };
}

export function bodyMassIndex(heightCm: string, weightKg: string) {
  const height = Number.parseFloat(heightCm);
  const weight = Number.parseFloat(weightKg);
  if (!Number.isFinite(height) || height <= 0 || !Number.isFinite(weight) || weight <= 0) {
    return null;
  }
  const meters = height / 100;
  return Math.round((weight / (meters * meters)) * 10) / 10;
}

export function ageYears(dateOfBirth: string) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(dateOfBirth)) {
    return null;
  }
  const birth = new Date(`${dateOfBirth}T00:00:00Z`);
  const now = new Date();
  let age = now.getUTCFullYear() - birth.getUTCFullYear();
  const month = now.getUTCMonth() - birth.getUTCMonth();
  if (month < 0 || (month === 0 && now.getUTCDate() < birth.getUTCDate())) {
    age -= 1;
  }
  return age >= 0 ? age : null;
}

export function toPayload(draft: AssessmentDraft, submit: boolean) {
  return {
    dateOfBirth: draft.dateOfBirth || null,
    gender: draft.gender || null,
    heightCm: num(draft.heightCm),
    currentWeightKg: num(draft.currentWeightKg),
    targetWeightKg: num(draft.targetWeightKg),
    waistCircumferenceCm: num(draft.waistCircumferenceCm),
    bodyFatPercentage: num(draft.bodyFatPercentage),
    country: draft.country || null,
    measurementUnit: draft.measurementUnit,
    primaryGoal: draft.primaryGoal || null,
    secondaryGoal: draft.secondaryGoal || null,
    goalTimeline: draft.goalTimeline || null,
    customGoalTimelineDays: int(draft.customGoalTimelineDays),
    fitnessLevel: draft.fitnessLevel || null,
    priority: draft.priority || null,
    goalDescription: draft.goalDescription || null,
    health: draft.health,
    workout: {
      ...draft.workout,
      daysPerWeek: draft.workout.daysPerWeek,
      durationMinutes: draft.workout.durationMinutes,
    },
    diet: {
      ...draft.diet,
      mealsPerDay: draft.diet.mealsPerDay,
      customBudgetAmount: draft.diet.customBudgetAmount,
    },
    lifestyle: {
      ...draft.lifestyle,
      activityLevel: draft.lifestyle.activityLevel || null,
      averageDailySteps: draft.lifestyle.averageDailySteps,
      averageSleepHours: draft.lifestyle.averageSleepHours,
      dailyWaterLitres: draft.lifestyle.dailyWaterLitres,
    },
    measurements: draft.measurements,
    currentStep: draft.currentStep,
    submit,
  };
}

function text(value: number | null | undefined) {
  return value == null ? '' : String(value);
}

function num(value: string) {
  const parsed = Number.parseFloat(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function int(value: string) {
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) ? parsed : null;
}

function normalizeHealth(value: FitnessAssessment['health']) {
  if (!value) return {};
  return {
    ...value,
    medicalConditions: value.medicalConditions ?? [],
    foodAllergies: value.foodAllergies ?? [],
    injuryDetails: value.injuryDetails ?? '',
    physicalLimitations: value.physicalLimitations ?? '',
    foodIntolerances: value.foodIntolerances ?? '',
    medications: value.medications ?? '',
    doctorRestrictions: value.doctorRestrictions ?? '',
    additionalNotes: value.additionalNotes ?? '',
  };
}

function normalizeWorkout(value: FitnessAssessment['workout']) {
  if (!value) return {};
  return {
    ...value,
    preferredDays: value.preferredDays ?? [],
    equipment: value.equipment ?? [],
    trainingStyles: value.trainingStyles ?? [],
    preferredMuscleGroups: value.preferredMuscleGroups ?? [],
    location: value.location ?? '',
    experience: value.experience ?? '',
    preferredTime: value.preferredTime ?? '',
    exercisesToAvoid: value.exercisesToAvoid ?? '',
    cardioPreference: value.cardioPreference ?? '',
    restDayPreference: value.restDayPreference ?? '',
  };
}

function normalizeDiet(value: FitnessAssessment['diet']) {
  if (!value) return {};
  return {
    ...value,
    preferredCuisines: value.preferredCuisines ?? [],
    dietType: value.dietType ?? '',
    dairyConsumption: value.dairyConsumption ?? '',
    preferredMealTimings: value.preferredMealTimings ?? '',
    foodsLiked: value.foodsLiked ?? '',
    foodsDisliked: value.foodsDisliked ?? '',
    foodsToAvoid: value.foodsToAvoid ?? '',
    cookingAvailability: value.cookingAvailability ?? '',
    dailyFoodBudget: value.dailyFoodBudget ?? '',
    supplements: value.supplements ?? '',
    preferredProteinSources: value.preferredProteinSources ?? '',
  };
}

function normalizeLifestyle(value: FitnessAssessment['lifestyle']) {
  if (!value) return {};
  return {
    ...value,
    occupation: value.occupation ?? '',
    workType: value.workType ?? '',
    activityLevel: value.activityLevel ?? '',
    sleepQuality: value.sleepQuality ?? '',
    stressLevel: value.stressLevel ?? '',
    preferredWorkoutSchedule: value.preferredWorkoutSchedule ?? '',
  };
}

function normalizeMeasurements(value: FitnessAssessment['measurements']) {
  if (!value) return {};
  return {
    ...value,
    fitnessNotes: value.fitnessNotes ?? '',
  };
}
