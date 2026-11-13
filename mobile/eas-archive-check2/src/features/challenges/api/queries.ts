import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { tenantKey } from '../../fitness/api/keys';
import { useTenantId } from '../../fitness/api/queries';
import { joinChallenge, listChallenges, recordChallengeProgress } from './challenges.api';

export function useChallenges(page: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'challenges', page),
    enabled: Boolean(tenantId),
    queryFn: ({ signal }) => listChallenges(page, signal),
  });
}

export function useJoinChallenge() {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => joinChallenge(id),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'challenges') });
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'dashboard') });
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'notifications') });
    },
  });
}

export function useRecordChallengeProgress() {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, value }: { id: string; value: number }) => recordChallengeProgress(id, value),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'challenges') });
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'dashboard') });
    },
  });
}
