import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { userEvent } from '@testing-library/react-native';
import { renderWithProviders } from '../../../test/render';
import { TrainerMemberPlanScreen } from './TrainerMemberPlanScreen';
import { TrainerMemberScreen } from './TrainerMemberScreen';

const mockMember = {
  data: {
    id: 'member-1',
    name: 'Ada Lovelace',
    phone: '+15550001111',
    goal: 'Strength',
    status: 'Active',
    joinDate: '2026-10-01',
    packages: [],
    sessions: [],
    notes: [],
    measurements: [],
  },
  isError: false,
  isLoading: false,
  isRefetching: false,
  error: null,
  refetch: jest.fn(),
};

const mockPrograms = {
  data: { items: [] as { id: string; name: string; goalType: string; durationWeeks: number }[], page: 1, pageSize: 50, totalCount: 0 },
  isError: false,
  isLoading: false,
  isRefetching: false,
  error: null,
  refetch: jest.fn(),
};

const mockProgramData = {
  id: 'program-1',
  name: 'AI Plan',
  description: null,
  goalType: 'Strength',
  difficulty: 'Intermediate',
  durationWeeks: 4,
  frequencyPerWeek: 3,
  status: 'Active',
  isOwnedByUser: true,
  days: [{ id: 'day-1', dayNumber: 1, name: 'Push', description: null, restDay: false, exercises: [{ id: 'ex-1', exerciseId: 'exercise-1', sequence: 1, sets: 3, targetReps: 8, targetDurationSeconds: null, targetDistance: null, targetWeight: 40, restSeconds: 90, notes: 'Tempo' }] }],
};

const mockMutate = jest.fn();

jest.mock('../api/workspace.queries', () => ({
  useTrainerMember: () => mockMember,
  useMemberProgress: () => ({ data: [], isError: false, isLoading: false, isRefetching: false, error: null, refetch: jest.fn() }),
  useMemberPrograms: () => mockPrograms,
  useMemberProgram: () => ({
    data: mockProgramData,
    isError: false,
    isLoading: false,
    isRefetching: false,
    error: null,
    refetch: jest.fn(),
  }),
  useUpdateMemberProgram: () => ({ mutate: mockMutate, isPending: false, isError: false, error: null }),
}));

jest.mock('../../fitness/api/queries', () => ({
  useExercise: () => ({ data: { name: 'Squat', equipment: 'Barbell' } }),
}));

describe('trainer member plan tab', () => {
  beforeEach(() => {
    mockPrograms.data = { items: [], page: 1, pageSize: 50, totalCount: 0 };
    mockMutate.mockClear();
  });

  it('shows the empty plan state on the Plan tab', async () => {
    const view = await renderWithProviders(
      <TrainerMemberScreen navigation={{ navigate: jest.fn() } as never} route={{ key: 'member', name: 'TrainerMember', params: { memberId: 'member-1' } }} />,
    );
    const user = userEvent.setup();
    await user.press(view.getByRole('button', { name: 'Plan' }));
    expect(view.getByText('No workout plans are available for this member yet.')).toBeTruthy();
  }, 15000);

  it('wires save through the update mutation', async () => {
    const view = await renderWithProviders(
      <TrainerMemberPlanScreen navigation={{ navigate: jest.fn() } as never} route={{ key: 'plan', name: 'TrainerMemberPlan', params: { memberId: 'member-1', programId: 'program-1' } }} />,
    );
    const user = userEvent.setup();
    await user.press(view.getByRole('button', { name: 'Edit plan' }));
    await user.press(view.getByRole('button', { name: 'Save plan' }));
    expect(mockMutate).toHaveBeenCalled();
    const body = mockMutate.mock.calls[0]?.[0] as { days: { exercises: { targetWeight: number | null; notes: string | null }[] }[] };
    expect(body.days[0].exercises[0].targetWeight).toBe(40);
    expect(body.days[0].exercises[0].notes).toBe('Tempo');
  }, 15000);
});
