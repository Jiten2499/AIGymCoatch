import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTenantId } from '../../fitness/api/queries';
import { changeMemberStatus, createMember, getMember, listMembers, loadMemberPhoto, membersQueryKey, removeMemberPhoto, updateMember, uploadMemberPhoto, type MemberInput, type MemberStatus } from './members.api';

export function useMembers(term: string, status: string, page: number, enabled: boolean) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: membersQueryKey(tenantId, term, status, page),
    enabled: Boolean(tenantId) && enabled,
    queryFn: ({ signal }) => listMembers(term, status, page, signal),
  });
}

export function useMember(id: string, enabled: boolean) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: membersQueryKey(tenantId, 'detail', id, 0),
    enabled: Boolean(tenantId) && enabled && Boolean(id),
    queryFn: ({ signal }) => getMember(id, signal),
  });
}

export function useMemberPhoto(id: string, hasPhoto: boolean, enabled: boolean) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: membersQueryKey(tenantId, 'photo', id, 0),
    enabled: Boolean(tenantId) && enabled && hasPhoto,
    gcTime: 0,
    queryFn: ({ signal }) => loadMemberPhoto(id, signal),
  });
}

export function useSaveMember(id?: string) {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: MemberInput) => (id ? updateMember(id, input) : createMember(input)),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: membersQueryKey(tenantId, '', '', 1).slice(0, 3) });
    },
  });
}

export function useMemberStatus(id: string) {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (status: MemberStatus) => changeMemberStatus(id, status),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: membersQueryKey(tenantId, '', '', 1).slice(0, 3) });
    },
  });
}

export function useMemberPhotoUpload(id: string) {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (photo: { uri: string; mimeType: string; fileName: string }) => uploadMemberPhoto(id, photo.uri, photo.mimeType, photo.fileName),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: membersQueryKey(tenantId, 'photo', id, 0) });
    },
  });
}

export function useMemberPhotoRemoval(id: string) {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => removeMemberPhoto(id),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: membersQueryKey(tenantId, 'photo', id, 0) });
    },
  });
}
