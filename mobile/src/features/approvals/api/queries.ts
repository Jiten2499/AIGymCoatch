import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSessionStore } from '../../../store/sessionStore';
import { approvalQueryKey, decideApprovalRequest, getApprovalRequest, listApprovalRequests } from './approvals.api';

export function useApprovalRequests(status: string, requestType: string, page: number, enabled: boolean) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useQuery({
    queryKey: approvalQueryKey(tenantId, status, requestType, page),
    queryFn: ({ signal }) => listApprovalRequests(status, requestType, page, signal),
    enabled: enabled && tenantId.length > 0,
    retry: false,
  });
}

export function useApprovalRequest(id: string, enabled: boolean) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useQuery({
    queryKey: [...approvalQueryKey(tenantId, '', '', 1), 'detail', id],
    queryFn: () => getApprovalRequest(id),
    enabled: enabled && tenantId.length > 0 && id.length > 0,
  });
}

export function useDecideApproval(id: string) {
  const client = useQueryClient();
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useMutation({
    mutationFn: ({ approve, comment }: { approve: boolean; comment: string }) => decideApprovalRequest(id, approve, comment),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ['tenant', tenantId, 'approval-requests'] });
      await client.invalidateQueries({ queryKey: ['approval-count', tenantId] });
      await client.invalidateQueries({ queryKey: ['tenant', tenantId, 'memberships'] });
    },
  });
}
