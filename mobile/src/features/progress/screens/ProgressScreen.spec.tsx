import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { getDashboard, listMeasurements, listRecords } from '../../fitness/api/fitness.api';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { ProgressScreen } from './ProgressScreen';

jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));

jest.mock('@react-navigation/native', () => {
  const actual = jest.requireActual<typeof import('@react-navigation/native')>('@react-navigation/native');
  return { ...actual, useNavigation: () => ({ navigate: jest.fn() }) };
});

jest.mock('../../fitness/api/fitness.api', () => ({
  getDashboard: jest.fn(),
  listMeasurements: jest.fn(),
  listRecords: jest.fn(),
}));

const emptyDashboard = {
  currentGoal: null,
  latestMeasurement: null,
  weeklyWorkouts: 0,
  monthlyWorkouts: 0,
  streakDays: 0,
  recentWorkouts: [],
  nutritionToday: { date: '2026-10-02', calories: 0, protein: 0, carbohydrates: 0, fat: 0, calorieTarget: 0, proteinTarget: 0, carbTarget: 0, fatTarget: 0 },
  activeChallenge: null,
  membershipStatus: null,
};

describe('ProgressScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'AI_COACH', enabled: false }]);
  });

  it('stays empty until there is real progress', async () => {
    jest.mocked(getDashboard).mockResolvedValue(emptyDashboard);
    jest.mocked(listMeasurements).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    jest.mocked(listRecords).mockResolvedValue([]);
    const view = await renderWithProviders(<ProgressScreen />);
    expect(await view.findByText('Complete a workout or log a measurement to see progress.')).toBeTruthy();
  });

  it('draws a weight trend only after two weigh-ins', async () => {
    jest.mocked(getDashboard).mockResolvedValue({ ...emptyDashboard, weeklyWorkouts: 1, monthlyWorkouts: 1, streakDays: 1 });
    jest.mocked(listMeasurements).mockResolvedValue({
      items: [
        { id: 'm2', unitSystem: 'Metric', measurementDate: '2026-10-02', weight: 69, bodyFatPercentage: null, chest: null, waist: null, hip: null, arm: null, thigh: null, notes: null },
        { id: 'm1', unitSystem: 'Metric', measurementDate: '2026-09-02', weight: 71, bodyFatPercentage: null, chest: null, waist: null, hip: null, arm: null, thigh: null, notes: null },
      ],
      page: 1, pageSize: 20, totalCount: 2,
    });
    jest.mocked(listRecords).mockResolvedValue([{ exerciseId: 'exercise-1', exerciseName: 'Squat', maxWeight: 60, maxReps: 5, maxDurationSeconds: null, maxDistance: null, estimatedOneRepMax: 70 }]);
    const view = await renderWithProviders(<ProgressScreen />);
    expect(await view.findByText('Squat · 60 · 5')).toBeTruthy();
    expect(view.getByText('2026-09-02 · 71')).toBeTruthy();
    expect(view.getByText('2026-10-02 · 69')).toBeTruthy();
  });
});
