import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent } from '@testing-library/react-native';
import { useNavigation } from '@react-navigation/native';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { getNutritionSummary, listNutritionLogs } from '../api/nutrition.api';
import { NutritionScreen } from './NutritionScreen';

jest.mock('../api/nutrition.api', () => ({ getNutritionSummary: jest.fn(), listNutritionLogs: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));
jest.mock('@react-navigation/native', () => {
  const actual = jest.requireActual<typeof import('@react-navigation/native')>('@react-navigation/native');
  const navigate = jest.fn();
  return { ...actual, useNavigation: () => ({ navigate }) };
});

const summary = { date: '2026-10-03', calories: 0, protein: 0, carbohydrates: 0, fat: 0, calorieTarget: 0, proteinTarget: 0, carbTarget: 0, fatTarget: 0 };
const emptyPage = { items: [], page: 1, pageSize: 20, totalCount: 0 };

function signIn() {
  useSessionStore.setState({
    status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
    user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
    currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
  });
}

describe('NutritionScreen', () => {
  beforeEach(() => {
    signIn();
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'NUTRITION', enabled: true }, { featureCode: 'AI_NUTRITION', enabled: true }]);
    jest.mocked(getNutritionSummary).mockResolvedValue(summary);
    jest.mocked(listNutritionLogs).mockResolvedValue(emptyPage);
    (useNavigation().navigate as unknown as jest.Mock).mockClear();
  });

  it('shows an empty day without invented targets', async () => {
    const view = await renderWithProviders(<NutritionScreen />);
    expect(await view.findByText('No meals logged for this day.')).toBeTruthy();
    expect(view.getByText('No daily target is saved yet.')).toBeTruthy();
  });

  it('shows the server summary and opens logging and history', async () => {
    jest.mocked(getNutritionSummary).mockResolvedValue({ ...summary, calories: 500, calorieTarget: 2000, protein: 30, proteinTarget: 120, carbohydrates: 40, carbTarget: 180, fat: 10, fatTarget: 50 });
    jest.mocked(listNutritionLogs).mockResolvedValue({ items: [{ id: 'log-1', logDate: '2026-10-03', mealType: 'Breakfast', foodName: 'Oats', quantity: 1, unit: 'bowl', calories: 300, protein: 10, carbohydrates: 40, fat: 6 }], page: 1, pageSize: 20, totalCount: 1 });
    const view = await renderWithProviders(<NutritionScreen />);
    expect(await view.findByText('Calories: 500 of 2,000')).toBeTruthy();
    expect(view.getByText('Breakfast · Oats')).toBeTruthy();
    await fireEvent.press(view.getByRole('button', { name: 'Log meal' }));
    expect(useNavigation().navigate).toHaveBeenCalledWith('LogMeal');
    await fireEvent.press(view.getByRole('button', { name: 'Meal history' }));
    expect(useNavigation().navigate).toHaveBeenCalledWith('NutritionHistory');
  });

  it('shows an error and retry', async () => {
    jest.mocked(getNutritionSummary).mockRejectedValue(new ApiError('Network request failed', 0, null, true));
    const view = await renderWithProviders(<NutritionScreen />);
    expect(await view.findByText('You appear to be offline.')).toBeTruthy();
    expect(view.getByRole('button', { name: 'Try again' })).toBeTruthy();
  });

  it('blocks the screen when nutrition is disabled', async () => {
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'NUTRITION', enabled: false }]);
    const view = await renderWithProviders(<NutritionScreen />);
    expect(await view.findByText('This feature is not enabled for the current workspace.')).toBeTruthy();
  });
});
