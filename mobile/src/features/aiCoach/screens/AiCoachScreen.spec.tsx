import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { getProfile } from '../../fitness/api/fitness.api';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { generateWorkout, listConversations } from '../api/ai.api';
import { AiCoachScreen } from './AiCoachScreen';

jest.mock('../../fitness/api/fitness.api', () => ({ getProfile: jest.fn() }));
jest.mock('../api/ai.api', () => ({ generateWorkout: jest.fn(), coachChat: jest.fn(), progressInsight: jest.fn(), listConversations: jest.fn(), getConversation: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));
const mockNavigate = jest.fn();

jest.mock('@react-navigation/native', () => {
  const actual = jest.requireActual<typeof import('@react-navigation/native')>('@react-navigation/native');
  return { ...actual, useNavigation: () => ({ navigate: mockNavigate }) };
});

describe('AiCoachScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated',
      accessToken: 'token',
      refreshToken: 'refresh',
      expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    jest.mocked(getProfile).mockResolvedValue({
      id: 'profile-1',
      unitSystem: 'Metric',
      height: 170,
      weight: 70,
      fitnessLevel: 'Beginner',
      primaryGoal: 'Strength',
      activityLevel: 'Moderate',
      trainingExperience: null,
      preferredWorkoutDays: 3,
      preferredWorkoutDuration: 45,
      preferredTrainingLocation: 'Home',
    });
    mockNavigate.mockClear();
    jest.mocked(listFeatureFlags).mockResolvedValue([
      { featureCode: 'AI_COACH', enabled: true },
      { featureCode: 'AI_WORKOUT_GENERATION', enabled: true },
      { featureCode: 'AI_PROGRESS_INSIGHTS', enabled: true },
    ]);
    jest.mocked(listConversations).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
  });

  it('reviews the saved profile before generation', async () => {
    const view = await renderWithProviders(<AiCoachScreen />);
    expect(await view.findByText("Let's build your plan")).toBeTruthy();
    expect(view.getByText('Strength')).toBeTruthy();
    expect(view.getByText('3 days each week')).toBeTruthy();
    expect(view.getByText('AI-generated fitness guidance is for general wellness and does not replace professional medical advice.')).toBeTruthy();
  }, 15000);

  it('shows a disabled-feature error from the API', async () => {
    jest.mocked(generateWorkout).mockRejectedValue(new ApiError('This feature is not enabled for the current workspace.', 403, null));
    const view = await renderWithProviders(<AiCoachScreen />);
    fireEvent.press(await view.findByRole('button', { name: 'Generate workout plan' }));
    expect(await view.findByText('This feature is not enabled for the current workspace.')).toBeTruthy();
    await waitFor(() => expect(mockNavigate).not.toHaveBeenCalled());
  });

  it('lists no conversations and opens a quick prompt as a draft', async () => {
    const view = await renderWithProviders(<AiCoachScreen />);
    expect(await view.findByText('No conversations yet.', {}, { timeout: 10000 })).toBeTruthy();
    fireEvent.press(view.getByRole('button', { name: "Help me plan today's workout" }));
    expect(mockNavigate).toHaveBeenCalledWith('AiConversation', { draft: "Help me plan today's workout" });
  });

  it('opens an existing conversation', async () => {
    jest.mocked(listConversations).mockResolvedValue({
      items: [{ id: 'conversation-1', title: 'Morning plan', updatedAt: '2026-10-03T10:00:00Z' }],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });
    const view = await renderWithProviders(<AiCoachScreen />);
    fireEvent.press(await view.findByRole('button', { name: 'Morning plan' }, { timeout: 10000 }));
    expect(mockNavigate).toHaveBeenCalledWith('AiConversation', { conversationId: 'conversation-1' });
  });

  it('hides coach chat when the workspace feature is off', async () => {
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'AI_COACH', enabled: false }]);
    const view = await renderWithProviders(<AiCoachScreen />);
    expect(await view.findByText("Let's build your plan")).toBeTruthy();
    expect(view.queryByRole('button', { name: 'Generate workout plan' })).toBeNull();
    expect(view.queryByText('No conversations yet.')).toBeNull();
    expect(view.queryByRole('button', { name: 'New conversation' })).toBeNull();
    expect(view.getByText('This feature is not enabled for the current workspace.')).toBeTruthy();
  }, 15000);
});
