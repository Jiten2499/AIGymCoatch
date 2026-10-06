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
import { featureCodes } from '../../../constants/access';
import { formatNumber } from '../../../services/localization/format';
import { errorText, isNotFound } from '../../../services/api/problem';
import { useWorkspaceFeatures } from '../../licensing/api/featureQuery';
import { useDashboard, useFitnessProfile, usePrograms } from '../../fitness/api/queries';
import { useSessionStore } from '../../../store/sessionStore';
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
  const profile = useFitnessProfile();
  const dashboard = useDashboard();
  const programs = usePrograms();
  const missingProfile = profile.isError && isNotFound(profile.error);
  const error = dashboard.isError ? errorText(dashboard.error, t('common.offline')) : null;
  const today = programs.data?.items[0]?.days.find((day) => !day.restDay) ?? null;
  const recent = dashboard.data?.recentWorkouts[0] ?? null;

  return (
    <ScreenState error={error} loading={dashboard.isLoading} onRetry={() => void dashboard.refetch()}>
      <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
        <View style={{ gap: theme.spacing.xs }}>
          <AppText variant="display">{t('journey.greeting', { name: user?.firstName ?? '' })}</AppText>
          <AppText tone="muted">{t('journey.today')}</AppText>
        </View>
        <WorkspaceSwitcher onSelect={(workspace) => void switchWorkspace(workspace.id).then(applyAuth)} />
        {missingProfile ? (
          <AppCard>
            <AppText>{t('journey.profileEmpty')}</AppText>
            <AppButton label={t('journey.completeProfile')} onPress={() => navigation.navigate('Fitness')} />
          </AppCard>
        ) : null}
        <AppCard>
          <AppText variant="label">{t('journey.currentGoal')}</AppText>
          <AppText>{dashboard.data?.currentGoal ? t(`journey.goalType.${dashboard.data.currentGoal.goalType}`) : t('journey.noGoal')}</AppText>
          {!dashboard.data?.currentGoal ? <AppButton label={t('journey.setGoal')} onPress={() => navigation.navigate('Fitness')} variant="secondary" /> : null}
        </AppCard>
        <AppCard>
          <AppText variant="label">{t('journey.thisWeek')}</AppText>
          <AppText>{t('journey.workoutsCount', { count: dashboard.data?.weeklyWorkouts ?? 0 })}</AppText>
          <AppText tone="muted">{t('journey.streak', { count: dashboard.data?.streakDays ?? 0 })}</AppText>
        </AppCard>
        <AppCard>
          <AppText variant="label">{today ? today.name : t('journey.recent')}</AppText>
          {recent?.status === 'InProgress' ? (
            <AppButton label={t('journey.continueWorkout')} onPress={() => navigation.navigate('WorkoutSession', { sessionId: recent.id })} />
          ) : today && programs.data?.items[0] ? (
            <AppButton label={t('journey.startWorkout')} onPress={() => navigation.navigate('WorkoutPlan', { programId: programs.data.items[0].id })} />
          ) : (
            <AppText tone="muted">{t('journey.noWorkout')}</AppText>
          )}
          <AppButton label={t('journey.openCoach')} onPress={() => navigation.navigate('AiCoach')} variant="secondary" />
          <AppButton label={t('journey.historyTitle')} onPress={() => navigation.navigate('WorkoutHistory')} variant="secondary" />
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
