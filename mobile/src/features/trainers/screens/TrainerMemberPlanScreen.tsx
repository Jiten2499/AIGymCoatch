import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { FormField } from '../../../components/forms/FormField';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { errorText } from '../../../services/api/problem';
import { useExercise } from '../../fitness/api/queries';
import type { ProgramDay, ProgramExercise, WorkoutProgram } from '../../fitness/types';
import { useMemberProgram, useUpdateMemberProgram } from '../api/workspace.queries';
import { TrainerCard, useTrainerLayout } from '../ui';

type Props = NativeStackScreenProps<AppStackParamList, 'TrainerMemberPlan'>;

type DraftExercise = {
  exerciseId: string;
  sets: string;
  targetReps: string;
  targetDurationSeconds: string;
  targetDistance: string;
  targetWeight: string;
  restSeconds: string;
  notes: string;
};

type DraftDay = {
  dayNumber: number;
  name: string;
  description: string;
  restDay: boolean;
  exercises: DraftExercise[];
};

type Draft = {
  name: string;
  description: string;
  goalType: string;
  difficulty: string;
  durationWeeks: number;
  frequencyPerWeek: number;
  days: DraftDay[];
};

export function TrainerMemberPlanScreen({ route }: Props) {
  const { t } = useTranslation();
  const layout = useTrainerLayout();
  const { memberId, programId } = route.params;
  const program = useMemberProgram(memberId, programId);
  const save = useUpdateMemberProgram(memberId, programId);
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    if (program.data && !editing) {
      setDraft(toDraft(program.data));
    }
  }, [program.data, editing]);

  const onSave = () => {
    if (!draft) {
      return;
    }

    setMessage(null);
    save.mutate(fromDraft(draft), {
      onSuccess: () => {
        setEditing(false);
        setMessage(t('trainer.planSaved'));
      },
    });
  };

  return (
    <ScreenState error={program.isError ? errorText(program.error, t('common.offline')) : null} loading={program.isLoading} onRetry={() => void program.refetch()}>
      <ScrollView contentContainerStyle={layout.content}>
        <AppText variant="display">{draft?.name ?? program.data?.name}</AppText>
        <AppText tone="muted">
          {draft ? `${t(`journey.goalType.${draft.goalType}`)} · ${t('journey.weeks', { count: draft.durationWeeks })} · ${t('journey.perWeek', { count: draft.frequencyPerWeek })}` : ''}
        </AppText>
        {draft?.description ? <AppText>{draft.description}</AppText> : null}
        {message ? <AppText>{message}</AppText> : null}
        {save.isError ? <AppText tone="danger">{errorText(save.error, t('common.offline'))}</AppText> : null}
        <View style={layout.row}>
          <AppButton label={editing ? t('common.cancel') : t('trainer.planEdit')} onPress={() => { setEditing((value) => !value); setMessage(null); if (program.data) { setDraft(toDraft(program.data)); } }} variant="secondary" />
          {editing ? <AppButton label={t('trainer.planSave')} loading={save.isPending} onPress={onSave} /> : null}
        </View>
        {draft?.days.map((day, dayIndex) => (
          <TrainerCard key={`${day.dayNumber}-${day.name}`}>
            {editing ? (
              <>
                <FormField label={t('trainer.planDayName')}>
                  <AppTextInput accessibilityLabel={t('trainer.planDayName')} onChangeText={(value) => setDraft((current) => current ? updateDay(current, dayIndex, { name: value }) : current)} value={day.name} />
                </FormField>
                <Pressable accessibilityRole="button" onPress={() => setDraft((current) => current ? updateDay(current, dayIndex, { restDay: !day.restDay, exercises: !day.restDay ? [] : day.exercises }) : current)} style={{ justifyContent: 'center', minHeight: 44 }}>
                  <AppText>{day.restDay ? t('trainer.planRestOn') : t('trainer.planRestOff')}</AppText>
                </Pressable>
              </>
            ) : (
              <>
                <AppText variant="label">{day.name}</AppText>
                <AppText tone="muted">{day.restDay ? t('journey.restDay') : t('journey.exercisesDone', { count: day.exercises.length })}</AppText>
              </>
            )}
            {!day.restDay ? day.exercises.map((exercise, exerciseIndex) => (
              <ExerciseEditor
                editing={editing}
                exercise={exercise}
                key={`${day.dayNumber}-${exercise.exerciseId}-${exerciseIndex}`}
                onChange={(patch) => setDraft((current) => current ? updateExercise(current, dayIndex, exerciseIndex, patch) : current)}
              />
            )) : null}
          </TrainerCard>
        ))}
      </ScrollView>
    </ScreenState>
  );
}

