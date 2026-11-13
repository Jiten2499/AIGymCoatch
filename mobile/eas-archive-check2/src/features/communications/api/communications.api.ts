import { apiClient } from '../../../services/api/client';
import type { PageResult } from '../../../types/api';
import type { CommunicationInboxSummary, CommunicationInboxSync, CommunicationType, MemberCommunication } from '../types';

export async function listMemberCommunications(
  page: number,
  type?: CommunicationType | null,
  unreadOnly = false,
  signal?: AbortSignal,
): Promise<PageResult<MemberCommunication>> {
  const response = await apiClient.get<PageResult<MemberCommunication>>('/communications/inbox', {
    params: { page, pageSize: 20, type: type || undefined, unreadOnly: unreadOnly || undefined },
    signal,
  });
  return response.data;
}

export async function getMemberCommunication(id: string, signal?: AbortSignal): Promise<MemberCommunication> {
  const response = await apiClient.get<MemberCommunication>(`/communications/inbox/${id}`, { signal });
  return response.data;
}

export async function markCommunicationRead(id: string, signal?: AbortSignal): Promise<void> {
  await apiClient.post(`/communications/inbox/${id}/read`, null, { signal });
}

export async function countUnreadCommunications(signal?: AbortSignal): Promise<number> {
  const response = await apiClient.get<number>('/communications/inbox/unread-count', { signal });
  return response.data;
}

export async function getCommunicationInboxSummary(signal?: AbortSignal): Promise<CommunicationInboxSummary> {
  const response = await apiClient.get<CommunicationInboxSummary>('/communications/inbox/summary', { signal });
  return response.data;
}

export async function syncInboxCommunications(sinceUtc: string | null, take = 30, signal?: AbortSignal): Promise<CommunicationInboxSync> {
  const response = await apiClient.get<CommunicationInboxSync>('/communications/inbox/sync', {
    params: { sinceUtc: sinceUtc || undefined, take },
    signal,
  });
  return response.data;
}
