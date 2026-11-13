import { apiClient } from '../../../services/api/client';
import type { PageResult } from '../../../types/api';
import { tenantKey } from '../../fitness/api/keys';

export type LeadListItem = {
  id: string;
  fullName: string;
  mobile: string;
  source: string;
  status: string;
  interestedPackage: string | null;
  assignedTo: string | null;
  nextFollowUpAt: string | null;
  trialAt: string | null;
};

export type LeadDetails = LeadListItem & {
  email: string | null;
  notes: string | null;
  interestedPlanId: string | null;
  assignedUserId: string | null;
  convertedMemberId: string | null;
  lostReason: string | null;
  createdAt: string;
};

export type FollowUpItem = {
  id: string;
  leadId: string;
  scheduledAt: string;
  completedAt: string | null;
  notes: string | null;
  outcome: string | null;
  leadName: string | null;
};

export type TrialItem = {
  id: string;
  leadId: string;
  scheduledAt: string;
  status: string;
  notes: string | null;
  outcome: string | null;
};

export type LeadInput = {
  fullName: string;
  mobile: string;
  email?: string | null;
  source: string;
  interestedPlanId?: string | null;
  assignedUserId?: string | null;
  nextFollowUpAt?: string | null;
  notes?: string | null;
};

export const leadsQueryKey = (tenantId: string, term: string, status: string, source: string, followUp: string, page: number) =>
  tenantKey(tenantId, 'leads', term, status, source, followUp, page);

export async function listLeads(term: string, status: string, source: string, followUp: string, page: number, signal?: AbortSignal) {
  const response = await apiClient.get<PageResult<LeadListItem>>('/leads', {
    params: { term, status: status || undefined, source: source || undefined, followUp: followUp || undefined, page, pageSize: 20, sort: 'created' },
    signal,
  });
  return response.data;
}

export async function getLead(id: string) {
  const response = await apiClient.get<LeadDetails>(`/leads/${id}`);
  return response.data;
}

export async function saveLead(id: string | undefined, input: LeadInput) {
  const response = id ? await apiClient.put<LeadDetails>(`/leads/${id}`, input) : await apiClient.post<LeadDetails>('/leads', input);
  return response.data;
}

export async function assignLead(id: string, userId: string) {
  const response = await apiClient.post<LeadDetails>(`/leads/${id}/assign`, { userId });
  return response.data;
}

export async function listFollowUps(leadId: string) {
  const response = await apiClient.get<FollowUpItem[]>(`/leads/${leadId}/follow-ups`);
  return response.data;
}

export async function listFollowUpQueue(bucket: string) {
  const response = await apiClient.get<PageResult<FollowUpItem>>('/leads/follow-ups', { params: { bucket, page: 1, pageSize: 20 } });
  return response.data;
}

export async function addFollowUp(leadId: string, scheduledAt: string, notes: string) {
  const response = await apiClient.post<FollowUpItem>(`/leads/${leadId}/follow-ups`, { scheduledAt, notes, outcome: null });
  return response.data;
}

export async function completeFollowUp(leadId: string, followUpId: string, outcome: string) {
  const response = await apiClient.post<FollowUpItem>(`/leads/${leadId}/follow-ups/${followUpId}/complete`, { scheduledAt: new Date().toISOString(), notes: null, outcome });
  return response.data;
}

export async function addTrial(leadId: string, scheduledAt: string, notes: string) {
  const response = await apiClient.post<TrialItem>(`/leads/${leadId}/trials`, { scheduledAt, notes });
  return response.data;
}

export async function changeTrialStatus(leadId: string, trialId: string, status: string) {
  const response = await apiClient.post<TrialItem>(`/leads/${leadId}/trials/${trialId}/status`, { status, notes: null, outcome: status });
  return response.data;
}

export async function convertLead(
  id: string,
  input: {
    memberProfileId?: string | null;
    firstName: string;
    lastName: string;
    phone: string;
    email?: string | null;
    joinDate: string;
    planId?: string | null;
    branchId?: string | null;
    startDate?: string | null;
    dateOfBirth?: string | null;
    gender?: string | null;
    address?: string | null;
    emergencyContactName?: string | null;
    emergencyContactPhone?: string | null;
    fitnessGoal?: string | null;
    assignedTrainerId?: string | null;
  },
) {
  const response = await apiClient.post<LeadDetails>(`/leads/${id}/convert`, {
    memberProfileId: input.memberProfileId ?? null,
    firstName: input.firstName,
    lastName: input.lastName,
    phone: input.phone,
    email: input.email ?? null,
    dateOfBirth: input.dateOfBirth ?? null,
    gender: input.gender ?? null,
    address: input.address ?? null,
    emergencyContactName: input.emergencyContactName ?? null,
    emergencyContactPhone: input.emergencyContactPhone ?? null,
    joinDate: input.joinDate,
    fitnessGoal: input.fitnessGoal ?? null,
    assignedTrainerId: input.assignedTrainerId ?? null,
    planId: input.planId ?? null,
    branchId: input.branchId ?? null,
    startDate: input.startDate ?? null,
  });
  return response.data;
}

export async function matchLeadMembers(id: string, mobile?: string, email?: string) {
  const response = await apiClient.get<Array<{ id: string; fullName: string; phone: string | null; email: string | null; memberCode: string; status: string; joinDate: string }>>(
    `/leads/${id}/member-matches`,
    { params: { mobile, email } },
  );
  return response.data;
}

export async function markLeadLost(id: string, reason: string, notes: string) {
  const response = await apiClient.post<LeadDetails>(`/leads/${id}/lost`, { reason, notes });
  return response.data;
}
