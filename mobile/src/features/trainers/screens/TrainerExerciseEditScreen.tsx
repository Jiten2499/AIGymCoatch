import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FormField } from '../../../components/forms/FormField';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { useExercise } from '../../fitness/api/queries';
import { ExerciseThumb } from '../exerciseThumb';
import { TrainerIcon } from '../icons';
import { exerciseIsValid, removeExercise, updateExercise, type DraftExercise } from '../planDraft';
import { usePlanEditStore } from '../planEditStore';
import { useTrainerLayout } from '../ui';

type Props = NativeStackScreenProps<AppStackParamList, 'TrainerExerciseEdit'>;

export function TrainerExerciseEditScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const layout = useTrainerLayout();
  const { memberId, programId, dayNumber, exerciseIndex } = route.params;
  const draft = usePlanEditStore((state) => state.draft);
  const replace = usePlanEditStore((state) => state.replace);
  const dayIndex = draft?.days.findIndex((day) => day.dayNumber === dayNumber) ?? -1;
  const current = dayIndex >= 0 ? draft?.days[dayIndex].exercises[exerciseIndex] : null;
  const [form, setForm] = useState<DraftExercise | null>(current ?? null);
  const [error, setError] = useState<string | null>(null);
  const detail = useExercise(form?.exerciseId ?? '');

  const patch = (next: Partial<DraftExercise>) => setForm((value) => (value ? { ...value, ...next } : value));

  const save = () => {
    if (!form || !draft || dayIndex < 0 || !exerciseIsValid(form)) {
      setError(t('trainer.exerciseInvalid'));
      return;
    }
    replace(updateExercise(draft, dayIndex, exerciseIndex, form));
    navigation.goBack();
  };

  if (!form || !draft) {
    return <AppText style={{ padding: theme.spacing.xl }} tone="muted">{t('trainer.editMissing')}</AppText>;
  }

  return (
    <ScrollView contentContainerStyle={layout.content}>
      <View style={{ alignItems: 'center', flexDirection: 'row', gap: 12 }}>
        <ExerciseThumb label={detail.data?.name ?? form.exerciseId} muscle={detail.data?.muscleGroup} />
        <AppText style={{ flex: 1 }} variant="title">{detail.data?.name ?? t('common.loading')}</AppText>
      </View>
      <AppButton label={t('trainer.replaceExercise')} onPress={() => navigation.navigate('TrainerExerciseLibrary', { memberId, programId, dayNumber, replaceIndex: exerciseIndex })} variant="secondary" />
      <FormField label={t('trainer.sets')}>
        <AppTextInput accessibilityLabel={t('trainer.sets')} keyboardType="number-pad" onChangeText={(sets) => patch({ sets })} value={form.sets} />
      </FormField>
      <FormField label={t('trainer.reps')}>
        <AppTextInput accessibilityLabel={t('trainer.reps')} keyboardType="number-pad" onChangeText={(targetReps) => patch({ targetReps })} value={form.targetReps} />
      </FormField>
      <FormField label={t('trainer.planRest')}>
        <AppTextInput accessibilityLabel={t('trainer.planRest')} keyboardType="number-pad" onChangeText={(restSeconds) => patch({ restSeconds })} value={form.restSeconds} />
      </FormField>
      <FormField label={t('trainer.planWeight')}>
        <AppTextInput accessibilityLabel={t('trainer.planWeight')} keyboardType="decimal-pad" onChangeText={(targetWeight) => patch({ targetWeight })} value={form.targetWeight} />
      </FormField>
      <FormField label={t('trainer.planNotes')}>
        <AppTextInput accessibilityLabel={t('trainer.planNotes')} multiline onChangeText={(notes) => patch({ notes })} value={form.notes} />
      </FormField>
      {error ? <AppText accessibilityRole="alert" tone="danger">{error}</AppText> : null}
      <AppButton label={t('trainer.removeExercise')} onPress={() => { replace(removeExercise(draft, dayIndex, exerciseIndex)); navigation.goBack(); }} style={{ backgroundColor: theme.colors.danger }} />
      <View style={{ alignItems: 'center', flexDirection: 'row', gap: 8 }}>
        <TrainerIcon color={theme.colors.onAccent} name="save" size={18} />
        <AppButton label={t('trainer.updateExercise')} onPress={save} />
      </View>
    </ScrollView>
  );
}
