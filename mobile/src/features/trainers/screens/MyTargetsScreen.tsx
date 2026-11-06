import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ScrollView } from 'react-native';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { errorText } from '../../../services/api/problem';
import type { MyTrainerTarget } from '../api/targets.api';
import { useMyTargets, useSubmitTarget } from '../api/queries';

export function MyTargetsScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const targets = useMyTargets(true);
  const items = targets.data ?? [];

  return (
    <ScreenState
      empty={targets.isFetched && items.length === 0}
      emptyBody={t('trainerTargets.emptyBody')}
      emptyTitle={t('trainerTargets.emptyTitle')}
      error={targets.isError ? errorText(targets.error, t('common.offline')) : null}
      loading={targets.isLoading}
      onRetry={() => void targets.refetch()}
    >
      <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
        <AppText variant="title">{t('trainerTargets.title')}</AppText>
        {items.map((item) => <TargetCard item={item} key={item.id} />)}
      </ScrollView>
    </ScreenState>
  );
}

function TargetCard({ item }: { item: MyTrainerTarget }) {
  const { t } = useTranslation();
  const [details, setDetails] = useState(item.submissionDetails ?? '');
  const [message, setMessage] = useState<string | null>(null);
  const submit = useSubmitTarget();
  const waiting = item.submissionStatus === 'Pending' || item.submissionStatus === 'Approved' || item.status !== 'Active';

  return (
    <AppCard>
      <AppText variant="label">{item.name}</AppText>
      <AppText>{t('trainerTargets.metric')}: {item.metric}</AppText>
      <AppText>{t('trainerTargets.goal')}: {item.goalValue}</AppText>
      <AppText>{t('trainerTargets.points')}: {item.awardPoints}</AppText>
      <AppText>{t('trainerTargets.period')}: {item.effectiveStart}{item.effectiveEnd ? ` – ${item.effectiveEnd}` : ''}</AppText>
      <AppText tone="muted">{item.submissionStatus ?? item.status}</AppText>
      {waiting ? null : (
        <>
          <AppTextInput accessibilityLabel={t('trainerTargets.details')} onChangeText={setDetails} value={details} />
          <AppButton
            label={t('trainerTargets.submit')}
            onPress={() => {
              setMessage(null);
              submit.mutate(
                { assignmentId: item.id, details },
                {
                  onSuccess: () => setMessage(t('trainerTargets.submitted')),
                  onError: (error) => setMessage(errorText(error, t('common.offline'))),
                },
              );
            }}
          />
        </>
      )}
      {message ? <AppText>{message}</AppText> : null}
      {item.submissionDetails && waiting ? <AppText tone="muted">{item.submissionDetails}</AppText> : null}
    </AppCard>
  );
}
