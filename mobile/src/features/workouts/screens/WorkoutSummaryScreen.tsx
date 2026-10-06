import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import { ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { errorText } from '../../../services/api/problem';
import { useExercise, useSession } from '../../fitness/api/queries';
import { progressInsight } from '../../aiCoach/api/ai.api';
import { useMutation } from '@tanstack/react-query';
import { featureCodes } from '../../../constants/access';
import { useWorkspaceFeatures } from '../../licensing/api/featureQuery';

type Props = NativeStackScreenProps<AppStackParamList, 'WorkoutSummary'>;

export function WorkoutSummaryScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const session = useSession(route.params.sessionId);
  const features = useWorkspaceFeatures();
  const insight = useMutation({ mutationFn: () => progressInsight() });
  const insightAllowed = features.data?.[featureCodes.aiProgressInsights] === true;
  const data = session.data;
  const sets = data?.exercises.reduce((total, exercise) => total + exercise.sets.length, 0) ?? 0;
  const minutes = data?.durationSeconds ? Math.max(1, Math.round(data.durationSeconds / 60)) : null;

  return (
    <ScreenState empty={session.isSuccess && !data} emptyBody={t('journey.noSummary')} emptyTitle={t('journey.summaryTitle')} error={session.isError ? errorText(session.error, t('common.offline')) : null} loading={session.isLoading} onRetry={() => void session.refetch()}>
      <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
        <AppText variant="title">{t('journey.summaryTitle')}</AppText>
        <AppCard>
          <AppText>{t('journey.exercisesDone', { count: data?.exercises.length ?? 0 })}</AppText>
          <AppText>{t('journey.setsDone', { count: sets })}</AppText>
          {minutes !== null ? <AppText>{t('journey.durationDone', { count: minutes })}</AppText> : null}
        </AppCard>
        {data?.exercises.map((exercise) => <SummaryExercise exerciseId={exercise.exerciseId} key={exercise.id} sets={exercise.sets.length} />)}
        <AppText variant="label">{t('journey.measured')}</AppText>
        <AppText tone="muted">{t('journey.safety')}</AppText>
        {insight.data ? (
          <AppCard>
            <AppText variant="label">{t('journey.aiLabel')}</AppText>
            <AppText>{insight.data.message}</AppText>
          </AppCard>
        ) : null}
        {insight.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(insight.error, t('common.offline'))}</AppText> : null}
        <View style={{ gap: theme.spacing.sm }}>
          {features.isSuccess && insightAllowed ? <AppButton label={t('journey.feedback')} loading={insight.isPending} onPress={() => insight.mutate()} /> : null}
          {features.isSuccess && !insightAllowed ? <AppText tone="muted">{t('journey.featureDisabled')}</AppText> : null}
          {features.data?.[featureCodes.aiCoach] === true ? <AppButton label={t('coach.ask')} onPress={() => navigation.navigate('AiCoach')} variant="secondary" /> : null}
          <AppButton label={t('nav.progress')} onPress={() => navigation.navigate('Tabs', { screen: 'Progress' })} variant="secondary" />
          <AppButton label={t('journey.historyTitle')} onPress={() => navigation.navigate('WorkoutHistory')} variant="secondary" />
          <AppButton label={t('journey.backHome')} onPress={() => navigation.navigate('Tabs', { screen: 'Dashboard' })} variant="secondary" />
        </View>
      </ScrollView>
    </ScreenState>
  );
}

function SummaryExercise({ exerciseId, sets }: { exerciseId: string; sets: number }) {
  const detail = useExercise(exerciseId);
  const { t } = useTranslation();
  return <AppText>{detail.data?.name ?? t('common.loading')} · {t('journey.setsDone', { count: sets })}</AppText>;
}
