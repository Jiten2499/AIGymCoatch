import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { AppStackParamList } from '../../../app/navigation/types';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { addSet, completeSession, getExercise, getProgram, getSession } from '../../fitness/api/fitness.api';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { WorkoutSessionScreen } from './WorkoutSessionScreen';

jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));
jest.mock('../../fitness/api/fitness.api', () => ({
  getSession: jest.fn(),
  getProgram: jest.fn(),
  getExercise: jest.fn(),
  addSet: jest.fn(),
  completeSession: jest.fn(),
}));

const navigation = { replace: jest.fn(), navigate: jest.fn() } as unknown as NativeStackScreenProps<AppStackParamList, 'WorkoutSession'>['navigation'];
const route = { key: 'session', name: 'WorkoutSession', params: { sessionId: 'session-1' } } as NativeStackScreenProps<AppStackParamList, 'WorkoutSession'>['route'];

describe('WorkoutSessionScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated',
      accessToken: 'token',
      refreshToken: 'refresh',
      expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'WORKOUT_PLANS', enabled: true }]);
    jest.mocked(getSession).mockClear();
    jest.mocked(getExercise).mockClear();
    jest.mocked(addSet).mockReset();
    jest.mocked(completeSession).mockReset();
    (navigation.replace as jest.Mock).mockClear();
    jest.mocked(getExercise).mockResolvedValue({
      id: 'exercise-1', name: 'Squat', description: null, instructions: 'Brace and sit down.', muscleGroup: 'Legs', equipment: 'Barbell', exerciseType: 'Strength', difficulty: 'Beginner', isSystem: true,
    });
    jest.mocked(getProgram).mockResolvedValue({
      id: 'program-1', name: 'Strength base', description: null, goalType: 'Strength', difficulty: 'Beginner', durationWeeks: 4, frequencyPerWeek: 3, status: 'Active', isOwnedByUser: true,
      days: [{ id: 'day-1', dayNumber: 1, name: 'Lower body', description: null, restDay: false, exercises: [{ id: 'item-1', exerciseId: 'exercise-1', sequence: 1, sets: 3, targetReps: 8, targetDurationSeconds: null, targetDistance: null, targetWeight: 40, restSeconds: 90, notes: null }] }],
    });
    jest.mocked(getSession).mockResolvedValue({
      id: 'session-1', workoutProgramId: 'program-1', workoutProgramDayId: 'day-1', startedAt: '2026-10-02T10:00:00Z', completedAt: null, status: 'InProgress', durationSeconds: null, notes: null,
      exercises: [{ id: 'session-exercise-1', exerciseId: 'exercise-1', sequence: 1, notes: null, sets: [] }],
    });
  });

  it('rejects an empty set and logs a valid one', async () => {
    jest.mocked(addSet).mockResolvedValue({ id: 'set-1', setNumber: 1, reps: 8, weight: 40, durationSeconds: null, distance: null, rpe: null, completed: true, estimatedOneRepMax: null });
    const view = await renderWithProviders(<WorkoutSessionScreen navigation={navigation} route={route} />);
    expect(await view.findByText('Squat')).toBeTruthy();
    await fireEvent.press(view.getByRole('button', { name: 'Log set' }));
    expect(await view.findByText('Enter reps, weight, duration, or distance.')).toBeTruthy();
    expect(addSet).not.toHaveBeenCalled();

    await fireEvent.changeText(view.getByLabelText('Reps'), '8');
    await fireEvent.changeText(view.getByLabelText('Weight'), '40');
    await fireEvent.press(view.getByRole('button', { name: 'Log set' }));
    await waitFor(() => expect(addSet).toHaveBeenCalledWith('session-exercise-1', expect.objectContaining({ reps: 8, weight: 40, completed: true })));

    jest.mocked(completeSession).mockRejectedValue(new ApiError('Network request failed', 0, null, true));
    await fireEvent.press(view.getByRole('button', { name: 'Finish workout' }));
    expect(await view.findByText('You appear to be offline.')).toBeTruthy();
    expect(navigation.replace).not.toHaveBeenCalled();
  });

  it('does not submit a set twice while the request is pending and waits to finish', async () => {
    jest.mocked(addSet).mockImplementation(() => new Promise(() => undefined));
    const view = await renderWithProviders(<WorkoutSessionScreen navigation={navigation} route={route} />);
    expect(await view.findByText('Squat')).toBeTruthy();
    await fireEvent.changeText(view.getByLabelText('Reps'), '8');
    await fireEvent.press(view.getByRole('button', { name: 'Log set' }));
    await waitFor(() => expect(addSet).toHaveBeenCalledTimes(1));
    await fireEvent.press(view.getByRole('button', { name: 'Log set' }));
    expect(addSet).toHaveBeenCalledTimes(1);

    jest.mocked(completeSession).mockResolvedValue({
      id: 'session-1', workoutProgramId: 'program-1', workoutProgramDayId: 'day-1', startedAt: '2026-10-02T10:00:00Z', completedAt: '2026-10-02T10:20:00Z', status: 'Completed', durationSeconds: 1200, notes: null, exercises: [],
    });
    await fireEvent.press(view.getByRole('button', { name: 'Finish workout' }));
    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith('WorkoutSummary', { sessionId: 'session-1' }));
  });

  it('blocks logging when workout plans are disabled', async () => {
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'WORKOUT_PLANS', enabled: false }]);
    const view = await renderWithProviders(<WorkoutSessionScreen navigation={navigation} route={route} />);
    expect(await view.findByText('This feature is not enabled for the current workspace.')).toBeTruthy();
    expect(addSet).not.toHaveBeenCalled();
  });
});
