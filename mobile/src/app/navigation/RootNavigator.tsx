import { DarkTheme, NavigationContainer, type Theme as NavigationTheme } from '@react-navigation/native';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { StatusBar } from 'expo-status-bar';
import { useTranslation } from 'react-i18next';
import { ActivityIndicator, View } from 'react-native';
import { ForgotPasswordScreen } from '../../features/auth/screens/ForgotPasswordScreen';
import { LoginScreen } from '../../features/auth/screens/LoginScreen';
import { RegisterScreen } from '../../features/auth/screens/RegisterScreen';
import { AiCoachScreen } from '../../features/aiCoach/screens/AiCoachScreen';
import { AiConversationScreen } from '../../features/aiCoach/screens/AiConversationScreen';
import { AttendanceScreen, BusinessDashboardScreen, BusinessSettingsScreen, StaffScreen, TrainersScreen } from '../../features/business/screens/BusinessScreens';
import { MyTargetsScreen } from '../../features/trainers/screens/MyTargetsScreen';
import { ExpenseCategoriesScreen } from '../../features/expenses/screens/ExpenseCategoriesScreen';
import { ExpenseDetailScreen } from '../../features/expenses/screens/ExpenseDetailScreen';
import { ExpenseFormScreen } from '../../features/expenses/screens/ExpenseFormScreen';
import { ExpensesScreen } from '../../features/expenses/screens/ExpensesScreen';
import { LedgerAccountsScreen } from '../../features/ledger/screens/LedgerAccountsScreen';
import { LedgerScreen } from '../../features/ledger/screens/LedgerScreen';
import { LedgerTransferScreen } from '../../features/ledger/screens/LedgerTransferScreen';
import { IntegrationsScreen } from '../../features/integrations/screens/IntegrationsScreen';
import { LeadDetailScreen } from '../../features/leads/screens/LeadDetailScreen';
import { LeadFormScreen } from '../../features/leads/screens/LeadFormScreen';
import { LeadsScreen } from '../../features/leads/screens/LeadsScreen';
import { ApprovalDetailScreen } from '../../features/approvals/screens/ApprovalDetailScreen';
import { TasksScreen } from '../../features/approvals/screens/TasksScreen';
import { ApplyLeaveScreen } from '../../features/leave/screens/ApplyLeaveScreen';
import { LeaveDetailScreen } from '../../features/leave/screens/LeaveDetailScreen';
import { MyLeavesScreen } from '../../features/leave/screens/MyLeavesScreen';
import { PackageFormScreen } from '../../features/memberships/screens/PackageFormScreen';
import { PackagesScreen } from '../../features/memberships/screens/PackagesScreen';
import { MemberDetailScreen } from '../../features/members/screens/MemberDetailScreen';
import { MemberFormScreen } from '../../features/members/screens/MemberFormScreen';
import { MembersScreen } from '../../features/members/screens/MembersScreen';
import { ChallengeDetailScreen } from '../../features/challenges/screens/ChallengeDetailScreen';
import { ChallengesScreen } from '../../features/challenges/screens/ChallengesScreen';
import { DashboardScreen } from '../../features/dashboard/screens/DashboardScreen';
import { FitnessAssessmentScreen } from '../../features/assessment/screens/FitnessAssessmentScreen';
import { FitnessScreen } from '../../features/fitness/screens/FitnessScreen';
import { ChatListScreen } from '../../features/chat/screens/ChatListScreen';
import { ChatThreadScreen } from '../../features/chat/screens/ChatThreadScreen';
import { CommunicationDetailScreen } from '../../features/communications/screens/CommunicationDetailScreen';
import { NotificationsScreen } from '../../features/notifications/screens/NotificationsScreen';
import { AiNutritionScreen } from '../../features/nutrition/screens/AiNutritionScreen';
import { LogMealScreen } from '../../features/nutrition/screens/LogMealScreen';
import { NutritionHistoryScreen } from '../../features/nutrition/screens/NutritionHistoryScreen';
import { NutritionProfileScreen } from '../../features/nutrition/screens/NutritionProfileScreen';
import { NutritionScreen } from '../../features/nutrition/screens/NutritionScreen';
import { ProfileScreen } from '../../features/profile/screens/ProfileScreen';
import { ProgressScreen } from '../../features/progress/screens/ProgressScreen';
import { SettingsScreen } from '../../features/settings/screens/SettingsScreen';
import { LicenseScreen } from '../../features/licensing/screens/LicenseScreen';
import { MoreScreen } from '../../features/shell/MoreScreen';
import { MeasurementsScreen } from '../../features/progress/screens/MeasurementsScreen';
import { AiProgressInsightScreen } from '../../features/progress/screens/AiProgressInsightScreen';
import { ExerciseDetailScreen } from '../../features/workouts/screens/ExerciseDetailScreen';
import { SessionDetailScreen } from '../../features/workouts/screens/SessionDetailScreen';
import { WorkoutDayScreen } from '../../features/workouts/screens/WorkoutDayScreen';
import { WorkoutHistoryScreen } from '../../features/workouts/screens/WorkoutHistoryScreen';
import { WorkoutPlanScreen } from '../../features/workouts/screens/WorkoutPlanScreen';
import { WorkoutSessionScreen } from '../../features/workouts/screens/WorkoutSessionScreen';
import { WorkoutSummaryScreen } from '../../features/workouts/screens/WorkoutSummaryScreen';
import { WorkoutsScreen } from '../../features/workouts/screens/WorkoutsScreen';
import { TrainerActiveScreen } from '../../features/trainers/screens/TrainerActiveScreen';
import { TrainerAlertsScreen } from '../../features/trainers/screens/TrainerAlertsScreen';
import { TrainerMemberScreen } from '../../features/trainers/screens/TrainerMemberScreen';
import { TrainerMemberPlanScreen } from '../../features/trainers/screens/TrainerMemberPlanScreen';
import { TrainerMemberProgressScreen } from '../../features/trainers/screens/TrainerMemberProgressScreen';
import { TrainerSessionScreen } from '../../features/trainers/screens/TrainerSessionScreen';
import { featureCodes } from '../../constants/access';
import { useWorkspaceFeatures } from '../../features/licensing/api/featureQuery';
import { useSessionStore } from '../../store/sessionStore';
import { useWorkspaceStore } from '../../store/workspaceStore';
import { useTheme } from '../providers/ThemeProvider';
import { NotificationBellButton } from './NotificationBellButton';
import { TabGlyph } from './TabGlyph';
import { TrainerTabs } from './TrainerTabs';
import type { AppStackParamList, AuthStackParamList, MemberTabParamList } from './types';

