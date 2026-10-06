import type { NavigatorScreenParams } from '@react-navigation/native';

export type AuthStackParamList = {
  Login: undefined;
  Register: undefined;
  ForgotPassword: undefined;
};

export type MemberTabParamList = {
  Dashboard: undefined;
  Workouts: undefined;
  Nutrition: undefined;
  Progress: undefined;
  More: undefined;
};

export type AppStackParamList = {
  Tabs: NavigatorScreenParams<MemberTabParamList> | undefined;
  Fitness: undefined;
  WorkoutPlan: { programId: string };
  WorkoutDay: { programId: string; dayId: string };
  ExerciseDetail: { exerciseId: string };
  WorkoutSession: { sessionId: string };
  WorkoutSummary: { sessionId: string };
  WorkoutHistory: undefined;
  SessionDetail: { sessionId: string };
  Measurements: undefined;
  AiInsight: undefined;
  NutritionProfile: undefined;
  LogMeal: undefined;
  NutritionHistory: undefined;
  AiNutrition: undefined;
  AiCoach: undefined;
  AiConversation: { conversationId?: string; draft?: string } | undefined;
  Challenges: undefined;
  ChallengeDetail: { challengeId: string; page: number };
  Notifications: undefined;
  Profile: undefined;
  Settings: undefined;
  BusinessDashboard: undefined;
  Members: undefined;
  Staff: undefined;
  Trainers: undefined;
  Memberships: undefined;
  Attendance: undefined;
  BusinessSettings: undefined;
  Licenses: undefined;
};
