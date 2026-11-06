import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSessionStore } from '../../../store/sessionStore';
import { addFollowUp, assignLead, completeFollowUp, convertLead, getLead, listFollowUpQueue, listFollowUps, listLeads, markLeadLost, saveLead, addTrial, changeTrialStatus, leadsQueryKey, type LeadInput } from './leads.api';

export function useLeads(term: string, status: string, source: string, followUp: string, page: number, enabled: boolean) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useQuery({
    queryKey: leadsQueryKey(tenantId, term, status, source, followUp, page),
    queryFn: ({ signal }) => listLeads(term, status, source, followUp, page, signal),
    enabled: enabled && tenantId.length > 0,
  });
}

export function useLead(id: string, enabled: boolean) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useQuery({
    queryKey: [...leadsQueryKey(tenantId, '', '', '', '', 1), 'detail', id],
    queryFn: () => getLead(id),
    enabled: enabled && tenantId.length > 0 && id.length > 0,
  });
}

export function useFollowUps(id: string, enabled: boolean) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useQuery({
    queryKey: [...leadsQueryKey(tenantId, '', '', '', '', 1), 'follow-ups', id],
    queryFn: () => listFollowUps(id),
    enabled,
  });
}

export function useFollowUpQueue(bucket: string, enabled: boolean) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useQuery({
    queryKey: leadsQueryKey(tenantId, '', '', '', bucket, 1),
    queryFn: () => listFollowUpQueue(bucket),
    enabled,
  });
}

export function useSaveLead() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, input }: { id?: string; input: LeadInput }) => saveLead(id, input),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ['tenant', useSessionStore.getState().currentTenant?.id ?? '', 'leads'] });
    },
  });
}

function splitName(fullName: string) {
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  if (parts.length < 2) {
    return { firstName: parts[0] ?? 'Member', lastName: parts[0] ?? 'Member' };
  }
  return { firstName: parts.slice(0, -1).join(' '), lastName: parts.at(-1) ?? 'Member' };
}

export function useLeadActions(id: string) {
  const client = useQueryClient();
  const refresh = () => {
    void client.invalidateQueries({ queryKey: ['tenant', useSessionStore.getState().currentTenant?.id ?? '', 'leads'] });
  };
  return {
    assign: useMutation({ mutationFn: (userId: string) => assignLead(id, userId), onSuccess: refresh }),
    followUp: useMutation({ mutationFn: (scheduledAt: string) => addFollowUp(id, scheduledAt, 'Follow-up'), onSuccess: refresh }),
    complete: useMutation({ mutationFn: (followUpId: string) => completeFollowUp(id, followUpId, 'Completed'), onSuccess: refresh }),
    trial: useMutation({ mutationFn: (scheduledAt: string) => addTrial(id, scheduledAt, 'Trial'), onSuccess: refresh }),
    trialStatus: useMutation({ mutationFn: ({ trialId, status }: { trialId: string; status: string }) => changeTrialStatus(id, trialId, status), onSuccess: refresh }),
    convert: useMutation({
      mutationFn: async (memberId?: string) => {
        const lead = await getLead(id);
        const name = splitName(lead.fullName);
        return convertLead(id, {
          memberProfileId: memberId ?? null,
          firstName: name.firstName,
          lastName: name.lastName,
          phone: lead.mobile,
          email: lead.email,
          joinDate: new Date().toISOString().slice(0, 10),
        });
      },
      onSuccess: refresh,
    }),
    lost: useMutation({ mutationFn: () => markLeadLost(id, 'NotInterested', 'Marked lost'), onSuccess: refresh }),
  };
}
