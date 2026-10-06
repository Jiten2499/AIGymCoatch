import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { KeyboardAvoidingView, Platform, ScrollView, View } from 'react-native';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FormField } from '../../../components/forms/FormField';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import { useAddSet, useCompleteSession, useExercise, useProgram, useSession } from '../../fitness/api/queries';

const schema = z.object({
  reps: z.string(),
  weight: z.string(),
  durationSeconds: z.string(),
  distance: z.string(),
});

type SetValues = z.infer<typeof schema>;

type Props = NativeStackScreenProps<AppStackParamList, 'WorkoutSession'>;

export function WorkoutSessionScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const session = useSession(route.params.sessionId);
  const program = useProgram(session.data?.workoutProgramId ?? '');
  const addSet = useAddSet(route.params.sessionId);
  const complete = useCompleteSession();
  const [index, setIndex] = useState(0);
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<SetValues>({ resolver: zodResolver(schema), defaultValues: { reps: '', weight: '', durationSeconds: '', distance: '' } });
  const current = session.data?.exercises[index] ?? null;
  const exercise = useExercise(current?.exerciseId ?? '');
  const target = useMemo(() => {
    const day = program.data?.days.find((item) => item.id === session.data?.workoutProgramDayId);
    return day?.exercises.find((item) => item.exerciseId === current?.exerciseId) ?? null;
  }, [program.data, session.data, current?.exerciseId]);
  const logged = current?.sets.length ?? 0;
  const targetSets = target?.sets ?? null;

  const submit = form.handleSubmit((values) => {
    if (!current) return;
    const reps = optionalInt(values.reps);
    const weight = optionalDecimal(values.weight);
    const durationSeconds = optionalInt(values.durationSeconds);
    const distance = optionalDecimal(values.distance);
    if (reps === 'invalid' || weight === 'invalid' || durationSeconds === 'invalid' || distance === 'invalid') {
      setFormError(t('journey.invalidNumber'));
      return;
    }
    if (reps === null && weight === null && durationSeconds === null && distance === null) {
      setFormError(t('journey.needValue'));
      return;
    }
    setFormError(null);
    addSet.mutate({
      sessionExerciseId: current.id,
      body: { reps, weight, durationSeconds, distance, rpe: null, completed: true },
    }, {
      onSuccess: () => {
        form.reset();
        if (targetSets !== null && logged + 1 >= targetSets && index < (session.data?.exercises.length ?? 1) - 1) {
          setIndex(index + 1);
        }
      },
    });
  });

  const exerciseCount = session.data?.exercises.length ?? 0;

  return (
    <FeatureGate code={featureCodes.workoutPlans}>
    <ScreenState error={session.isError ? errorText(session.error, t('common.offline')) : null} loading={session.isLoading} onRetry={() => void session.refetch()}>
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1 }}>
        <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }} keyboardShouldPersistTaps="handled">
          <AppText tone="muted">{program.data?.name}</AppText>
          <AppText variant="title">{exercise.data?.name ?? t('journey.sessionTitle')}</AppText>
          {exerciseCount > 0 ? <AppText tone="muted">{t('journey.exerciseOf', { current: index + 1, total: exerciseCount })}</AppText> : null}
          <AppText tone="muted">{targetSets ? t('journey.setOf', { current: Math.min(logged + 1, targetSets), total: targetSets }) : t('journey.setsDone', { count: logged })}</AppText>
          {target?.targetReps ? <AppText tone="muted">{t('journey.reps', { count: target.targetReps })}</AppText> : null}
          {target?.targetWeight ? <AppText tone="muted">{t('journey.weightLabel')}: {target.targetWeight}</AppText> : null}
          {exercise.data?.instructions ? <AppText>{exercise.data.instructions}</AppText> : null}
          <FormField label={t('journey.repsLabel')}>
            <AppTextInput accessibilityLabel={t('journey.repsLabel')} keyboardType="number-pad" onChangeText={(value) => form.setValue('reps', value)} value={form.watch('reps')} />
          </FormField>
          <FormField label={t('journey.weightLabel')}>
            <AppTextInput accessibilityLabel={t('journey.weightLabel')} keyboardType="decimal-pad" onChangeText={(value) => form.setValue('weight', value)} value={form.watch('weight')} />
          </FormField>
          <FormField label={t('journey.durationLabel')}>
            <AppTextInput accessibilityLabel={t('journey.durationLabel')} keyboardType="number-pad" onChangeText={(value) => form.setValue('durationSeconds', value)} value={form.watch('durationSeconds')} />
          </FormField>
          <FormField label={t('journey.distanceLabel')}>
            <AppTextInput accessibilityLabel={t('journey.distanceLabel')} keyboardType="decimal-pad" onChangeText={(value) => form.setValue('distance', value)} value={form.watch('distance')} />
          </FormField>
          {formError ? <AppText accessibilityRole="alert" tone="danger">{formError}</AppText> : null}
          {addSet.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(addSet.error, t('common.offline'))}</AppText> : null}
          {addSet.isSuccess ? <AppText tone="accent">{t('journey.logged')}</AppText> : null}
          <AppButton label={t('journey.logSet')} loading={addSet.isPending} onPress={submit} />
          {index < (session.data?.exercises.length ?? 0) - 1 ? (
            <AppButton label={t('journey.nextExercise')} onPress={() => setIndex(index + 1)} variant="secondary" />
          ) : null}
          <View>
            <AppButton label={t('journey.finish')} loading={complete.isPending} onPress={() => {
              complete.mutate(route.params.sessionId, { onSuccess: () => navigation.replace('WorkoutSummary', { sessionId: route.params.sessionId }) });
            }} variant="secondary" />
            {complete.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(complete.error, t('common.offline'))}</AppText> : null}
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </ScreenState>
    </FeatureGate>
  );
}

function optionalInt(value: string): number | null | 'invalid' {
  const trimmed = value.trim();
  if (!trimmed) return null;
  if (!/^\d+$/.test(trimmed)) return 'invalid';
  return Number(trimmed);
}

function optionalDecimal(value: string): number | null | 'invalid' {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const parsed = Number(trimmed);
  if (!Number.isFinite(parsed) || parsed < 0) return 'invalid';
  return parsed;
}
