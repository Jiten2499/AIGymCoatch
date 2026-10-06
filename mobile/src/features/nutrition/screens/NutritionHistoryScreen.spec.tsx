import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { listNutritionLogs } from '../api/nutrition.api';
import { NutritionHistoryScreen } from './NutritionHistoryScreen';

jest.mock('../api/nutrition.api', () => ({ listNutritionLogs: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));

const oat = { id: 'log-1', logDate: '2026-10-03', mealType: 'Breakfast' as const, foodName: 'Oats', quantity: 1, unit: 'bowl', calories: 300, protein: 10, carbohydrates: 40, fat: 6 };
const rice = { id: 'log-2', logDate: '2026-10-02', mealType: 'Lunch' as const, foodName: 'Rice', quantity: 1, unit: 'plate', calories: 250, protein: 5, carbohydrates: 45, fat: 2 };

function signIn(tenantId: string) {
  useSessionStore.setState({
    status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
    user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
    currentTenant: { id: tenantId, name: 'Workspace', type: 'Personal', status: 'Active' },
  });
}

describe('NutritionHistoryScreen', () => {
  beforeEach(() => {
    signIn('tenant-a');
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'NUTRITION', enabled: true }]);
    jest.mocked(listNutritionLogs).mockReset();
  });

  it('shows an empty history', async () => {
    jest.mocked(listNutritionLogs).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const view = await renderWithProviders(<NutritionHistoryScreen />);
    expect(await view.findByText('Logged meals will show up here.')).toBeTruthy();
  });

  it('shows a logged meal and requests the next server page', async () => {
    jest.mocked(listNutritionLogs).mockImplementation(async (page) => page === 1
      ? { items: [oat], page: 1, pageSize: 20, totalCount: 21 }
      : { items: [rice], page: 2, pageSize: 20, totalCount: 21 });
    const view = await renderWithProviders(<NutritionHistoryScreen />);
    expect(await view.findByText('Oats')).toBeTruthy();
    expect(view.getByText('Calories: 300')).toBeTruthy();
    await fireEvent.press(view.getByRole('button', { name: 'Next page' }));
    await waitFor(() => expect(view.getByText('Rice')).toBeTruthy());
    expect(view.queryByText('Oats')).toBeNull();
    expect(listNutritionLogs).toHaveBeenCalledWith(2, null, expect.anything());
  });

  it('replaces meals after the workspace changes', async () => {
    jest.mocked(listNutritionLogs).mockImplementation(async () => useSessionStore.getState().currentTenant?.id === 'tenant-b'
      ? { items: [rice], page: 1, pageSize: 20, totalCount: 1 }
      : { items: [oat], page: 1, pageSize: 20, totalCount: 1 });
    const view = await renderWithProviders(<NutritionHistoryScreen />);
    expect(await view.findByText('Oats')).toBeTruthy();
    signIn('tenant-b');
    await waitFor(() => expect(view.getByText('Rice')).toBeTruthy());
    expect(view.queryByText('Oats')).toBeNull();
  });

  it('shows an error with retry', async () => {
    jest.mocked(listNutritionLogs).mockRejectedValue(new ApiError('Network request failed', 0, null, true));
    const view = await renderWithProviders(<NutritionHistoryScreen />);
    expect(await view.findByText('You appear to be offline.')).toBeTruthy();
    expect(view.getByRole('button', { name: 'Try again' })).toBeTruthy();
  });
});
