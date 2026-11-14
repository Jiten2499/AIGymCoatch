import type { WorkspaceSummary } from '../../types/api';

export function activeWorkspaces(workspaces: readonly WorkspaceSummary[]): WorkspaceSummary[] {
  return workspaces.filter((workspace) => workspace.status === 'Active');
}

export function canSwitchWorkspaces(workspaces: readonly WorkspaceSummary[]): boolean {
  return activeWorkspaces(workspaces).length > 1;
}