function ExerciseEditor({ exercise, editing, onChange }: { exercise: DraftExercise; editing: boolean; onChange: (patch: Partial<DraftExercise>) => void }) {
  const { t } = useTranslation();
  const detail = useExercise(exercise.exerciseId);
  if (!editing) {
    return (
      <View style={{ gap: 4, paddingVertical: 8 }}>
        <AppText>{detail.data?.name ?? t('common.loading')}</AppText>
        <AppText tone="muted">
          {[
            exercise.sets ? t('journey.sets', { count: Number(exercise.sets) }) : null,
            exercise.targetReps ? t('journey.reps', { count: Number(exercise.targetReps) }) : null,
            exercise.targetWeight ? `${t('journey.weightLabel')}: ${exercise.targetWeight}` : null,
            exercise.restSeconds ? t('journey.rest', { count: Number(exercise.restSeconds) }) : null,
          ].filter(Boolean).join(' · ')}
        </AppText>
        {exercise.notes ? <AppText tone="muted">{exercise.notes}</AppText> : null}
      </View>
    );
  }

  return (
    <View style={{ gap: 8, paddingVertical: 8 }}>
      <AppText variant="label">{detail.data?.name ?? t('common.loading')}</AppText>
      <FormField label={t('trainer.sets')}><AppTextInput accessibilityLabel={t('trainer.sets')} keyboardType="number-pad" onChangeText={(value) => onChange({ sets: value })} value={exercise.sets} /></FormField>
      <FormField label={t('trainer.reps')}><AppTextInput accessibilityLabel={t('trainer.reps')} keyboardType="number-pad" onChangeText={(value) => onChange({ targetReps: value })} value={exercise.targetReps} /></FormField>
      <FormField label={t('trainer.planWeight')}><AppTextInput accessibilityLabel={t('trainer.planWeight')} keyboardType="decimal-pad" onChangeText={(value) => onChange({ targetWeight: value })} value={exercise.targetWeight} /></FormField>
      <FormField label={t('trainer.planRest')}><AppTextInput accessibilityLabel={t('trainer.planRest')} keyboardType="number-pad" onChangeText={(value) => onChange({ restSeconds: value })} value={exercise.restSeconds} /></FormField>
      <FormField label={t('trainer.planNotes')}><AppTextInput accessibilityLabel={t('trainer.planNotes')} multiline onChangeText={(value) => onChange({ notes: value })} value={exercise.notes} /></FormField>
    </View>
  );
}

function toDraft(program: WorkoutProgram): Draft {
  return {
    name: program.name,
    description: program.description ?? '',
    goalType: program.goalType,
    difficulty: program.difficulty,
    durationWeeks: program.durationWeeks,
    frequencyPerWeek: program.frequencyPerWeek,
    days: program.days.map((day) => toDraftDay(day)),
  };
}

function toDraftDay(day: ProgramDay): DraftDay {
  return {
    dayNumber: day.dayNumber,
    name: day.name,
    description: day.description ?? '',
    restDay: day.restDay,
    exercises: day.exercises.map((exercise) => toDraftExercise(exercise)),
  };
}

function toDraftExercise(exercise: ProgramExercise): DraftExercise {
  return {
    exerciseId: exercise.exerciseId,
    sets: numberText(exercise.sets),
    targetReps: numberText(exercise.targetReps),
    targetDurationSeconds: numberText(exercise.targetDurationSeconds),
    targetDistance: numberText(exercise.targetDistance),
    targetWeight: numberText(exercise.targetWeight),
    restSeconds: numberText(exercise.restSeconds),
    notes: exercise.notes ?? '',
  };
}

function fromDraft(draft: Draft) {
  return {
    name: draft.name,
    description: draft.description.trim() || null,
    goalType: draft.goalType,
    difficulty: draft.difficulty,
    durationWeeks: draft.durationWeeks,
    frequencyPerWeek: draft.frequencyPerWeek,
    days: draft.days.map((day) => ({
      dayNumber: day.dayNumber,
      name: day.name,
      description: day.description.trim() || null,
      restDay: day.restDay,
      exercises: day.restDay ? [] : day.exercises.map((exercise) => ({
        exerciseId: exercise.exerciseId,
        sets: parseOptionalInt(exercise.sets),
        targetReps: parseOptionalInt(exercise.targetReps),
        targetDurationSeconds: parseOptionalInt(exercise.targetDurationSeconds),
        targetDistance: parseOptionalNumber(exercise.targetDistance),
        targetWeight: parseOptionalNumber(exercise.targetWeight),
        restSeconds: parseOptionalInt(exercise.restSeconds),
        notes: exercise.notes.trim() || null,
      })),
    })),
  };
}

function updateDay(draft: Draft, index: number, patch: Partial<DraftDay>): Draft {
  return {
    ...draft,
    days: draft.days.map((day, dayIndex) => (dayIndex === index ? { ...day, ...patch } : day)),
  };
}

function updateExercise(draft: Draft, dayIndex: number, exerciseIndex: number, patch: Partial<DraftExercise>): Draft {
  return updateDay(draft, dayIndex, {
    exercises: draft.days[dayIndex].exercises.map((exercise, index) => (index === exerciseIndex ? { ...exercise, ...patch } : exercise)),
  });
}

function numberText(value: number | null) {
  return value === null || value === undefined ? '' : String(value);
}

function parseOptionalInt(value: string) {
  const trimmed = value.trim();
  if (!trimmed) {
    return null;
  }
  const parsed = Number.parseInt(trimmed, 10);
  return Number.isFinite(parsed) ? parsed : null;
}

function parseOptionalNumber(value: string) {
  const trimmed = value.trim();
  if (!trimmed) {
    return null;
  }
  const parsed = Number.parseFloat(trimmed);
  return Number.isFinite(parsed) ? parsed : null;
}
