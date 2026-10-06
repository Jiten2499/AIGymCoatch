import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { progressInsight } from '../../aiCoach/api/ai.api';
import { getExercise, getSession } from '../../fitness/api/fitness.api';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { WorkoutSummaryScreen } from './WorkoutSummaryScreen';

jest.mock('../../fitness/api/fitness.api', () => ({ getSession: jest.fn(), getExercise: jest.fn() }));
jest.mock('../../aiCoach/api/ai.api', () => ({ progressInsight: jest.fn(), generateWorkout: jest.fn(), coachChat: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));

const navigation = { navigate: jest.fn() } as unknown as NativeStackScreenProps<AppStackParamList, 'WorkoutSummary'>['navigation'];
const route = { key: 'summary', name: 'WorkoutSummary', params: { sessionId: 'session-1' } } as NativeStackScreenProps<AppStackParamList, 'WorkoutSummary'>['route'];

describe('WorkoutSummaryScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'AI_PROGRESS_INSIGHTS', enabled: true }]);
    jest.mocked(getExercise).mockResolvedValue({
      id: 'exercise-1', name: 'Squat', description: null, instructions: null, muscleGroup: 'Legs', equipment: null, exerciseType: 'Strength', difficulty: 'Beginner', isSystem: true,
    });
    jest.mocked(getSession).mockResolvedValue({
      id: 'session-1', workoutProgramId: 'program-1', workoutProgramDayId: 'day-1', startedAt: '2026-10-02T10:00:00Z', completedAt: '2026-10-02T10:40:00Z', status: 'Completed', durationSeconds: 2400, notes: null,
      exercises: [{ id: 'session-exercise-1', exerciseId: 'exercise-1', sequence: 1, notes: null, sets: [{ id: 'set-1', setNumber: 1, reps: 8, weight: 40, durationSeconds: null, distance: null, rpe: null, completed: true, estimatedOneRepMax: null }] }],
    });
    jest.mocked(progressInsight).mockResolvedValue({ conversationId: 'conversation-1', message: 'Keep the same weight next session.', inputTokens: 1, outputTokens: 1 });
  });

  it('shows the recorded summary and requests feedback', async () => {
    const view = await renderWithProviders(<WorkoutSummaryScreen navigation={navigation} route={route} />);
    expect(await view.findByText('1 exercises')).toBeTruthy();
    expect(view.getByText('1 sets logged')).toBeTruthy();
    expect(view.getByText('40 minutes')).toBeTruthy();
    fireEvent.press(await view.findByRole('button', { name: 'How did I perform?' }));
    await waitFor(() => expect(progressInsight).toHaveBeenCalled());
    expect(await view.findByText('Keep the same weight next session.')).toBeTruthy();
  });
});
