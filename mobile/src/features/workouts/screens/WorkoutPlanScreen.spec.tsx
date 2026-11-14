import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { getExercise, getProgram, startProgram } from '../../fitness/api/fitness.api';
import { WorkoutPlanScreen } from './WorkoutPlanScreen';

jest.mock('../../fitness/api/fitness.api', () => ({
  getProgram: jest.fn(),
  getExercise: jest.fn(),
  startProgram: jest.fn(),
}));

const navigation = { navigate: jest.fn() } as unknown as NativeStackScreenProps<AppStackParamList, 'WorkoutPlan'>['navigation'];
const route = { key: 'plan', name: 'WorkoutPlan', params: { programId: 'program-1' } } as NativeStackScreenProps<AppStackParamList, 'WorkoutPlan'>['route'];

describe('WorkoutPlanScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated',
      accessToken: 'token',
      refreshToken: 'refresh',
      expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    jest.mocked(getExercise).mockResolvedValue({
      id: 'exercise-1',
      name: 'Squat',
      description: null,
      instructions: 'Brace and sit down.',
      muscleGroup: 'Legs',
      equipment: 'Barbell',
      exerciseType: 'Strength',
      difficulty: 'Beginner',
      isSystem: true,
    });
    jest.mocked(getProgram).mockResolvedValue({
      id: 'program-1',
      name: 'Strength base',
      description: 'Four week start',
      goalType: 'Strength',
      difficulty: 'Beginner',
      durationWeeks: 4,
      frequencyPerWeek: 3,
      status: 'Active',
      isOwnedByUser: true,
      days: [{
        id: 'day-1',
        dayNumber: 1,
        name: 'Lower body',
        description: null,
        restDay: false,
        exercises: [{ id: 'item-1', exerciseId: 'exercise-1', sequence: 1, sets: 3, targetReps: 8, targetDurationSeconds: null, targetDistance: null, targetWeight: 40, restSeconds: 90, notes: null }],
      }],
    });
    jest.mocked(startProgram).mockResolvedValue({
      id: 'session-1',
      workoutProgramId: 'program-1',
      workoutProgramDayId: 'day-1',
      startedAt: '2026-10-02T10:00:00Z',
      completedAt: null,
      status: 'InProgress',
      durationSeconds: null,
      notes: null,
      exercises: [],
    });
  });

  it('renders the plan and starts the selected day', async () => {
    const view = await renderWithProviders(<WorkoutPlanScreen navigation={navigation} route={route} />);
    expect(await view.findByText('Strength base')).toBeTruthy();
    fireEvent.press(view.getByLabelText('Lower body'));
    expect(await view.findByText('Squat')).toBeTruthy();
    expect(view.getByText('Brace and sit down.')).toBeTruthy();
    fireEvent.press(view.getByRole('button', { name: 'Start workout' }));
    await waitFor(() => expect(startProgram).toHaveBeenCalledWith('program-1', 'day-1'));
    await waitFor(() => expect(navigation.navigate).toHaveBeenCalledWith('WorkoutSession', { sessionId: 'session-1' }));
  }, 20000);
});
