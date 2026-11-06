import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent } from '@testing-library/react-native';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { getCommunicationInboxSummary, listMemberCommunications } from '../api/communications.api';
import { CommunicationsScreen } from './CommunicationsScreen';

const mockNavigate = jest.fn();
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));
jest.mock('../api/communications.api', () => ({
  listMemberCommunications: jest.fn(),
  getMemberCommunication: jest.fn(),
  markCommunicationRead: jest.fn(),
  countUnreadCommunications: jest.fn(),
  getCommunicationInboxSummary: jest.fn(),
  syncInboxCommunications: jest.fn(),
}));
jest.mock('@react-navigation/native', () => {
  const actual = jest.requireActual<typeof import('@react-navigation/native')>('@react-navigation/native');
  return {
    ...actual,
    useNavigation: () => ({ navigate: mockNavigate }),
    useIsFocused: () => false,
  };
});

describe('CommunicationsScreen', () => {
  beforeEach(() => {
    mockNavigate.mockClear();
    useSessionStore.setState({
      status: 'authenticated',
      accessToken: 'token',
      refreshToken: 'refresh',
      expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Gym A', type: 'Business', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'GYM_COMMUNICATIONS', enabled: true }]);
    jest.mocked(listMemberCommunications).mockReset();
    jest.mocked(getCommunicationInboxSummary).mockResolvedValue({
      unreadCount: 1,
      latestActivityAt: '2026-10-07T10:00:00Z',
      serverTimeUtc: '2026-10-07T12:00:00Z',
    });
  });

  it('lists member communications and opens detail', async () => {
    jest.mocked(listMemberCommunications).mockResolvedValue({
      items: [{
        id: 'c1',
        type: 'Announcement',
        title: 'Holiday hours',
        message: 'We close early on Friday.',
        priority: 'Normal',
        publishAt: '2026-10-07T10:00:00Z',
        expiresAt: null,
        isRead: false,
        imageStorageKey: null,
      }],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });
    const view = await renderWithProviders(<CommunicationsScreen />);
    expect(await view.findByText('Holiday hours')).toBeTruthy();
    fireEvent.press(view.getByText('Holiday hours'));
    expect(mockNavigate).toHaveBeenCalledWith('CommunicationDetail', { communicationId: 'c1' });
  }, 15000);
});
