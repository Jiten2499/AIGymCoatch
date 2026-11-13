import { apiClient } from '../../../services/api/client';
import type { PageResult } from '../../../types/api';
import { tenantKey } from '../../fitness/api/keys';

export type MembershipPaymentMethod =
  | 'Cash'
  | 'Upi'
  | 'DebitCard'
  | 'CreditCard'
  | 'BankTransfer'
  | 'OnlinePayment'
  | 'Cheque'
  | 'Other';

export type MembershipPayment = {
  id: string;
  memberProfileId: string;
  gymMembershipId: string;
  amount: number;
  currency: string;
  paymentMethod: MembershipPaymentMethod;
  paymentDate: string;
  referenceNumber: string | null;
  notes: string | null;
  status: 'Posted' | 'Voided';
  receivedByUserId: string;
  receivedBy: string | null;
  createdAt: string;
  voidedAt: string | null;
};

export type MembershipPaymentSummary = {
  membershipId: string;
  memberProfileId: string;
  packagePrice: number;
  discountAmount: number;
  finalAmount: number;
  currency: string;
  discountRuleId: string | null;
  discountName: string | null;
  totalPaid: number;
  outstanding: number;
  settlementStatus: 'Unpaid' | 'PartiallyPaid' | 'Paid';
};

export type CollectPaymentInput = {
  amount: number;
  paymentMethod: MembershipPaymentMethod;
  paymentDate: string;
  referenceNumber?: string | null;
  notes?: string | null;
};

export const paymentMethods: MembershipPaymentMethod[] = [
  'Cash',
  'Upi',
  'DebitCard',
  'CreditCard',
  'BankTransfer',
  'OnlinePayment',
  'Cheque',
  'Other',
];

export const membershipPaymentsQueryKey = (tenantId: string, membershipId: string, page: number) =>
  tenantKey(tenantId, 'membership-payments', membershipId, page);

export const membershipPaymentSummaryQueryKey = (tenantId: string, membershipId: string) =>
  tenantKey(tenantId, 'membership-payment-summary', membershipId);

export async function listMembershipPayments(membershipId: string, page: number, signal?: AbortSignal) {
  const response = await apiClient.get<PageResult<MembershipPayment>>(`/memberships/${membershipId}/payments`, {
    params: { page, pageSize: 20 },
    signal,
  });
  return response.data;
}

export async function getMembershipPaymentSummary(membershipId: string, signal?: AbortSignal) {
  const response = await apiClient.get<MembershipPaymentSummary>(`/memberships/${membershipId}/payment-summary`, { signal });
  return response.data;
}

export async function collectMembershipPayment(membershipId: string, input: CollectPaymentInput) {
  const response = await apiClient.post<MembershipPayment>(`/memberships/${membershipId}/payments`, input);
  return response.data;
}

export async function voidMembershipPayment(id: string) {
  const response = await apiClient.post<MembershipPayment>(`/membership-payments/${id}/void`);
  return response.data;
}
