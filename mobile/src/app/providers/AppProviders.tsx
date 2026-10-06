import { QueryClientProvider } from '@tanstack/react-query';
import { useEffect, type ReactNode } from 'react';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { registerSessionBridge, restoreSession } from '../../services/auth/session';
import { readPreferences } from '../../services/storage/preferences';
import { usePreferencesStore } from '../../store/preferencesStore';
import { queryClient } from './queryClient';
import { ThemeProvider } from './ThemeProvider';

registerSessionBridge();

export function AppProviders({ children }: { children: ReactNode }) {
  const hydrate = usePreferencesStore((state) => state.hydrate);

  useEffect(() => {
    void readPreferences().then(hydrate);
    void restoreSession();
  }, [hydrate]);

  return (
    <SafeAreaProvider>
      <QueryClientProvider client={queryClient}>
        <ThemeProvider>{children}</ThemeProvider>
      </QueryClientProvider>
    </SafeAreaProvider>
  );
}
