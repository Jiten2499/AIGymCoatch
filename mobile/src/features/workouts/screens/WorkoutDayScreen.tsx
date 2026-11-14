import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import { Pressable, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import { useExercise, useProgram, useStartProgram } from '../../fitness/api/queries';
import type { ProgramExercise } from '../../fitness/types';

type Props = NativeStackScreenProps<AppStackParamList, 'WorkoutDay'>;

export function WorkoutDayScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const program = useProgram(route.params.programId);
  const start = useStartProgram();
  const day = program.data?.days.find((item) => item.id === route.params.dayId) ?? null;

  return (
    <FeatureGate code={featureCodes.workoutPlans}>
      <ScreenState empty={program.isSuccess && !day} emptyBody={t('journey.dayMissing')} emptyTitle={t('journey.dayTitle')} error={program.isError ? errorText(program.error, t('common.offline')) : null} loading={program.isLoading} onRetry={() => void program.refetch()}>
        <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
          <AppText variant="title">{day?.name}</AppText>
          {day?.description ? <AppText>{day.description}</AppText> : null}
          <AppText tone="muted">{day?.restDay ? t('journey.restDay') : t('journey.exercisesDone', { count: day?.exercises.length ?? 0 })}</AppText>
          {start.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(start.error, t('common.offline'))}</AppText> : null}
          {day?.exercises.map((exercise) => (
            <DayExercise exercise={exercise} key={exercise.id} onOpen={() => navigation.navigate('ExerciseDetail', { exerciseId: exercise.exerciseId })} />
          ))}
          {day && !day.restDay ? (
            <AppButton label={t('journey.startWorkout')} loading={start.isPending} onPress={() => {
              start.mutate({ programId: route.params.programId, dayId: day.id }, { onSuccess: (session) => navigation.navigate('WorkoutSession', { sessionId: session.id }) });
            }} />
          ) : null}
        </ScrollView>
      </ScreenState>
    </FeatureGate>
  );
}

function DayExercise({ exercise, onOpen }: { exercise: ProgramExercise; onOpen: () => void }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const detail = useExercise(exercise.exerciseId);
  const targets = [
    exercise.sets ? t('journey.sets', { count: exercise.sets }) : null,
    exercise.targetReps ? t('journey.reps', { count: exercise.targetReps }) : null,
    exercise.targetWeight !== null ? `${t('journey.weightLabel')}: ${exercise.targetWeight}` : null,
    exercise.targetDurationSeconds ? t('journey.minutes', { count: Math.round(exercise.targetDurationSeconds / 60) }) : null,
    exercise.restSeconds ? t('journey.rest', { count: exercise.restSeconds }) : null,
  ].filter(Boolean).join(' · ');
  return (
    <Pressable accessibilityRole="button" onPress={onOpen}>
      <AppCard>
        <View style={{ gap: theme.spacing.xs, minHeight: theme.control.minTouch, justifyContent: 'center' }}>
          <AppText variant="label">{detail.data?.name ?? t('common.loading')}</AppText>
          {targets ? <AppText tone="muted">{targets}</AppText> : null}
          {exercise.notes ? <AppText tone="muted">{exercise.notes}</AppText> : null}
        </View>
      </AppCard>
    </Pressable>
  );
}
