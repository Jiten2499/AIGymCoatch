import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { expensesQueryKey, getExpense, getExpenseSummary, listExpenseCategories, listExpenses, setExpenseCategoryActive, voidExpense } from '../api/expenses.api';
import { CategoryList } from './ExpenseCategoriesScreen';
import { ExpenseDetail } from './ExpenseDetailScreen';
import { ExpenseList } from './ExpensesScreen';
import { ExpensesScreen } from './ExpensesScreen';

jest.mock('../api/expenses.api', () => ({
  listExpenses: jest.fn(),
  getExpenseSummary: jest.fn(),
  listExpenseCategories: jest.fn(),
  saveExpense: jest.fn(),
  getExpense: jest.fn(),
  voidExpense: jest.fn(),
  saveExpenseCategory: jest.fn(),
  setExpenseCategoryActive: jest.fn(),
  expensesQueryKey: jest.requireActual<typeof import('../api/expenses.api')>('../api/expenses.api').expensesQueryKey,
}));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));
jest.mock('@react-navigation/native', () => {
  const actual = jest.requireActual<typeof import('@react-navigation/native')>('@react-navigation/native');
  return { ...actual, useNavigation: () => ({ navigate: jest.fn(), replace: jest.fn() }) };
});

const expense = {
  id: 'expense-1',
  category: 'Rent',
  description: 'October rent',
  amount: 15000,
  currency: 'INR',
  expenseDate: '2026-10-05',
  paymentMethod: 'BankTransfer',
  recordedBy: 'Gym Owner',
  status: 'Active',
  categoryId: 'category-1',
  recordedByUserId: 'user-1',
  notes: 'Paid',
  createdAt: '2026-10-06T00:00:00Z',
  updatedAt: null,
};

describe('gym expenses', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated',
      accessToken: 'token',
      refreshToken: 'refresh',
      expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'owner@gym.test', firstName: 'Gym', lastName: 'Owner' },
      currentTenant: { id: 'tenant-a', name: 'North Gym', type: 'Gym', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'GYM_EXPENSE_MANAGEMENT', enabled: true }]);
    jest.mocked(listExpenses).mockResolvedValue({ items: [expense], page: 1, pageSize: 20, totalCount: 1 });
    jest.mocked(getExpenseSummary).mockResolvedValue({ totalAmount: 15000, totalCount: 1, todayAmount: 0, todayCount: 0, weekAmount: 15000, monthAmount: 15000, currency: 'INR', categories: [{ categoryId: 'category-1', name: 'Rent', amount: 15000, count: 1 }] });
    jest.mocked(getExpense).mockResolvedValue(expense);
    jest.mocked(listExpenseCategories).mockResolvedValue([{ id: 'category-1', name: 'Rent', description: null, isActive: true }]);
  });

  it('keeps the workspace in the expense query key', () => {
    expect(expensesQueryKey('tenant-a', 'rent', '', 'Active', 1)).not.toEqual(expensesQueryKey('tenant-b', 'rent', '', 'Active', 1));
  });

  it('searches, filters, and shows the summary', async () => {
    const view = await renderWithProviders(<ExpenseList enabled />);
    expect(await view.findByText('October rent')).toBeTruthy();
    expect(await view.findByText('Rent 15000')).toBeTruthy();
    fireEvent.changeText(view.getByLabelText('Search expenses'), 'rent');
    await waitFor(() => expect(listExpenses).toHaveBeenCalledWith('rent', '', '', 1, expect.anything()));
    fireEvent.press(view.getByText('Status'));
    await waitFor(() => expect(listExpenses).toHaveBeenCalledWith('rent', '', 'Active', 1, expect.anything()));
  }, 15000);

  it('shows empty, error, and unauthorized states', async () => {
    jest.mocked(listExpenses).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const empty = await renderWithProviders(<ExpenseList enabled />);
    expect(await empty.findByText('No expenses in this workspace.')).toBeTruthy();
    jest.mocked(listExpenses).mockReset();
    jest.mocked(listExpenses).mockRejectedValue(new ApiError('The request failed.', 500, { detail: 'The request failed.' }));
    const failed = await renderWithProviders(<ExpenseList enabled />);
    expect(await failed.findByText('The request failed.')).toBeTruthy();
    jest.mocked(listExpenses).mockReset();
    jest.mocked(listExpenses).mockRejectedValue(new ApiError('You do not have permission to perform this action.', 403, { detail: 'You do not have permission to perform this action.' }));
    const denied = await renderWithProviders(<ExpenseList enabled />);
    expect(await denied.findByText('You do not have permission to manage expenses in this workspace.')).toBeTruthy();
  });

  it('does not query when the feature is off', async () => {
    jest.mocked(listExpenses).mockClear();
    const hidden = await renderWithProviders(<ExpenseList enabled={false} />);
    expect(hidden.queryByText('October rent')).toBeNull();
    expect(listExpenses).not.toHaveBeenCalled();
    jest.mocked(listFeatureFlags).mockResolvedValue([]);
    const gated = await renderWithProviders(<ExpensesScreen />);
    expect(await gated.findByText('This feature is not enabled for the current workspace.')).toBeTruthy();
  });

  it('voids an expense and deactivates a category', async () => {
    jest.mocked(voidExpense).mockResolvedValue({ ...expense, status: 'Voided' });
    jest.mocked(setExpenseCategoryActive).mockResolvedValue({ id: 'category-1', name: 'Rent', description: null, isActive: false });
    const view = await renderWithProviders(<ExpenseDetail enabled expenseId="expense-1" onEdit={jest.fn()} />);
    expect(await view.findByText('October rent')).toBeTruthy();
    fireEvent.press(view.getByText('Void expense'));
    await waitFor(() => expect(voidExpense).toHaveBeenCalledWith('expense-1'));
    const categories = await renderWithProviders(<CategoryList />);
    expect(await categories.findByText('Rent · Active')).toBeTruthy();
    fireEvent.press(categories.getByText('Deactivate'));
    await waitFor(() => expect(setExpenseCategoryActive).toHaveBeenCalledWith('category-1', false));
  }, 15000);
});
