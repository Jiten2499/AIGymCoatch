import { describe, expect, it } from '@jest/globals';
import { activeWorkspaces, canSwitchWorkspaces } from './activeWorkspaces';

describe('activeWorkspaces', () => {
  it('keeps only active workspaces and requires more than one to switch', () => {
    const rows = [
      { id: 'a', name: 'A', type: 'Gym', status: 'Active', roles: ['Member'] },
      { id: 'b', name: 'B', type: 'Gym', status: 'Inactive', roles: ['Member'] },
      { id: 'c', name: 'C', type: 'Platform', status: 'Active', roles: ['PersonalMember'] },
    ];

    expect(activeWorkspaces(rows).map((row) => row.id)).toEqual(['a', 'c']);
    expect(canSwitchWorkspaces(rows)).toBe(true);
    expect(canSwitchWorkspaces(rows.slice(0, 2))).toBe(false);
  });
});
