import { apiClient } from '../../../services/api/client';

export type MyTrainerTarget = {
  id: string;
  name: string;
  metric: string;
  goalValue: number;
  awardPoints: number;
  period: string;
  effectiveStart: string;
  effectiveEnd: string | null;
  status: string;
  submissionStatus: string | null;
  submissionDetails: string | null;
};

export function myTargetsQueryKey(tenantId: string) {
  return ['tenant', tenantId, 'my-trainer-targets'] as const;
}

export async function listMyTargets(signal?: AbortSignal) {
  const response = await apiClient.get<MyTrainerTarget[]>('/trainers/me/targets', { signal });
  return response.data;
}

export async function submitMyTarget(assignmentId: string, details: string) {
  const response = await apiClient.post(`/trainers/me/targets/${assignmentId}/submit`, { details });
  return response.data;
}
