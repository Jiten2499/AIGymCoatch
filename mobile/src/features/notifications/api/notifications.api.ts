import { apiClient } from '../../../services/api/client';
import type { PageResult } from '../../../types/api';
import type { AppNotification } from '../types';

export async function listNotifications(page: number, signal?: AbortSignal): Promise<PageResult<AppNotification>> {
  const response = await apiClient.get<PageResult<AppNotification>>('/notifications', { params: { page, pageSize: 20 }, signal });
  return response.data;
}

export async function markNotificationRead(id: string, signal?: AbortSignal): Promise<AppNotification> {
  const response = await apiClient.post<AppNotification>(`/notifications/${id}/read`, null, { signal });
  return response.data;
}
