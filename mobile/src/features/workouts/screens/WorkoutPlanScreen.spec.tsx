import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { getFitnessAssessment } from '../../assessment/api/assessment.api';
import type { FitnessAssessment } from '../../assessment/types';
import { getProgram } from '../../fitness/api/fitness.api';
import { WorkoutPlanScreen } from './WorkoutPlanScreen';

jest.mock('../../fitness/api/fitness.api', () => ({
  getProgram: jest.fn(),
  getExercise: jest.fn(),
  startProgram: jest.fn(),
  listSessions: jest.fn(async () => ({ items: [], page: 1, pageSize: 20, totalCount: 0 })),
}));

jest.mock('../../assessment/api/assessment.api', () => ({
  ...jest.requireActual<typeof import('../../assessment/api/assessment.api')>('../../assessment/api/assessment.api'),
  getFitnessAssessment: jest.fn(),
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
    jest.mocked(getFitnessAssessment).mockResolvedValue({
      primaryGoal: 'WeightLoss',
      secondaryGoal: 'MuscleGain',
      priority: 'Strength',
      goalTimeline: 'OneMonth',
      customGoalTimelineDays: null,
      workout: { daysPerWeek: 5, durationMinutes: 45, preferredDays: ['Monday', 'Wednesday', 'Friday'], trainingStyles: ['Cardio'] },
    } as FitnessAssessment);
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
  });

  it('shows the plan summary and opens the schedule', async () => {
    const view = await renderWithProviders(<WorkoutPlanScreen navigation={navigation} route={route} />);
    expect(await view.findByText('Strength base')).toBeTruthy();
    expect(view.getByText('1')).toBeTruthy();
    expect(view.getByText('5')).toBeTruthy();
    expect(view.getByText('~45 min')).toBeTruthy();
    expect(view.getByText('Weight loss')).toBeTruthy();
    expect(view.getByText('Muscle gain')).toBeTruthy();
    expect(view.getByText('Cardio')).toBeTruthy();
    expect(view.queryByText('Build lean muscle')).toBeNull();
    expect(view.queryByText('Lower body')).toBeNull();
    expect(view.queryByText('Start workout')).toBeNull();
    fireEvent.press(view.getByRole('button', { name: 'View 30-Day Schedule →' }));
    await waitFor(() => expect(navigation.navigate).toHaveBeenCalledWith('PlanCalendar', { programId: 'program-1' }));
  }, 20000);
});
