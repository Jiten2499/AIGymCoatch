import { apiClient } from '../../../services/api/client';
import type { PageResult } from '../../../types/api';
import { tenantKey } from '../../fitness/api/keys';

export type MemberStatus = 'Active' | 'Inactive';

export type MemberListItem = {
  id: string;
  fullName: string;
  mobile: string | null;
  hasPhoto: boolean;
  status: MemberStatus;
  joinDate: string;
};

export type MemberDetails = MemberListItem & {
  userId: string;
  memberCode: string;
  email: string;
  firstName: string;
  lastName: string;
  phone: string | null;
  dateOfBirth: string | null;
  gender: string | null;
  emergencyContactName: string | null;
  emergencyContactPhone: string | null;
  emergencyContactRelationship: string | null;
  address: string | null;
  city: string | null;
  state: string | null;
  postalCode: string | null;
  fitnessGoal: string | null;
  assignedTrainerId: string | null;
  assignedTrainerName: string | null;
};

export type MemberInput = {
  email?: string;
  password?: string;
  firstName: string;
  lastName: string;
  phone?: string;
  gender?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  emergencyContactRelationship?: string;
  address?: string;
  joinDate?: string;
  fitnessGoal?: string;
  status?: MemberStatus;
};

export function membersQueryKey(tenantId: string, term: string, status: string, page: number): readonly unknown[] {
  return tenantKey(tenantId, 'members', term, status, page);
}

export async function listMembers(term: string, status: string, page: number, signal?: AbortSignal): Promise<PageResult<MemberListItem>> {
  const response = await apiClient.get<PageResult<MemberListItem>>('/members', {
    params: { term: term || undefined, status: status || undefined, page, pageSize: 20, sort: 'name' },
    signal,
  });
  return response.data;
}

export async function getMember(id: string, signal?: AbortSignal): Promise<MemberDetails> {
  const response = await apiClient.get<MemberDetails>(`/members/${id}`, { signal });
  return response.data;
}

export async function createMember(input: MemberInput): Promise<MemberDetails> {
  const response = await apiClient.post<MemberDetails>('/members', input);
  return response.data;
}

export async function updateMember(id: string, input: MemberInput): Promise<MemberDetails> {
  const response = await apiClient.put<MemberDetails>(`/members/${id}`, input);
  return response.data;
}

export async function changeMemberStatus(id: string, status: MemberStatus): Promise<MemberDetails> {
  const response = await apiClient.patch<MemberDetails>(`/members/${id}/status`, { status });
  return response.data;
}

export async function uploadMemberPhoto(id: string, uri: string, mimeType: string, fileName: string): Promise<MemberDetails> {
  const body = new FormData();
  body.append('file', { uri, name: fileName, type: mimeType } as unknown as Blob);
  const response = await apiClient.post<MemberDetails>(`/members/${id}/profile-photo`, body, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return response.data;
}

export async function removeMemberPhoto(id: string): Promise<MemberDetails> {
  const response = await apiClient.delete<MemberDetails>(`/members/${id}/profile-photo`);
  return response.data;
}

export async function loadMemberPhoto(id: string, signal?: AbortSignal): Promise<string | null> {
  try {
    const response = await apiClient.get<ArrayBuffer>(`/members/${id}/profile-photo`, { responseType: 'arraybuffer', signal });
    const bytes = new Uint8Array(response.data);
    let binary = '';
    bytes.forEach((value) => {
      binary += String.fromCharCode(value);
    });
    const type = String(response.headers['content-type'] ?? 'image/jpeg');
    return `data:${type};base64,${globalThis.btoa(binary)}`;
  } catch {
    return null;
  }
}
