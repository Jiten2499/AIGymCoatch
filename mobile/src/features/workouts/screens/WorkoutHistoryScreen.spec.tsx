import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { listSessions } from '../../fitness/api/fitness.api';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { WorkoutHistoryScreen } from './WorkoutHistoryScreen';

jest.mock('../../fitness/api/fitness.api', () => ({ listSessions: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));
jest.mock('@react-navigation/native', () => {
  const actual = jest.requireActual<typeof import('@react-navigation/native')>('@react-navigation/native');
  return { ...actual, useNavigation: () => ({ navigate: jest.fn() }) };
});

describe('WorkoutHistoryScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'WORKOUT_PLANS', enabled: true }]);
  });

  it('shows an empty history', async () => {
    jest.mocked(listSessions).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const view = await renderWithProviders(<WorkoutHistoryScreen />);
    expect(await view.findByText('Completed and in-progress workouts will show up here.')).toBeTruthy();
  });

  it('shows a completed session from the API', async () => {
    jest.mocked(listSessions).mockResolvedValue({
      items: [{ id: 'session-1', workoutProgramId: 'program-1', workoutProgramDayId: 'day-1', startedAt: '2026-10-02T10:00:00Z', completedAt: '2026-10-02T10:40:00Z', status: 'Completed', durationSeconds: 2400, notes: null, exercises: [] }],
      page: 1, pageSize: 20, totalCount: 1,
    });
    const view = await renderWithProviders(<WorkoutHistoryScreen />);
    expect(await view.findByText('Completed')).toBeTruthy();
  });
});
