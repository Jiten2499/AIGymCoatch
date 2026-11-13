import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FormField } from '../../../components/forms/FormField';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { errorText } from '../../../services/api/problem';
import { useTrainerMutations, useTrainerSession } from '../api/workspace.queries';
import { formatSeconds, TrainerCard, useTrainerLayout } from '../ui';

type Props = NativeStackScreenProps<AppStackParamList, 'TrainerActive'>;

export function TrainerActiveScreen({ route, navigation }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const layout = useTrainerLayout();
  const session = useTrainerSession(route.params.sessionId);
  const mutations = useTrainerMutations();
  const [running, setRunning] = useState(false);
  const [elapsed, setElapsed] = useState(0);
  const [notes, setNotes] = useState('');
  const data = session.data;

  useEffect(() => {
    if (data) {
      setElapsed(data.session.elapsedSeconds);
      setNotes(data.session.notes ?? '');
      setRunning(data.session.status === 'Scheduled' && Boolean(data.session.startedAtUtc));
    }
  }, [data?.session.elapsedSeconds, data?.session.status, data?.session.startedAtUtc, data?.session.notes]);

  useEffect(() => {
    if (!running) {
      return undefined;
    }
    const timer = setInterval(() => setElapsed((value) => value + 1), 1000);
    return () => clearInterval(timer);
  }, [running]);

  const pause = () => {
    setRunning(false);
    mutations.progress.mutate({ sessionId: route.params.sessionId, elapsedSeconds: elapsed });
  };

  return (
    <ScreenState error={session.isError ? errorText(session.error, t('common.offline')) : null} loading={session.isLoading} onRetry={() => void session.refetch()}>
      <ScrollView contentContainerStyle={layout.content}>
        <AppText tone="muted" variant="label">{data?.session.memberName}</AppText>
        <AppText style={{ color: theme.colors.accent, fontSize: 48, lineHeight: 56 }}>{formatSeconds(elapsed)}</AppText>
        <View style={{ flexDirection: 'row', gap: theme.spacing.sm }}>
          <View style={{ flex: 1 }}><AppButton label={running ? t('trainer.pause') : t('trainer.resume')} onPress={() => (running ? pause() : setRunning(true))} variant="secondary" /></View>
          <View style={{ flex: 1 }}><AppButton label={t('trainer.end')} onPress={() => { setRunning(false); mutations.progress.mutate({ sessionId: route.params.sessionId, elapsedSeconds: elapsed }); }} style={{ backgroundColor: theme.colors.danger }} /></View>
        </View>
        <AppText variant="title">{t('trainer.plan')}</AppText>
        {data?.exercises.map((item) => (
          <Pressable accessibilityRole="button" key={item.id} onPress={() => mutations.toggle.mutate({ sessionId: route.params.sessionId, exerciseId: item.id })}>
            <TrainerCard>
              <AppText>{item.completed ? '✓ ' : ''}{item.name}</AppText>
              <AppText tone="muted">{item.sets} × {item.reps}</AppText>
            </TrainerCard>
          </Pressable>
        ))}
        <AppText variant="title">{t('trainer.completion')}</AppText>
        <FormField label={t('trainer.notes')}><AppTextInput accessibilityLabel={t('trainer.notes')} multiline onChangeText={setNotes} value={notes} /></FormField>
        {mutations.complete.isError ? <AppText tone="danger">{errorText(mutations.complete.error, t('common.offline'))}</AppText> : null}
        <AppButton
          label={t('trainer.completeSession')}
          loading={mutations.complete.isPending}
          onPress={() => mutations.complete.mutate({ sessionId: route.params.sessionId, notes, elapsedSeconds: elapsed }, { onSuccess: () => navigation.goBack() })}
        />
      </ScrollView>
    </ScreenState>
  );
}
