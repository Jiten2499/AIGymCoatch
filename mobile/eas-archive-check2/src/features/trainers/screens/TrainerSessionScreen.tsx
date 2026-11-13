import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, RefreshControl, ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { FormField } from '../../../components/forms/FormField';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { errorText } from '../../../services/api/problem';
import { useTrainerMutations, useTrainerSession } from '../api/workspace.queries';
import { clock, StatusPill, TrainerCard, useTrainerLayout } from '../ui';

type Props = NativeStackScreenProps<AppStackParamList, 'TrainerSession'>;

export function TrainerSessionScreen({ route, navigation }: Props) {
  const { t } = useTranslation();
  const layout = useTrainerLayout();
  const session = useTrainerSession(route.params.sessionId);
  const mutations = useTrainerMutations();
  const [name, setName] = useState('');
  const [sets, setSets] = useState('3');
  const [reps, setReps] = useState('12');
  const data = session.data;
  const scheduled = data?.session.status === 'Scheduled';

  return (
    <ScreenState error={session.isError ? errorText(session.error, t('common.offline')) : null} loading={session.isLoading} onRetry={() => void session.refetch()}>
      <ScrollView contentContainerStyle={layout.content} refreshControl={<RefreshControl onRefresh={() => void session.refetch()} refreshing={session.isRefetching} tintColor={layout.refresh} />}>
        <AppText variant="display">{data?.session.memberName}</AppText>
        <AppText>{data ? `${clock(data.session.scheduledStartUtc)} – ${clock(data.session.scheduledEndUtc)}` : ''}</AppText>
        <AppText tone="muted">{data?.session.sessionType ?? t('trainer.session')}{data?.session.location ? ` · ${data.session.location}` : ''}</AppText>
        {data ? <StatusPill status={data.session.status} /> : null}
        {data?.session.focus ? <AppText>{data.session.focus}</AppText> : null}
        {data?.session.packageName ? <AppText tone="muted">{data.session.packageName} · {data.session.sessionsRemaining}/{data.session.sessionCount}</AppText> : null}
        <AppText variant="title">{t('trainer.plan')}</AppText>
        {(data?.exercises.length ?? 0) === 0 ? <AppText tone="muted">{t('trainer.noExercises')}</AppText> : data?.exercises.map((item) => (
          <Pressable accessibilityRole="button" key={item.id} onPress={() => scheduled && mutations.toggle.mutate({ sessionId: route.params.sessionId, exerciseId: item.id })}>
            <TrainerCard>
              <AppText variant="label">{item.completed ? '✓ ' : ''}{item.name}</AppText>
              <AppText tone="muted">{item.muscleGroup ?? ''} · {item.sets} × {item.reps}</AppText>
            </TrainerCard>
          </Pressable>
        ))}
        {scheduled ? (
          <TrainerCard>
            <FormField label={t('trainer.exercise')}><AppTextInput accessibilityLabel={t('trainer.exercise')} onChangeText={setName} value={name} /></FormField>
            <FormField label={t('trainer.sets')}><AppTextInput accessibilityLabel={t('trainer.sets')} keyboardType="number-pad" onChangeText={setSets} value={sets} /></FormField>
            <FormField label={t('trainer.reps')}><AppTextInput accessibilityLabel={t('trainer.reps')} onChangeText={setReps} value={reps} /></FormField>
            <AppButton label={t('trainer.addExercise')} onPress={() => { const parsed = Number(sets); if (!name.trim() || !reps.trim() || parsed < 1) { return; } mutations.exercise.mutate({ sessionId: route.params.sessionId, name, sets: parsed, reps }, { onSuccess: () => setName('') }); }} />
          </TrainerCard>
        ) : null}
        {data?.session.notes ? <AppText>{data.session.notes}</AppText> : null}
        {mutations.start.isError || mutations.cancel.isError ? <AppText tone="danger">{errorText(mutations.start.error ?? mutations.cancel.error, t('common.offline'))}</AppText> : null}
        {scheduled ? <AppButton label={t('trainer.startSession')} loading={mutations.start.isPending} onPress={() => mutations.start.mutate(route.params.sessionId, { onSuccess: () => navigation.navigate('TrainerActive', { sessionId: route.params.sessionId }) })} /> : null}
        {scheduled ? <AppButton label={t('trainer.cancelSession')} onPress={() => mutations.cancel.mutate(route.params.sessionId, { onSuccess: () => navigation.goBack() })} variant="secondary" /> : null}
      </ScrollView>
    </ScreenState>
  );
}
