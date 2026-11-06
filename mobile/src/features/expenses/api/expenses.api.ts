import { apiClient } from '../../../services/api/client';
import type { PageResult } from '../../../types/api';
import { tenantKey } from '../../fitness/api/keys';

export type ExpenseCategory = { id: string; name: string; description: string | null; isActive: boolean };

export type ExpenseListItem = {
  id: string;
  category: string;
  description: string;
  amount: number;
  currency: string;
  expenseDate: string;
  paymentMethod: string;
  recordedBy: string | null;
  status: string;
};

export type ExpenseDetails = ExpenseListItem & {
  categoryId: string;
  recordedByUserId: string;
  notes: string | null;
  createdAt: string;
  updatedAt: string | null;
};

export type ExpenseSummary = {
  totalAmount: number;
  totalCount: number;
  todayAmount: number;
  todayCount: number;
  weekAmount: number;
  monthAmount: number;
  currency: string;
  categories: { categoryId: string; name: string; amount: number; count: number }[];
};

export type ExpenseInput = {
  categoryId: string;
  description: string;
  amount: number;
  expenseDate: string;
  paymentMethod: string;
  notes?: string | null;
};

export const expensesQueryKey = (tenantId: string, term: string, categoryId: string, status: string, page: number) =>
  tenantKey(tenantId, 'expenses', term, categoryId, status, page);

export async function listExpenses(term: string, categoryId: string, status: string, page: number, signal?: AbortSignal) {
  const response = await apiClient.get<PageResult<ExpenseListItem>>('/expenses', {
    params: { term, categoryId: categoryId || undefined, status: status || undefined, page, pageSize: 20, sort: 'date' },
    signal,
  });
  return response.data;
}

export async function getExpenseSummary(signal?: AbortSignal) {
  const response = await apiClient.get<ExpenseSummary>('/expenses/summary', { signal });
  return response.data;
}

export async function listExpenseCategories(signal?: AbortSignal) {
  const response = await apiClient.get<ExpenseCategory[]>('/expense-categories', { signal });
  return response.data;
}

export async function saveExpense(id: string | undefined, input: ExpenseInput) {
  const response = id ? await apiClient.put<ExpenseDetails>(`/expenses/${id}`, input) : await apiClient.post<ExpenseDetails>('/expenses', input);
  return response.data;
}

export async function getExpense(id: string) {
  const response = await apiClient.get<ExpenseDetails>(`/expenses/${id}`);
  return response.data;
}

export async function voidExpense(id: string) {
  const response = await apiClient.post<ExpenseDetails>(`/expenses/${id}/void`);
  return response.data;
}

export async function saveExpenseCategory(id: string | undefined, name: string) {
  const response = id
    ? await apiClient.put<ExpenseCategory>(`/expense-categories/${id}`, { name, description: null })
    : await apiClient.post<ExpenseCategory>('/expense-categories', { name, description: null });
  return response.data;
}

export async function setExpenseCategoryActive(id: string, active: boolean) {
  const response = await apiClient.post<ExpenseCategory>(`/expense-categories/${id}/status`, { active });
  return response.data;
}
