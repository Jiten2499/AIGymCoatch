import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { RefreshControl, ScrollView } from 'react-native';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { formatDate, formatNumber } from '../../../services/localization/format';
import { errorText } from '../../../services/api/problem';
import { useNutritionLogs } from '../api/queries';
import type { NutritionLog } from '../types';

export function NutritionHistoryScreen() {
  const theme = useTheme();
  const { t, i18n } = useTranslation();
  const [page, setPage] = useState(1);
  const logs = useNutritionLogs(page, null);
  const items = logs.data?.items ?? [];
  const totalPages = logs.data && logs.data.pageSize > 0 ? Math.ceil(logs.data.totalCount / logs.data.pageSize) : 1;

  return (
    <FeatureGate code={featureCodes.nutrition}>
      <ScreenState empty={logs.isSuccess && items.length === 0} emptyBody={t('nutrition.historyEmpty')} emptyTitle={t('nutrition.history')} error={logs.isError ? errorText(logs.error, t('common.offline')) : null} loading={logs.isLoading} onRetry={() => void logs.refetch()}>
        <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }} refreshControl={<RefreshControl refreshing={logs.isRefetching} onRefresh={() => void logs.refetch()} />}>
          <AppText variant="title">{t('nutrition.history')}</AppText>
          {items.map((log) => <LogCard key={log.id} locale={i18n.language} log={log} />)}
          {totalPages > 1 ? <AppText tone="muted">{t('journey.pageOf', { page, total: totalPages })}</AppText> : null}
          {page > 1 ? <AppButton label={t('journey.previousPage')} onPress={() => setPage(page - 1)} variant="secondary" /> : null}
          {page < totalPages ? <AppButton label={t('journey.nextPage')} onPress={() => setPage(page + 1)} variant="secondary" /> : null}
        </ScrollView>
      </ScreenState>
    </FeatureGate>
  );
}

function LogCard({ log, locale }: { log: NutritionLog; locale: string }) {
  const { t } = useTranslation();
  return (
    <AppCard>
      <AppText variant="label">{log.foodName}</AppText>
      <AppText tone="muted">{formatDate(new Date(`${log.logDate}T00:00:00`), locale)} · {t(`nutrition.mealTypeValue.${log.mealType}`)}</AppText>
      <AppText tone="muted">{formatNumber(log.quantity, locale)} {log.unit}</AppText>
      <AppText>{t('nutrition.calories')}: {formatNumber(log.calories, locale)}</AppText>
      <AppText>{t('nutrition.protein')}: {formatNumber(log.protein, locale)}</AppText>
      <AppText>{t('nutrition.carbohydrates')}: {formatNumber(log.carbohydrates, locale)}</AppText>
      <AppText>{t('nutrition.fat')}: {formatNumber(log.fat, locale)}</AppText>
    </AppCard>
  );
}
