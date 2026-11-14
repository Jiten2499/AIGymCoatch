import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { isNotFound } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { tenantKey } from '../../fitness/api/keys';
import {
  assessmentCompletionKey,
  assessmentKey,
  deleteFitnessAssessment,
  getFitnessAssessment,
  getFitnessAssessmentCompletion,
  saveFitnessAssessment,
} from './assessment.api';

function useTenantId() {
  return useSessionStore((state) => state.currentTenant?.id ?? '');
}

export function useFitnessAssessment() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: assessmentKey(tenantId),
    queryFn: async ({ signal }) => {
      try {
        return await getFitnessAssessment(signal);
      } catch (error) {
        if (isNotFound(error)) {
          return null;
        }
        throw error;
      }
    },
    enabled: tenantId.length > 0,
  });
}

export function useFitnessAssessmentCompletion() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: assessmentCompletionKey(tenantId),
    queryFn: ({ signal }) => getFitnessAssessmentCompletion(signal),
    enabled: tenantId.length > 0,
  });
}

export function useSaveFitnessAssessment() {
  const client = useQueryClient();
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: ({ body, create }: { body: Record<string, unknown>; create: boolean }) => saveFitnessAssessment(body, create),
    onSuccess: async (data) => {
      client.setQueryData(assessmentKey(tenantId), data);
      await client.invalidateQueries({ queryKey: assessmentCompletionKey(tenantId) });
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'fitnessProfile') });
    },
  });
}

export function useDeleteFitnessAssessment() {
  const client = useQueryClient();
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: deleteFitnessAssessment,
    onSuccess: async () => {
      client.setQueryData(assessmentKey(tenantId), null);
      await client.invalidateQueries({ queryKey: assessmentCompletionKey(tenantId) });
    },
  });
}
