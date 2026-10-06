import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { getNutritionProfile, saveNutritionProfile } from '../api/nutrition.api';
import { NutritionProfileScreen } from './NutritionProfileScreen';

jest.mock('../api/nutrition.api', () => ({ getNutritionProfile: jest.fn(), saveNutritionProfile: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));

describe('NutritionProfileScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'NUTRITION', enabled: true }]);
    jest.mocked(saveNutritionProfile).mockReset();
  });

  it('rejects an incomplete profile', async () => {
    jest.mocked(getNutritionProfile).mockRejectedValue(new ApiError('missing', 404, null));
    const view = await renderWithProviders(<NutritionProfileScreen />);
    await fireEvent.press(await view.findByRole('button', { name: 'Save profile' }));
    expect(await view.findByText('Calories must be from 800 to 6000')).toBeTruthy();
    expect(saveNutritionProfile).not.toHaveBeenCalled();
  });

  it('shows a saved profile and submits once while the request is pending', async () => {
    jest.mocked(getNutritionProfile).mockResolvedValue({ id: 'profile-1', dailyCalorieTarget: 2000, proteinTarget: 120, carbTarget: 180, fatTarget: 50, dietaryPreference: 'Vegetarian', mealFrequency: 3 });
    let resolveSave: (value: Awaited<ReturnType<typeof saveNutritionProfile>>) => void = () => undefined;
    jest.mocked(saveNutritionProfile).mockImplementation(() => new Promise((resolve) => { resolveSave = resolve; }));
    const view = await renderWithProviders(<NutritionProfileScreen />);
    expect(await view.findByDisplayValue('2000')).toBeTruthy();
    await fireEvent.press(view.getByRole('button', { name: 'Save profile' }));
    await waitFor(() => expect(saveNutritionProfile).toHaveBeenCalledTimes(1));
    await fireEvent.press(view.getByRole('button', { name: 'Save profile' }));
    expect(saveNutritionProfile).toHaveBeenCalledTimes(1);
    resolveSave({ id: 'profile-1', dailyCalorieTarget: 2000, proteinTarget: 120, carbTarget: 180, fatTarget: 50, dietaryPreference: 'Vegetarian', mealFrequency: 3 });
    expect(await view.findByText('Saved')).toBeTruthy();
  });

  it('shows an API error', async () => {
    jest.mocked(getNutritionProfile).mockRejectedValue(new ApiError('Network request failed', 0, null, true));
    const view = await renderWithProviders(<NutritionProfileScreen />);
    expect(await view.findByText('You appear to be offline.', {}, { timeout: 10000 })).toBeTruthy();
  });
});
