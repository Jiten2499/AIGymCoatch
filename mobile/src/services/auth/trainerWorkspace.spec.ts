import { trainerWorkspaceToOpen } from './trainerWorkspace';

const personal = { id: 'personal', name: 'Personal', type: 'Platform', status: 'Active', roles: ['PersonalMember'] };
const gym = { id: 'gym', name: 'North Gym', type: 'Gym', status: 'Active', roles: ['Trainer'] };

describe('trainerWorkspaceToOpen', () => {
  it('opens the gym when login landed on a personal workspace', () => {
    expect(trainerWorkspaceToOpen(personal.id, [personal, gym])?.id).toBe('gym');
  });

  it('stays put when the current workspace is already the trainer gym', () => {
    expect(trainerWorkspaceToOpen(gym.id, [personal, gym])).toBeNull();
  });

  it('does not switch a member who has no trainer workspace', () => {
    expect(trainerWorkspaceToOpen(personal.id, [personal])).toBeNull();
  });
});
