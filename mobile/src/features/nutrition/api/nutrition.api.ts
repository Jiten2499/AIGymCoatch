import { apiClient } from '../../../services/api/client';
import type { PageResult } from '../../../types/api';
import type { NutritionLog, NutritionLogInput, NutritionProfile, NutritionProfileInput, NutritionSummary } from '../types';

export async function getNutritionProfile(signal?: AbortSignal): Promise<NutritionProfile> {
  const response = await apiClient.get<NutritionProfile>('/nutrition/profile', { signal });
  return response.data;
}

export async function saveNutritionProfile(body: NutritionProfileInput, signal?: AbortSignal): Promise<NutritionProfile> {
  const response = await apiClient.put<NutritionProfile>('/nutrition/profile', body, { signal });
  return response.data;
}

export async function listNutritionLogs(page: number, date: string | null, signal?: AbortSignal): Promise<PageResult<NutritionLog>> {
  const response = await apiClient.get<PageResult<NutritionLog>>('/nutrition/logs', {
    params: { page, pageSize: 20, ...(date ? { date } : {}) },
    signal,
  });
  return response.data;
}

export async function createNutritionLog(body: NutritionLogInput, signal?: AbortSignal): Promise<NutritionLog> {
  const response = await apiClient.post<NutritionLog>('/nutrition/logs', body, { signal });
  return response.data;
}

export async function getNutritionSummary(date: string, signal?: AbortSignal): Promise<NutritionSummary> {
  const response = await apiClient.get<NutritionSummary>('/nutrition/summary', { params: { date }, signal });
  return response.data;
}
