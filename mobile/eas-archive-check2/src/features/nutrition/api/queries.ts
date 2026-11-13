import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { isNotFound } from '../../../services/api/problem';
import { tenantKey } from '../../fitness/api/keys';
import { useTenantId } from '../../fitness/api/queries';
import { createNutritionLog, getNutritionProfile, getNutritionSummary, listNutritionLogs, saveNutritionProfile } from './nutrition.api';
import type { NutritionLogInput, NutritionProfileInput } from '../types';

export function useNutritionProfile() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'nutrition', 'profile'),
    enabled: Boolean(tenantId),
    retry: (count, error) => !isNotFound(error) && count < 1,
    queryFn: ({ signal }) => getNutritionProfile(signal),
  });
}

export function useSaveNutritionProfile() {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: NutritionProfileInput) => saveNutritionProfile(body),
    onSuccess: async (profile) => {
      client.setQueryData(tenantKey(tenantId, 'nutrition', 'profile'), profile);
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'nutrition', 'summary') });
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'dashboard') });
    },
  });
}

export function useNutritionSummary(date: string) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'nutrition', 'summary', date),
    enabled: Boolean(tenantId && date),
    queryFn: ({ signal }) => getNutritionSummary(date, signal),
  });
}

export function useNutritionLogs(page: number, date: string | null) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'nutrition', 'logs', date ?? 'all', page),
    enabled: Boolean(tenantId),
    queryFn: ({ signal }) => listNutritionLogs(page, date, signal),
  });
}

export function useCreateNutritionLog() {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: NutritionLogInput) => createNutritionLog(body),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'nutrition', 'logs') });
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'nutrition', 'summary') });
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'dashboard') });
    },
  });
}
