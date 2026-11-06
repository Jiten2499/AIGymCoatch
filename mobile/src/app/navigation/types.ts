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
  Chats: undefined;
  More: undefined;
};

export type TrainerTabParamList = {
  Home: undefined;
  Schedule: undefined;
  Members: undefined;
  Performance: undefined;
  Chats: undefined;
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
  Chats: undefined;
  ChatThread: { conversationId: string; title: string; role: string };
  CommunicationDetail: { communicationId: string };
  Profile: undefined;
  Settings: undefined;
  BusinessDashboard: undefined;
  Members: undefined;
  MemberDetail: { memberId: string };
  MemberForm: { memberId?: string } | undefined;
  Staff: undefined;
  Trainers: undefined;
  MyTargets: undefined;
  TrainerMember: { memberId: string };
  TrainerMemberProgress: { memberId: string; assessmentId?: string; start?: 'record' };
  TrainerSession: { sessionId: string };
  TrainerActive: { sessionId: string };
  TrainerAlerts: undefined;
  Memberships: undefined;
  Tasks: undefined;
  ApprovalDetail: { requestId: string };
  PackageForm: { packageId?: string } | undefined;
  Leads: undefined;
  LeadDetail: { leadId: string };
  LeadForm: { leadId?: string } | undefined;
  Expenses: undefined;
  ExpenseDetail: { expenseId: string };
  ExpenseForm: { expenseId?: string } | undefined;
  ExpenseCategories: undefined;
  Ledger: undefined;
  LedgerTransfer: undefined;
  LedgerAccounts: undefined;
  Integrations: undefined;
  Attendance: undefined;
  MyLeaves: undefined;
  ApplyLeave: undefined;
  LeaveDetail: { leaveId: string };
  BusinessSettings: undefined;
  Licenses: undefined;
};
