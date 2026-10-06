import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { listNotifications, markNotificationRead } from '../api/notifications.api';
import { NotificationsScreen } from './NotificationsScreen';

jest.mock('../api/notifications.api', () => ({ listNotifications: jest.fn(), markNotificationRead: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));

const unread = { id: 'note-1', type: 'ChallengeReminder' as const, title: 'Challenge joined', message: 'October steps', isRead: false, createdAt: '2026-10-03T10:00:00Z' };

function signIn(tenantId: string) {
  useSessionStore.setState({
    status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
    user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
    currentTenant: { id: tenantId, name: 'Workspace', type: 'Personal', status: 'Active' },
  });
}

describe('NotificationsScreen', () => {
  beforeEach(() => {
    signIn('tenant-a');
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'WORKOUT_PLANS', enabled: true }]);
    jest.mocked(listNotifications).mockReset();
    jest.mocked(markNotificationRead).mockReset();
  });

  it('shows an empty list', async () => {
    jest.mocked(listNotifications).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const view = await renderWithProviders(<NotificationsScreen />);
    expect(await view.findByText('No notifications yet.')).toBeTruthy();
  });

  it('shows an unread notification and marks it read once', async () => {
    jest.mocked(listNotifications).mockResolvedValue({ items: [unread], page: 1, pageSize: 20, totalCount: 1 });
    let resolveRead: (value: typeof unread) => void = () => undefined;
    jest.mocked(markNotificationRead).mockImplementation(() => new Promise((resolve) => { resolveRead = resolve; }));
    const view = await renderWithProviders(<NotificationsScreen />);
    expect(await view.findByText('Unread')).toBeTruthy();
    expect(view.getByText('October steps')).toBeTruthy();
    await fireEvent.press(view.getByRole('button', { name: 'Mark as read' }));
    await waitFor(() => expect(markNotificationRead).toHaveBeenCalledTimes(1));
    await fireEvent.press(view.getByRole('button', { name: 'Mark as read' }));
    expect(markNotificationRead).toHaveBeenCalledTimes(1);
    resolveRead({ ...unread, isRead: true });
  });

  it('requests the next server page', async () => {
    jest.mocked(listNotifications).mockImplementation(async (page) => page === 1
      ? { items: [unread], page: 1, pageSize: 20, totalCount: 21 }
      : { items: [{ ...unread, id: 'note-2', title: 'Older note', isRead: true }], page: 2, pageSize: 20, totalCount: 21 });
    const view = await renderWithProviders(<NotificationsScreen />);
    expect(await view.findByText('Challenge joined')).toBeTruthy();
    await fireEvent.press(view.getByRole('button', { name: 'Next page' }));
    await waitFor(() => expect(view.getByText('Older note')).toBeTruthy());
    expect(view.queryByText('Challenge joined')).toBeNull();
  });

  it('replaces notifications after the workspace changes', async () => {
    jest.mocked(listNotifications).mockImplementation(async () => useSessionStore.getState().currentTenant?.id === 'tenant-b'
      ? { items: [{ ...unread, id: 'note-b', title: 'Business note' }], page: 1, pageSize: 20, totalCount: 1 }
      : { items: [unread], page: 1, pageSize: 20, totalCount: 1 });
    const view = await renderWithProviders(<NotificationsScreen />);
    expect(await view.findByText('Challenge joined')).toBeTruthy();
    signIn('tenant-b');
    await waitFor(() => expect(view.getByText('Business note')).toBeTruthy());
    expect(view.queryByText('Challenge joined')).toBeNull();
  });

  it('shows an error with retry', async () => {
    jest.mocked(listNotifications).mockRejectedValue(new ApiError('Network request failed', 0, null, true));
    const view = await renderWithProviders(<NotificationsScreen />);
    expect(await view.findByText('You appear to be offline.')).toBeTruthy();
    expect(view.getByRole('button', { name: 'Try again' })).toBeTruthy();
  });
});
