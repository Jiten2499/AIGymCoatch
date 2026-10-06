import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { joinChallenge, listChallenges, recordChallengeProgress } from '../api/challenges.api';
import { ChallengeDetailScreen } from './ChallengeDetailScreen';

jest.mock('../api/challenges.api', () => ({ listChallenges: jest.fn(), joinChallenge: jest.fn(), recordChallengeProgress: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));

const navigation = {} as NativeStackScreenProps<AppStackParamList, 'ChallengeDetail'>['navigation'];
const route = { key: 'detail', name: 'ChallengeDetail' as const, params: { challengeId: 'challenge-1', page: 1 } };
const challenge = { id: 'challenge-1', name: 'October steps', description: 'Walk most days.', challengeType: 'ConsistencyDays' as const, startDate: '2026-10-01', endDate: '2026-10-31', targetValue: 20, status: 'Active' as const, isSystem: true, currentValue: null as number | null, completedAt: null as string | null };

describe('ChallengeDetailScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'WORKOUT_PLANS', enabled: true }]);
    jest.mocked(joinChallenge).mockReset();
    jest.mocked(recordChallengeProgress).mockReset();
  });

  it('joins once while the request is pending', async () => {
    jest.mocked(listChallenges).mockResolvedValue({ items: [challenge], page: 1, pageSize: 20, totalCount: 1 });
    let resolveJoin: (value: typeof challenge) => void = () => undefined;
    jest.mocked(joinChallenge).mockImplementation(() => new Promise((resolve) => { resolveJoin = resolve; }));
    const view = await renderWithProviders(<ChallengeDetailScreen navigation={navigation} route={route} />);
    expect(await view.findByText('Walk most days.')).toBeTruthy();
    expect(view.getByText('Not joined')).toBeTruthy();
    await fireEvent.press(view.getByRole('button', { name: 'Join challenge' }));
    await waitFor(() => expect(joinChallenge).toHaveBeenCalledTimes(1));
    await fireEvent.press(view.getByRole('button', { name: 'Join challenge' }));
    expect(joinChallenge).toHaveBeenCalledTimes(1);
    resolveJoin({ ...challenge, currentValue: 0 });
  });

  it('records progress for a joined challenge', async () => {
    jest.mocked(listChallenges).mockResolvedValue({ items: [{ ...challenge, currentValue: 4 }], page: 1, pageSize: 20, totalCount: 1 });
    jest.mocked(recordChallengeProgress).mockResolvedValue({ ...challenge, currentValue: 6 });
    const view = await renderWithProviders(<ChallengeDetailScreen navigation={navigation} route={route} />);
    expect(await view.findByText('4 of 20')).toBeTruthy();
    await fireEvent.changeText(view.getByLabelText('Progress value'), '6');
    await fireEvent.press(view.getByRole('button', { name: 'Save progress' }));
    await waitFor(() => expect(recordChallengeProgress).toHaveBeenCalledWith('challenge-1', 6));
  });

  it('rejects a negative progress value before calling the API', async () => {
    jest.mocked(listChallenges).mockResolvedValue({ items: [{ ...challenge, currentValue: 4 }], page: 1, pageSize: 20, totalCount: 1 });
    const view = await renderWithProviders(<ChallengeDetailScreen navigation={navigation} route={route} />);
    await fireEvent.changeText(await view.findByLabelText('Progress value'), '-1');
    await fireEvent.press(view.getByRole('button', { name: 'Save progress' }));
    expect(view.getByText('Enter a valid number')).toBeTruthy();
    expect(recordChallengeProgress).not.toHaveBeenCalled();
  });
});
