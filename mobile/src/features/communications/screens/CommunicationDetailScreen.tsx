import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Image, ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { imageDataUri } from '../../../services/api/image';
import { formatDateTime } from '../../../services/localization/format';
import { errorText } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { useMarkCommunicationRead, useMemberCommunication } from '../api/queries';

type Props = NativeStackScreenProps<AppStackParamList, 'CommunicationDetail'>;

export function CommunicationDetailScreen({ route }: Props) {
  const theme = useTheme();
  const { t, i18n } = useTranslation();
  const tenantName = useSessionStore((state) => state.currentTenant?.name ?? '');
  const communication = useMemberCommunication(route.params.communicationId);
  const markRead = useMarkCommunicationRead();
  const item = communication.data;
  const [attachment, setAttachment] = useState<string | null>(null);

  useEffect(() => {
    if (item && !item.isRead && !markRead.isPending && !markRead.isSuccess) {
      markRead.mutate(item.id);
    }
  }, [item, markRead]);

  useEffect(() => {
    if (!item?.imageStorageKey) {
      setAttachment(null);
      return;
    }

    let cancelled = false;
    void imageDataUri(`/communications/${item.id}/image`).then((uri) => {
      if (!cancelled) {
        setAttachment(uri);
      }
    });
    return () => {
      cancelled = true;
    };
  }, [item?.id, item?.imageStorageKey]);

  return (
    <FeatureGate code={featureCodes.gymCommunications}>
      <ScreenState
        error={communication.isError ? errorText(communication.error, t('common.offline')) : null}
        loading={communication.isLoading}
        onRetry={() => void communication.refetch()}
      >
        {item ? (
          <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
            <AppCard>
              <AppText variant="label">{t('notification.circularBadge')}</AppText>
              <AppText variant="label">{t(`communication.type.${item.type}`)}</AppText>
              <AppText variant="title">{item.title}</AppText>
              <AppText tone="muted">{formatDateTime(new Date(item.publishAt), i18n.language)}</AppText>
              <AppText tone="muted">
                {[item.createdBy, tenantName].filter(Boolean).join(' · ') || tenantName}
              </AppText>
              <AppText tone="muted">{t(`communication.priority.${item.priority}`)}</AppText>
              <AppText>{item.message}</AppText>
              {attachment ? (
                <Image
                  accessibilityLabel={t('notification.attachment')}
                  source={{ uri: attachment }}
                  style={{ borderRadius: theme.radius.md, height: 220, width: '100%' }}
                />
              ) : null}
            </AppCard>
          </ScrollView>
        ) : null}
      </ScreenState>
    </FeatureGate>
  );
}
