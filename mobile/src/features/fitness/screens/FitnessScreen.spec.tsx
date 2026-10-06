import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { createGoal, getProfile, listGoals, saveProfile } from '../api/fitness.api';
import { FitnessScreen } from './FitnessScreen';

jest.mock('../api/fitness.api', () => ({
  getProfile: jest.fn(),
  listGoals: jest.fn(),
  saveProfile: jest.fn(),
  createGoal: jest.fn(),
}));

describe('FitnessScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated',
      accessToken: 'token',
      refreshToken: 'refresh',
      expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    jest.mocked(getProfile).mockRejectedValue(new ApiError('missing', 404, null));
    jest.mocked(listGoals).mockResolvedValue([]);
    jest.mocked(saveProfile).mockResolvedValue({
      id: 'profile-1',
      unitSystem: 'Metric',
      height: null,
      weight: null,
      fitnessLevel: 'Beginner',
      primaryGoal: 'GeneralFitness',
      activityLevel: 'Moderate',
      trainingExperience: null,
      preferredWorkoutDays: 3,
      preferredWorkoutDuration: 45,
      preferredTrainingLocation: null,
    });
    jest.mocked(createGoal).mockResolvedValue({
      id: 'goal-1',
      goalType: 'MuscleGain',
      targetValue: null,
      currentValue: null,
      startDate: '2026-10-02',
      targetDate: null,
      status: 'Active',
    });
    jest.mocked(createGoal).mockClear();
    jest.mocked(saveProfile).mockClear();
  });

  it('lets a new member save a profile and choose a goal', async () => {
    const view = await renderWithProviders(<FitnessScreen />);
    expect(await view.findByText('Complete your fitness profile to get personalized recommendations.')).toBeTruthy();

    fireEvent.press(view.getByTestId('goal-MuscleGain'));
    await waitFor(() => expect(view.getByTestId('goal-MuscleGain').props.accessibilityState).toEqual(expect.objectContaining({ selected: true })));
    fireEvent.press(view.getByRole('button', { name: 'Set a goal' }));
    await waitFor(() => expect(jest.mocked(createGoal).mock.calls.at(-1)?.[0]).toEqual(expect.objectContaining({ goalType: 'MuscleGain', targetValue: null })));

    fireEvent.press(view.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(saveProfile).toHaveBeenCalledWith(expect.objectContaining({
      unitSystem: 'Metric',
      fitnessLevel: 'Beginner',
      primaryGoal: 'GeneralFitness',
      preferredWorkoutDays: 3,
    })));
  });

  it('shows a server validation error on the field', async () => {
    jest.mocked(saveProfile).mockRejectedValue(new ApiError('One or more validation errors occurred.', 400, {
      title: 'One or more validation errors occurred.',
      errors: { Height: ['Height must be between 1 and 300'] },
    }));
    const view = await renderWithProviders(<FitnessScreen />);
    fireEvent.press(await view.findByText('Save'));
    expect(await view.findByText('Height must be between 1 and 300')).toBeTruthy();
    expect(view.queryByText('One or more validation errors occurred.')).toBeNull();
  });

  it('marks required goal fields and shows an invalid target on that field', async () => {
    const view = await renderWithProviders(<FitnessScreen />);
    expect(await view.findByLabelText('Primary goal, required')).toBeTruthy();
    expect(view.getByLabelText('Goal, required')).toBeTruthy();
    expect(view.getByLabelText('Training days each week, required')).toBeTruthy();

    fireEvent.changeText(view.getByTestId('goal-target'), 'nope');
    fireEvent.press(view.getByRole('button', { name: 'Set a goal' }));
    expect(await view.findByText('Enter a valid number')).toBeTruthy();
    expect(createGoal).not.toHaveBeenCalled();

    jest.mocked(createGoal).mockRejectedValue(new ApiError('One or more validation errors occurred.', 400, {
      title: 'One or more validation errors occurred.',
      errors: { GoalType: ['Goal type is not valid.'] },
    }));
    fireEvent.changeText(view.getByTestId('goal-target'), '70');
    fireEvent.press(view.getByRole('button', { name: 'Set a goal' }));
    expect(await view.findByText('Goal type is not valid.')).toBeTruthy();
    expect(view.queryByText('One or more validation errors occurred.')).toBeNull();
  });
});
