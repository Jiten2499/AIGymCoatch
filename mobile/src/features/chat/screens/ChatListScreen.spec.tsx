import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent } from '@testing-library/react-native';
import { renderWithProviders } from '../../../test/render';
import { ChatListScreen } from './ChatListScreen';

jest.mock('@react-navigation/native', () => {
  const actual = jest.requireActual<typeof import('@react-navigation/native')>('@react-navigation/native');
  return { ...actual, useNavigation: () => ({ dispatch: jest.fn() }) };
});

const mockOpen = jest.fn();

jest.mock('../../licensing/api/featureQuery', () => ({
  useWorkspaceFeatures: () => ({ data: { GYM_CHAT: true }, isLoading: false, isError: false, refetch: jest.fn() }),
}));
jest.mock('../realtime', () => ({ useChatRealtime: () => undefined }));
jest.mock('../api/queries', () => ({
  useChatInbox: () => ({ data: [], isSuccess: true, isError: false, isLoading: false, error: null, refetch: jest.fn() }),
  useChatRecipients: () => ({
    data: [{ userId: 'member-user', displayName: 'Ada Lovelace', role: 'Member' }],
    isSuccess: true,
    isError: false,
    isLoading: false,
    error: null,
  }),
  useOpenChat: () => ({ mutate: mockOpen, isPending: false }),
}));

describe('ChatListScreen', () => {
  beforeEach(() => {
    mockOpen.mockClear();
  });

  it('lets a trainer start a chat when the inbox is empty', async () => {
    const view = await renderWithProviders(<ChatListScreen />);
    expect(await view.findByText('No conversations yet.')).toBeTruthy();
    expect(view.getByText('Start a chat with an assigned member or another person you are allowed to message.')).toBeTruthy();
    await fireEvent.press(view.getByRole('button', { name: 'New chat' }));
    expect(view.getByText('Ada Lovelace')).toBeTruthy();
    await fireEvent.press(view.getByRole('button', { name: 'Ada Lovelace' }));
    expect(mockOpen).toHaveBeenCalledWith('member-user', expect.any(Object));
  });
});
