import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { errorText } from '../../../services/api/problem';
import { useExerciseCatalog } from '../../fitness/api/queries';
import { ExerciseThumb } from '../exerciseThumb';
import { TrainerIcon } from '../icons';
import { addExercise, replaceExercise } from '../planDraft';
import { usePlanEditStore } from '../planEditStore';
import { FilterChip, TrainerCard, useTrainerLayout } from '../ui';

type Props = NativeStackScreenProps<AppStackParamList, 'TrainerExerciseLibrary'>;
const categories = ['all', 'chest', 'back', 'shoulder', 'arms', 'legs', 'core', 'glutes'] as const;

export function TrainerExerciseLibraryScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const layout = useTrainerLayout();
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState<(typeof categories)[number]>('all');
  const [notice, setNotice] = useState<string | null>(null);
  const catalog = useExerciseCatalog(search);
  const params = route.params ?? {};
  const canAdd = Boolean(params.memberId && params.programId && params.dayNumber);
  const rows = (catalog.data?.items ?? []).filter((item) => matchesCategory(item.muscleGroup, category));

  const add = (exerciseId: string) => {
    const state = usePlanEditStore.getState();
    if (!state.draft || !params.dayNumber) return;
    const dayIndex = state.draft.days.findIndex((day) => day.dayNumber === params.dayNumber);
    if (dayIndex < 0) return;
    const result = params.replaceIndex === undefined
      ? addExercise(state.draft, dayIndex, exerciseId)
      : replaceExercise(state.draft, dayIndex, params.replaceIndex, exerciseId);
    if (result.duplicate) {
      setNotice(t('trainer.duplicateBlocked'));
      return;
    }
    state.replace(result.draft);
    setNotice(t('trainer.exerciseAdded'));
    navigation.goBack();
  };

  return (
    <ScreenState error={catalog.isError ? errorText(catalog.error, t('common.offline')) : null} loading={catalog.isLoading} onRetry={() => void catalog.refetch()}>
      <ScrollView contentContainerStyle={layout.content}>
        <View style={{ alignItems: 'center', flexDirection: 'row', gap: theme.spacing.sm }}>
          <TrainerIcon color={theme.colors.muted} name="search" size={18} />
          <View style={{ flex: 1 }}>
            <AppTextInput accessibilityLabel={t('trainer.searchExercise')} onChangeText={setSearch} placeholder={t('trainer.searchExercise')} value={search} />
          </View>
        </View>
        {notice ? <AppText tone="accent">{notice}</AppText> : null}
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: theme.spacing.sm }}>
          {categories.map((item) => (
            <FilterChip key={item} label={t(`trainer.muscle.${item}`)} onPress={() => setCategory(item)} selected={category === item} />
          ))}
        </ScrollView>
        {rows.length === 0 ? <AppText tone="muted">{t('trainer.libraryEmpty')}</AppText> : rows.map((item) => (
          <TrainerCard key={item.id}>
            <View style={{ alignItems: 'center', flexDirection: 'row', gap: 12 }}>
              <Pressable accessibilityRole="button" onPress={() => navigation.navigate('ExerciseDetail', { exerciseId: item.id })} style={{ flex: 1 }}>
                <View style={{ alignItems: 'center', flexDirection: 'row', gap: 12 }}>
                  <ExerciseThumb label={item.name} muscle={item.muscleGroup} />
                  <View style={{ flex: 1, gap: 4 }}>
                    <AppText variant="label">{item.name}</AppText>
                    <AppText tone="muted">{[item.equipment, item.muscleGroup].filter(Boolean).join(' · ')}</AppText>
                  </View>
                </View>
              </Pressable>
              {canAdd ? (
                <Pressable accessibilityLabel={t('trainer.addExercise')} accessibilityRole="button" onPress={() => add(item.id)} style={{ alignItems: 'center', backgroundColor: theme.colors.accent, borderRadius: 18, height: 36, justifyContent: 'center', width: 36 }}>
                  <TrainerIcon color={theme.colors.onAccent} name="plus" size={18} />
                </Pressable>
              ) : null}
            </View>
          </TrainerCard>
        ))}
      </ScrollView>
    </ScreenState>
  );
}

function matchesCategory(muscleGroup: string, category: (typeof categories)[number]) {
  if (category === 'all') return true;
  const text = muscleGroup.toLowerCase();
  if (category === 'shoulder') return text.includes('shoulder') || text.includes('delt');
  if (category === 'arms') return /arm|bicep|tricep|forearm/.test(text);
  if (category === 'core') return /core|ab|oblique/.test(text);
  return text.includes(category);
}
