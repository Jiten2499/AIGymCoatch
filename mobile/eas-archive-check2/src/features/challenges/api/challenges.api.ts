import { apiClient } from '../../../services/api/client';
import type { PageResult } from '../../../types/api';
import type { Challenge } from '../types';

export async function listChallenges(page: number, signal?: AbortSignal): Promise<PageResult<Challenge>> {
  const response = await apiClient.get<PageResult<Challenge>>('/challenges', { params: { page, pageSize: 20 }, signal });
  return response.data;
}

export async function joinChallenge(id: string, signal?: AbortSignal): Promise<Challenge> {
  const response = await apiClient.post<Challenge>(`/challenges/${id}/join`, null, { signal });
  return response.data;
}

export async function recordChallengeProgress(id: string, value: number, signal?: AbortSignal): Promise<Challenge> {
  const response = await apiClient.post<Challenge>(`/challenges/${id}/progress`, { value }, { signal });
  return response.data;
}
