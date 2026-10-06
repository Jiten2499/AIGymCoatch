import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { ApiError } from '../../../services/api/problem';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { getExercise } from '../../fitness/api/fitness.api';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { ExerciseDetailScreen } from './ExerciseDetailScreen';

jest.mock('../../fitness/api/fitness.api', () => ({ getExercise: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));

const route = { key: 'exercise', name: 'ExerciseDetail', params: { exerciseId: 'exercise-1' } } as NativeStackScreenProps<AppStackParamList, 'ExerciseDetail'>['route'];

describe('ExerciseDetailScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'WORKOUT_PLANS', enabled: true }]);
  });

  it('shows the exercise returned by the API', async () => {
    jest.mocked(getExercise).mockResolvedValue({ id: 'exercise-1', name: 'Squat', description: 'A knee bend.', instructions: 'Brace and sit down.', muscleGroup: 'Legs', equipment: 'Barbell', exerciseType: 'Strength', difficulty: 'Beginner', isSystem: true });
    const view = await renderWithProviders(<ExerciseDetailScreen navigation={{} as never} route={route} />);
    expect(await view.findByText('Squat')).toBeTruthy();
    expect(view.getByText('Brace and sit down.')).toBeTruthy();
    expect(view.getByText('Equipment: Barbell')).toBeTruthy();
  });

  it('shows an error with retry', async () => {
    jest.mocked(getExercise).mockRejectedValue(new ApiError('Network request failed', 0, null, true));
    const view = await renderWithProviders(<ExerciseDetailScreen navigation={{} as never} route={route} />);
    expect(await view.findByText('You appear to be offline.')).toBeTruthy();
    expect(view.getByRole('button', { name: 'Try again' })).toBeTruthy();
  });
});
