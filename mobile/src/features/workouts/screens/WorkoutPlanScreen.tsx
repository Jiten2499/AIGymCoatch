import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { errorText } from '../../../services/api/problem';
import { useExercise, useProgram, useStartProgram } from '../../fitness/api/queries';
import type { ProgramExercise } from '../../fitness/types';

type Props = NativeStackScreenProps<AppStackParamList, 'WorkoutPlan'>;

export function WorkoutPlanScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const program = useProgram(route.params.programId);
  const start = useStartProgram();
  const [openDay, setOpenDay] = useState<string | null>(null);
  const data = program.data;

  return (
    <ScreenState error={program.isError ? errorText(program.error, t('common.offline')) : null} loading={program.isLoading} onRetry={() => void program.refetch()}>
      <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
        <View style={{ gap: theme.spacing.xs }}>
          <AppText variant="title">{data?.name}</AppText>
          <AppText tone="muted">{data ? t(`journey.goalType.${data.goalType}`) : ''}</AppText>
          <AppText tone="muted">{data ? `${t('journey.weeks', { count: data.durationWeeks })} · ${t('journey.perWeek', { count: data.frequencyPerWeek })}` : ''}</AppText>
          {data?.description ? <AppText>{data.description}</AppText> : null}
        </View>
        {start.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(start.error, t('common.offline'))}</AppText> : null}
        {data?.days.map((day) => (
          <AppCard key={day.id}>
            <Pressable accessibilityLabel={day.name} accessibilityRole="button" onPress={() => setOpenDay(openDay === day.id ? null : day.id)} style={{ minHeight: theme.control.minTouch, justifyContent: 'center' }}>
              <AppText variant="label">{day.name}</AppText>
              <AppText tone="muted">{day.restDay ? t('journey.restDay') : t('journey.exercisesDone', { count: day.exercises.length })}</AppText>
            </Pressable>
            {openDay === day.id && !day.restDay ? day.exercises.map((exercise) => <PlanExercise exercise={exercise} key={exercise.id} onOpen={() => navigation.navigate('ExerciseDetail', { exerciseId: exercise.exerciseId })} />) : null}
            {!day.restDay ? (
              <AppButton label={t('journey.viewDay')} onPress={() => navigation.navigate('WorkoutDay', { programId: route.params.programId, dayId: day.id })} variant="secondary" />
            ) : null}
            {!day.restDay ? (
              <AppButton label={t('journey.startWorkout')} loading={start.isPending} onPress={() => {
                start.mutate({ programId: route.params.programId, dayId: day.id }, { onSuccess: (session) => navigation.navigate('WorkoutSession', { sessionId: session.id }) });
              }} />
            ) : null}
          </AppCard>
        ))}
      </ScrollView>
    </ScreenState>
  );
}

function PlanExercise({ exercise, onOpen }: { exercise: ProgramExercise; onOpen: () => void }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const detail = useExercise(exercise.exerciseId);
  return (
    <View style={{ gap: 4, paddingVertical: 8 }}>
      <Pressable accessibilityRole="button" onPress={onOpen} style={{ justifyContent: 'center', minHeight: theme.control.minTouch }}>
        <AppText>{detail.data?.name ?? t('common.loading')}</AppText>
      </Pressable>
      <AppText tone="muted">{[exercise.sets ? t('journey.sets', { count: exercise.sets }) : null, exercise.targetReps ? t('journey.reps', { count: exercise.targetReps }) : null, exercise.restSeconds ? t('journey.rest', { count: exercise.restSeconds }) : null, detail.data?.equipment].filter(Boolean).join(' · ')}</AppText>
      {detail.data?.instructions ? <AppText tone="muted">{detail.data.instructions}</AppText> : null}
    </View>
  );
}
