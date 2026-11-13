import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { RefreshControl, ScrollView, View } from 'react-native';
import { FormField } from '../../../components/forms/FormField';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { errorText } from '../../../services/api/problem';
import { useMyTargets, useSubmitTarget } from '../api/queries';
import { useTrainerPerformance } from '../api/workspace.queries';
import { monthBounds, ProgressBar, Stat, TrainerCard, useTrainerLayout } from '../ui';
import { AppTextInput } from '../../../components/ui/AppTextInput';

export function TrainerPerformanceScreen() {
  const { t } = useTranslation();
  const layout = useTrainerLayout();
  const [cursor] = useState(() => new Date());
  const bounds = monthBounds(cursor);
  const performance = useTrainerPerformance(bounds.from, bounds.to);
  const targets = useMyTargets(true);
  const submit = useSubmitTarget();
  const [details, setDetails] = useState<Record<string, string>>({});
  const data = performance.data;

  return (
    <ScreenState error={performance.isError ? errorText(performance.error, t('common.offline')) : null} loading={performance.isLoading} onRetry={() => void performance.refetch()}>
      <ScrollView contentContainerStyle={layout.content} refreshControl={<RefreshControl onRefresh={() => void performance.refetch()} refreshing={performance.isRefetching} tintColor={layout.refresh} />}>
        <AppText variant="display">{t('trainer.performance')}</AppText>
        <TrainerCard>
          <AppText variant="title">{data?.overallPercent ?? 0}%</AppText>
          <AppText tone="muted">{t('trainer.overall')}</AppText>
          <ProgressBar percent={data?.overallPercent ?? 0} />
          <AppText variant="title">{data?.creditBalance ?? 0}</AppText>
          <AppText tone="muted">{t('trainer.credits')}</AppText>
        </TrainerCard>
        <View style={layout.row}>
          <Stat label={t('trainer.sessionsCompleted')} value={`${data?.sessionsCompleted ?? 0}`} />
          <Stat label={t('trainer.packageValue')} value={`${data?.packageValue ?? 0} ${data?.currency ?? ''}`.trim()} />
          <Stat label={t('trainer.packagesSold')} value={`${data?.packagesSold ?? 0}`} />
          <Stat label={t('trainer.newClients')} value={`${data?.newClients ?? 0}`} />
        </View>
        <AppText variant="title">{t('trainer.targets')}</AppText>
        {(data?.metrics.length ?? 0) === 0 ? <AppText tone="muted">{t('trainer.noTargets')}</AppText> : data?.metrics.map((item) => (
          <TrainerCard key={item.id}>
            <AppText variant="label">{item.name}</AppText>
            <AppText tone="muted">{item.metric} · {item.actual}/{item.goal}</AppText>
            <ProgressBar percent={item.percent} />
          </TrainerCard>
        ))}
        <AppText variant="title">{t('trainer.submitTarget')}</AppText>
        {(targets.data ?? []).filter((item) => item.status === 'Active' && item.submissionStatus !== 'Pending' && item.submissionStatus !== 'Approved').map((item) => (
          <TrainerCard key={item.id}>
            <AppText>{item.name}</AppText>
            <FormField label={t('trainerTargets.details')}><AppTextInput accessibilityLabel={t('trainerTargets.details')} onChangeText={(value) => setDetails((current) => ({ ...current, [item.id]: value }))} value={details[item.id] ?? ''} /></FormField>
            <AppButton label={t('trainerTargets.submit')} loading={submit.isPending} onPress={() => { const text = details[item.id]?.trim(); if (text) { submit.mutate({ assignmentId: item.id, details: text }); } }} />
          </TrainerCard>
        ))}
        <AppText variant="title">{t('trainer.creditHistory')}</AppText>
        {(data?.credits.length ?? 0) === 0 ? <AppText tone="muted">{t('trainer.noCredits')}</AppText> : data?.credits.map((item) => (
          <TrainerCard key={item.id}>
            <AppText>{item.points > 0 ? `+${item.points}` : item.points} · {item.type}</AppText>
            <AppText tone="muted">{item.status}{item.reason ? ` · ${item.reason}` : ''}</AppText>
          </TrainerCard>
        ))}
      </ScrollView>
    </ScreenState>
  );
}
