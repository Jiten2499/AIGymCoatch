import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { progressInsight } from '../../aiCoach/api/ai.api';
import { getDashboard, listRecords } from '../../fitness/api/fitness.api';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { AiProgressInsightScreen } from './AiProgressInsightScreen';

jest.mock('../../fitness/api/fitness.api', () => ({ getDashboard: jest.fn(), listRecords: jest.fn() }));
jest.mock('../../aiCoach/api/ai.api', () => ({ progressInsight: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));

const dashboard = {
  currentGoal: null, latestMeasurement: null, weeklyWorkouts: 2, monthlyWorkouts: 2, streakDays: 2, recentWorkouts: [],
  nutritionToday: { date: '2026-10-03', calories: 0, protein: 0, carbohydrates: 0, fat: 0, calorieTarget: 0, proteinTarget: 0, carbTarget: 0, fatTarget: 0 },
  activeChallenge: null, membershipStatus: null,
};

describe('AiProgressInsightScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    jest.mocked(getDashboard).mockResolvedValue(dashboard);
    jest.mocked(listRecords).mockResolvedValue([]);
  });

  it('blocks the insight when the feature is disabled', async () => {
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'AI_PROGRESS_INSIGHTS', enabled: false }]);
    const view = await renderWithProviders(<AiProgressInsightScreen />);
    expect(await view.findByText('This feature is not enabled for the current workspace.')).toBeTruthy();
    expect(progressInsight).not.toHaveBeenCalled();
  });

  it('shows measured progress and the AI message after success', async () => {
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'AI_PROGRESS_INSIGHTS', enabled: true }]);
    jest.mocked(progressInsight).mockResolvedValue({ conversationId: 'c1', message: 'Keep the same weight next session.', inputTokens: 1, outputTokens: 1 });
    const view = await renderWithProviders(<AiProgressInsightScreen />);
    expect(await view.findByText('2 workouts')).toBeTruthy();
    fireEvent.press(view.getByRole('button', { name: 'How did I perform?' }));
    expect(await view.findByText('Keep the same weight next session.')).toBeTruthy();
  });

  it('shows an error when the insight request fails', async () => {
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'AI_PROGRESS_INSIGHTS', enabled: true }]);
    jest.mocked(progressInsight).mockRejectedValue(new ApiError('Network request failed', 0, null, true));
    const view = await renderWithProviders(<AiProgressInsightScreen />);
    fireEvent.press(await view.findByRole('button', { name: 'How did I perform?' }));
    expect(await view.findByText('You appear to be offline.')).toBeTruthy();
    await waitFor(() => expect(view.getByRole('button', { name: 'Try again' })).toBeTruthy());
  });
});