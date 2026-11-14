import { apiClient } from '../../../services/api/client';
import type { FitnessAssessment, FitnessAssessmentCompletion } from '../types';

export const assessmentKey = (tenantId: string) => ['fitness-assessment', tenantId] as const;
export const assessmentCompletionKey = (tenantId: string) => ['fitness-assessment-completion', tenantId] as const;

export async function getFitnessAssessment(signal?: AbortSignal) {
  const response = await apiClient.get<FitnessAssessment>('/fitness-assessment', { signal });
  return response.data;
}

export async function getFitnessAssessmentCompletion(signal?: AbortSignal) {
  const response = await apiClient.get<FitnessAssessmentCompletion>('/fitness-assessment/completion', { signal });
  return response.data;
}

export async function saveFitnessAssessment(body: Record<string, unknown>, create: boolean) {
  const response = create
    ? await apiClient.post<FitnessAssessment>('/fitness-assessment', body)
    : await apiClient.put<FitnessAssessment>('/fitness-assessment', body);
  return response.data;
}

export async function deleteFitnessAssessment() {
  const response = await apiClient.delete<string>('/fitness-assessment');
  return response.data;
}
