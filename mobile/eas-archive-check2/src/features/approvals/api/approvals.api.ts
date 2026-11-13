import { apiClient } from '../../../services/api/client';
import type { PageResult } from '../../../types/api';

export type ApprovalRequestItem = {
  id: string;
  requestType: 'MembershipFreeze' | 'MembershipExtension' | 'TrainerRewardAward';
  title: string;
  summary: string;
  requestedByName: string;
  requestedAt: string;
  relatedLabel: string;
  amount: number | null;
  currency: string | null;
  days: number | null;
  status: 'Pending' | 'Approved' | 'Rejected';
};

export type ApprovalRequestDetails = ApprovalRequestItem & {
  reason: string | null;
  memberProfileId: string | null;
  sourceId: string;
  originalEndDate: string | null;
  proposedEndDate: string | null;
  decisionComment: string | null;
  history: { action: string; at: string; actorName: string; comment: string | null }[];
};

export function approvalQueryKey(tenantId: string, status: string, requestType: string, page: number) {
  return ['tenant', tenantId, 'approval-requests', status, requestType, page] as const;
}

export async function listApprovalRequests(status: string, requestType: string, page: number, signal?: AbortSignal) {
  const response = await apiClient.get<PageResult<ApprovalRequestItem>>('/approval-requests', {
    params: {
      page,
      pageSize: 20,
      sort: 'createdAt',
      status: status || undefined,
      includeAllStatuses: status ? undefined : true,
      requestType: requestType || undefined,
    },
    signal,
  });
  return response.data;
}

export async function getApprovalCount(signal?: AbortSignal) {
  const response = await apiClient.get<{ pendingCount: number }>('/approval-requests/pending-count', { signal });
  return response.data;
}

export async function getApprovalRequest(id: string) {
  const response = await apiClient.get<ApprovalRequestDetails>(`/approval-requests/${id}`);
  return response.data;
}

export async function decideApprovalRequest(id: string, approve: boolean, comment: string) {
  const response = await apiClient.post<ApprovalRequestDetails>(`/approval-requests/${id}/${approve ? 'approve' : 'reject'}`, { comment: comment.trim() || null });
  return response.data;
}
