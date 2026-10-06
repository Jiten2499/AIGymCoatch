import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { getExercise, getSession } from '../../fitness/api/fitness.api';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { SessionDetailScreen } from './SessionDetailScreen';

jest.mock('../../fitness/api/fitness.api', () => ({ getSession: jest.fn(), getExercise: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));

const navigation = { navigate: jest.fn() } as unknown as NativeStackScreenProps<AppStackParamList, 'SessionDetail'>['navigation'];
const route = { key: 'detail', name: 'SessionDetail', params: { sessionId: 'session-1' } } as NativeStackScreenProps<AppStackParamList, 'SessionDetail'>['route'];

describe('SessionDetailScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'WORKOUT_PLANS', enabled: true }]);
    jest.mocked(getExercise).mockResolvedValue({ id: 'exercise-1', name: 'Squat', description: null, instructions: null, muscleGroup: 'Legs', equipment: null, exerciseType: 'Strength', difficulty: 'Beginner', isSystem: true });
  });

  it('shows the recorded set', async () => {
    jest.mocked(getSession).mockResolvedValue({
      id: 'session-1', workoutProgramId: 'program-1', workoutProgramDayId: 'day-1', startedAt: '2026-10-02T10:00:00Z', completedAt: '2026-10-02T10:40:00Z', status: 'Completed', durationSeconds: 2400, notes: null,
      exercises: [{ id: 'session-exercise-1', exerciseId: 'exercise-1', sequence: 1, notes: null, sets: [{ id: 'set-1', setNumber: 1, reps: 8, weight: 40, durationSeconds: null, distance: null, rpe: null, completed: true, estimatedOneRepMax: null }] }],
    });
    const view = await renderWithProviders(<SessionDetailScreen navigation={navigation} route={route} />);
    expect(await view.findByText('Squat')).toBeTruthy();
    expect(view.getByText('Set 1 · 8 reps · 40')).toBeTruthy();
    expect(view.getByText('These are the values recorded for this workout.')).toBeTruthy();
  });
});
