import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { errorText } from '../../../services/api/problem';
import { useMemberProgram, useUpdateMemberProgram } from '../api/workspace.queries';
import { TrainerIcon } from '../icons';
import { fromDraft, toDraft, weeksOf } from '../planDraft';
import { usePlanEditStore } from '../planEditStore';
import { FilterChip, TrainerCard, useTrainerLayout } from '../ui';

type Props = NativeStackScreenProps<AppStackParamList, 'TrainerMemberPlan'>;

export function TrainerMemberPlanScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const layout = useTrainerLayout();
  const { memberId, programId } = route.params;
  const program = useMemberProgram(memberId, programId);
  const save = useUpdateMemberProgram(memberId, programId);
  const draft = usePlanEditStore((state) => state.draft);
  const dirty = usePlanEditStore((state) => state.dirty);
  const load = usePlanEditStore((state) => state.load);
  const markPublished = usePlanEditStore((state) => state.markPublished);
  const [week, setWeek] = useState(1);
  const [message, setMessage] = useState<string | null>(null);
  const active = draft && usePlanEditStore.getState().memberId === memberId && usePlanEditStore.getState().programId === programId ? draft : null;

  useEffect(() => {
    if (program.data) {
      load(memberId, programId, program.data);
    }
  }, [load, memberId, program.data, programId]);

  const weeks = active ? weeksOf(active) : [];
  const selected = weeks.find((item) => item.week === week) ?? weeks[0];

  const publish = () => {
    const current = usePlanEditStore.getState().draft ?? (program.data ? toDraft(program.data) : null);
    if (!current) return;
    setMessage(null);
    save.mutate(fromDraft(current), {
      onSuccess: () => {
        markPublished();
        setMessage(t('trainer.planSaved'));
      },
    });
  };

  return (
    <ScreenState error={program.isError ? errorText(program.error, t('common.offline')) : null} loading={program.isLoading} onRetry={() => void program.refetch()}>
      <ScrollView contentContainerStyle={layout.content}>
        <View style={{ alignItems: 'center', flexDirection: 'row', justifyContent: 'space-between' }}>
          <View style={{ flex: 1, gap: 4 }}>
            <AppText variant="display">{active?.name ?? program.data?.name}</AppText>
            <AppText tone="muted">{active ? `${t(`journey.goalType.${active.goalType}`)} · ${t('journey.weeks', { count: active.durationWeeks })}` : ''}</AppText>
          </View>
          <AppButton label={t('trainer.history')} onPress={() => navigation.navigate('TrainerPlanHistory', { memberId, programId })} variant="secondary" />
        </View>
        {message ? <AppText tone="accent">{message}</AppText> : null}
        {save.isError ? <AppText tone="danger">{errorText(save.error, t('common.offline'))}</AppText> : null}
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: theme.spacing.sm }}>
          {weeks.map((item) => (
            <FilterChip key={item.week} label={t('trainer.weekChip', { week: item.week })} onPress={() => setWeek(item.week)} selected={item.week === (selected?.week ?? week)} />
          ))}
        </ScrollView>
        <AppButton label={t('trainer.modifyPlan')} onPress={() => navigation.navigate('TrainerPlanReview', { memberId, programId })} />
        {selected?.days.length === 0 ? <AppText tone="muted">{t('trainer.weekEmpty')}</AppText> : null}
        {selected?.days.map(({ day }) => (
          <TrainerCard key={`${day.dayNumber}-${day.name}`} onPress={day.restDay ? undefined : () => navigation.navigate('TrainerPlanDay', { memberId, programId, dayNumber: day.dayNumber })}>
            <View style={{ alignItems: 'center', flexDirection: 'row', gap: 12 }}>
              <TrainerIcon color={theme.colors.accent} name="dumbbell" size={20} />
              <View style={{ flex: 1, gap: 4 }}>
                <AppText variant="label">{t('trainer.dayTitle', { day: day.dayNumber, name: day.name })}</AppText>
                <AppText tone="muted">{day.restDay ? t('journey.restDay') : day.description || t('journey.exercisesDone', { count: day.exercises.length })}</AppText>
              </View>
              {day.restDay ? null : <TrainerIcon color={theme.colors.muted} name="chevron" size={18} />}
            </View>
          </TrainerCard>
        ))}
        <AppButton label={dirty ? t('trainer.publish') : t('trainer.planSave')} loading={save.isPending} onPress={publish} />
      </ScrollView>
    </ScreenState>
  );
}
