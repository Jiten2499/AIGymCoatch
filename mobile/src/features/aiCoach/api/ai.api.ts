import { apiClient } from '../../../services/api/client';
import type { PageResult } from '../../../types/api';
import type { AiChat, WorkoutProgram } from '../../fitness/types';
import type { ConversationDetail, ConversationSummary } from '../types';

const aiTimeout = 60_000;

export async function generateWorkout(notes: string | null, signal?: AbortSignal): Promise<WorkoutProgram> {
  const response = await apiClient.post<WorkoutProgram>('/ai/workouts/generate', { notes }, { timeout: aiTimeout, signal });
  return response.data;
}

export async function coachChat(message: string, conversationId: string | null, signal?: AbortSignal): Promise<AiChat> {
  const response = await apiClient.post<AiChat>('/ai/coach/chat', { message, conversationId }, { timeout: aiTimeout, signal });
  return response.data;
}

export async function nutritionAdvice(question: string | null, signal?: AbortSignal): Promise<AiChat> {
  const response = await apiClient.post<AiChat>('/ai/nutrition/advice', { question }, { timeout: aiTimeout, signal });
  return response.data;
}

export async function listConversations(page: number, signal?: AbortSignal): Promise<PageResult<ConversationSummary>> {
  const response = await apiClient.get<PageResult<ConversationSummary>>('/ai/conversations', { params: { page, pageSize: 20 }, signal });
  return response.data;
}

export async function getConversation(id: string, page: number, signal?: AbortSignal): Promise<ConversationDetail> {
  const response = await apiClient.get<ConversationDetail>(`/ai/conversations/${id}`, { params: { page, pageSize: 100 }, signal });
  return response.data;
}

export async function progressInsight(signal?: AbortSignal): Promise<AiChat> {
  const response = await apiClient.post<AiChat>('/ai/progress/insight', null, { timeout: aiTimeout, signal });
  return response.data;
}
