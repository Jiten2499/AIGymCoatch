import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent } from '@testing-library/react-native';
import { useSessionStore } from '../../store/sessionStore';
import { useWorkspaceStore } from '../../store/workspaceStore';
import { renderWithProviders } from '../../test/render';
import { MoreScreen } from './MoreScreen';

const mockNavigate = jest.fn();
jest.mock('@react-navigation/native', () => {
  const actual = jest.requireActual<typeof import('@react-navigation/native')>('@react-navigation/native');
  return { ...actual, useNavigation: () => ({ navigate: mockNavigate }) };
});

describe('MoreScreen', () => {
  beforeEach(() => {
    mockNavigate.mockClear();
    useSessionStore.setState({
      status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    useWorkspaceStore.setState({ workspaces: [{ id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active', roles: ['PersonalOwner'] }] });
  });

  it('opens coach, challenges, notifications, and settings for a personal workspace', async () => {
    const view = await renderWithProviders(<MoreScreen />);
    fireEvent.press(view.getByRole('button', { name: 'AI coach' }));
    fireEvent.press(view.getByRole('button', { name: 'Challenges' }));
    fireEvent.press(view.getByRole('button', { name: 'Notifications' }));
    fireEvent.press(view.getByRole('button', { name: 'Settings' }));
    expect(mockNavigate).toHaveBeenNthCalledWith(1, 'AiCoach');
    expect(mockNavigate).toHaveBeenNthCalledWith(2, 'Challenges');
    expect(mockNavigate).toHaveBeenNthCalledWith(3, 'Notifications');
    expect(mockNavigate).toHaveBeenNthCalledWith(4, 'Settings');
    expect(view.queryByRole('button', { name: 'Business' })).toBeNull();
    expect(view.queryByRole('button', { name: 'Licenses' })).toBeNull();
  });
});