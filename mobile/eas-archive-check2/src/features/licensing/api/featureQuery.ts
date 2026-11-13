import { useQuery } from '@tanstack/react-query';
import { tenantKey } from '../../fitness/api/keys';
import { useTenantId } from '../../fitness/api/queries';
import { listFeatureFlags } from './licenses.api';

export function useWorkspaceFeatures() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'features'),
    enabled: Boolean(tenantId),
    staleTime: 0,
    refetchOnMount: 'always',
    queryFn: async ({ signal }) => {
      const rows = await listFeatureFlags(signal);
      return Object.fromEntries(rows.map((row) => [row.featureCode, row.enabled]));
    },
  });
}
