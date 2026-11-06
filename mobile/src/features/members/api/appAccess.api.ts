import { apiClient } from '../../../services/api/client';
import { tenantKey } from '../../fitness/api/keys';

export type WorkspacePlan = {
  subscriptionId: string;
  planCode: string;
  planName: string;
  description: string | null;
  status: string;
  seats: number;
  assigned: number;
  available: number;
  usable: boolean;
  features: { code: string; enabled: boolean }[];
};

export type MemberAppAccess = {
  memberProfileId: string;
  userId: string | null;
  hasBasicAccess: boolean;
  hasAppPlan: boolean;
  hasAppAccess: boolean;
  basicStatus: string;
  planStatus: string;
  seatId: string | null;
  subscriptionId: string | null;
  planCode: string | null;
  planName: string | null;
  assignedAt: string | null;
  availableSeats: number | null;
};

export const workspacePlansQueryKey = (tenantId: string) => tenantKey(tenantId, 'workspace-plans');
export const memberAppAccessQueryKey = (tenantId: string, memberId: string) => tenantKey(tenantId, 'member-app-access', memberId);

export async function listWorkspacePlans(signal?: AbortSignal) {
  const response = await apiClient.get<WorkspacePlan[]>('/business/plans', { signal });
  return response.data;
}

export async function getMemberAppAccess(memberId: string, signal?: AbortSignal) {
  const response = await apiClient.get<MemberAppAccess>(`/members/${memberId}/app-access`, { signal });
  return response.data;
}

export async function enableMemberBasicAccess(memberId: string, email: string) {
  const response = await apiClient.post<MemberAppAccess>(`/members/${memberId}/basic-access`, { email });
  return response.data;
}

export async function assignMemberAppAccess(memberId: string, subscriptionId: string, email?: string | null) {
  const response = await apiClient.post<MemberAppAccess>(`/members/${memberId}/app-access`, { subscriptionId, email: email ?? null });
  return response.data;
}

export async function removeMemberAppAccess(memberId: string) {
  await apiClient.delete(`/members/${memberId}/app-access`);
}
