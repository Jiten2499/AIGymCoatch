import { apiClient } from '../../../services/api/client';
import type { PageResult } from '../../../types/api';
import type { AppNotification, NotificationSummary, NotificationSync } from '../types';

export async function listNotifications(page: number, signal?: AbortSignal): Promise<PageResult<AppNotification>> {
  const response = await apiClient.get<PageResult<AppNotification>>('/notifications', { params: { page, pageSize: 20 }, signal });
  return response.data;
}

export async function getNotificationSummary(signal?: AbortSignal): Promise<NotificationSummary> {
  const response = await apiClient.get<NotificationSummary>('/notifications/summary', { signal });
  return response.data;
}

export async function syncNotifications(sinceUtc: string | null, take = 50, signal?: AbortSignal): Promise<NotificationSync> {
  const response = await apiClient.get<NotificationSync>('/notifications/sync', {
    params: { sinceUtc: sinceUtc || undefined, take },
    signal,
  });
  return response.data;
}

export async function markNotificationRead(id: string, signal?: AbortSignal): Promise<AppNotification> {
  const response = await apiClient.post<AppNotification>(`/notifications/${id}/read`, null, { signal });
  return response.data;
}
