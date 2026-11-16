import type { CompositeNavigationProp } from '@react-navigation/native';
import type { BottomTabNavigationProp } from '@react-navigation/bottom-tabs';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useNavigation } from '@react-navigation/native';
import { useTranslation } from 'react-i18next';
import { ScrollView, View } from 'react-native';
import type { AppStackParamList, MemberTabParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { useMyProfilePhoto } from '../../profile/photo';
import { useMyMemberPhoto } from '../../members/myPhoto';
import { featureCodes } from '../../../constants/access';
import { formatNumber } from '../../../services/localization/format';
import { errorText, isNotFound } from '../../../services/api/problem';
import { useWorkspaceFeatures } from '../../licensing/api/featureQuery';
import { useFitnessAssessmentCompletion } from '../../assessment/api/assessment.queries';
import { useDashboard, useFitnessProfile, usePrograms, useSessions } from '../../fitness/api/queries';
import { CoachPhoto } from '../../plan/heroes';
import { completedDayIds, nextTrainingDay } from '../../plan/planModel';
import { useSessionStore } from '../../../store/sessionStore';
import { useWorkspaceStore } from '../../../store/workspaceStore';
import { WorkspaceSwitcher } from '../../tenant/WorkspaceSwitcher';
import { applyAuth } from '../../../services/auth/session';
import { switchWorkspace } from '../../auth/api/auth.api';

type DashboardNavigation = CompositeNavigationProp<
  BottomTabNavigationProp<MemberTabParamList, 'Dashboard'>,
  NativeStackNavigationProp<AppStackParamList>
>;

export function DashboardScreen() {
  const theme = useTheme();
  const { t, i18n } = useTranslation();
  const navigation = useNavigation<DashboardNavigation>();
  const features = useWorkspaceFeatures();
  const user = useSessionStore((state) => state.user);
  const photo = useMyProfilePhoto();
  const memberPhoto = useMyMemberPhoto();
  const current = useSessionStore((state) => state.currentTenant);
  const workspaces = useWorkspaceStore((state) => state.workspaces);
  const isTrainer = workspaces.some((workspace) => workspace.id === current?.id && workspace.roles.includes('Trainer'));
  const profile = useFitnessProfile();
  const assessmentCompletion = useFitnessAssessmentCompletion();
  const dashboard = useDashboard();
  const programs = usePrograms();
  const sessions = useSessions(1);
  const activePlan = programs.data?.items[0] ?? null;
  const nextDay = activePlan ? nextTrainingDay(activePlan, completedDayIds(activePlan, sessions.data?.items ?? [])) : null;
  const missingProfile = profile.isError && isNotFound(profile.error);
  const needsAssessment = missingProfile || (assessmentCompletion.data != null && !assessmentCompletion.data.isCompleted);
  const error = dashboard.isError ? errorText(dashboard.error, t('common.offline')) : null;
  const today = nextDay;
  const recent = dashboard.data?.recentWorkouts[0] ?? null;

  return (
    <ScreenState error={error} loading={dashboard.isLoading} onRetry={() => void dashboard.refetch()}>
      <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
        <View style={{ alignItems: 'center', flexDirection: 'row', gap: theme.spacing.md }}>
          <View style={{ flex: 1, gap: theme.spacing.xs }}>
            <AppText variant="display">{t('journey.greeting', { name: user?.firstName ?? '' })}</AppText>
            <AppText tone="muted">{t('journey.today')}</AppText>
          </View>
          <CoachPhoto uri={memberPhoto ?? photo.data} />
        </View>
        <WorkspaceSwitcher onSelect={(workspace) => void switchWorkspace(workspace.id).then(applyAuth)} />
        {isTrainer ? (
          <AppCard>
            <AppText variant="label">{t('trainerTargets.title')}</AppText>
            <AppText tone="muted">{t('trainerTargets.emptyBody')}</AppText>
            <AppButton label={t('trainerTargets.title')} onPress={() => navigation.navigate('MyTargets')} />
          </AppCard>
        ) : null}
        {needsAssessment ? (
          <AppCard>
            <AppText>{t('journey.profileEmpty')}</AppText>
            <AppButton label={t('journey.completeProfile')} onPress={() => navigation.navigate('FitnessAssessmentIntro')} />
          </AppCard>
        ) : null}
        <AppCard>
          <AppText variant="label">{t('journey.currentGoal')}</AppText>
          <AppText>{dashboard.data?.currentGoal ? t(`journey.goalType.${dashboard.data.currentGoal.goalType}`) : t('journey.noGoal')}</AppText>
          {!dashboard.data?.currentGoal ? <AppButton label={t('journey.setGoal')} onPress={() => navigation.navigate('Fitness')} variant="secondary" /> : null}
        </AppCard>
        {activePlan ? (
          <AppCard>
            <AppText variant="label">{t('journey.planCard')}</AppText>
            <AppText>{activePlan.name}</AppText>
            <AppText tone="accent">{t('journey.planActive')}</AppText>
            <AppText tone="muted">{t('journey.planDay', { current: today?.dayNumber ?? activePlan.days.length, total: activePlan.days.length })}</AppText>
          </AppCard>
        ) : null}
        <AppCard>
          <AppText variant="label">{t('journey.thisWeek')}</AppText>
          <AppText>{t('journey.workoutsCount', { count: dashboard.data?.weeklyWorkouts ?? 0 })}</AppText>
          <AppText tone="muted">{t('journey.streak', { count: dashboard.data?.streakDays ?? 0 })}</AppText>
        </AppCard>
        <AppCard>
          <AppText variant="label">{today ? today.name : t('journey.recent')}</AppText>
          {recent?.status === 'InProgress' ? (
            <AppButton label={t('journey.continueWorkout')} onPress={() => navigation.navigate('WorkoutSession', { sessionId: recent.id })} />
          ) : today && activePlan ? (
            <AppButton label={t('journey.continueWorkout')} onPress={() => navigation.navigate('WorkoutDay', { programId: activePlan.id, dayId: today.id })} />
          ) : (
            <>
              <AppText tone="muted">{t('journey.noWorkout')}</AppText>
              <AppButton label={t('journey.generate')} onPress={() => navigation.navigate('PlanGenerate')} />
            </>
          )}
          {activePlan ? (
            <AppButton label={t('journey.quickPlan')} onPress={() => navigation.navigate('WorkoutPlan', { programId: activePlan.id })} variant="secondary" />
          ) : null}
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.sm }}>
            <AppButton label={t('journey.quickAssessment')} onPress={() => navigation.navigate('FitnessAssessmentIntro')} variant="secondary" />
            <AppButton label={t('journey.quickProgress')} onPress={() => navigation.navigate('Progress')} variant="secondary" />
            <AppButton label={t('journey.quickNutrition')} onPress={() => navigation.navigate('Nutrition')} variant="secondary" />
          </View>
          <View style={{ gap: theme.spacing.sm }}>
            <AppButton label={t('journey.openCoach')} onPress={() => navigation.navigate('AiCoach')} variant="secondary" />
            <AppButton label={t('journey.historyTitle')} onPress={() => navigation.navigate('WorkoutHistory')} variant="secondary" />
          </View>
        </AppCard>
        {dashboard.data?.activeChallenge ? (
          <AppCard>
            <AppText variant="label">{t('nav.challenges')}</AppText>
            <AppText>{dashboard.data.activeChallenge.name}</AppText>
            {dashboard.data.activeChallenge.currentValue !== null ? (
              <AppText tone="muted">{t('challenge.progress', { current: formatNumber(dashboard.data.activeChallenge.currentValue, i18n.language), target: formatNumber(dashboard.data.activeChallenge.targetValue, i18n.language) })}</AppText>
            ) : null}
            <AppButton label={t('nav.challenges')} onPress={() => navigation.navigate('Challenges')} variant="secondary" />
          </AppCard>
        ) : null}
        {features.data?.[featureCodes.nutrition] === true && dashboard.data ? (
          <AppCard>
            <AppText variant="label">{t('nav.nutrition')}</AppText>
            <AppText>
              {dashboard.data.nutritionToday.calorieTarget > 0
                ? t('nutrition.consumedOfTarget', { consumed: formatNumber(dashboard.data.nutritionToday.calories, i18n.language), target: formatNumber(dashboard.data.nutritionToday.calorieTarget, i18n.language) })
                : formatNumber(dashboard.data.nutritionToday.calories, i18n.language)}
            </AppText>
            <AppText tone="muted">{t('nutrition.protein')}: {formatNumber(dashboard.data.nutritionToday.protein, i18n.language)}</AppText>
            <AppButton label={t('nav.nutrition')} onPress={() => navigation.navigate('Nutrition')} variant="secondary" />
          </AppCard>
        ) : null}
      </ScrollView>
    </ScreenState>
  );
}
