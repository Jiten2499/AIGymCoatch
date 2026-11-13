export const dietaryPreferences = ['Vegetarian', 'Vegan', 'NonVegetarian', 'Custom'] as const;
export const mealTypes = ['Breakfast', 'Lunch', 'Dinner', 'Snack'] as const;

export type DietaryPreference = (typeof dietaryPreferences)[number];
export type MealType = (typeof mealTypes)[number];

export type NutritionProfile = {
  id: string;
  dailyCalorieTarget: number;
  proteinTarget: number;
  carbTarget: number;
  fatTarget: number;
  dietaryPreference: DietaryPreference;
  mealFrequency: number;
};

export type NutritionLog = {
  id: string;
  logDate: string;
  mealType: MealType;
  foodName: string;
  quantity: number;
  unit: string;
  calories: number;
  protein: number;
  carbohydrates: number;
  fat: number;
};

export type NutritionSummary = {
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

export type NutritionLogInput = Omit<NutritionLog, 'id'>;
export type NutritionProfileInput = Omit<NutritionProfile, 'id'>;