const AuthStack = createNativeStackNavigator<AuthStackParamList>();
const AppStack = createNativeStackNavigator<AppStackParamList>();
const Tabs = createBottomTabNavigator<MemberTabParamList>();

function MemberTabs() {
  const theme = useTheme();
  const { t } = useTranslation();
  const tenantType = useSessionStore((state) => state.currentTenant?.type);
  const features = useWorkspaceFeatures();
  const chatEnabled = tenantType !== 'Personal' && features.data?.[featureCodes.gymChat] === true;
  return (
    <Tabs.Navigator
      screenOptions={{
        headerShadowVisible: false,
        headerStyle: { backgroundColor: theme.colors.background },
        headerTintColor: theme.colors.text,
        headerRight: () => <NotificationBellButton />,
        tabBarActiveTintColor: theme.colors.accent,
        tabBarInactiveTintColor: theme.colors.text,
        tabBarLabelPosition: 'below-icon',
        tabBarStyle: { backgroundColor: theme.colors.surface, borderTopColor: theme.colors.border },
      }}
    >
      <Tabs.Screen name="Dashboard" component={DashboardScreen} options={{ title: t('nav.dashboard'), tabBarIcon: ({ color, size }) => <TabGlyph color={color} name="home" size={size} /> }} />
      <Tabs.Screen name="Workouts" component={WorkoutsScreen} options={{ title: t('nav.workouts'), tabBarIcon: ({ color, size }) => <TabGlyph color={color} name="workout" size={size} /> }} />
      <Tabs.Screen name="Nutrition" component={NutritionScreen} options={{ title: t('nav.nutrition'), tabBarIcon: ({ color, size }) => <TabGlyph color={color} name="nutrition" size={size} /> }} />
      <Tabs.Screen name="Progress" component={ProgressScreen} options={{ title: t('nav.progress'), tabBarIcon: ({ color, size }) => <TabGlyph color={color} name="progress" size={size} /> }} />
      <Tabs.Screen
        name="Chats"
        component={ChatListScreen}
        options={{
          title: t('nav.chat'),
          tabBarIcon: ({ color, size }) => <TabGlyph color={color} name="chat" size={size} />,
          tabBarButton: chatEnabled ? undefined : () => null,
        }}
      />
      <Tabs.Screen name="More" component={MoreScreen} options={{ title: t('nav.more'), tabBarIcon: ({ color, size }) => <TabGlyph color={color} name="more" size={size} /> }} />
    </Tabs.Navigator>
  );
}

function AppTabs() {
  const theme = useTheme();
  const currentId = useSessionStore((state) => state.currentTenant?.id);
  const ready = useWorkspaceStore((state) => state.ready);
  const workspaces = useWorkspaceStore((state) => state.workspaces);
  const isTrainer = workspaces.some((workspace) => workspace.id === currentId && workspace.roles.includes('Trainer'));
  if (!ready) {
    return (
      <View style={{ alignItems: 'center', backgroundColor: theme.colors.background, flex: 1, justifyContent: 'center' }}>
        <ActivityIndicator color={theme.colors.accent} />
      </View>
    );
  }
  return isTrainer ? <TrainerTabs /> : <MemberTabs />;
}

