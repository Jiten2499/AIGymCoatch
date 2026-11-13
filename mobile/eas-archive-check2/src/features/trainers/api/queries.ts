import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSessionStore } from '../../../store/sessionStore';
import { listMyTargets, myTargetsQueryKey, submitMyTarget } from './targets.api';

export function useMyTargets(enabled: boolean) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useQuery({
    queryKey: myTargetsQueryKey(tenantId),
    queryFn: ({ signal }) => listMyTargets(signal),
    enabled: enabled && tenantId.length > 0,
  });
}

export function useSubmitTarget() {
  const client = useQueryClient();
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useMutation({
    mutationFn: ({ assignmentId, details }: { assignmentId: string; details: string }) => submitMyTarget(assignmentId, details),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: myTargetsQueryKey(tenantId) });
    },
  });
}
