import { apiClient } from '../../../services/api/client';
import type { PageResult } from '../../../types/api';
import { tenantKey } from '../../fitness/api/keys';

export type MembershipPackage = {
  id: string;
  name: string;
  description: string | null;
  durationDays: number;
  membershipType: string;
  price: number;
  currency: string;
  isActive: boolean;
  category: string;
  durationUnit: string;
  durationCount: number;
};

export type GymMembership = {
  id: string;
  memberProfileId: string;
  branchId: string | null;
  membershipPlanId: string;
  membershipNumber: string;
  startDate: string;
  endDate: string;
  status: string;
  autoRenew: boolean;
  notes: string | null;
  memberName: string | null;
  planName: string | null;
  packagePrice?: number;
  discountAmount?: number;
  finalAmount?: number;
  currency?: string;
  discountRuleId?: string | null;
  discountName?: string | null;
  totalPaid?: number;
  outstanding?: number;
  settlementStatus?: 'Unpaid' | 'PartiallyPaid' | 'Paid';
};

export type PackageInput = {
  name: string;
  description?: string;
  durationDays: number;
  membershipType: string;
  maxVisits?: number | null;
  price: number;
  currency: string;
  category: string;
  durationUnit: string;
  durationCount: number;
};

export const packagesQueryKey = (tenantId: string, term: string, activeOnly: boolean, page: number) =>
  tenantKey(tenantId, 'packages', term, activeOnly ? 'active' : 'all', page);

export const membershipsQueryKey = (tenantId: string, memberId: string) =>
  tenantKey(tenantId, 'memberships', memberId);

export async function listPackages(term: string, activeOnly: boolean, page: number, signal?: AbortSignal) {
  const response = await apiClient.get<PageResult<MembershipPackage>>('/membership-plans', {
    params: { term, activeOnly, page, pageSize: 20 },
    signal,
  });
  return response.data;
}

export async function getPackage(id: string) {
  const response = await apiClient.get<MembershipPackage>(`/membership-plans/${id}`);
  return response.data;
}

export async function savePackage(id: string | undefined, input: PackageInput) {
  const response = id
    ? await apiClient.put<MembershipPackage>(`/membership-plans/${id}`, input)
    : await apiClient.post<MembershipPackage>('/membership-plans', input);
  return response.data;
}

export async function setPackageActive(id: string, active: boolean) {
  if (active) {
    await apiClient.post(`/membership-plans/${id}/activate`);
    return;
  }
  await apiClient.delete(`/membership-plans/${id}`);
}

export async function listMemberMemberships(memberId: string, signal?: AbortSignal) {
  const response = await apiClient.get<PageResult<GymMembership>>('/memberships', {
    params: { memberId, sort: 'startDate', page: 1, pageSize: 20 },
    signal,
  });
  return response.data;
}

export type AssignMembershipInput = {
  branchId: string | null;
  planId: string;
  startDate: string;
  assignAppAccess?: boolean;
  appSubscriptionId?: string | null;
  discountRuleId?: string | null;
  paymentAmount?: number | null;
  paymentMethod?: string | null;
  paymentDate?: string | null;
  paymentReferenceNumber?: string | null;
  paymentNotes?: string | null;
};

export async function assignMembership(memberId: string, input: AssignMembershipInput) {
  const response = await apiClient.post<GymMembership>('/memberships', {
    memberProfileId: memberId,
    branchId: input.branchId,
    planId: input.planId,
    startDate: input.startDate,
    autoRenew: false,
    assignAppAccess: input.assignAppAccess ?? false,
    appSubscriptionId: input.assignAppAccess ? input.appSubscriptionId ?? null : null,
    discountRuleId: input.discountRuleId ?? null,
    paymentAmount: input.paymentAmount ?? null,
    paymentMethod: input.paymentMethod ?? null,
    paymentDate: input.paymentDate ?? null,
    paymentReferenceNumber: input.paymentReferenceNumber ?? null,
    paymentNotes: input.paymentNotes ?? null,
  });
  return response.data;
}

export async function changeMembership(id: string, action: 'renew' | 'freeze' | 'unfreeze' | 'extend' | 'cancel', days = 0) {
  const response = await apiClient.post<GymMembership>(`/memberships/${id}/${action}`, { reason: null, autoRenew: false, days });
  return response.data;
}