function AuthenticatedStack() {
  const { t } = useTranslation();
  return (
    <AppStack.Navigator screenOptions={{ headerShadowVisible: false }}>
      <AppStack.Screen name="Tabs" component={AppTabs} options={{ headerShown: false }} />
      <AppStack.Screen name="Fitness" component={FitnessScreen} options={{ title: t('nav.fitness') }} />
      <AppStack.Screen name="FitnessAssessment" component={FitnessAssessmentScreen} options={{ title: t('assessment.title') }} />
      <AppStack.Screen name="WorkoutPlan" component={WorkoutPlanScreen} options={{ title: t('nav.workouts') }} />
      <AppStack.Screen name="WorkoutDay" component={WorkoutDayScreen} options={{ title: t('journey.dayTitle') }} />
      <AppStack.Screen name="ExerciseDetail" component={ExerciseDetailScreen} options={{ title: t('journey.exerciseDetail') }} />
      <AppStack.Screen name="WorkoutSession" component={WorkoutSessionScreen} options={{ title: t('journey.sessionTitle') }} />
      <AppStack.Screen name="WorkoutSummary" component={WorkoutSummaryScreen} options={{ title: t('journey.summaryTitle') }} />
      <AppStack.Screen name="WorkoutHistory" component={WorkoutHistoryScreen} options={{ title: t('journey.historyTitle') }} />
      <AppStack.Screen name="SessionDetail" component={SessionDetailScreen} options={{ title: t('journey.sessionDetail') }} />
      <AppStack.Screen name="Measurements" component={MeasurementsScreen} options={{ title: t('journey.measurements') }} />
      <AppStack.Screen name="AiInsight" component={AiProgressInsightScreen} options={{ title: t('journey.aiLabel') }} />
      <AppStack.Screen name="NutritionProfile" component={NutritionProfileScreen} options={{ title: t('nutrition.profile') }} />
      <AppStack.Screen name="LogMeal" component={LogMealScreen} options={{ title: t('nutrition.logMeal') }} />
      <AppStack.Screen name="NutritionHistory" component={NutritionHistoryScreen} options={{ title: t('nutrition.history') }} />
      <AppStack.Screen name="AiNutrition" component={AiNutritionScreen} options={{ title: t('nutrition.aiTitle') }} />
      <AppStack.Screen name="AiCoach" component={AiCoachScreen} options={{ title: t('nav.ai') }} />
      <AppStack.Screen name="AiConversation" component={AiConversationScreen} options={{ title: t('coach.thread') }} />
      <AppStack.Screen name="Challenges" component={ChallengesScreen} options={{ title: t('nav.challenges') }} />
      <AppStack.Screen name="ChallengeDetail" component={ChallengeDetailScreen} options={{ title: t('nav.challenges') }} />
      <AppStack.Screen name="Notifications" component={NotificationsScreen} options={{ title: t('nav.notifications') }} />
      <AppStack.Screen name="Chats" component={ChatListScreen} options={{ title: t('nav.chat') }} />
      <AppStack.Screen name="ChatThread" component={ChatThreadScreen} options={{ title: t('nav.chat') }} />
      <AppStack.Screen name="CommunicationDetail" component={CommunicationDetailScreen} options={{ title: t('notification.circularBadge') }} />
      <AppStack.Screen name="Profile" component={ProfileScreen} options={{ title: t('nav.profile') }} />
      <AppStack.Screen name="Settings" component={SettingsScreen} options={{ title: t('nav.settings') }} />
      <AppStack.Screen name="BusinessDashboard" component={BusinessDashboardScreen} options={{ title: t('nav.business') }} />
      <AppStack.Screen name="Members" component={MembersScreen} options={{ title: t('nav.members') }} />
      <AppStack.Screen name="MemberDetail" component={MemberDetailScreen} options={{ title: t('nav.members') }} />
      <AppStack.Screen name="MemberForm" component={MemberFormScreen} options={{ title: t('members.add') }} />
      <AppStack.Screen name="Staff" component={StaffScreen} options={{ title: t('nav.staff') }} />
      <AppStack.Screen name="Trainers" component={TrainersScreen} options={{ title: t('nav.trainers') }} />
      <AppStack.Screen name="MyTargets" component={MyTargetsScreen} options={{ title: t('trainerTargets.title') }} />
      <AppStack.Screen name="TrainerMember" component={TrainerMemberScreen} options={{ title: t('trainer.member') }} />
      <AppStack.Screen name="TrainerMemberProgress" component={TrainerMemberProgressScreen} options={{ title: t('trainer.progress') }} />
      <AppStack.Screen name="TrainerMemberPlan" component={TrainerMemberPlanScreen} options={{ title: t('trainer.plans') }} />
      <AppStack.Screen name="TrainerSession" component={TrainerSessionScreen} options={{ title: t('trainer.session') }} />
      <AppStack.Screen name="TrainerActive" component={TrainerActiveScreen} options={{ title: t('trainer.activeSession') }} />
      <AppStack.Screen name="TrainerAlerts" component={TrainerAlertsScreen} options={{ title: t('trainer.alerts') }} />
      <AppStack.Screen name="Memberships" component={PackagesScreen} options={{ title: t('nav.memberships') }} />
      <AppStack.Screen name="Tasks" component={TasksScreen} options={{ title: t('nav.tasks') }} />
      <AppStack.Screen name="ApprovalDetail" component={ApprovalDetailScreen} options={{ title: t('tasks.approvals') }} />
      <AppStack.Screen name="PackageForm" component={PackageFormScreen} options={{ title: t('memberships.edit') }} />
      <AppStack.Screen name="Leads" component={LeadsScreen} options={{ title: t('nav.leads') }} />
      <AppStack.Screen name="LeadDetail" component={LeadDetailScreen} options={{ title: t('nav.leads') }} />
      <AppStack.Screen name="LeadForm" component={LeadFormScreen} options={{ title: t('leads.add') }} />
      <AppStack.Screen name="Expenses" component={ExpensesScreen} options={{ title: t('nav.expenses') }} />
      <AppStack.Screen name="ExpenseDetail" component={ExpenseDetailScreen} options={{ title: t('nav.expenses') }} />
      <AppStack.Screen name="ExpenseForm" component={ExpenseFormScreen} options={{ title: t('expenses.add') }} />
      <AppStack.Screen name="ExpenseCategories" component={ExpenseCategoriesScreen} options={{ title: t('expenses.categories') }} />
      <AppStack.Screen name="Ledger" component={LedgerScreen} options={{ title: t('nav.ledger') }} />
      <AppStack.Screen name="LedgerTransfer" component={LedgerTransferScreen} options={{ title: t('ledger.transfer') }} />
      <AppStack.Screen name="LedgerAccounts" component={LedgerAccountsScreen} options={{ title: t('ledger.accounts') }} />
      <AppStack.Screen name="Integrations" component={IntegrationsScreen} options={{ title: t('nav.integrations') }} />
      <AppStack.Screen name="Attendance" component={AttendanceScreen} options={{ title: t('nav.attendance') }} />
      <AppStack.Screen name="MyLeaves" component={MyLeavesScreen} options={{ title: t('nav.myLeaves') }} />
      <AppStack.Screen name="ApplyLeave" component={ApplyLeaveScreen} options={{ title: t('nav.applyLeave') }} />
      <AppStack.Screen name="LeaveDetail" component={LeaveDetailScreen} options={{ title: t('nav.myLeaves') }} />
      <AppStack.Screen name="BusinessSettings" component={BusinessSettingsScreen} options={{ title: t('nav.businessSettings') }} />
      <AppStack.Screen name="Licenses" component={LicenseScreen} options={{ title: t('nav.licenses') }} />
    </AppStack.Navigator>
  );
}

