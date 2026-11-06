import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useIsFocused, useNavigation } from '@react-navigation/native';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, RefreshControl, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { formatDateTime } from '../../../services/localization/format';
import { errorText } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { useMemberCommunications } from '../../communications/api/queries';
import { useWorkspaceFeatures } from '../../licensing/api/featureQuery';
import { mergeNotificationFeed } from '../api/feed';
import { useMarkNotificationRead, useNotifications } from '../api/queries';
import type { FeedItem } from '../types';

export function NotificationsScreen() {
  const theme = useTheme();
  const { t, i18n } = useTranslation();
  const focused = useIsFocused();
  const navigation = useNavigation<NativeStackNavigationProp<AppStackParamList>>();
  const tenantName = useSessionStore((state) => state.currentTenant?.name ?? '');
  const features = useWorkspaceFeatures();
  const circularsEnabled = features.data?.[featureCodes.gymCommunications] === true;
  const [page, setPage] = useState(1);
  const notifications = useNotifications(page, { pollWhileActive: focused });
  const circulars = useMemberCommunications(1, null, circularsEnabled, false);
  const markRead = useMarkNotificationRead();
  const systemItems = notifications.data?.items ?? [];
  const circularItems = circulars.data?.items ?? [];
  const feed = useMemo(() => mergeNotificationFeed(systemItems, circularItems), [systemItems, circularItems]);
  const totalPages = notifications.data && notifications.data.pageSize > 0
    ? Math.ceil(notifications.data.totalCount / notifications.data.pageSize)
    : 1;
  const loading = notifications.isLoading || (circularsEnabled && circulars.isLoading);
  const error = notifications.isError
    ? errorText(notifications.error, t('common.offline'))
    : circularsEnabled && circulars.isError
      ? errorText(circulars.error, t('common.offline'))
      : null;

  async function refresh() {
    await Promise.all([
      notifications.refetch(),
      circularsEnabled ? circulars.refetch() : Promise.resolve(),
    ]);
  }

  return (
    <FeatureGate alternateCode={featureCodes.gymCommunications} code={featureCodes.workoutPlans}>
      <ScreenState
        empty={notifications.isSuccess && feed.length === 0}
        emptyBody={t('notification.empty')}
        emptyTitle={t('nav.notifications')}
        error={error}
        loading={loading}
        onRetry={() => void refresh()}
      >
        <ScrollView
          contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}
          refreshControl={<RefreshControl refreshing={notifications.isRefetching || circulars.isRefetching} onRefresh={() => void refresh()} />}
        >
          <AppText variant="title">{t('nav.notifications')}</AppText>
          <AppText tone="muted">{t('notification.feedHint')}</AppText>
          {feed.map((item) => (
            <FeedCard
              key={`${item.kind}-${item.id}`}
              item={item}
              language={i18n.language}
              markReadPending={markRead.isPending && markRead.variables === item.id}
              onMarkNotificationRead={(id) => markRead.mutate(id)}
              onOpenCircular={(id) => navigation.navigate('CommunicationDetail', { communicationId: id })}
              tenantName={tenantName}
            />
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

function FeedCard({
  item,
  language,
  markReadPending,
  onMarkNotificationRead,
  onOpenCircular,
  tenantName,
}: {
  item: FeedItem;
  language: string;
  markReadPending: boolean;
  onMarkNotificationRead: (id: string) => void;
  onOpenCircular: (id: string) => void;
  tenantName: string;
}) {
  const theme = useTheme();
  const { t } = useTranslation();

  if (item.kind === 'circular') {
    return (
      <Pressable accessibilityRole="button" onPress={() => onOpenCircular(item.id)}>
        <AppCard>
          <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: theme.spacing.sm }}>
            <AppText variant="label">{t('notification.circularBadge')}</AppText>
            <AppText tone="muted">{item.isRead ? t('notification.read') : t('notification.unread')}</AppText>
          </View>
          <AppText variant="label">{t(`communication.type.${item.circularType}`)}</AppText>
          <AppText>{item.title}</AppText>
          <AppText numberOfLines={2} tone="muted">{item.message}</AppText>
          <AppText tone="muted">{formatDateTime(new Date(item.occurredAt), language)}</AppText>
          <AppText tone="muted">
            {[item.createdBy, tenantName].filter(Boolean).join(' · ') || tenantName}
          </AppText>
          {item.hasAttachment ? <AppText tone="muted">{t('notification.attachment')}</AppText> : null}
          <AppText tone="muted">{t('notification.openCircular')}</AppText>
        </AppCard>
      </Pressable>
    );
  }

  return (
    <AppCard>
      <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: theme.spacing.sm }}>
        <AppText variant="label">{t('notification.systemBadge')}</AppText>
        <AppText tone="muted">{item.isRead ? t('notification.read') : t('notification.unread')}</AppText>
      </View>
      <AppText variant="label">{item.title}</AppText>
      <AppText>{item.message}</AppText>
      <AppText tone="muted">{t(`notification.type.${item.notificationType}`)}</AppText>
      <AppText tone="muted">{formatDateTime(new Date(item.occurredAt), language)}</AppText>
      {item.isRead ? null : (
        <AppButton
          label={t('notification.markRead')}
          loading={markReadPending}
          onPress={() => onMarkNotificationRead(item.id)}
          variant="secondary"
        />
      )}
    </AppCard>
  );
}
