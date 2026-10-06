import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useNavigation } from '@react-navigation/native';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, RefreshControl, ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { formatDate } from '../../../services/localization/format';
import { errorText } from '../../../services/api/problem';
import { useSessions } from '../../fitness/api/queries';
import type { WorkoutSession } from '../../fitness/types';

export function WorkoutHistoryScreen() {
  const theme = useTheme();
  const { t, i18n } = useTranslation();
  const navigation = useNavigation<NativeStackNavigationProp<AppStackParamList>>();
  const [page, setPage] = useState(1);
  const sessions = useSessions(page);
  const items = sessions.data?.items ?? [];
  const totalPages = sessions.data && sessions.data.pageSize > 0 ? Math.ceil(sessions.data.totalCount / sessions.data.pageSize) : 1;

  return (
    <FeatureGate code={featureCodes.workoutPlans}>
      <ScreenState empty={sessions.isSuccess && items.length === 0} emptyBody={t('journey.historyEmpty')} emptyTitle={t('journey.historyTitle')} error={sessions.isError ? errorText(sessions.error, t('common.offline')) : null} loading={sessions.isLoading} onRetry={() => void sessions.refetch()}>
        <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }} refreshControl={<RefreshControl refreshing={sessions.isRefetching} onRefresh={() => void sessions.refetch()} />}>
          <AppText variant="title">{t('journey.historyTitle')}</AppText>
          {items.map((session) => (
            <HistoryRow key={session.id} locale={i18n.language} onPress={() => navigation.navigate(session.status === 'Completed' ? 'SessionDetail' : 'WorkoutSession', { sessionId: session.id })} session={session} />
          ))}
          {totalPages > 1 ? <AppText tone="muted">{t('journey.pageOf', { page, total: totalPages })}</AppText> : null}
          {page > 1 ? <AppButton label={t('journey.previousPage')} onPress={() => setPage(page - 1)} variant="secondary" /> : null}
          {page < totalPages ? <AppButton label={t('journey.nextPage')} onPress={() => setPage(page + 1)} variant="secondary" /> : null}
        </ScrollView>
      </ScreenState>
    </FeatureGate>
  );
}

function HistoryRow({ session, locale, onPress }: { session: WorkoutSession; locale: string; onPress: () => void }) {
  const { t } = useTranslation();
  const when = session.completedAt ?? session.startedAt;
  const minutes = session.durationSeconds ? Math.max(1, Math.round(session.durationSeconds / 60)) : null;
  return (
    <Pressable accessibilityRole="button" onPress={onPress}>
      <AppCard>
        <AppText variant="label">{when ? formatDate(new Date(when), locale) : t('journey.sessionTitle')}</AppText>
        <AppText tone="muted">{t(`journey.sessionStatus.${session.status}`)}</AppText>
        {minutes !== null ? <AppText tone="muted">{t('journey.durationDone', { count: minutes })}</AppText> : null}
      </AppCard>
    </Pressable>
  );
}