export function RootNavigator() {
  const theme = useTheme();
  const status = useSessionStore((state) => state.status);
  const navigationTheme: NavigationTheme = {
    ...DarkTheme,
    dark: theme.scheme === 'dark',
    colors: {
      ...DarkTheme.colors,
      background: theme.colors.background,
      card: theme.colors.surface,
      text: theme.colors.text,
      primary: theme.colors.accent,
      border: theme.colors.border,
      notification: theme.colors.accent,
    },
  };

  return (
    <NavigationContainer theme={navigationTheme}>
      <StatusBar style={theme.scheme === 'dark' ? 'light' : 'dark'} />
      {status === 'unknown' ? (
        <View style={{ alignItems: 'center', backgroundColor: theme.colors.background, flex: 1, justifyContent: 'center' }}>
          <ActivityIndicator color={theme.colors.accent} />
        </View>
      ) : status === 'authenticated' ? (
        <AuthenticatedStack />
      ) : (
        <AuthStack.Navigator screenOptions={{ headerShadowVisible: false }}>
          <AuthStack.Screen name="Login" component={LoginScreen} options={{ headerShown: false }} />
          <AuthStack.Screen name="Register" component={RegisterScreen} options={{ title: '' }} />
          <AuthStack.Screen name="ForgotPassword" component={ForgotPasswordScreen} options={{ title: '' }} />
        </AuthStack.Navigator>
      )}
    </NavigationContainer>
  );
}
