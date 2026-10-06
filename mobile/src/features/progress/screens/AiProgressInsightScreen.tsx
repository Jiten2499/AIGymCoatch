import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { ScrollView } from 'react-native';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import { progressInsight } from '../../aiCoach/api/ai.api';
import { useDashboard, useRecords } from '../../fitness/api/queries';

export function AiProgressInsightScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const dashboard = useDashboard();
  const records = useRecords();
  const insight = useMutation({ mutationFn: () => progressInsight() });
  const loading = dashboard.isLoading || records.isLoading;
  const error = dashboard.error ?? records.error;

  return (
    <FeatureGate code={featureCodes.aiProgressInsights}>
      <ScreenState error={error ? errorText(error, t('common.offline')) : null} loading={loading} onRetry={() => { void dashboard.refetch(); void records.refetch(); }}>
        <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
          <AppText variant="title">{t('journey.measured')}</AppText>
          <AppCard>
            <AppText>{t('journey.workoutsCount', { count: dashboard.data?.weeklyWorkouts ?? 0 })}</AppText>
            <AppText tone="muted">{t('journey.streak', { count: dashboard.data?.streakDays ?? 0 })}</AppText>
            {dashboard.data?.latestMeasurement?.weight !== null && dashboard.data?.latestMeasurement ? (
              <AppText>{t('journey.weight')}: {dashboard.data.latestMeasurement.weight}</AppText>
            ) : null}
          </AppCard>
          <AppCard>
            <AppText variant="label">{t('journey.records')}</AppText>
            {(records.data?.length ?? 0) === 0 ? <AppText tone="muted">{t('journey.noRecords')}</AppText> : records.data?.map((record) => (
              <AppText key={record.exerciseId}>{record.exerciseName}{record.maxWeight !== null ? ` · ${record.maxWeight}` : ''}{record.maxReps !== null ? ` · ${record.maxReps}` : ''}</AppText>
            ))}
          </AppCard>
          <AppText variant="title">{t('journey.aiLabel')}</AppText>
          <AppText tone="muted">{t('journey.safety')}</AppText>
          {insight.data ? (
            <AppCard>
              <AppText>{insight.data.message}</AppText>
            </AppCard>
          ) : <AppText tone="muted">{t('journey.insightEmpty')}</AppText>}
          {insight.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(insight.error, t('common.offline'))}</AppText> : null}
          <AppButton label={insight.isError ? t('common.retry') : t('journey.feedback')} loading={insight.isPending} onPress={() => insight.mutate()} />
        </ScrollView>
      </ScreenState>
    </FeatureGate>
  );
}
