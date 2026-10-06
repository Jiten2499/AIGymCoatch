import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { getProfile, listMeasurements, recordMeasurement } from '../../fitness/api/fitness.api';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { MeasurementsScreen } from './MeasurementsScreen';

jest.mock('../../fitness/api/fitness.api', () => ({ listMeasurements: jest.fn(), getProfile: jest.fn(), recordMeasurement: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));

describe('MeasurementsScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'WORKOUT_PLANS', enabled: true }]);
    jest.mocked(getProfile).mockResolvedValue({ id: 'profile-1', unitSystem: 'Metric', height: 170, weight: 70, fitnessLevel: 'Beginner', primaryGoal: 'Strength', activityLevel: 'Moderate', trainingExperience: null, preferredWorkoutDays: 3, preferredWorkoutDuration: 45, preferredTrainingLocation: null });
  });

  it('shows an empty measurement list', async () => {
    jest.mocked(listMeasurements).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const view = await renderWithProviders(<MeasurementsScreen />);
    expect(await view.findByText('No measurements recorded yet.')).toBeTruthy();
  });

  it('saves a weight and rejects a negative value', async () => {
    jest.mocked(listMeasurements).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    jest.mocked(recordMeasurement).mockResolvedValue({ id: 'm1', unitSystem: 'Metric', measurementDate: '2026-10-03', weight: 70, bodyFatPercentage: null, chest: null, waist: null, hip: null, arm: null, thigh: null, notes: null });
    const view = await renderWithProviders(<MeasurementsScreen />);
    await fireEvent.changeText(await view.findByLabelText('Weight'), '-1');
    await fireEvent.press(view.getByRole('button', { name: 'Save weight' }));
    expect(await view.findByText('Enter a valid number')).toBeTruthy();
    expect(recordMeasurement).not.toHaveBeenCalled();
    await fireEvent.changeText(view.getByLabelText('Weight'), '70');
    await fireEvent.press(view.getByRole('button', { name: 'Save weight' }));
    await waitFor(() => expect(recordMeasurement).toHaveBeenCalledWith(expect.objectContaining({ weight: 70, unitSystem: 'Metric' })));
  });
});
