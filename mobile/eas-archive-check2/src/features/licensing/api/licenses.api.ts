import { apiClient } from '../../../services/api/client';

export type LicensePool = {
  licenseId: string;
  planCode: string;
  status: string;
  startsAt: string;
  expiresAt: string | null;
  baseCapacity: number;
  extraCapacity: number;
  assigned: number;
  available: number;
  usable: boolean;
};

export type LicenseAssignment = {
  id: string;
  userId: string;
  status: string;
  assignedAt: string;
  revokedAt: string | null;
};

export type FeatureFlag = {
  featureCode: string;
  enabled: boolean;
};

export async function listFeatureFlags(signal?: AbortSignal): Promise<FeatureFlag[]> {
  const response = await apiClient.get<FeatureFlag[]>('/features', { signal });
  return response.data;
}

export async function getLicensePool(signal?: AbortSignal): Promise<LicensePool> {
  const response = await apiClient.get<LicensePool>('/licenses/pool', { signal });
  return response.data;
}

export async function listAssignments(signal?: AbortSignal): Promise<LicenseAssignment[]> {
  const response = await apiClient.get<LicenseAssignment[]>('/licenses/assignments', { signal });
  return response.data;
}

export async function assignLicense(userId: string): Promise<LicenseAssignment> {
  const response = await apiClient.post<LicenseAssignment>('/licenses/assignments', { userId });
  return response.data;
}

export async function revokeAssignment(id: string): Promise<void> {
  await apiClient.post(`/licenses/assignments/${id}/revoke`);
}
