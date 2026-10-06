import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { listPrograms } from '../../fitness/api/fitness.api';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { WorkoutsScreen } from './WorkoutsScreen';

jest.mock('../../fitness/api/fitness.api', () => ({ listPrograms: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));
jest.mock('@react-navigation/native', () => {
  const actual = jest.requireActual<typeof import('@react-navigation/native')>('@react-navigation/native');
  return { ...actual, useNavigation: () => ({ navigate: jest.fn() }) };
});

describe('WorkoutsScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'WORKOUT_PLANS', enabled: true }]);
  });

  it('shows an empty state when no plan exists', async () => {
    jest.mocked(listPrograms).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const view = await renderWithProviders(<WorkoutsScreen />);
    expect(await view.findByText('Generate a plan with the AI coach, or check back after one is saved.')).toBeTruthy();
  });

  it('blocks the list when workout plans are disabled', async () => {
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'WORKOUT_PLANS', enabled: false }]);
    const view = await renderWithProviders(<WorkoutsScreen />);
    expect(await view.findByText('This feature is not enabled for the current workspace.')).toBeTruthy();
  });
});
