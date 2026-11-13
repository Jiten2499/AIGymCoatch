import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSessionStore } from '../../../store/sessionStore';
import {
  createFinancialAccount,
  createTransfer,
  getLedgerSummary,
  ledgerQueryKey,
  listFinancialAccounts,
  listLedgerTransactions,
  setFinancialAccountActive,
  voidLedgerTransaction,
  type TransferInput,
} from './ledger.api';

function refreshKey() {
  return ['tenant', useSessionStore.getState().currentTenant?.id ?? '', 'ledger'] as const;
}

export function useLedgerSummary(from: string, to: string, enabled: boolean) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useQuery({
    queryKey: ledgerQueryKey(tenantId, 'summary', from, to),
    queryFn: ({ signal }) => getLedgerSummary(from, to, signal),
    enabled: enabled && tenantId.length > 0,
  });
}

export function useLedgerTransactions(
  from: string,
  to: string,
  entryType: string,
  financialAccountId: string,
  category: string,
  status: string,
  page: number,
  enabled: boolean,
) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useQuery({
    queryKey: ledgerQueryKey(tenantId, 'transactions', from, to, entryType, financialAccountId, category, status, page),
    queryFn: ({ signal }) => listLedgerTransactions({ from, to, entryType, financialAccountId, category, status, page }, signal),
    enabled: enabled && tenantId.length > 0,
  });
}

export function useFinancialAccounts(enabled: boolean) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useQuery({
    queryKey: ledgerQueryKey(tenantId, 'accounts'),
    queryFn: ({ signal }) => listFinancialAccounts(signal),
    enabled: enabled && tenantId.length > 0,
  });
}

export function useCreateTransfer() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: TransferInput) => createTransfer(input),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: refreshKey() }); },
  });
}

export function useVoidLedgerTransaction() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => voidLedgerTransaction(id),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: refreshKey() }); },
  });
}

export function useSaveFinancialAccount() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: { id?: string; name: string; accountKind?: string; active?: boolean }) =>
      input.active === undefined
        ? createFinancialAccount(input.name, input.accountKind ?? 'Cash')
        : setFinancialAccountActive(input.id ?? '', input.active),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: refreshKey() }); },
  });
}
