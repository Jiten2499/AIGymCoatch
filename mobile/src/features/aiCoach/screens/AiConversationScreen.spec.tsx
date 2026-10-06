import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { AppStackParamList } from '../../../app/navigation/types';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { coachChat, getConversation } from '../api/ai.api';
import { AiConversationScreen } from './AiConversationScreen';

jest.mock('../api/ai.api', () => ({ coachChat: jest.fn(), getConversation: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));

const navigation = { navigate: jest.fn() } as unknown as NativeStackScreenProps<AppStackParamList, 'AiConversation'>['navigation'];
const route = { key: 'chat', name: 'AiConversation' as const, params: { conversationId: 'conversation-1' } };

describe('AiConversationScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'AI_COACH', enabled: true }]);
    jest.mocked(coachChat).mockReset();
    jest.mocked(getConversation).mockResolvedValue({
      id: 'conversation-1',
      title: 'Plan',
      messages: [{ role: 'Assistant', content: 'Start with a short walk.', createdAt: '2026-10-03T10:00:00Z' }],
    });
  });

  it('shows the stored AI message and an empty draft conversation', async () => {
    const view = await renderWithProviders(<AiConversationScreen navigation={navigation} route={route} />);
    expect(await view.findByText('Start with a short walk.')).toBeTruthy();
    expect(view.getByText('AI insight')).toBeTruthy();
    expect(view.getByText('LifeFit AI is not a medical diagnosis system.')).toBeTruthy();
  });

  it('sends one message and keeps the draft when the request fails', async () => {
    jest.mocked(getConversation).mockResolvedValue({ id: 'conversation-1', title: 'Plan', messages: [] });
    let resolveChat: (value: { conversationId: string; message: string; inputTokens: number; outputTokens: number }) => void = () => undefined;
    jest.mocked(coachChat).mockImplementation(() => new Promise((resolve) => { resolveChat = resolve; }));
    const view = await renderWithProviders(<AiConversationScreen navigation={navigation} route={route} />);
    await fireEvent.changeText(await view.findByLabelText('Ask the coach'), 'How should I train?');
    await fireEvent.press(view.getByRole('button', { name: 'Send' }));
    await waitFor(() => expect(coachChat).toHaveBeenCalledTimes(1));
    await fireEvent.press(view.getByRole('button', { name: 'Send' }));
    expect(coachChat).toHaveBeenCalledTimes(1);
    resolveChat({ conversationId: 'conversation-1', message: 'Keep the session short.', inputTokens: 1, outputTokens: 1 });
    expect(await view.findByText('Keep the session short.')).toBeTruthy();
    expect(view.getByText('You')).toBeTruthy();
  });

  it('shows an error and leaves the draft in place', async () => {
    jest.mocked(getConversation).mockResolvedValue({ id: 'conversation-1', title: 'Plan', messages: [] });
    jest.mocked(coachChat).mockRejectedValue(new ApiError('Network request failed', 0, null, true));
    const view = await renderWithProviders(<AiConversationScreen navigation={navigation} route={route} />);
    await fireEvent.changeText(await view.findByLabelText('Ask the coach'), 'Hello');
    await fireEvent.press(view.getByRole('button', { name: 'Send' }));
    expect(await view.findByText('You appear to be offline.')).toBeTruthy();
    expect(view.getByLabelText('Ask the coach').props.value).toBe('Hello');
  });

  it('blocks the thread when AI coach is disabled', async () => {
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'AI_COACH', enabled: false }]);
    const view = await renderWithProviders(<AiConversationScreen navigation={navigation} route={{ ...route, params: undefined }} />);
    expect(await view.findByText('This feature is not enabled for the current workspace.')).toBeTruthy();
    expect(coachChat).not.toHaveBeenCalled();
  });
});
