import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Pressable, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { errorText } from '../../../services/api/problem';
import { useExercise } from '../../fitness/api/queries';
import { useMemberProgram } from '../api/workspace.queries';
import { ExerciseThumb } from '../exerciseThumb';
import { TrainerIcon } from '../icons';
import { duplicateExercise, moveExercise, removeExercise, type DraftExercise } from '../planDraft';
import { usePlanEditStore } from '../planEditStore';
import { TrainerCard, useTrainerLayout } from '../ui';

type Props = NativeStackScreenProps<AppStackParamList, 'TrainerPlanDay'>;

export function TrainerPlanDayScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const layout = useTrainerLayout();
  const { memberId, programId, dayNumber } = route.params;
  const program = useMemberProgram(memberId, programId);
  const load = usePlanEditStore((state) => state.load);
  const draft = usePlanEditStore((state) => state.draft);
  const dirty = usePlanEditStore((state) => state.dirty);
  const replace = usePlanEditStore((state) => state.replace);
  const revert = usePlanEditStore((state) => state.revert);
  const leaving = useRef(false);
  const [menu, setMenu] = useState<number | null>(null);

  useEffect(() => {
    if (program.data) load(memberId, programId, program.data);
  }, [load, memberId, program.data, programId]);

  useEffect(() => {
    const unsubscribe = navigation.addListener('beforeRemove', (event) => {
      if (leaving.current || !usePlanEditStore.getState().dirty) return;
      event.preventDefault();
      Alert.alert(t('trainer.discardTitle'), t('trainer.discardBody'), [
        { text: t('common.cancel'), style: 'cancel' },
        { text: t('trainer.discard'), style: 'destructive', onPress: () => { revert(); leaving.current = true; navigation.dispatch(event.data.action); } },
      ]);
    });
    return unsubscribe;
  }, [navigation, revert, t]);

  const dayIndex = draft?.days.findIndex((day) => day.dayNumber === dayNumber) ?? -1;
  const day = dayIndex >= 0 ? draft?.days[dayIndex] : null;

  const change = (next: NonNullable<typeof draft>) => replace(next);

  return (
    <ScreenState error={program.isError ? errorText(program.error, t('common.offline')) : null} loading={program.isLoading || !day} onRetry={() => void program.refetch()}>
      <ScrollView contentContainerStyle={layout.content}>
        {day && draft ? (
          <>
            <AppText variant="title">{t('trainer.dayTitle', { day: day.dayNumber, name: day.name })}</AppText>
            <AppText tone="muted">{day.description || t('journey.exercisesDone', { count: day.exercises.length })}</AppText>
            <AppText tone="muted">{draft.difficulty}</AppText>
            {day.exercises.map((exercise, index) => (
              <ExerciseRow
                exercise={exercise}
                key={`${exercise.exerciseId}-${index}`}
                menuOpen={menu === index}
                onEdit={() => { setMenu(null); navigation.navigate('TrainerExerciseEdit', { memberId, programId, dayNumber, exerciseIndex: index }); }}
                onMenu={() => setMenu(menu === index ? null : index)}
                onMove={(direction) => { setMenu(null); change(moveExercise(draft, dayIndex, index, index + direction)); }}
                onDuplicate={() => { setMenu(null); change(duplicateExercise(draft, dayIndex, index)); }}
                onRemove={() => { setMenu(null); change(removeExercise(draft, dayIndex, index)); }}
                onReplace={() => { setMenu(null); navigation.navigate('TrainerExerciseLibrary', { memberId, programId, dayNumber, replaceIndex: index }); }}
              />
            ))}
            <AppButton label={t('trainer.addExercise')} onPress={() => navigation.navigate('TrainerExerciseLibrary', { memberId, programId, dayNumber })} variant="secondary" />
            <View style={{ flexDirection: 'row', gap: theme.spacing.sm }}>
              <AppButton label={t('common.cancel')} onPress={() => navigation.goBack()} variant="secondary" />
              <AppButton label={t('trainer.saveChanges')} onPress={() => { leaving.current = true; navigation.goBack(); }} />
            </View>
            {dirty ? <AppButton label={t('trainer.reviewChanges')} onPress={() => { leaving.current = true; navigation.navigate('TrainerPlanReview', { memberId, programId }); }} variant="secondary" /> : null}
          </>
        ) : null}
      </ScrollView>
    </ScreenState>
  );
}

function ExerciseRow({ exercise, menuOpen, onMenu, onMove, onEdit, onReplace, onDuplicate, onRemove }: {
  exercise: DraftExercise;
  menuOpen: boolean;
  onMenu: () => void;
  onMove: (direction: -1 | 1) => void;
  onEdit: () => void;
  onReplace: () => void;
  onDuplicate: () => void;
  onRemove: () => void;
}) {
  const theme = useTheme();
  const { t } = useTranslation();
  const detail = useExercise(exercise.exerciseId);
  const name = detail.data?.name ?? t('common.loading');
  return (
    <TrainerCard>
      <View style={{ alignItems: 'center', flexDirection: 'row', gap: 12 }}>
        <ExerciseThumb label={name} muscle={detail.data?.muscleGroup} />
        <View style={{ flex: 1, gap: 4 }}>
          <AppText variant="label">{name}</AppText>
          <AppText tone="muted">{t('trainer.setLine', { sets: exercise.sets || '—', reps: exercise.targetReps || '—', rest: exercise.restSeconds || '—' })}</AppText>
        </View>
        <Pressable accessibilityLabel={t('trainer.moveEarlier')} accessibilityRole="button" onPress={() => onMove(-1)} style={{ minHeight: 44, justifyContent: 'center' }}>
          <TrainerIcon color={theme.colors.muted} name="grip" size={18} />
        </Pressable>
        <Pressable accessibilityLabel={t('trainer.moreExercise')} accessibilityRole="button" onPress={onMenu} style={{ minHeight: 44, justifyContent: 'center' }}>
          <TrainerIcon color={theme.colors.text} name="ellipsis" size={18} />
        </Pressable>
      </View>
      {menuOpen ? (
        <View style={{ gap: 8 }}>
          <AppText onPress={onEdit}>{t('trainer.editExercise')}</AppText>
          <AppText onPress={onReplace}>{t('trainer.replaceExercise')}</AppText>
          <AppText onPress={onDuplicate}>{t('trainer.duplicateExercise')}</AppText>
          <AppText onPress={onRemove} tone="danger">{t('trainer.removeExercise')}</AppText>
          <AppText onPress={() => onMove(1)}>{t('trainer.moveLater')}</AppText>
        </View>
      ) : null}
    </TrainerCard>
  );
}
