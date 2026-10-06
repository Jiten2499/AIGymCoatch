import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { tenantKey } from '../../fitness/api/keys';
import { useTenantId } from '../../fitness/api/queries';
import { listNotifications, markNotificationRead } from './notifications.api';

export function useNotifications(page: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'notifications', page),
    enabled: Boolean(tenantId),
    queryFn: ({ signal }) => listNotifications(page, signal),
  });
}

export function useMarkNotificationRead() {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => markNotificationRead(id),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'notifications') });
    },
  });
}
