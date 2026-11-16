import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { errorText } from '../../../services/api/problem';
import { useExercise } from '../../fitness/api/queries';
import { useTrainerMember, useUpdateMemberProgram } from '../api/workspace.queries';
import { TrainerIcon, type TrainerIconName } from '../icons';
import { exerciseIsValid, fromDraft, planChanges } from '../planDraft';
import { usePlanEditStore } from '../planEditStore';
import { TrainerCard, useTrainerLayout } from '../ui';

type Props = NativeStackScreenProps<AppStackParamList, 'TrainerPlanReview'>;

export function TrainerPlanReviewScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const layout = useTrainerLayout();
  const { memberId, programId } = route.params;
  const member = useTrainerMember(memberId);
  const save = useUpdateMemberProgram(memberId, programId);
  const baseline = usePlanEditStore((state) => state.baseline);
  const draft = usePlanEditStore((state) => state.draft);
  const markPublished = usePlanEditStore((state) => state.markPublished);
  const [message, setMessage] = useState<string | null>(null);
  const changes = baseline && draft ? planChanges(baseline, draft) : [];
  const invalid = draft?.days.some((day) => day.exercises.some((exercise) => !exerciseIsValid(exercise))) ?? false;

  const publish = () => {
    if (!draft || invalid) return;
    Alert.alert(t('trainer.publishTitle'), t('trainer.publishBody'), [
      { text: t('common.cancel'), style: 'cancel' },
      {
        text: t('trainer.publish'),
        onPress: () => save.mutate(fromDraft(draft), {
          onSuccess: () => {
            markPublished();
            setMessage(t('trainer.published'));
            navigation.goBack();
          },
        }),
      },
    ]);
  };

  return (
    <ScrollView contentContainerStyle={layout.content}>
      <AppText variant="title">{member.data?.name ?? t('trainer.member')}</AppText>
      <AppText tone="muted">{draft?.name}</AppText>
      {changes.length === 0 ? <AppText tone="muted">{t('trainer.noChanges')}</AppText> : changes.map((change) => (
        <ChangeRow change={change} key={`${change.kind}-${change.dayName}-${change.exerciseId}`} />
      ))}
      {invalid ? <AppText tone="danger">{t('trainer.exerciseInvalid')}</AppText> : null}
      {message ? <AppText tone="accent">{message}</AppText> : null}
      {save.isError ? <AppText tone="danger">{errorText(save.error, t('common.offline'))}</AppText> : null}
      <AppText tone="muted">{t('trainer.publishNotice')}</AppText>
      <View style={{ gap: theme.spacing.sm }}>
        <AppButton label={t('trainer.keepDraft')} onPress={() => navigation.goBack()} variant="secondary" />
        <AppButton label={t('trainer.publish')} loading={save.isPending} onPress={publish} />
      </View>
    </ScrollView>
  );
}

function ChangeRow({ change }: { change: { dayName: string; exerciseId: string; kind: 'added' | 'removed' | 'modified' } }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const detail = useExercise(change.exerciseId);
  const icon: TrainerIconName = change.kind === 'modified' ? 'pencil' : change.kind === 'added' ? 'plus' : 'close';
  const color = change.kind === 'removed' ? theme.colors.danger : theme.colors.accent;
  return (
    <TrainerCard>
      <View style={{ alignItems: 'center', flexDirection: 'row', gap: 12 }}>
        <TrainerIcon color={color} name={icon} size={18} />
        <View style={{ flex: 1 }}>
          <AppText>{detail.data?.name ?? t('common.loading')}</AppText>
          <AppText tone="muted">{change.dayName} · {t(`trainer.change.${change.kind}`)}</AppText>
        </View>
      </View>
    </TrainerCard>
  );
}
