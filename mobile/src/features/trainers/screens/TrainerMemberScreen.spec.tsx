import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { userEvent } from '@testing-library/react-native';
import { renderWithProviders } from '../../../test/render';
import { TrainerMemberScreen } from './TrainerMemberScreen';

const mockMember = {
  data: {
    id: 'member-1',
    name: 'Ada Lovelace',
    phone: '+15550001111',
    goal: 'Strength',
    status: 'Active',
    joinDate: '2026-10-01',
    packages: [{ name: 'PT 12', status: 'Active', sessionsRemaining: 8, sessionCount: 12, endDate: '2026-12-01', price: 120, currency: 'USD', expiring: false }],
    sessions: [{ id: 'session-1', scheduledStartUtc: '2026-10-25T09:00:00Z', sessionType: 'Strength', status: 'Scheduled' }],
    notes: [{ sessionId: 'session-1', atUtc: '2026-10-25T09:30:00Z', text: 'Keep the last set controlled.' }],
    measurements: [],
  },
  isError: false,
  isLoading: false,
  isRefetching: false,
  error: null,
  refetch: jest.fn(),
};

jest.mock('../api/workspace.queries', () => ({
  useTrainerMember: () => mockMember,
  useMemberProgress: () => ({ data: [], isError: false, isLoading: false, isRefetching: false, error: null, refetch: jest.fn() }),
  useMemberPrograms: () => ({ data: { items: [], page: 1, pageSize: 50, totalCount: 0 }, isError: false, isLoading: false, isRefetching: false, error: null, refetch: jest.fn() }),
}));

function renderScreen() {
  return renderWithProviders(
    <TrainerMemberScreen navigation={{ navigate: jest.fn() } as never} route={{ key: 'member', name: 'TrainerMember', params: { memberId: 'member-1' } }} />,
  );
}

describe('trainer member tabs', () => {
  beforeEach(() => {
    mockMember.refetch.mockClear();
  });

  it('shows details first and reveals the other sections from their tabs', async () => {
    const view = await renderScreen();
    const user = userEvent.setup();
    expect(view.getByText('+15550001111')).toBeTruthy();
    expect(view.queryByText('PT 12')).toBeNull();
    expect(view.queryByText('No sessions in this view.')).toBeNull();
    expect(view.queryByText('Keep the last set controlled.')).toBeNull();

    await user.press(view.getByRole('button', { name: 'Packages' }));
    expect(view.getByText('PT 12')).toBeTruthy();
    expect(view.queryByText('+15550001111')).toBeNull();

    await user.press(view.getByRole('button', { name: 'Sessions' }));
    expect(view.getByText('Scheduled')).toBeTruthy();
    expect(view.queryByText('PT 12')).toBeNull();

    await user.press(view.getByRole('button', { name: 'Notes' }));
    expect(view.getByText('Keep the last set controlled.')).toBeTruthy();

    await user.press(view.getByRole('button', { name: 'Details' }));
    expect(view.getByText('+15550001111')).toBeTruthy();
  });
});
