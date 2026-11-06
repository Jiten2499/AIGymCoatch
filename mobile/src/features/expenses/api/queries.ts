import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSessionStore } from '../../../store/sessionStore';
import { getExpense, getExpenseSummary, listExpenseCategories, listExpenses, saveExpense, saveExpenseCategory, setExpenseCategoryActive, voidExpense, expensesQueryKey, type ExpenseInput } from './expenses.api';

function refreshKey() {
  return ['tenant', useSessionStore.getState().currentTenant?.id ?? '', 'expenses'] as const;
}

export function useExpenses(term: string, categoryId: string, status: string, page: number, enabled: boolean) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useQuery({
    queryKey: expensesQueryKey(tenantId, term, categoryId, status, page),
    queryFn: ({ signal }) => listExpenses(term, categoryId, status, page, signal),
    enabled: enabled && tenantId.length > 0,
  });
}

export function useExpenseSummary(enabled: boolean) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useQuery({
    queryKey: expensesQueryKey(tenantId, 'summary', '', '', 0),
    queryFn: ({ signal }) => getExpenseSummary(signal),
    enabled: enabled && tenantId.length > 0,
  });
}

export function useExpenseCategories(enabled: boolean) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useQuery({
    queryKey: expensesQueryKey(tenantId, 'categories', '', '', 0),
    queryFn: ({ signal }) => listExpenseCategories(signal),
    enabled: enabled && tenantId.length > 0,
  });
}

export function useExpense(id: string, enabled: boolean) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  return useQuery({
    queryKey: [...expensesQueryKey(tenantId, '', '', '', 1), 'detail', id],
    queryFn: () => getExpense(id),
    enabled: enabled && id.length > 0,
  });
}

export function useSaveExpense() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, input }: { id?: string; input: ExpenseInput }) => saveExpense(id, input),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: refreshKey() });
      await client.invalidateQueries({ queryKey: ['tenant', useSessionStore.getState().currentTenant?.id ?? '', 'ledger'] });
    },
  });
}

export function useVoidExpense(id: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => voidExpense(id),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: refreshKey() });
      await client.invalidateQueries({ queryKey: ['tenant', useSessionStore.getState().currentTenant?.id ?? '', 'ledger'] });
    },
  });
}

export function useSaveExpenseCategory() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, name, active }: { id?: string; name: string; active?: boolean }) => (active === undefined ? saveExpenseCategory(id, name) : setExpenseCategoryActive(id ?? '', active)),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: refreshKey() }); },
  });
}
