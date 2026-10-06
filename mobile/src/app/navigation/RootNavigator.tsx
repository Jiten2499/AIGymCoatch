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
import { AttendanceScreen, BusinessDashboardScreen, BusinessSettingsScreen, MembersScreen, MembershipsScreen, StaffScreen, TrainersScreen } from '../../features/business/screens/BusinessScreens';
import { ChallengeDetailScreen } from '../../features/challenges/screens/ChallengeDetailScreen';
import { ChallengesScreen } from '../../features/challenges/screens/ChallengesScreen';
import { DashboardScreen } from '../../features/dashboard/screens/DashboardScreen';
import { FitnessScreen } from '../../features/fitness/screens/FitnessScreen';
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
import { useSessionStore } from '../../store/sessionStore';
import { useTheme } from '../providers/ThemeProvider';
import type { AppStackParamList, AuthStackParamList, MemberTabParamList } from './types';

const AuthStack = createNativeStackNavigator<AuthStackParamList>();
const AppStack = createNativeStackNavigator<AppStackParamList>();
const Tabs = createBottomTabNavigator<MemberTabParamList>();

function MemberTabs() {
  const { t } = useTranslation();
  return (
    <Tabs.Navigator screenOptions={{ headerShadowVisible: false }}>
      <Tabs.Screen name="Dashboard" component={DashboardScreen} options={{ title: t('nav.dashboard') }} />
      <Tabs.Screen name="Workouts" component={WorkoutsScreen} options={{ title: t('nav.workouts') }} />
      <Tabs.Screen name="Nutrition" component={NutritionScreen} options={{ title: t('nav.nutrition') }} />
      <Tabs.Screen name="Progress" component={ProgressScreen} options={{ title: t('nav.progress') }} />
      <Tabs.Screen name="More" component={MoreScreen} options={{ title: t('nav.more') }} />
    </Tabs.Navigator>
  );
}

function AuthenticatedStack() {
  const { t } = useTranslation();
  return (
    <AppStack.Navigator screenOptions={{ headerShadowVisible: false }}>
      <AppStack.Screen name="Tabs" component={MemberTabs} options={{ headerShown: false }} />
      <AppStack.Screen name="Fitness" component={FitnessScreen} options={{ title: t('nav.fitness') }} />
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
      <AppStack.Screen name="Profile" component={ProfileScreen} options={{ title: t('nav.profile') }} />
      <AppStack.Screen name="Settings" component={SettingsScreen} options={{ title: t('nav.settings') }} />
      <AppStack.Screen name="BusinessDashboard" component={BusinessDashboardScreen} options={{ title: t('nav.business') }} />
      <AppStack.Screen name="Members" component={MembersScreen} options={{ title: t('nav.members') }} />
      <AppStack.Screen name="Staff" component={StaffScreen} options={{ title: t('nav.staff') }} />
      <AppStack.Screen name="Trainers" component={TrainersScreen} options={{ title: t('nav.trainers') }} />
      <AppStack.Screen name="Memberships" component={MembershipsScreen} options={{ title: t('nav.memberships') }} />
      <AppStack.Screen name="Attendance" component={AttendanceScreen} options={{ title: t('nav.attendance') }} />
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
