import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import { Pressable, ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { formatDate } from '../../../services/localization/format';
import { errorText } from '../../../services/api/problem';
import { useExercise, useSession } from '../../fitness/api/queries';
import type { WorkoutSet } from '../../fitness/types';

type Props = NativeStackScreenProps<AppStackParamList, 'SessionDetail'>;

export function SessionDetailScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const { t, i18n } = useTranslation();
  const session = useSession(route.params.sessionId);
  const data = session.data;
  const when = data?.completedAt ?? data?.startedAt ?? null;
  const minutes = data?.durationSeconds ? Math.max(1, Math.round(data.durationSeconds / 60)) : null;

  return (
    <FeatureGate code={featureCodes.workoutPlans}>
      <ScreenState error={session.isError ? errorText(session.error, t('common.offline')) : null} loading={session.isLoading} onRetry={() => void session.refetch()}>
        <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
          <AppText variant="title">{t('journey.sessionDetail')}</AppText>
          {when ? <AppText tone="muted">{formatDate(new Date(when), i18n.language)}</AppText> : null}
          {data ? <AppText>{t(`journey.sessionStatus.${data.status}`)}</AppText> : null}
          {minutes !== null ? <AppText>{t('journey.durationDone', { count: minutes })}</AppText> : null}
          {data?.exercises.map((exercise) => (
            <Pressable accessibilityRole="button" key={exercise.id} onPress={() => navigation.navigate('ExerciseDetail', { exerciseId: exercise.exerciseId })}>
              <SessionExercise exerciseId={exercise.exerciseId} sets={exercise.sets} />
            </Pressable>
          ))}
        </ScrollView>
      </ScreenState>
    </FeatureGate>
  );
}

function SessionExercise({ exerciseId, sets }: { exerciseId: string; sets: WorkoutSet[] }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const detail = useExercise(exerciseId);
  return (
    <AppCard>
      <AppText variant="label">{detail.data?.name ?? t('common.loading')}</AppText>
      {sets.map((set) => (
        <AppText key={set.id} tone="muted">
          {t('journey.setLine', { number: set.setNumber, reps: set.reps ?? '—', weight: set.weight ?? '—' })}
        </AppText>
      ))}
      {sets.length === 0 ? <AppText tone="muted">{t('journey.setsDone', { count: 0 })}</AppText> : null}
      <AppText style={{ marginTop: theme.spacing.xs }} tone="muted">{t('journey.recordedOnly')}</AppText>
    </AppCard>
  );
}
