import { apiClient } from '../../../services/api/client';

export type StaffLeaveListItem = {
  id: string;
  staffProfileId: string;
  userId: string;
  staffName: string;
  employeeCode: string;
  leaveType: string;
  startDate: string;
  endDate: string;
  durationDays: number;
  status: string;
  reason: string;
  approvalRequestId: string | null;
  submittedAt: string;
};

export type StaffLeaveDetails = StaffLeaveListItem & {
  attachmentFileId: string | null;
  decisionComment: string | null;
  decidedByUserId: string | null;
  decidedAt: string | null;
  canCancel: boolean;
};

export type ApplyLeaveInput = {
  leaveType: string;
  startDate: string;
  endDate: string;
  reason: string;
  attachmentFileId?: string | null;
};

export async function listMyLeaves(signal?: AbortSignal) {
  const response = await apiClient.get<StaffLeaveListItem[]>('/leaves/mine', { signal });
  return response.data;
}

export async function getLeave(id: string, signal?: AbortSignal) {
  const response = await apiClient.get<StaffLeaveDetails>(`/leaves/${id}`, { signal });
  return response.data;
}

export async function applyLeave(input: ApplyLeaveInput) {
  const response = await apiClient.post<StaffLeaveDetails>('/leaves', input);
  return response.data;
}

export async function cancelLeave(id: string) {
  const response = await apiClient.post<StaffLeaveDetails>(`/leaves/${id}/cancel`);
  return response.data;
}

export function leaveDuration(startDate: string, endDate: string): number {
  if (!startDate || !endDate) return 0;
  const start = Date.parse(`${startDate}T00:00:00Z`);
  const end = Date.parse(`${endDate}T00:00:00Z`);
  if (Number.isNaN(start) || Number.isNaN(end) || end < start) return 0;
  return Math.floor((end - start) / 86_400_000) + 1;
}
