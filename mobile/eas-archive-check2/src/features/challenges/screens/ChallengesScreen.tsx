import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useNavigation } from '@react-navigation/native';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { RefreshControl, ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { formatDate, formatNumber } from '../../../services/localization/format';
import { errorText } from '../../../services/api/problem';
import { useChallenges } from '../api/queries';
import type { Challenge } from '../types';

export function ChallengesScreen() {
  const theme = useTheme();
  const { t, i18n } = useTranslation();
  const navigation = useNavigation<NativeStackNavigationProp<AppStackParamList>>();
  const [page, setPage] = useState(1);
  const challenges = useChallenges(page);
  const items = challenges.data?.items ?? [];
  const totalPages = challenges.data && challenges.data.pageSize > 0 ? Math.ceil(challenges.data.totalCount / challenges.data.pageSize) : 1;

  return (
    <FeatureGate code={featureCodes.workoutPlans}>
      <ScreenState empty={challenges.isSuccess && items.length === 0} emptyBody={t('challenge.empty')} emptyTitle={t('nav.challenges')} error={challenges.isError ? errorText(challenges.error, t('common.offline')) : null} loading={challenges.isLoading} onRetry={() => void challenges.refetch()}>
        <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }} refreshControl={<RefreshControl refreshing={challenges.isRefetching} onRefresh={() => void challenges.refetch()} />}>
          <AppText variant="title">{t('nav.challenges')}</AppText>
          {items.map((challenge) => (
            <AppCard key={challenge.id}>
              <AppText variant="label">{challenge.name}</AppText>
              <AppText tone="muted">{t(`challenge.status.${challenge.status}`)} · {t(`challenge.type.${challenge.challengeType}`)}</AppText>
              <AppText tone="muted">{formatDate(new Date(`${challenge.startDate}T00:00:00`), i18n.language)} – {formatDate(new Date(`${challenge.endDate}T00:00:00`), i18n.language)}</AppText>
              <AppText>{challenge.currentValue === null ? t('challenge.notJoined') : t('challenge.progress', { current: formatNumber(challenge.currentValue, i18n.language), target: formatNumber(challenge.targetValue, i18n.language) })}</AppText>
              <AppButton label={t('challenge.open')} onPress={() => navigation.navigate('ChallengeDetail', { challengeId: challenge.id, page })} variant="secondary" />
            </AppCard>
          ))}
          {totalPages > 1 ? <AppText tone="muted">{t('journey.pageOf', { page, total: totalPages })}</AppText> : null}
          {page > 1 ? <AppButton label={t('journey.previousPage')} onPress={() => setPage(page - 1)} variant="secondary" /> : null}
          {page < totalPages ? <AppButton label={t('journey.nextPage')} onPress={() => setPage(page + 1)} variant="secondary" /> : null}
        </ScrollView>
      </ScreenState>
    </FeatureGate>
  );
}
