import { QueryClientProvider } from '@tanstack/react-query';
import { useEffect, type ReactNode } from 'react';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { registerSessionBridge, restoreSession } from '../../services/auth/session';
import { subscribePushNotifications } from '../../services/push/pushBridge';
import { readPreferences } from '../../services/storage/preferences';
import { usePreferencesStore } from '../../store/preferencesStore';
import { useSessionStore } from '../../store/sessionStore';
import { notificationSummaryKey } from '../../features/notifications/api/queries';
import { communicationSummaryKey, communicationUnreadKey } from '../../features/communications/api/queries';
import { tenantKey } from '../../features/fitness/api/keys';
import { bindQueryLifecycle } from './queryLifecycle';
import { queryClient } from './queryClient';
import { RealtimeProvider } from '../../features/chat/realtime';
import { ThemeProvider } from './ThemeProvider';

registerSessionBridge();

export function AppProviders({ children }: { children: ReactNode }) {
  const hydrate = usePreferencesStore((state) => state.hydrate);

  useEffect(() => {
    void readPreferences().then(hydrate);
    void restoreSession();
  }, [hydrate]);

  useEffect(() => bindQueryLifecycle(), []);

  useEffect(() => {
    return subscribePushNotifications((payload) => {
      const tenantId = useSessionStore.getState().currentTenant?.id;
      if (!tenantId || (payload.tenantId && payload.tenantId !== tenantId)) {
        return;
      }

      void queryClient.invalidateQueries({ queryKey: notificationSummaryKey(tenantId) });
      void queryClient.invalidateQueries({ queryKey: tenantKey(tenantId, 'notifications') });
      if (payload.communicationId) {
        void queryClient.invalidateQueries({ queryKey: communicationUnreadKey(tenantId) });
        void queryClient.invalidateQueries({ queryKey: communicationSummaryKey(tenantId) });
        void queryClient.invalidateQueries({ queryKey: tenantKey(tenantId, 'member-communications') });
      }
    });
  }, []);

  return (
    <SafeAreaProvider>
      <QueryClientProvider client={queryClient}>
        <ThemeProvider>
          <RealtimeProvider>{children}</RealtimeProvider>
        </ThemeProvider>
      </QueryClientProvider>
    </SafeAreaProvider>
  );
}
