import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { tenantKey } from '../../fitness/api/keys';
import { useSessionStore } from '../../../store/sessionStore';
import { applyLeave, cancelLeave, getLeave, listMyLeaves, type ApplyLeaveInput } from './leaves.api';

export function useMyLeaves(enabled: boolean) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useQuery({
    queryKey: tenantKey(tenantId, 'my-leaves'),
    enabled: enabled && Boolean(tenantId),
    queryFn: ({ signal }) => listMyLeaves(signal),
  });
}

export function useLeave(id: string, enabled: boolean) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useQuery({
    queryKey: tenantKey(tenantId, 'leave', id),
    enabled: enabled && Boolean(tenantId) && Boolean(id),
    queryFn: ({ signal }) => getLeave(id, signal),
  });
}

export function useApplyLeave() {
  const client = useQueryClient();
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useMutation({
    mutationFn: (input: ApplyLeaveInput) => applyLeave(input),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'my-leaves') });
    },
  });
}

export function useCancelLeave() {
  const client = useQueryClient();
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useMutation({
    mutationFn: (id: string) => cancelLeave(id),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'my-leaves') });
    },
  });
}
