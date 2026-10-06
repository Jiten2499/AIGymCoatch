import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useNavigation } from '@react-navigation/native';
import { useTranslation } from 'react-i18next';
import { ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import { useDashboard, useMeasurements, useRecords } from '../../fitness/api/queries';
import { useWorkspaceFeatures } from '../../licensing/api/featureQuery';

export function ProgressScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const navigation = useNavigation<NativeStackNavigationProp<AppStackParamList>>();
  const features = useWorkspaceFeatures();
  const dashboard = useDashboard();
  const measurements = useMeasurements();
  const records = useRecords();
  const weights = (measurements.data?.items ?? []).filter((item) => item.weight !== null).slice().reverse();
  const loading = dashboard.isLoading || measurements.isLoading || records.isLoading;
  const error = dashboard.error ?? measurements.error ?? records.error;
  const empty = dashboard.isSuccess && measurements.isSuccess && records.isSuccess && (dashboard.data.weeklyWorkouts + dashboard.data.monthlyWorkouts) === 0 && weights.length === 0 && (records.data?.length ?? 0) === 0;

  return (
    <ScreenState error={error ? errorText(error, t('common.offline')) : null} loading={loading} onRetry={() => { void dashboard.refetch(); void measurements.refetch(); void records.refetch(); }}>
      <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
        <AppText variant="title">{t('nav.progress')}</AppText>
        {empty ? <AppText tone="muted">{t('journey.progressEmpty')}</AppText> : null}
        <AppCard>
          <AppText variant="label">{t('journey.consistency')}</AppText>
          <AppText>{t('journey.workoutsCount', { count: dashboard.data?.weeklyWorkouts ?? 0 })}</AppText>
          <AppText tone="muted">{t('journey.monthly', { count: dashboard.data?.monthlyWorkouts ?? 0 })}</AppText>
          <AppText tone="muted">{t('journey.streak', { count: dashboard.data?.streakDays ?? 0 })}</AppText>
          {dashboard.data?.currentGoal ? <AppText>{t(`journey.goalType.${dashboard.data.currentGoal.goalType}`)}</AppText> : null}
        </AppCard>
        <AppCard>
          <AppText variant="label">{t('journey.weightTrend')}</AppText>
          {weights.length < 2 ? <AppText tone="muted">{t('journey.trendEmpty')}</AppText> : <WeightBars points={weights.map((item) => ({ date: item.measurementDate, weight: item.weight ?? 0 }))} />}
        </AppCard>
        <AppCard>
          <AppText variant="label">{t('journey.records')}</AppText>
          {(records.data?.length ?? 0) === 0 ? <AppText tone="muted">{t('journey.noRecords')}</AppText> : records.data?.map((record) => (
            <AppText key={record.exerciseId}>{record.exerciseName}{record.maxWeight !== null ? ` · ${record.maxWeight}` : ''}{record.maxReps !== null ? ` · ${record.maxReps}` : ''}</AppText>
          ))}
        </AppCard>
        <AppButton label={t('journey.measurements')} onPress={() => navigation.navigate('Measurements')} variant="secondary" />
        <AppButton label={t('journey.historyTitle')} onPress={() => navigation.navigate('WorkoutHistory')} variant="secondary" />
        <AppButton label={t('journey.aiLabel')} onPress={() => navigation.navigate('AiInsight')} variant="secondary" />
        {features.data?.[featureCodes.aiCoach] === true ? <AppButton label={t('coach.discussProgress')} onPress={() => navigation.navigate('AiCoach')} variant="secondary" /> : null}
      </ScrollView>
    </ScreenState>
  );
}

function WeightBars({ points }: { points: { date: string; weight: number }[] }) {
  const theme = useTheme();
  const max = Math.max(...points.map((point) => point.weight), 1);
  return (
    <View style={{ gap: theme.spacing.sm }}>
      {points.map((point) => (
        <View accessibilityLabel={`${point.date} ${point.weight}`} key={point.date} style={{ gap: theme.spacing.xs }}>
          <AppText tone="muted">{point.date} · {point.weight}</AppText>
          <View style={{ backgroundColor: theme.colors.accent, borderRadius: theme.radius.sm, height: 8, width: `${Math.max(8, (point.weight / max) * 100)}%` }} />
        </View>
      ))}
    </View>
  );
}
