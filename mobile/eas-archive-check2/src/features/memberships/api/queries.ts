import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTenantId } from '../../fitness/api/queries';
import { listWorkspacePlans, memberAppAccessQueryKey, workspacePlansQueryKey } from '../../members/api/appAccess.api';
import { assignMembership, changeMembership, getPackage, listMemberMemberships, listPackages, membershipsQueryKey, packagesQueryKey, savePackage, setPackageActive, type AssignMembershipInput, type PackageInput } from './memberships.api';
import { collectMembershipPayment, getMembershipPaymentSummary, listMembershipPayments, membershipPaymentSummaryQueryKey, membershipPaymentsQueryKey, type CollectPaymentInput } from './payments.api';

export function useWorkspacePlans(enabled: boolean) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: workspacePlansQueryKey(tenantId),
    enabled: Boolean(tenantId) && enabled,
    queryFn: ({ signal }) => listWorkspacePlans(signal),
  });
}

export function usePackages(term: string, activeOnly: boolean, page: number, enabled: boolean) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: packagesQueryKey(tenantId, term, activeOnly, page),
    enabled: Boolean(tenantId) && enabled,
    queryFn: ({ signal }) => listPackages(term, activeOnly, page, signal),
  });
}

export function usePackage(id: string) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: packagesQueryKey(tenantId, 'detail', false, 0).concat(id),
    enabled: Boolean(tenantId) && Boolean(id),
    queryFn: () => getPackage(id),
  });
}

export function useSavePackage(id?: string) {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: PackageInput) => savePackage(id, input),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: packagesQueryKey(tenantId, '', false, 1).slice(0, 3) });
    },
  });
}

export function usePackageStatus() {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string; active: boolean }) => setPackageActive(input.id, input.active),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: packagesQueryKey(tenantId, '', false, 1).slice(0, 3) });
    },
  });
}

export function useMemberMemberships(memberId: string, enabled: boolean) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: membershipsQueryKey(tenantId, memberId),
    enabled: Boolean(tenantId) && enabled && Boolean(memberId),
    queryFn: ({ signal }) => listMemberMemberships(memberId, signal),
  });
}

export function useMembershipAction(memberId: string) {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input:
      | { id: string; action: 'renew' | 'freeze' | 'unfreeze' | 'extend' | 'cancel'; days?: number }
      | ({ action: 'assign' } & AssignMembershipInput)) =>
      input.action === 'assign'
        ? assignMembership(memberId, input)
        : changeMembership(input.id, input.action, input.days ?? 0),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: membershipsQueryKey(tenantId, memberId) });
      await client.invalidateQueries({ queryKey: workspacePlansQueryKey(tenantId) });
      await client.invalidateQueries({ queryKey: memberAppAccessQueryKey(tenantId, memberId) });
      await client.invalidateQueries({ queryKey: tenantKeyPayments(tenantId) });
    },
  });
}

function tenantKeyPayments(tenantId: string | null) {
  return membershipPaymentsQueryKey(tenantId ?? '', '', 1).slice(0, 3);
}

export function useMembershipPaymentSummary(membershipId: string, enabled: boolean) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: membershipPaymentSummaryQueryKey(tenantId, membershipId),
    enabled: Boolean(tenantId) && enabled && Boolean(membershipId),
    queryFn: ({ signal }) => getMembershipPaymentSummary(membershipId, signal),
  });
}

export function useMembershipPayments(membershipId: string, page: number, enabled: boolean) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: membershipPaymentsQueryKey(tenantId, membershipId, page),
    enabled: Boolean(tenantId) && enabled && Boolean(membershipId),
    queryFn: ({ signal }) => listMembershipPayments(membershipId, page, signal),
  });
}

export function useCollectMembershipPayment(memberId: string, membershipId: string) {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: CollectPaymentInput) => collectMembershipPayment(membershipId, input),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: membershipsQueryKey(tenantId, memberId) });
      await client.invalidateQueries({ queryKey: membershipPaymentSummaryQueryKey(tenantId, membershipId) });
      await client.invalidateQueries({ queryKey: tenantKeyPayments(tenantId) });
    },
  });
}
