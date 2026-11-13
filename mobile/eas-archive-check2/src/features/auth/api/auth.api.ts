import type { AuthResponse, WorkspaceSummary } from '../../../types/api';
import { apiClient, refreshClient } from '../../../services/api/client';
import { createCorrelationId } from '../../../services/api/correlation';
import { toApiError } from '../../../services/api/problem';

export type LoginBody = { email: string; password: string };
export type RegisterBody = LoginBody & { firstName: string; lastName: string; phone?: string };

export async function login(body: LoginBody, signal?: AbortSignal): Promise<AuthResponse> {
  const response = await apiClient.post<AuthResponse>('/auth/login', body, { signal });
  return response.data;
}

export async function register(body: RegisterBody, signal?: AbortSignal): Promise<AuthResponse> {
  const response = await apiClient.post<AuthResponse>('/auth/register', body, { signal });
  return response.data;
}

export async function forgotPassword(email: string, signal?: AbortSignal): Promise<void> {
  await apiClient.post('/auth/forgot-password', { email }, { signal });
}

export async function logout(refreshToken: string): Promise<void> {
  await apiClient.post('/auth/logout', { refreshToken });
}

export async function refreshSession(refreshToken: string): Promise<AuthResponse> {
  try {
    const response = await refreshClient.post<AuthResponse>(
      '/auth/refresh',
      { refreshToken },
      { headers: { 'X-Correlation-ID': createCorrelationId() } },
    );
    return response.data;
  } catch (error) {
    throw toApiError(error);
  }
}

export async function listWorkspaces(signal?: AbortSignal): Promise<WorkspaceSummary[]> {
  const response = await apiClient.get<WorkspaceSummary[]>('/tenants/me', { signal });
  return response.data;
}

export async function switchWorkspace(tenantId: string, signal?: AbortSignal): Promise<AuthResponse> {
  const response = await apiClient.post<AuthResponse>('/tenants/switch', { tenantId }, { signal });
  return response.data;
}
