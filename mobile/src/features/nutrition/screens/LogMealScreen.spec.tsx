import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { createNutritionLog } from '../api/nutrition.api';
import { LogMealScreen } from './LogMealScreen';

jest.mock('../api/nutrition.api', () => ({ createNutritionLog: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));

const saved = { id: 'log-1', logDate: '2026-10-03', mealType: 'Lunch' as const, foodName: 'Rice', quantity: 1, unit: 'plate', calories: 250, protein: 5, carbohydrates: 45, fat: 2 };

describe('LogMealScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'NUTRITION', enabled: true }]);
    jest.mocked(createNutritionLog).mockReset();
  });

  it('requires a meal before saving', async () => {
    const view = await renderWithProviders(<LogMealScreen />);
    await fireEvent.press(await view.findByRole('button', { name: 'Save meal' }));
    expect(await view.findByText('This field is required')).toBeTruthy();
    expect(createNutritionLog).not.toHaveBeenCalled();
  });

  it('saves one meal and ignores a second tap while the request is pending', async () => {
    let resolveSave: (value: typeof saved) => void = () => undefined;
    jest.mocked(createNutritionLog).mockImplementation(() => new Promise((resolve) => { resolveSave = resolve; }));
    const view = await renderWithProviders(<LogMealScreen />);
    await fireEvent.press(await view.findByRole('button', { name: 'Lunch' }));
    await fireEvent.changeText(view.getByLabelText('Food'), 'Rice');
    await fireEvent.changeText(view.getByLabelText('Quantity'), '1');
    await fireEvent.changeText(view.getByLabelText('Unit'), 'plate');
    await fireEvent.changeText(view.getByLabelText('Calories'), '250');
    await fireEvent.changeText(view.getByLabelText('Protein'), '5');
    await fireEvent.changeText(view.getByLabelText('Carbohydrates'), '45');
    await fireEvent.changeText(view.getByLabelText('Fat'), '2');
    await fireEvent.press(view.getByRole('button', { name: 'Save meal' }));
    await waitFor(() => expect(createNutritionLog).toHaveBeenCalledWith(expect.objectContaining({ foodName: 'Rice', mealType: 'Lunch', calories: 250 })));
    await fireEvent.press(view.getByRole('button', { name: 'Save meal' }));
    expect(createNutritionLog).toHaveBeenCalledTimes(1);
    resolveSave(saved);
    expect(await view.findByText('Saved')).toBeTruthy();
  });

  it('shows a server error', async () => {
    jest.mocked(createNutritionLog).mockRejectedValue(new ApiError('Network request failed', 0, null, true));
    const view = await renderWithProviders(<LogMealScreen />);
    await fireEvent.press(await view.findByRole('button', { name: 'Breakfast' }));
    await fireEvent.changeText(view.getByLabelText('Food'), 'Oats');
    await fireEvent.changeText(view.getByLabelText('Quantity'), '1');
    await fireEvent.changeText(view.getByLabelText('Unit'), 'bowl');
    await fireEvent.changeText(view.getByLabelText('Calories'), '0');
    await fireEvent.changeText(view.getByLabelText('Protein'), '0');
    await fireEvent.changeText(view.getByLabelText('Carbohydrates'), '0');
    await fireEvent.changeText(view.getByLabelText('Fat'), '0');
    await fireEvent.press(view.getByRole('button', { name: 'Save meal' }));
    expect(await view.findByText('You appear to be offline.')).toBeTruthy();
  });
});
