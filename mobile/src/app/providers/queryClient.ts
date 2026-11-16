import { QueryCache, QueryClient } from '@tanstack/react-query';
import { markQueryOnline, reportQueryConnectivity, shouldRetryQuery } from './queryLifecycle';

export const queryClient = new QueryClient({
  queryCache: new QueryCache({
    onError: (error) => reportQueryConnectivity(error),
    onSuccess: () => markQueryOnline(),
  }),
  defaultOptions: {
    queries: {
      retry: shouldRetryQuery,
      retryDelay: (attempt) => Math.min(400 * 2 ** attempt, 3_000),
      staleTime: 30_000,
      refetchOnReconnect: true,
      refetchOnWindowFocus: true,
    },
  },
});
