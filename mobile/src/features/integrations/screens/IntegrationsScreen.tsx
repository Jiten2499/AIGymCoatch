import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Linking, ScrollView } from 'react-native';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import {
  beginGoogleDriveConnection,
  disconnectGoogleDrive,
  getGoogleDriveConnection,
  integrationsQueryKey,
  testGoogleDriveConnection,
} from '../api/integrations.api';

export function IntegrationsScreen() {
  return (
    <FeatureGate code={featureCodes.googleDriveStorage}>
      <IntegrationsBody />
    </FeatureGate>
  );
}

function IntegrationsBody() {
  const theme = useTheme();
  const { t } = useTranslation();
  const client = useQueryClient();
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  const connection = useQuery({
    queryKey: integrationsQueryKey(tenantId),
    queryFn: ({ signal }) => getGoogleDriveConnection(signal),
    enabled: tenantId.length > 0,
  });

  useEffect(() => {
    const sub = Linking.addEventListener('url', (event) => {
      if (event.url.includes('integrations/google-drive')) {
        void client.invalidateQueries({ queryKey: integrationsQueryKey(tenantId) });
      }
    });
    return () => sub.remove();
  }, [client, tenantId]);

  const connect = useMutation({
    mutationFn: beginGoogleDriveConnection,
    onSuccess: async (data) => {
      await Linking.openURL(data.authorizationUrl);
    },
  });
  const test = useMutation({
    mutationFn: testGoogleDriveConnection,
    onSuccess: async () => { await client.invalidateQueries({ queryKey: integrationsQueryKey(tenantId) }); },
  });
  const disconnect = useMutation({
    mutationFn: disconnectGoogleDrive,
    onSuccess: async () => { await client.invalidateQueries({ queryKey: integrationsQueryKey(tenantId) }); },
  });

  const data = connection.data;
  const connected = data?.status === 'Connected';
  const mutationError = connect.error ?? test.error ?? disconnect.error;

  return (
    <ScreenState
      error={connection.isError || mutationError ? errorText(connection.error ?? mutationError, t('common.offline')) : null}
      loading={connection.isLoading}
      onRetry={() => void connection.refetch()}
    >
      <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
        <AppText variant="title">{t('integrations.title')}</AppText>
        <AppText>{t('integrations.googleDrive')}</AppText>
        <AppText tone="muted">{t('integrations.storageBlurb')}</AppText>
        <AppText>{t('integrations.status')}: {data?.status ?? t('integrations.notConnected')}</AppText>
        {data?.accountEmail ? <AppText>{t('integrations.account')}: {data.accountEmail}</AppText> : null}
        {data?.rootFolderId ? <AppText>{t('integrations.folder')}</AppText> : null}
        {data?.lastError ? <AppText>{data.lastError}</AppText> : null}
        {!connected ? (
          <AppButton label={t('integrations.connect')} onPress={() => void connect.mutateAsync()} />
        ) : (
          <>
            <AppButton label={t('integrations.test')} onPress={() => void test.mutateAsync()} />
            <AppButton label={t('integrations.disconnect')} onPress={() => void disconnect.mutateAsync()} />
            <AppButton label={t('integrations.reconnect')} onPress={() => void connect.mutateAsync()} />
          </>
        )}
      </ScrollView>
    </ScreenState>
  );
}
