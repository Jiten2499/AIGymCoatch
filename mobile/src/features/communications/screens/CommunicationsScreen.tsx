import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useNavigation } from '@react-navigation/native';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, RefreshControl, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { formatDateTime } from '../../../services/localization/format';
import { errorText } from '../../../services/api/problem';
import { useMemberCommunications, useUnreadCommunicationCount } from '../api/queries';
import type { CommunicationType } from '../types';

const filters: Array<CommunicationType | 'All' | 'Unread'> = ['All', 'Unread', 'Announcement', 'Circular', 'Offer', 'Event', 'Alert'];

export function CommunicationsScreen() {
  const theme = useTheme();
  const { t, i18n } = useTranslation();
  const navigation = useNavigation<NativeStackNavigationProp<AppStackParamList>>();
  const [page, setPage] = useState(1);
  const [filter, setFilter] = useState<CommunicationType | 'All' | 'Unread'>('All');
  const unread = useUnreadCommunicationCount();
  const communications = useMemberCommunications(
    page,
    filter === 'All' || filter === 'Unread' ? null : filter,
    true,
    filter === 'Unread',
  );
  const items = communications.data?.items ?? [];
  const totalPages = communications.data && communications.data.pageSize > 0
    ? Math.ceil(communications.data.totalCount / communications.data.pageSize)
    : 1;

  return (
    <FeatureGate code={featureCodes.gymCommunications}>
      <ScreenState
        empty={communications.isSuccess && items.length === 0}
        emptyBody={t('communication.empty')}
        emptyTitle={t('nav.communications')}
        error={communications.isError ? errorText(communications.error, t('common.offline')) : null}
        loading={communications.isLoading}
        onRetry={() => void communications.refetch()}
      >
        <ScrollView
          contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}
          refreshControl={<RefreshControl refreshing={communications.isRefetching} onRefresh={() => void communications.refetch()} />}
        >
          <AppText variant="title">{t('nav.communications')}{unread.data ? ` (${unread.data})` : ''}</AppText>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.sm }}>
            {filters.map((value) => (
              <Pressable
                accessibilityRole="button"
                key={value}
                onPress={() => { setFilter(value); setPage(1); }}
                style={{
                  borderColor: theme.colors.border,
                  borderRadius: theme.radius.md,
                  borderWidth: 1,
                  minHeight: theme.control.minTouch,
                  justifyContent: 'center',
                  paddingHorizontal: theme.spacing.md,
                  backgroundColor: filter === value ? theme.colors.accent : 'transparent',
                }}
              >
                <AppText>{value === 'All' ? t('communication.filterAll') : value === 'Unread' ? t('communication.unread') : t(`communication.type.${value}`)}</AppText>
              </Pressable>
            ))}
          </View>
          {items.map((item) => (
            <Pressable accessibilityRole="button" key={item.id} onPress={() => navigation.navigate('CommunicationDetail', { communicationId: item.id })}>
              <AppCard>
                <AppText variant="label">{t(`communication.type.${item.type}`)}</AppText>
                <AppText>{item.title}</AppText>
                <AppText tone="muted" numberOfLines={2}>{item.message}</AppText>
                <AppText tone="muted">{formatDateTime(new Date(item.publishAt), i18n.language)}</AppText>
                <AppText>{item.isRead ? t('communication.read') : t('communication.unread')}</AppText>
              </AppCard>
            </Pressable>
          ))}
          {totalPages > 1 ? <AppText tone="muted">{t('journey.pageOf', { page, total: totalPages })}</AppText> : null}
          {page > 1 ? (
            <Pressable accessibilityRole="button" onPress={() => setPage((current) => current - 1)}>
              <AppText>{t('journey.previousPage')}</AppText>
            </Pressable>
          ) : null}
          {page < totalPages ? (
            <Pressable accessibilityRole="button" onPress={() => setPage((current) => current + 1)}>
              <AppText>{t('journey.nextPage')}</AppText>
            </Pressable>
          ) : null}
        </ScrollView>
      </ScreenState>
    </FeatureGate>
  );
}
