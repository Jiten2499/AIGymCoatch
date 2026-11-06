import { apiClient } from '../../../services/api/client';
import type { PageResult } from '../../../types/api';
import { tenantKey } from '../../fitness/api/keys';

export type FinancialAccount = {
  id: string;
  name: string;
  accountKind: string;
  currency: string;
  isActive: boolean;
  balance: number;
};

export type LedgerTransaction = {
  id: string;
  entryDate: string;
  description: string;
  entryType: string;
  accountName: string | null;
  category: string | null;
  amount: number;
  currency: string;
  status: string;
  recordedBy: string | null;
  sourceType: string;
  sourceId: string | null;
};

export type LedgerSummary = {
  moneyIn: number;
  moneyOut: number;
  netCashFlow: number;
  transactionCount: number;
  currency: string;
  accounts: { accountId: string; accountName: string; moneyIn: number; moneyOut: number; balance: number }[];
  categories: { category: string; moneyIn: number; moneyOut: number; count: number }[];
};

export type TransferInput = {
  fromAccountId: string;
  toAccountId: string;
  amount: number;
  transferDate: string;
  notes?: string | null;
};

export const ledgerQueryKey = (tenantId: string, ...parts: (string | number)[]) => tenantKey(tenantId, 'ledger', ...parts);

export async function getLedgerSummary(from: string, to: string, signal?: AbortSignal) {
  const response = await apiClient.get<LedgerSummary>('/ledger/summary', { params: { from, to }, signal });
  return response.data;
}

export async function listLedgerTransactions(params: {
  from: string;
  to: string;
  entryType?: string;
  financialAccountId?: string;
  category?: string;
  status?: string;
  page: number;
}, signal?: AbortSignal) {
  const response = await apiClient.get<PageResult<LedgerTransaction>>('/ledger/transactions', {
    params: {
      from: params.from,
      to: params.to,
      entryType: params.entryType || undefined,
      financialAccountId: params.financialAccountId || undefined,
      category: params.category || undefined,
      status: params.status || undefined,
      page: params.page,
      pageSize: 20,
    },
    signal,
  });
  return response.data;
}

export async function listFinancialAccounts(signal?: AbortSignal) {
  const response = await apiClient.get<FinancialAccount[]>('/financial-accounts', { signal });
  return response.data;
}

export async function createTransfer(input: TransferInput) {
  const response = await apiClient.post<LedgerTransaction>('/ledger/transfers', input);
  return response.data;
}

export async function voidLedgerTransaction(id: string) {
  const response = await apiClient.post<LedgerTransaction>(`/ledger/transactions/${id}/void`);
  return response.data;
}

export async function createFinancialAccount(name: string, accountKind: string) {
  const response = await apiClient.post<FinancialAccount>('/financial-accounts', { name, accountKind });
  return response.data;
}

export async function setFinancialAccountActive(id: string, active: boolean) {
  const response = await apiClient.post<FinancialAccount>(`/financial-accounts/${id}/status`, { active });
  return response.data;
}
