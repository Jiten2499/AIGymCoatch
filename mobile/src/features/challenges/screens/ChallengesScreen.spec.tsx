import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { listChallenges } from '../api/challenges.api';
import { ChallengesScreen } from './ChallengesScreen';

jest.mock('../api/challenges.api', () => ({ listChallenges: jest.fn(), joinChallenge: jest.fn(), recordChallengeProgress: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));
const mockNavigate = jest.fn();
jest.mock('@react-navigation/native', () => {
  const actual = jest.requireActual<typeof import('@react-navigation/native')>('@react-navigation/native');
  return { ...actual, useNavigation: () => ({ navigate: mockNavigate }) };
});

const october = { id: 'challenge-1', name: 'October steps', description: 'Walk', challengeType: 'ConsistencyDays' as const, startDate: '2026-10-01', endDate: '2026-10-31', targetValue: 20, status: 'Active' as const, isSystem: true, currentValue: null, completedAt: null };

function signIn(tenantId: string) {
  useSessionStore.setState({
    status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
    user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
    currentTenant: { id: tenantId, name: 'Workspace', type: 'Personal', status: 'Active' },
  });
}

describe('ChallengesScreen', () => {
  beforeEach(() => {
    signIn('tenant-a');
    mockNavigate.mockClear();
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'WORKOUT_PLANS', enabled: true }]);
    jest.mocked(listChallenges).mockReset();
  });

  it('shows an empty list', async () => {
    jest.mocked(listChallenges).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const view = await renderWithProviders(<ChallengesScreen />);
    expect(await view.findByText('No challenges are available.')).toBeTruthy();
  });

  it('shows a challenge and opens it', async () => {
    jest.mocked(listChallenges).mockResolvedValue({ items: [{ ...october, currentValue: 4 }], page: 1, pageSize: 20, totalCount: 1 });
    const view = await renderWithProviders(<ChallengesScreen />);
    expect(await view.findByText('October steps')).toBeTruthy();
    expect(view.getByText('4 of 20')).toBeTruthy();
    expect(view.queryByText('Not joined')).toBeNull();
    await fireEvent.press(view.getByRole('button', { name: 'View challenge' }));
    expect(mockNavigate).toHaveBeenCalledWith('ChallengeDetail', { challengeId: 'challenge-1', page: 1 });
  });

  it('replaces challenges after the workspace changes', async () => {
    jest.mocked(listChallenges).mockImplementation(async () => useSessionStore.getState().currentTenant?.id === 'tenant-b'
      ? { items: [{ ...october, id: 'challenge-2', name: 'Strength month', currentValue: 1 }], page: 1, pageSize: 20, totalCount: 1 }
      : { items: [october], page: 1, pageSize: 20, totalCount: 1 });
    const view = await renderWithProviders(<ChallengesScreen />);
    expect(await view.findByText('October steps')).toBeTruthy();
    signIn('tenant-b');
    await waitFor(() => expect(view.getByText('Strength month')).toBeTruthy());
    expect(view.queryByText('October steps')).toBeNull();
  });

  it('shows an error with retry', async () => {
    jest.mocked(listChallenges).mockRejectedValue(new ApiError('Network request failed', 0, null, true));
    const view = await renderWithProviders(<ChallengesScreen />);
    expect(await view.findByText('You appear to be offline.')).toBeTruthy();
    expect(view.getByRole('button', { name: 'Try again' })).toBeTruthy();
  });

  it('blocks the list when workout plans are disabled', async () => {
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'WORKOUT_PLANS', enabled: false }]);
    const view = await renderWithProviders(<ChallengesScreen />);
    expect(await view.findByText('This feature is not enabled for the current workspace.')).toBeTruthy();
  });
});
