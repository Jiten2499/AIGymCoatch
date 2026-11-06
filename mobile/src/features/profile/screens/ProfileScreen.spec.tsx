import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent } from '@testing-library/react-native';
import { endSession } from '../../../services/auth/session';
import { useSessionStore } from '../../../store/sessionStore';
import { useWorkspaceStore } from '../../../store/workspaceStore';
import { renderWithProviders } from '../../../test/render';
import { ProfileScreen } from './ProfileScreen';

jest.mock('../../../services/auth/session', () => ({ endSession: jest.fn(async () => undefined) }));
jest.mock('../photo', () => ({
  useMyProfilePhoto: () => ({ data: null }),
  useUploadProfilePhoto: () => ({ mutateAsync: jest.fn(), isPending: false }),
  useRemoveProfilePhoto: () => ({ mutateAsync: jest.fn(), isPending: false }),
  pickProfilePhoto: jest.fn(),
}));

describe('ProfileScreen', () => {
  beforeEach(() => {
    jest.mocked(endSession).mockClear();
    useSessionStore.setState({
      status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    useWorkspaceStore.setState({ workspaces: [{ id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active', roles: ['PersonalOwner'] }] });
  });

  it('shows the signed-in person and current workspace', async () => {
    const view = await renderWithProviders(<ProfileScreen />);
    expect(view.getByText('Ada Lovelace')).toBeTruthy();
    expect(view.getByText('AL')).toBeTruthy();
    expect(view.getByText('ada@example.com')).toBeTruthy();
    expect(view.getByText('Ada Personal')).toBeTruthy();
  });

  it('logs out without exposing the session token', async () => {
    const view = await renderWithProviders(<ProfileScreen />);
    expect(view.queryByText('token')).toBeNull();
    fireEvent.press(view.getByRole('button', { name: 'Log out' }));
    expect(endSession).toHaveBeenCalledTimes(1);
  });
});