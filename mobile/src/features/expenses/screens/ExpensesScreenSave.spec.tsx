import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { listExpenseCategories, saveExpense } from '../api/expenses.api';
import { ExpenseForm } from './ExpenseFormScreen';

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
jest.mock('@react-navigation/native', () => {
  const actual = jest.requireActual<typeof import('@react-navigation/native')>('@react-navigation/native');
  return { ...actual, useNavigation: () => ({ navigate: jest.fn(), replace: jest.fn() }) };
});

describe('expense form', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated',
      accessToken: 'token',
      refreshToken: 'refresh',
      expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'owner@gym.test', firstName: 'Gym', lastName: 'Owner' },
      currentTenant: { id: 'tenant-a', name: 'North Gym', type: 'Gym', status: 'Active' },
    });
    jest.mocked(listExpenseCategories).mockResolvedValue([{ id: 'category-1', name: 'Rent', description: null, isActive: true }]);
  });

  it('saves an expense', async () => {
    jest.mocked(saveExpense).mockResolvedValue({
      id: 'expense-1',
      category: 'Rent',
      description: 'October rent',
      amount: 15000,
      currency: 'INR',
      expenseDate: '2026-10-07',
      paymentMethod: 'Cash',
      recordedBy: null,
      status: 'Active',
      categoryId: 'category-1',
      recordedByUserId: 'user-1',
      notes: '',
      createdAt: '2026-10-07T00:00:00Z',
      updatedAt: null,
    });
    const saved = jest.fn();
    const view = await renderWithProviders(<ExpenseForm onSaved={saved} />);
    expect(await view.findByText('Rent')).toBeTruthy();
    fireEvent.changeText(view.getByLabelText('Description'), 'October rent');
    fireEvent.changeText(view.getByLabelText('Amount'), '15000');
    fireEvent.press(view.getByText('Save expense'));
    await waitFor(() => expect(saveExpense).toHaveBeenCalled());
    expect(saved).toHaveBeenCalled();
  });
});
