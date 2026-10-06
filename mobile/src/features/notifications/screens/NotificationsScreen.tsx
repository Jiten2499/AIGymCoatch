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
import { formatDateTime } from '../../../services/localization/format';
import { errorText } from '../../../services/api/problem';
import { useMarkNotificationRead, useNotifications } from '../api/queries';

export function NotificationsScreen() {
  const theme = useTheme();
  const { t, i18n } = useTranslation();
  const [page, setPage] = useState(1);
  const notifications = useNotifications(page);
  const markRead = useMarkNotificationRead();
  const items = notifications.data?.items ?? [];
  const totalPages = notifications.data && notifications.data.pageSize > 0 ? Math.ceil(notifications.data.totalCount / notifications.data.pageSize) : 1;

  return (
    <FeatureGate code={featureCodes.workoutPlans}>
      <ScreenState empty={notifications.isSuccess && items.length === 0} emptyBody={t('notification.empty')} emptyTitle={t('nav.notifications')} error={notifications.isError ? errorText(notifications.error, t('common.offline')) : null} loading={notifications.isLoading} onRetry={() => void notifications.refetch()}>
        <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }} refreshControl={<RefreshControl refreshing={notifications.isRefetching} onRefresh={() => void notifications.refetch()} />}>
          <AppText variant="title">{t('nav.notifications')}</AppText>
          <AppText tone="muted">{t('notification.noDeepLink')}</AppText>
          {items.map((item) => (
            <AppCard key={item.id}>
              <AppText variant="label">{item.title}</AppText>
              <AppText>{item.message}</AppText>
              <AppText tone="muted">{t(`notification.type.${item.type}`)}</AppText>
              <AppText tone="muted">{formatDateTime(new Date(item.createdAt), i18n.language)}</AppText>
              <AppText>{item.isRead ? t('notification.read') : t('notification.unread')}</AppText>
              {item.isRead ? null : (
                <AppButton
                  label={t('notification.markRead')}
                  loading={markRead.isPending && markRead.variables === item.id}
                  onPress={() => markRead.mutate(item.id)}
                  variant="secondary"
                />
              )}
            </AppCard>
          ))}
          {markRead.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(markRead.error, t('common.offline'))}</AppText> : null}
          {totalPages > 1 ? <AppText tone="muted">{t('journey.pageOf', { page, total: totalPages })}</AppText> : null}
          {page > 1 ? <AppButton label={t('journey.previousPage')} onPress={() => setPage(page - 1)} variant="secondary" /> : null}
          {page < totalPages ? <AppButton label={t('journey.nextPage')} onPress={() => setPage(page + 1)} variant="secondary" /> : null}
        </ScrollView>
      </ScreenState>
    </FeatureGate>
  );
}
