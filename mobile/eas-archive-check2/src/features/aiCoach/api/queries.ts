import { useQuery } from '@tanstack/react-query';
import { tenantKey } from '../../fitness/api/keys';
import { useTenantId } from '../../fitness/api/queries';
import { getConversation, listConversations } from './ai.api';

export function useConversations(page: number, enabled = true) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'ai', 'conversations', page),
    enabled: Boolean(tenantId && enabled),
    queryFn: ({ signal }) => listConversations(page, signal),
  });
}

export function useConversation(id: string, page: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'ai', 'conversation', id, page),
    enabled: Boolean(tenantId && id),
    queryFn: ({ signal }) => getConversation(id, page, signal),
  });
}
