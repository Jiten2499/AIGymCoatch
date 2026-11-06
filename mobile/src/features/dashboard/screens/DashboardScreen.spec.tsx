import { describe, expect, it, jest, beforeEach } from '@jest/globals';
import { waitFor } from '@testing-library/react-native';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { getDashboard, getProfile, listPrograms } from '../../fitness/api/fitness.api';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import type { MemberDashboard } from '../../fitness/types';
import { DashboardScreen } from './DashboardScreen';

jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));
jest.mock('../../profile/photo', () => ({ useMyProfilePhoto: () => ({ data: null }) }));
jest.mock('../../communications/api/communications.api', () => ({
  listMemberCommunications: jest.fn(async () => ({ items: [], page: 1, pageSize: 20, totalCount: 0 })),
  getMemberCommunication: jest.fn(),
  markCommunicationRead: jest.fn(),
}));
jest.mock('../../fitness/api/fitness.api', () => ({
  getDashboard: jest.fn(),
  getProfile: jest.fn(),
  listPrograms: jest.fn(),
}));

jest.mock('@react-navigation/native', () => {
  const actual = jest.requireActual<typeof import('@react-navigation/native')>('@react-navigation/native');
  return { ...actual, useNavigation: () => ({ navigate: jest.fn() }) };
});

function signIn(tenantId: string) {
  useSessionStore.setState({
    status: 'authenticated',
    accessToken: 'token',
    refreshToken: 'refresh',
    expiresAt: '2099-01-01T00:00:00Z',
    user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
    currentTenant: { id: tenantId, name: 'Ada Personal', type: 'Personal', status: 'Active' },
  });
}

describe('DashboardScreen', () => {
  beforeEach(() => {
    signIn('tenant-a');
    jest.mocked(getProfile).mockRejectedValue(new ApiError('missing', 404, null));
    jest.mocked(listPrograms).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'NUTRITION', enabled: false }]);
  });

  it('shows the personal greeting, an empty profile prompt, and no invented workout', async () => {
    jest.mocked(getDashboard).mockResolvedValue({
      currentGoal: null,
      latestMeasurement: null,
      weeklyWorkouts: 0,
      monthlyWorkouts: 0,
      streakDays: 0,
      recentWorkouts: [],
      nutritionToday: { date: '2026-10-02', calories: 0, protein: 0, carbohydrates: 0, fat: 0, calorieTarget: 0, proteinTarget: 0, carbTarget: 0, fatTarget: 0 },
      activeChallenge: null,
      membershipStatus: null,
    });
    const view = await renderWithProviders(<DashboardScreen />);

    expect(await view.findByText('Hello, Ada')).toBeTruthy();
    expect(view.getByText('Complete your fitness profile to get personalized recommendations.')).toBeTruthy();
    expect(view.getByText('You do not have a workout plan yet.')).toBeTruthy();
  });

  it('replaces the previous workspace goal after a switch', async () => {
    const fatLoss = {
      currentGoal: { id: 'g1', goalType: 'FatLoss' as const, targetValue: null, currentValue: null, startDate: '2026-10-02', targetDate: null, status: 'Active' as const },
      latestMeasurement: null,
      weeklyWorkouts: 2,
      monthlyWorkouts: 4,
      streakDays: 2,
      recentWorkouts: [],
      nutritionToday: { date: '2026-10-02', calories: 0, protein: 0, carbohydrates: 0, fat: 0, calorieTarget: 0, proteinTarget: 0, carbTarget: 0, fatTarget: 0 },
      activeChallenge: null,
      membershipStatus: null,
    };
    const strength: MemberDashboard = { ...fatLoss, currentGoal: { ...fatLoss.currentGoal, id: 'g2', goalType: 'Strength' }, weeklyWorkouts: 1, monthlyWorkouts: 1, streakDays: 1 };
    let active: MemberDashboard = fatLoss;
    jest.mocked(getDashboard).mockImplementation(async () => active);
    const view = await renderWithProviders(<DashboardScreen />);
    expect(await view.findByText('Fat loss')).toBeTruthy();

    active = strength;
    signIn('tenant-b');

    await waitFor(() => expect(view.getByText('Strength')).toBeTruthy());
    expect(view.queryByText('Fat loss')).toBeNull();
  });

  it('shows today nutrition when the workspace feature is enabled', async () => {
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'NUTRITION', enabled: true }]);
    jest.mocked(getDashboard).mockResolvedValue({
      currentGoal: null, latestMeasurement: null, weeklyWorkouts: 0, monthlyWorkouts: 0, streakDays: 0, recentWorkouts: [],
      nutritionToday: { date: '2026-10-03', calories: 400, protein: 20, carbohydrates: 30, fat: 10, calorieTarget: 2000, proteinTarget: 100, carbTarget: 150, fatTarget: 50 },
      activeChallenge: null, membershipStatus: null,
    });
    const view = await renderWithProviders(<DashboardScreen />);
    expect(await view.findByText('400 of 2,000')).toBeTruthy();
    expect(view.getByText('Protein: 20')).toBeTruthy();
  });

  it('shows the active challenge returned by the dashboard', async () => {
    jest.mocked(getDashboard).mockResolvedValue({
      currentGoal: null, latestMeasurement: null, weeklyWorkouts: 1, monthlyWorkouts: 1, streakDays: 1, recentWorkouts: [],
      nutritionToday: { date: '2026-10-03', calories: 0, protein: 0, carbohydrates: 0, fat: 0, calorieTarget: 0, proteinTarget: 0, carbTarget: 0, fatTarget: 0 },
      activeChallenge: { id: 'challenge-1', name: 'October steps', currentValue: 4, targetValue: 20 },
      membershipStatus: null,
    });
    const view = await renderWithProviders(<DashboardScreen />);
    expect(await view.findByText('October steps', {}, { timeout: 10000 })).toBeTruthy();
    expect(view.getByText('4 of 20')).toBeTruthy();
  });

  it('shows an offline error with retry', async () => {
    jest.mocked(getDashboard).mockRejectedValue(new ApiError('Network request failed', 0, null, true));
    const view = await renderWithProviders(<DashboardScreen />);

    expect(await view.findByText('You appear to be offline.')).toBeTruthy();
    expect(view.getByRole('button', { name: 'Try again' })).toBeTruthy();
  });
});
